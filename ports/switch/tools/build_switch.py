#!/usr/bin/env python3
"""Source-pinned Linux build pipeline; successful stages are not hardware validation."""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys

ENGINE = '650036e08177daeb0f72b7c2201fad7710efeffa'
RUNTIME = '4a1e59e06fba330166a070eab784cc37a3232f6a'
ENGINE_URL = 'https://github.com/SnepOMatic/Godot-for-Nintendo-Switch.git'
RUNTIME_URL = 'https://github.com/pixelomer/dotnet-runtime.git'
STAGES = ['runtime', 'native', 'editor', 'game', 'package']


def digest(path):
    h = hashlib.sha256()
    with Path(path).open('rb') as f:
        for block in iter(lambda: f.read(1048576), b''):
            h.update(block)
    return h.hexdigest()


def require(value, message):
    if not value:
        raise RuntimeError(message)


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, indent=2) + '\n')
    temporary.replace(path)


class Pipeline:
    def __init__(self, args):
        self.args = args
        self.project = Path(__file__).resolve().parents[1]
        self.work = args.work_dir.resolve()
        self.source = args.game_source.resolve()
        require(platform.system() == 'Linux' and platform.machine() == 'x86_64', 'Build requires Linux x86_64; use tools/Dockerfile.switch')
        require(sys.version_info >= (3, 12), 'Use Python 3.12 or newer')
        require(args.jobs > 0, 'jobs must be positive')
        require(not any(c.isspace() for c in str(self.work)), 'Build path cannot contain whitespace')
        require(not self.work.is_relative_to(self.source), 'Build output must be outside game source')
        self.pins_path = self.project / 'engine/source-pins.json'
        self.patch = self.project / 'engine/godot-switch-coreclr.patch'
        self.pins = json.loads(self.pins_path.read_text())
        require(self.pins.get('engine_commit') == ENGINE and self.pins.get('runtime_commit') == RUNTIME, 'Unexpected engine/runtime source pins')
        self.patch_hash = digest(self.patch)
        require(self.pins.get('godot_patch_sha256', self.pins.get('patch_sha256')) == self.patch_hash, 'Patch differs from source pins')
        self.runtime_patch = self.project / 'engine/dotnet-build-dependencies.patch'
        self.runtime_patch_hash = digest(self.runtime_patch)
        require(self.pins.get('runtime_patch_sha256') == self.runtime_patch_hash, 'Runtime build dependency patch differs from source pins')
        require(self.pins.get('game_patch_sha256') == digest(self.project / 'engine/game-port.patch'), 'Game patch differs from source pins')
        self.work.mkdir(parents=True, exist_ok=True)
        (self.work / 'logs').mkdir(exist_ok=True)
        self.scons_cache = self.work / 'scons-cache'
        self.engine = self.work / 'sources/godot'
        self.runtime = self.work / 'sources/dotnet-runtime'
        self.env = os.environ.copy()
        self.records = {'schema_version': 1, 'status': 'running', 'stages': [], 'hardware_validation': 'not_run'}

    def run(self, command, name, cwd=None, env=None):
        command = list(map(str, command))
        print('+ ' + ' '.join(command), flush=True)
        with (self.work / 'logs' / (name + '.log')).open('w') as log:
            child = subprocess.Popen(command, cwd=cwd, env=env or self.env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, errors='replace')
            for line in child.stdout:
                print(line, end='', flush=True)
                log.write(line)
            code = child.wait()
        require(code == 0, f'{name} failed ({code}); see logs/{name}.log')

    def fetch(self, path, url, revision, name):
        if not path.exists():
            path.mkdir(parents=True)
            self.run(['git', 'init', path], name + '-init')
            self.run(['git', '-C', path, 'remote', 'add', 'origin', url], name + '-remote')
            self.run(['git', '-C', path, 'fetch', '--depth=1', 'origin', revision], name + '-fetch')
            self.run(['git', '-C', path, 'checkout', '--detach', revision], name + '-checkout')
        actual = subprocess.check_output(['git', '-C', path, 'rev-parse', 'HEAD'], text=True).strip()
        remote = subprocess.check_output(['git', '-C', path, 'remote', 'get-url', 'origin'], text=True).strip()
        require(actual == revision and remote == url, f'{name} existing checkout identity mismatch; choose fresh work directory')

    def apply_patch(self, checkout, patch, patch_hash, name):
        stamp = self.work / (name + '-patch.json')
        if stamp.exists():
            require(json.loads(stamp.read_text())['sha256'] == patch_hash, name + ' patch changed; use a fresh work directory')
            self.run(['git', '-C', checkout, 'apply', '--reverse', '--check', patch], name + '-patch-present')
        else:
            require(not subprocess.check_output(['git', '-C', checkout, 'status', '--porcelain'], text=True).strip(), 'Unrecorded source edits; use a fresh work directory')
            self.run(['git', '-C', checkout, 'apply', '--check', patch], name + '-patch-check')
            self.run(['git', '-C', checkout, 'apply', patch], name + '-patch')
            write(stamp, {'sha256': patch_hash})

    def prepare(self):
        self.fetch(self.runtime, RUNTIME_URL, RUNTIME, 'runtime-source')
        self.apply_patch(self.runtime, self.runtime_patch, self.runtime_patch_hash, 'runtime')
        self.fetch(self.engine, ENGINE_URL, ENGINE, 'engine-source')
        self.apply_patch(self.engine, self.patch, self.patch_hash, 'engine')

    def runtime_stage(self):
        self.run([sys.executable, self.runtime / 'eng/libnx/build.py', '--flavor', 'coreclr', '--jobs', self.args.jobs], 'runtime-build', self.runtime)
        configuration = json.loads((self.runtime / 'artifacts/horizon/environment.json').read_text())
        self.env.update(configuration)
        self.env['PATH'] = str(Path(configuration['DEVKITA64']) / 'bin') + ':' + str(Path(configuration['DEVKITPRO']) / 'tools/bin') + ':' + self.env['PATH']
        self.dotnet = self.runtime / '.dotnet/dotnet'
        require(self.dotnet.is_file(), 'Runtime bootstrap did not produce .dotnet/dotnet')
        self.env.update(DOTNET_ROOT=str(self.dotnet.parent), DOTNET_CLI_TELEMETRY_OPTOUT='1')
        self.env['PATH'] = str(self.dotnet.parent) + ':' + self.env['PATH']
        self.corelib = self.runtime / 'artifacts/bin/coreclr/libnx.arm64.Release/IL'
        self.framework = self.runtime / 'artifacts/bin/runtime/net10.0-libnx-Release-arm64'
        self.icu = Path(configuration['ICU_NX_INSTALL_DIR']) / 'share/icu/77.1/icudt77l.dat'
        files = {p.name: digest(p) for p in self.framework.glob('*.dll')}
        core = self.corelib / 'System.Private.CoreLib.dll'
        require('System.Runtime.dll' in files and core.is_file() and self.icu.is_file(), 'Source-built framework/ICU incomplete')
        if core.name in files:
            require(files[core.name] == digest(core), 'CoreLib differs between runtime/framework outputs')
        files[core.name] = digest(core)
        self.framework_receipt = self.work / 'framework-receipt.json'
        write(self.framework_receipt, {'schema_version': 1, 'target_os': 'libnx', 'target_arch': 'arm64', 'runtime_commit': RUNTIME, 'runtime_patch_sha256': self.runtime_patch_hash, 'files': files,
             'icu': {'name': self.icu.name, 'sha256': digest(self.icu)}})

    def native_stage(self):
        output = self.engine / 'bin/godot.switch.template_release.arm64.mono'
        self.run(['scons', 'platform=switch', 'target=template_release', 'arch=arm64', 'module_mono_enabled=yes',
                  'switch_coreclr_root=' + str(self.runtime), 'debug_symbols=yes', 'lto=none',
                  'cache_path=' + str(self.scons_cache), 'cache_limit=6', f'-j{self.args.jobs}',
                  str(output.relative_to(self.engine))], 'godot-native', self.engine)
        require(output.is_file(), 'Expected unstripped native ELF missing')
        native = self.work / 'native'
        native.mkdir(exist_ok=True)
        self.elf, self.nro = native / 'urman.elf', native / 'urman.nro'
        shutil.copyfile(output, self.elf)
        tools = Path(self.env['DEVKITPRO']) / 'tools/bin'
        self.run([tools / 'nacptool', '--create', 'URMAN', 'URMAN Team', '0.1.0', native / 'urman.nacp'], 'nacp')
        romfs = native / 'romfs'
        romfs.mkdir(exist_ok=True)
        authored_romfs = self.engine / 'platform/switch/romfs'
        if authored_romfs.is_dir():
            shutil.copytree(authored_romfs, romfs, dirs_exist_ok=True)
        self.run([tools / 'elf2nro', self.elf, self.nro, '--nacp=' + str(native / 'urman.nacp'), '--icon=' + str(self.engine / 'platform/switch/icon.jpg'), '--romfsdir=' + str(romfs)], 'nro')
        self.engine_receipt = self.work / 'engine-receipt.json'
        write(self.engine_receipt, {'schema_version': 1, 'engine_commit': ENGINE, 'runtime_commit': RUNTIME,
              'godot_patch_sha256': self.patch_hash, 'runtime_patch_sha256': self.runtime_patch_hash, 'files': {'urman.nro': digest(self.nro), 'urman.elf': digest(self.elf)}})

    def editor_stage(self):
        # This editor only imports/exports assets. Avoid optimizing its large
        # desktop binary; the Switch release target retains its optimizations.
        self.run(['scons', 'platform=linuxbsd', 'target=editor', 'arch=x86_64', 'module_mono_enabled=yes',
            'optimize=none', 'debug_symbols=no',
            'x11=no', 'wayland=no', 'vulkan=no', 'opengl3=yes', 'openxr=no', 'alsa=no', 'pulseaudio=no',
            'speechd=no', 'udev=no', 'dbus=no', 'lto=none',
            'cache_path=' + str(self.scons_cache), 'cache_limit=6', f'-j{self.args.jobs}'], 'godot-editor', self.engine)
        self.editor = self.engine / 'bin/godot.linuxbsd.editor.x86_64.mono'
        require(self.editor.is_file(), 'Built Mono editor not found')
        self.run([self.editor, '--headless', '--rendering-driver', 'dummy', '--rendering-method', 'dummy', '--generate-mono-glue', self.engine / 'modules/mono/glue'], 'mono-glue', self.engine)
        self.nuget = self.work / 'godot-nuget'
        self.nuget.mkdir(exist_ok=True)
        self.run([sys.executable, self.engine / 'modules/mono/build_scripts/build_assemblies.py', '--godot-output-dir', self.engine / 'bin',
                  '--godot-platform', 'linuxbsd', '--push-nupkgs-local', self.nuget], 'mono-assemblies', self.engine)

    def game_stage(self):
        self.game_output = self.work / 'game-export'
        self.run([sys.executable, self.project / 'tools/export_switch_game.py', '--game-source', self.source, '--godot-source', self.engine,
            '--godot-editor', self.editor, '--godot-nuget', self.nuget, '--dotnet', self.dotnet,
            '--work-dir', self.game_output, '--game-patch', self.project / 'engine/game-port.patch',
            '--jobs', self.args.jobs], 'game-export')

    def package_stage(self):
        self.run([sys.executable, self.project / 'tools/package_switch.py', '--nro', self.nro, '--elf', self.elf,
            '--pck', self.game_output / 'urman.pck', '--corelib-dir', self.corelib, '--framework-dir', self.framework,
            '--framework-receipt', self.framework_receipt, '--engine-receipt', self.engine_receipt,
            '--game-receipt', self.game_output / 'external-managed-manifest.json',
            '--managed-dir', self.game_output / 'managed', '--icu-data', self.icu, '--runtime-source', self.runtime,
            '--source-pins', self.pins_path, '--art-root', self.source / 'game/assets/textures',
            '--art-manifest', self.project / 'engine/art-manifest.json', '--output', self.work / 'sd-package'], 'package')

    def execute(self):
        try:
            self.prepare()
            last = len(STAGES) - 1 if self.args.stage == 'all' else STAGES.index(self.args.stage)
            for name in STAGES[:last+1]:
                self.records['active_stage'] = name
                write(self.work / 'build-status.json', self.records)
                getattr(self, name + '_stage')()
                self.records['stages'].append({'name': name, 'status': 'completed'})
            self.records.update(status='requested_build_stages_completed', active_stage=None)
        except Exception as error:
            self.records.update(status='failed', error=str(error))
            raise
        finally:
            write(self.work / 'build-status.json', self.records)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work-dir', type=Path, required=True)
    parser.add_argument('--game-source', type=Path, required=True)
    parser.add_argument('--stage', choices=['all'] + STAGES, default='all')
    parser.add_argument('--jobs', type=int, default=min(os.cpu_count() or 2, 4))
    args = parser.parse_args()
    try:
        Pipeline(args).execute()
    except (RuntimeError, OSError, ValueError, subprocess.CalledProcessError) as error:
        parser.exit(1, f'Build stopped: {error}\n')


if __name__ == '__main__':
    main()
