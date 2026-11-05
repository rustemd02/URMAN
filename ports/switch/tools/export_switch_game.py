#!/usr/bin/env python3
"""Build managed IL and a Switch-feature resource PCK in an isolated source copy.
Requires the exact pinned Mono editor with URMAN's external-managed export hook
and matching locally built Godot NuGet packages. Does not run the game.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

MARKER = 'URMAN_SWITCH_EXTERNAL_MANAGED_V1'
IGNORED = {'.tools', '.git', '.godot', 'bin', 'obj', '__pycache__'}


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda: f.read(1048576), b''):
            h.update(block)
    return h.hexdigest()


def require(value, message):
    if not value:
        raise ValueError(message)


def local_packages(directory):
    packages = {}
    for path in sorted(directory.rglob('*.nupkg')):
        with zipfile.ZipFile(path) as archive:
            specs = [n for n in archive.namelist() if n.endswith('.nuspec')]
            require(len(specs) == 1, f'Invalid NuGet archive: {path}')
            root = ET.fromstring(archive.read(specs[0]))
            metadata = next(e for e in root if e.tag.rsplit('}', 1)[-1] == 'metadata')
            fields = {e.tag.rsplit('}', 1)[-1]: e.text for e in metadata}
            name, version = fields['id'], fields['version']
            if name.startswith('Godot'):
                require(name not in packages, f'Ambiguous local package versions: {name}')
                packages[name] = (version, path)
    require('Godot.NET.Sdk' in packages and 'GodotSharp' in packages, 'Need source-built Godot.NET.Sdk and GodotSharp packages')
    require(packages['Godot.NET.Sdk'][0] == packages['GodotSharp'][0], 'Local Godot SDK/API package versions differ')
    return packages


def update_setting(text, section, key, value):
    header = f'[{section}]'
    if header not in text:
        return text.rstrip() + f'\n\n{header}\n{key}={value}\n'
    start = text.index(header) + len(header)
    end = text.find('\n[', start)
    if end < 0:
        end = len(text)
    body = text[start:end]
    pattern = rf'(?m)^{re.escape(key)}=.*$'
    body = re.sub(pattern, lambda _: f'{key}={value}', body) if re.search(pattern, body) else body.rstrip() + f'\n{key}={value}\n'
    return text[:start] + body + text[end:]


def apply_game_patch(source, patch):
    if patch is None:
        return {'state': 'not_requested', 'sha256': None}
    patch = patch.resolve(strict=True)
    require(patch.is_file(), 'Game patch must be a regular file')
    fingerprint = sha(patch)
    env = os.environ.copy()
    # The staged tree deliberately has no .git. Do not discover a repository
    # above it and silently skip paths outside that repository's subdirectory.
    for variable in ('GIT_DIR', 'GIT_WORK_TREE', 'GIT_INDEX_FILE'):
        env.pop(variable, None)
    env['GIT_CEILING_DIRECTORIES'] = str(source.parent)
    def check(reverse=False):
        command = ['git', 'apply', '--check']
        if reverse:
            command.append('--reverse')
        return subprocess.run(command + [str(patch)], cwd=source, env=env,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    forward = check()
    if forward.returncode == 0:
        subprocess.run(['git', 'apply', str(patch)], cwd=source, env=env, check=True)
        require(check(reverse=True).returncode == 0, 'Applied game patch failed full reverse verification')
        state = 'applied'
    else:
        reverse = check(reverse=True)
        require(reverse.returncode == 0,
                'Game patch is neither wholly applicable nor wholly already applied. '
                'Mixed/conflicting input refused. Forward check: ' + forward.stderr.strip()
                + '; reverse check: ' + reverse.stderr.strip())
        state = 'already_applied'
    require(sha(patch) == fingerprint, 'Game patch changed during application')
    return {'state': state, 'sha256': fingerprint}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('game-source', 'godot-source', 'godot-editor', 'godot-nuget', 'dotnet', 'work-dir'):
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--game-patch', type=Path, help='Apply complete port patch only to staged source; fully applied input is accepted')
    parser.add_argument('--jobs', type=int, default=1)
    args = parser.parse_args()
    try:
        build(args)
    except (ValueError, OSError, subprocess.CalledProcessError, ET.ParseError, zipfile.BadZipFile) as error:
        parser.exit(1, f'Export failed: {error}\n')


def build(args):
    for key in ('game_source', 'godot_source', 'godot_editor', 'godot_nuget', 'dotnet', 'work_dir'):
        setattr(args, key, getattr(args, key).resolve())
    require(args.jobs > 0, 'jobs must be positive')
    require((args.game_source / 'game/project.godot').is_file(), '--game-source must be the URMAN repository root')
    require(not args.work_dir.exists(), 'Use a fresh --work-dir; existing builds are never replaced')
    require(not args.work_dir.is_relative_to(args.game_source), 'Work directory must be outside canonical game source')
    plugin = args.godot_source / 'modules/mono/editor/GodotTools/GodotTools/Export/ExportPlugin.cs'
    require(MARKER in plugin.read_text(), 'Engine lacks external-managed PCK export hook; rebuild editor tools after applying it')
    packages = local_packages(args.godot_nuget)
    engine_commit = subprocess.check_output(['git', '-C', str(args.godot_source), 'rev-parse', 'HEAD'], text=True).strip()
    editor_version = subprocess.check_output([str(args.godot_editor), '--version'], text=True).strip()
    require(engine_commit[:7] in editor_version, 'Editor version does not identify pinned engine commit')
    args.work_dir.mkdir(parents=True)
    source = args.work_dir / 'source'
    # This copy is the only tree modified by builds/import/export.
    shutil.copytree(args.game_source, source, ignore=lambda _, names: set(names) & IGNORED, symlinks=False)
    patch_record = apply_game_patch(source, args.game_patch)
    (args.work_dir / 'game-patch.json').write_text(json.dumps(patch_record, indent=2) + '\n')
    feed = args.work_dir / 'godot-nuget'
    feed.mkdir()
    for version, path in packages.values():
        shutil.copyfile(path, feed / path.name)
    sdk_version = packages['Godot.NET.Sdk'][0]
    csproj = source / 'game/Urman.Game.csproj'
    text = csproj.read_text()
    text, count = re.subn(r'Sdk="Godot.NET.Sdk/[^\"]+"', f'Sdk="Godot.NET.Sdk/{sdk_version}"', text)
    require(count == 1, 'Unexpected game SDK declaration')
    csproj.write_text(text)
    config = ET.Element('configuration')
    feeds = ET.SubElement(config, 'packageSources')
    ET.SubElement(feeds, 'clear')
    ET.SubElement(feeds, 'add', key='godot-local', value=str(feed))
    ET.SubElement(feeds, 'add', key='nuget.org', value='https://api.nuget.org/v3/index.json')
    mapping = ET.SubElement(config, 'packageSourceMapping')
    for key, pattern in [('godot-local', 'Godot*'), ('nuget.org', '*')]:
        ET.SubElement(ET.SubElement(mapping, 'packageSource', key=key), 'package', pattern=pattern)
    ET.ElementTree(config).write(source / 'NuGet.Config', encoding='utf-8', xml_declaration=True)
    env = os.environ.copy()
    env.update(DOTNET_ROOT=str(args.dotnet.parent), DOTNET_CLI_HOME=str(args.work_dir / 'dotnet-home'),
               NUGET_PACKAGES=str(args.work_dir / 'nuget-cache'), DOTNET_CLI_TELEMETRY_OPTOUT='1',
               DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', PATH=str(args.dotnet.parent) + os.pathsep + env.get('PATH', ''))
    # Query chosen executable outside the source tree so the old SDK pin cannot select another SDK.
    chosen_sdk = subprocess.check_output([str(args.dotnet), '--version'], cwd=args.work_dir, env=env, text=True).strip()
    (source / 'global.json').write_text(json.dumps({'sdk': {'version': chosen_sdk, 'rollForward': 'disable'}}) + '\n')
    logs = args.work_dir / 'logs'
    logs.mkdir()
    commands = []
    def run(command, name, cwd=source):
        commands.append(command)
        with (logs / (name + '.log')).open('w') as log:
            child = subprocess.Popen(command, cwd=cwd, env=env, stdout=subprocess.PIPE,
                                     stderr=subprocess.STDOUT, text=True, errors='replace')
            for line in child.stdout:
                print(line, end='', flush=True)
                log.write(line)
            code = child.wait()
            require(code == 0, f'{name} failed ({code}); see {logs / (name + ".log")}')
    managed = args.work_dir / 'managed'
    run([str(args.dotnet), 'publish', str(csproj), '-c', 'ExportRelease', '-o', str(managed),
         '--self-contained', 'false', '-p:PublishSelfContained=false', '-p:PublishReadyToRun=false',
         '-p:PublishAot=false', '-p:UseAppHost=false', '-p:RuntimeIdentifier=', '-p:RuntimeIdentifiers=',
         '-p:GodotTargetPlatform=switch', f'-m:{args.jobs}', '--disable-build-servers'], 'managed')
    require(all((managed / n).is_file() for n in ('Urman.Game.dll', 'GodotSharp.dll')), 'Managed output missing game/Godot API assemblies')
    require(not (managed / 'System.Private.CoreLib.dll').exists(), 'Unexpected desktop framework in managed publish')
    external_manifest = args.work_dir / 'external-managed-manifest.json'
    external_manifest.write_text(json.dumps({'schema_version': 1, 'engine_commit': engine_commit,
        'sdk_version': sdk_version, 'game_patch': patch_record, 'files': {p.relative_to(managed).as_posix(): sha(p) for p in sorted(managed.rglob('*')) if p.is_file()}}, indent=2) + '\n')
    env['URMAN_SWITCH_MANAGED_MANIFEST'] = str(external_manifest)
    game = source / 'game'
    project = game / 'project.godot'
    project_text = project.read_text()
    require('run/main_scene="res://scenes/photoworlds_game.tscn"' in project_text, 'Expected full production PhotoWorlds entry scene')
    for section, key, value in [
        ('rendering', 'renderer/rendering_method', '"gl_compatibility"'),
        ('rendering', 'renderer/rendering_method.mobile', '"gl_compatibility"'),
        ('rendering', 'textures/vram_compression/import_s3tc_bptc', 'true'),
        ('rendering', 'textures/vram_compression/import_etc2_astc', 'true'),
        ('filesystem', 'import/blender/enabled', 'false'),
        ('editor', 'import/use_multiple_threads', 'false')]:
        project_text = update_setting(project_text, section, key, value)
    project.write_text(project_text)
    (game / 'export_presets.cfg').write_text('''[preset.0]
name="URMAN Switch Resources"
platform="Linux"
runnable=false
custom_features="switch,urman_switch_pck"
export_filter="all_resources"
include_filter="content/*"
exclude_filter="tests/*,studio/*,*.csproj,*.sln,*.slnx"
export_path=""
script_export_mode=2
[preset.0.options]
texture_format/s3tc_bptc=true
texture_format/etc2_astc=true
binary_format/architecture="arm64"
dotnet/include_scripts_content=false
dotnet/include_debug_symbols=false
dotnet/embed_build_outputs=false
''')
    # Existing clean-import bootstrap avoids a cold custom-font load before import.
    project.write_text(update_setting(project_text, 'gui', 'theme/custom_font', '""'))
    try:
        run([str(args.godot_editor), '--headless', '--rendering-driver', 'dummy', '--rendering-method', 'dummy', '--path', str(game), '--editor', '--import'], 'import')
    finally:
        project.write_text(project_text)
    pck = args.work_dir / 'urman.pck'
    run([str(args.godot_editor), '--headless', '--rendering-driver', 'dummy', '--rendering-method', 'dummy', '--path', str(game), '--export-pack', 'URMAN Switch Resources', str(pck)], 'pck')
    require(MARKER in (logs / 'pck.log').read_text(), 'Export did not acknowledge external managed hook; refusing unverified PCK')
    require(pck.is_file() and pck.stat().st_size > 100, 'Exporter produced no resource PCK')
    with pck.open('rb') as stream:
        require(stream.read(4) == b'GDPC', 'Exporter produced invalid PCK magic')
    report = {'schema_version': 1, 'status': 'exported_hardware_validation_not_run', 'engine_commit': engine_commit,
        'editor_version': editor_version, 'editor_sha256': sha(args.godot_editor), 'export_hook_sha256': sha(plugin),
        'sdk_version': sdk_version, 'game_patch': patch_record, 'dotnet_sdk': chosen_sdk, 'godot_packages': {name: {'version': value[0], 'sha256': sha(value[1])} for name, value in packages.items()},
        'pck': str(pck), 'pck_sha256': sha(pck), 'managed': str(managed), 'external_managed_manifest': str(external_manifest),
        'commands': commands, 'limitations': ['PCK export and managed compilation do not prove Horizon runtime compatibility or Switch playability.']}
    (args.work_dir / 'export-report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({'pck': str(pck), 'managed': str(managed), 'report': str(args.work_dir / 'export-report.json')}))


if __name__ == '__main__':
    main()
