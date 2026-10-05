"""Filesystem regressions for the exact installer shipped to players."""
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('installer', ROOT / 'packaging/Installer/s1ds_installer.py')
i = importlib.util.module_from_spec(spec)
spec.loader.exec_module(i)


class InstallerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.game = self.root / 'Game folder [test]'
        self.game.mkdir()
        (self.game / 'Schedule I.exe').write_bytes(b'game')
        (self.game / 'GameAssembly.dll').write_bytes(b'game-assembly')
        (self.game / 'UserData').mkdir()
        (self.game / 'UserData/save.json').write_text('saved progress')
        self.mod = self.root / 'DedicatedServerMod_Il2cpp_Client.dll'
        self.mod.write_bytes(b'public-mod')
        self.manifest = dict(channel='public', tag='public-v1.1.0-unn.1', runtime='Il2cpp', side='Client', version='1.1.0-unn.1')
        self.settings = {'favorite': {'Name': 'Test server', 'Host': '127.0.0.1', 'Port': 38465}}
        self.loader = self.root / 'Loader'
        (self.loader / 'MelonLoader/net6').mkdir(parents=True)
        (self.loader / 'MelonLoader/net6/MelonLoader.dll').write_bytes(b'loader')
        (self.loader / 'version.dll').write_bytes(b'version')

    def install(self, loader=True):
        i.install(self.game, self.manifest, self.mod, self.settings, self.loader if loader else None)

    def test_public_beta_public_switch_and_uninstall(self):
        self.install()
        added = i.favorites(self.game)[1]['Favorites'][0]['Id']
        self.mod.write_bytes(b'beta-mod')
        self.manifest.update(channel='beta', tag='beta-v1.1.0-unn.1')
        self.install(False)
        self.assertEqual((self.game / 'Mods' / self.mod.name).read_bytes(), b'beta-mod')
        self.mod.write_bytes(b'public-mod')
        self.manifest.update(channel='public', tag='public-v1.1.0-unn.1')
        self.install(False)
        self.assertEqual(i.favorites(self.game)[1]['Favorites'][0]['Id'], added)
        self.assertEqual(i.load_state(self.game)['channel'], 'public')
        i.uninstall(self.game)
        self.assertFalse((self.game / 'Mods' / self.mod.name).exists())
        self.assertFalse((self.game / 'version.dll').exists())
        self.assertEqual((self.game / 'UserData/save.json').read_text(), 'saved progress')
        self.assertEqual(i.favorites(self.game)[1]['Favorites'], [])
        i.uninstall(self.game)

    def test_existing_favorites_history_and_loader_preserved(self):
        original = {'Favorites': [{'Id': 'existing', 'Host': 'elsewhere', 'Port': 10}], 'History': [{'Host': 'history'}], 'Extra': 1}
        i.write_json(self.game / 'UserData/DedicatedServerClientServers.json', original)
        (self.game / 'version.dll').write_bytes(b'preexisting-loader')
        self.install(False)
        i.uninstall(self.game)
        self.assertEqual(i.favorites(self.game)[1], original)
        self.assertEqual((self.game / 'version.dll').read_bytes(), b'preexisting-loader')

    def test_existing_client_backup_is_not_reactivated_by_default(self):
        (self.game / 'Mods').mkdir()
        existing = self.game / 'Mods' / self.mod.name
        existing.write_bytes(b'upstream')
        self.install(False)
        original = i.load_state(self.game)['files']['Mods/' + self.mod.name]['original']
        self.assertEqual((self.game / i.STATE_DIR / original).read_bytes(), b'upstream')
        i.uninstall(self.game)
        self.assertFalse(existing.exists())

    def test_explicit_restore_previous_mod(self):
        (self.game / 'Mods').mkdir()
        target = self.game / 'Mods' / self.mod.name
        target.write_bytes(b'upstream')
        self.install(False)
        i.uninstall(self.game, restore_previous=True)
        self.assertEqual(target.read_bytes(), b'upstream')

    def test_other_mod_keeps_shared_loader(self):
        self.install()
        (self.game / 'Mods/OtherMod.dll').write_bytes(b'other')
        i.uninstall(self.game)
        self.assertEqual((self.game / 'Mods/OtherMod.dll').read_bytes(), b'other')
        self.assertTrue((self.game / 'version.dll').exists())
        self.assertFalse((self.game / 'Mods' / self.mod.name).exists())
        (self.game / 'Mods/OtherMod.dll').unlink()
        i.uninstall(self.game)
        self.assertFalse((self.game / 'version.dll').exists())

    def test_modified_files_are_preserved(self):
        self.install()
        target = self.game / 'Mods' / self.mod.name
        target.write_bytes(b'local edit')
        self.assertIn('Mods/' + self.mod.name, i.uninstall(self.game))
        self.assertEqual(target.read_bytes(), b'local edit')

    def test_modified_files_backed_up_on_update(self):
        self.install(False)
        (self.game / 'Mods' / self.mod.name).write_bytes(b'local edit')
        self.install(False)
        copies = list((self.game / i.STATE_DIR / 'recovery').rglob('*.dll'))
        self.assertEqual(copies[0].read_bytes(), b'local edit')

    def test_failed_install_rolls_back_files_favorites_and_receipt(self):
        self.install()
        receipt = (self.game / i.STATE_DIR / 'state.json').read_bytes()
        self.mod.write_bytes(b'new mod')
        real_write = i.write_json
        def failing_write(path, value):
            if path.name == 'state.json':
                raise OSError('simulated disk failure')
            return real_write(path, value)
        with patch.object(i, 'write_json', side_effect=failing_write):
            with self.assertRaises(OSError):
                self.install()
        self.assertEqual((self.game / 'Mods' / self.mod.name).read_bytes(), b'public-mod')
        self.assertEqual((self.game / i.STATE_DIR / 'state.json').read_bytes(), receipt)
        self.assertFalse((self.game / i.STATE_DIR / 'operation.lock').exists())

    def test_first_install_failure_rolls_back_favorite(self):
        with patch.object(i, 'write_json', side_effect=OSError('failure')):
            with self.assertRaises(OSError):
                self.install()
        self.assertFalse((self.game / 'Mods' / self.mod.name).exists())
        self.assertFalse((self.game / 'UserData/DedicatedServerClientServers.json').exists())
        self.assertFalse((self.game / i.STATE_DIR / 'state.json').exists())

    def test_failed_rollback_keeps_recovery_files_and_lock(self):
        self.install()
        self.mod.write_bytes(b'new mod')
        real_copy = i.shutil.copy2
        def failing_copy(source, target, *args, **kwargs):
            if any(p.startswith('transaction-') for p in Path(source).parts):
                raise OSError('simulated rollback failure')
            return real_copy(source, target, *args, **kwargs)
        real_write = i.write_json
        def failing_state(path, value):
            if path.name == 'state.json':
                raise OSError('simulated receipt failure')
            return real_write(path, value)
        with patch.object(i.shutil, 'copy2', side_effect=failing_copy), patch.object(i, 'write_json', side_effect=failing_state):
            with self.assertRaisesRegex(OSError, 'Recovery copies are retained'):
                self.install()
        self.assertTrue((self.game / i.STATE_DIR / 'operation.lock').exists())
        copies = list((self.game / i.STATE_DIR).glob('transaction-*/Mods/' + self.mod.name))
        self.assertEqual(copies[0].read_bytes(), b'public-mod')

    def test_malformed_favorites_prevents_install(self):
        (self.game / 'UserData/DedicatedServerClientServers.json').write_text('{broken')
        with self.assertRaises(ValueError):
            self.install()
        self.assertFalse((self.game / 'Mods').exists())

    def test_extraction_blocks_path_traversal_and_windows_drives(self):
        for name in ('../escape', '/absolute', 'C:/escape', '..\\escape', 'MelonLoader/../../escape'):
            archive = self.root / 'bad.zip'
            with zipfile.ZipFile(archive, 'w') as z:
                z.writestr(name, b'bad')
            with self.subTest(name=name), self.assertRaises(ValueError):
                i.extract(archive, self.root / 'extract')

    def test_symlink_install_target_is_rejected(self):
        if os.name == 'nt':
            self.skipTest('Windows symlink creation requires privilege; junction rejection is also implemented.')
        outside = self.root / 'outside'; outside.mkdir()
        (self.game / 'Mods').symlink_to(outside, target_is_directory=True)
        with self.assertRaises(ValueError):
            self.install(False)
        self.assertFalse(list(outside.iterdir()))

    def test_release_selection_separates_channels_and_drafts(self):
        def release(tag, published, draft=False, prerelease=False):
            return dict(tag_name=tag, published_at=published, draft=draft, prerelease=prerelease, assets=[{'name': 'release-manifest.json'}])
        releases = [release('public-v1', '2026-01-01'), release('beta-v9', '2026-10-01', prerelease=True), release('public-v2', '2026-10-02', draft=True), release('v99', '2026-12-01')]
        self.assertEqual(i.select_release(releases, 'public')['tag_name'], 'public-v1')
        self.assertEqual(i.select_release(releases, 'beta')['tag_name'], 'beta-v9')

    def test_game_build_mismatch_is_rejected(self):
        library = self.root / 'steamapps'; game = library / 'common/Schedule I'; game.mkdir(parents=True)
        (game / 'Schedule I.exe').touch(); (game / 'GameAssembly.dll').touch()
        (library / 'appmanifest_3164500.acf').write_text('"buildid" "24705572"')
        with patch.object(i.subprocess, 'run') as run:
            run.return_value.returncode = 0 if os.name == 'nt' else 1
            with self.assertRaisesRegex(ValueError, 'Switch/update'):
                i.check_game(game, 'Il2cpp', '25622691')
            i.check_game(game, 'Il2cpp', '25622691', True)

    def test_installer_lock_prevents_concurrent_mutation(self):
        with i.operation(self.game):
            with self.assertRaisesRegex(ValueError, 'Another installer'):
                self.install()
        self.assertFalse((self.game / 'Mods' / self.mod.name).exists())

    def test_managed_receipt_cannot_target_save_files(self):
        home = self.game / i.STATE_DIR; home.mkdir()
        i.write_json(home / 'state.json', {'schema': 1, 'files': {'UserData/save.json': {'installed_sha256': '0'*64}}})
        with self.assertRaises(ValueError):
            i.uninstall(self.game)
        self.assertEqual((self.game / 'UserData/save.json').read_text(), 'saved progress')

    def test_package_hash_mismatch_never_installs(self):
        package = self.root / 'package'; (package / 'Packages/public').mkdir(parents=True)
        archive = package / 'Packages/public/mod.zip'; archive.write_bytes(b'changed')
        manifest = {'schema': 1, 'channel': 'public', 'tag': 'public-v1', 'packages': [{'runtime': 'Il2cpp', 'side': 'Client', 'file': 'mod.zip', 'sha256': '0'*64}]}
        i.write_json(package / 'Packages/public/release-manifest.json', manifest)
        with patch.object(i, 'ROOT', package), self.assertRaisesRegex(ValueError, 'checksum'):
            i.prepare({}, 'public', 'Il2cpp', 'Client', self.root / 'stage', None, True)

    def test_uninstall_does_not_remove_user_edited_favorite(self):
        self.install()
        path, data = i.favorites(self.game)
        data['Favorites'][0]['Name'] = 'My renamed server'; i.write_json(path, data)
        i.uninstall(self.game)
        self.assertEqual(i.favorites(self.game)[1]['Favorites'][0]['Name'], 'My renamed server')

    def test_reinstall_clears_uninstalled_marker(self):
        self.install(); i.uninstall(self.game); self.install()
        self.assertFalse(i.load_state(self.game)['uninstalled'])


if __name__ == '__main__':
    unittest.main()
