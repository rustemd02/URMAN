"""Packaging refusal tests; fixtures are private temporary format controls, not builds."""
import hashlib
import json
import importlib.util
from pathlib import Path
import struct
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('package_switch', Path(__file__).resolve().parents[1] / 'package_switch.py')
package = importlib.util.module_from_spec(spec)
spec.loader.exec_module(package)


class PackageRefusalTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def file(self, name, contents):
        path = self.root / name
        path.write_bytes(contents)
        return path

    def test_rejects_elf_renamed_as_nro(self):
        with self.assertRaisesRegex(ValueError, 'NRO header'):
            package.validate_nro(self.file('wrong.nro', b'\x7fELF' + bytes(128)))

    def test_rejects_truncated_nro_segment(self):
        data = bytearray(256)
        data[0x10:0x14] = b'NRO0'
        struct.pack_into('<I', data, 0x18, 256)
        struct.pack_into('<IIIIII', data, 0x20, 0, 128, 128, 64, 192, 100)
        with self.assertRaisesRegex(ValueError, 'segment bounds'):
            package.validate_nro(self.file('wrong.nro', data))

    def test_rejects_x86_elf(self):
        data = bytearray(64)
        data[:6] = b'\x7fELF\x02\x01'
        struct.pack_into('<H', data, 18, 62)
        with self.assertRaisesRegex(ValueError, 'AArch64'):
            package.validate_elf(self.file('wrong.elf', data))

    def test_rejects_nonpack_and_wrong_icu(self):
        with self.assertRaises(ValueError):
            package.validate_pck(self.file('wrong.pck', b'not a pack'))
        with self.assertRaises(ValueError):
            package.validate_icu(self.file('icudt74l.dat', bytes(100)))

    def test_framework_collision_rejected_even_same_bytes(self):
        game = self.root / 'game'
        game.mkdir()
        (game / 'System.Runtime.dll').write_bytes(b'same')
        with self.assertRaisesRegex(ValueError, 'collides'):
            package.game_inputs(game, {'System.Runtime.dll': game / 'System.Runtime.dll'})

    def test_requires_real_game_assembly_names(self):
        with self.assertRaisesRegex(ValueError, 'Urman.Game.dll'):
            package.game_inputs(self.root, {})

    def test_rejects_game_symlinks(self):
        target = self.file('target', b'')
        (self.root / 'linked.dll').symlink_to(target)
        with self.assertRaisesRegex(ValueError, 'Symlink'):
            package.game_inputs(self.root, {})

    def art_fixture(self):
        root = (self.root / 'textures').resolve()
        names = ['ui/switch_loading_forest_v1.png'] + [f'police_signage_imagegen_v1/sign_{i}.png' for i in range(15)]
        # Tiny signature controls for input validation; not generated game art.
        payload = b'\x89PNG\r\n\x1a\nunit-test-control'
        files = {}
        for name in names:
            path = root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(payload)
            files[name] = hashlib.sha256(payload).hexdigest()
        manifest = self.root / 'art.json'
        manifest.write_text(json.dumps({'schema_version': 1, 'files': files}))
        return root, manifest

    def test_art_accepts_exact_signature_and_hash_controls(self):
        root, manifest = self.art_fixture()
        inputs, receipt = package.art_inputs(root, manifest)
        self.assertEqual(len(inputs), 16)
        self.assertEqual(set(inputs), set(receipt['files']))

    def test_art_hash_mismatch_rejected(self):
        root, manifest = self.art_fixture()
        with (root / 'ui/switch_loading_forest_v1.png').open('ab') as stream:
            stream.write(b'changed')
        with self.assertRaisesRegex(ValueError, 'hash mismatch'):
            package.art_inputs(root, manifest)

    def test_art_traversal_rejected(self):
        root, manifest = self.art_fixture()
        data = json.loads(manifest.read_text())
        old = 'police_signage_imagegen_v1/sign_0.png'
        data['files']['police_signage_imagegen_v1/../../outside.png'] = data['files'].pop(old)
        manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'unsafe'):
            package.art_inputs(root, manifest)

    def test_art_symlink_ancestor_rejected(self):
        root, manifest = self.art_fixture()
        real = root / 'police_signage_imagegen_v1'
        moved = root / 'real-signs'
        real.rename(moved)
        real.symlink_to(moved, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, 'Symlink'):
            package.art_inputs(root, manifest)

    def test_art_non_png_rejected_even_with_matching_hash(self):
        root, manifest = self.art_fixture()
        name = 'ui/switch_loading_forest_v1.png'
        payload = b'not a PNG'
        (root / name).write_bytes(payload)
        data = json.loads(manifest.read_text())
        data['files'][name] = hashlib.sha256(payload).hexdigest()
        manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, 'PNG signature'):
            package.art_inputs(root, manifest)

    def test_atomic_publish_cannot_replace_existing_empty_dir(self):
        stage, dest = self.root / 'stage', self.root / 'dest'
        stage.mkdir()
        dest.mkdir()
        with self.assertRaises(OSError):
            package.publish_new(stage, dest)
        self.assertTrue(stage.is_dir())
        self.assertTrue(dest.is_dir())

    def test_atomic_publish_new_destination(self):
        stage, dest = self.root / 'stage', self.root / 'dest'
        stage.mkdir()
        (stage / 'marker').write_text('control')
        package.publish_new(stage, dest)
        self.assertFalse(stage.exists())
        self.assertEqual((dest / 'marker').read_text(), 'control')


if __name__ == '__main__':
    unittest.main()
