"""Run the shipping Linux launcher against isolated game files and Proton tools."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile

ROOT = Path(__file__).resolve().parents[2]


@unittest.skipUnless(sys.platform.startswith('linux'), 'Linux shell integration')
class LinuxLauncherTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='s1ds-linux-test-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.setup = self.root / 'Installer with spaces'
        shutil.copytree(ROOT / 'packaging/Installer', self.setup, ignore=shutil.ignore_patterns('__pycache__'))
        self.game = self.root / 'Steam Library/steamapps/common/Schedule I'
        self.game.mkdir(parents=True)
        (self.game / 'Schedule I.exe').write_bytes(b'game')
        (self.game / 'GameAssembly.dll').write_bytes(b'assembly')
        (self.game / 'UserData').mkdir()
        (self.game / 'UserData/save.json').write_text('keep save')
        self.bin = self.root / 'bin'; self.bin.mkdir()
        for command, source in [('python3', sys.executable), ('dirname', shutil.which('dirname'))]:
            (self.bin / command).symlink_to(source)
        self.calls = self.root / 'proton-calls.jsonl'
        self.environment = dict(os.environ, PATH=str(self.bin), S1DS_TEST_CALLS=str(self.calls))
        self.add_tool('protontricks')
        dependency = self.setup / 'Dependencies/loader.zip'; dependency.parent.mkdir()
        with zipfile.ZipFile(dependency, 'w') as archive:
            archive.writestr('version.dll', b'loader injection')
            archive.writestr('MelonLoader/net6/MelonLoader.dll', b'loader')
        loader = {'version': '0.7.2', 'file': 'loader.zip', 'url': 'https://example.invalid/loader.zip',
                  'sha256': self.digest(dependency)}
        settings = {'repository': 'test/test', 'loader': loader,
                    'favorite': {'Name': 'Test server', 'Host': '127.0.0.1', 'Port': 38465}}
        (self.setup / 'installer-settings.json').write_text(json.dumps(settings))
        for channel in ('public', 'beta'):
            folder = self.setup / 'Packages' / channel; folder.mkdir(parents=True)
            package = folder / 'S1DS.zip'; payload = channel.encode()
            with zipfile.ZipFile(package, 'w') as archive:
                archive.writestr('Mods/DedicatedServerMod_Il2cpp_Client.dll', payload)
            manifest = {'schema': 1, 'channel': channel, 'tag': channel + '-v1.0.0', 'version': '1.0.0',
                        'game': {'build_id': '123'}, 'loader': loader,
                        'packages': [{'file': package.name, 'runtime': 'Il2cpp', 'side': 'Client',
                                      'sha256': self.digest(package), 'dll_sha256': hashlib.sha256(payload).hexdigest()}]}
            (folder / 'release-manifest.json').write_text(json.dumps(manifest))

    @staticmethod
    def digest(path):
        return hashlib.sha256(path.read_bytes()).hexdigest()

    def add_tool(self, name):
        tool = self.bin / name
        tool.write_text(f'#!{sys.executable}\n' + '''import json,os,sys
with open(os.environ['S1DS_TEST_CALLS'],'a') as out:
    out.write(json.dumps([os.path.basename(sys.argv[0]),*sys.argv[1:]])+'\\n')
sys.exit(7 if os.environ.get('S1DS_TEST_FAIL_PROTON') and sys.argv[1:2] != ['info'] else 0)
''')
        tool.chmod(0o755)

    def launch(self, *args, input=None, script='Install-Linux.sh'):
        return subprocess.run(['/bin/bash', str(self.setup / script), *args], input=input,
                              text=True, capture_output=True, env=self.environment)

    def records(self):
        return [json.loads(line) for line in self.calls.read_text().splitlines()] if self.calls.exists() else []

    def assert_success(self, result):
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_actual_public_beta_public_and_uninstall_preserve_save(self):
        for channel in ('public', 'beta', 'public'):
            self.assert_success(self.launch(str(self.game), channel, '--offline'))
            self.assertEqual((self.game / 'Mods/DedicatedServerMod_Il2cpp_Client.dll').read_bytes(), channel.encode())
        self.assert_success(self.launch('--game-directory', str(self.game), script='Uninstall-Linux.sh'))
        self.assertFalse((self.game / 'version.dll').exists())
        self.assertFalse((self.game / 'Mods/DedicatedServerMod_Il2cpp_Client.dll').exists())
        self.assertEqual((self.game / 'UserData/save.json').read_text(), 'keep save')

    def test_existing_loader_still_prepares_proton(self):
        (self.game / 'version.dll').write_bytes(b'existing loader')
        (self.game / 'MelonLoader/net6').mkdir(parents=True)
        (self.game / 'MelonLoader/net6/MelonLoader.dll').write_bytes(b'existing loader')
        self.assert_success(self.launch(str(self.game), 'public', '--offline'))
        self.assertTrue(any('dotnetdesktop6' in call for call in self.records()))
        self.assertTrue(any('native,builtin' in ' '.join(call) for call in self.records()))
        self.assertEqual((self.game / 'version.dll').read_bytes(), b'existing loader')

    def test_failed_proton_setup_does_not_install_mod(self):
        self.environment['S1DS_TEST_FAIL_PROTON'] = '1'
        result = self.launch(str(self.game), 'public', '--offline')
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse((self.game / 'Mods').exists())
        self.assertFalse((self.game / 'version.dll').exists())

    def test_bad_payload_stops_before_proton_changes(self):
        (self.setup / 'Packages/public/S1DS.zip').write_bytes(b'corrupt')
        self.assertNotEqual(self.launch(str(self.game), 'public', '--offline').returncode, 0)
        self.assertEqual(self.records(), [])

    def test_flatpak_protontricks_gets_selected_steam_library(self):
        (self.bin / 'protontricks').unlink(); self.add_tool('flatpak')
        self.assert_success(self.launch(str(self.game), 'beta', '--offline'))
        commands = [call for call in self.records() if call[1:2] == ['run']]
        self.assertEqual(len(commands), 2)
        self.assertTrue(all('--filesystem=' + str(self.game.parent.parent) in call for call in commands))
        self.assertTrue(all('com.github.Matoking.protontricks' in call for call in commands))

    def test_interactive_menu_uses_bundled_beta_and_can_uninstall(self):
        self.assert_success(self.launch(input=f'2\n{self.game}\n2\n'))
        self.assertEqual((self.game / 'Mods/DedicatedServerMod_Il2cpp_Client.dll').read_bytes(), b'beta')
        self.assert_success(self.launch(input=f'3\n{self.game}\n'))
        self.assertFalse((self.game / 'Mods/DedicatedServerMod_Il2cpp_Client.dll').exists())

    def test_status_menu_does_not_require_protontricks(self):
        (self.bin / 'protontricks').unlink()
        self.assert_success(self.launch(input=f'4\n{self.game}\n'))
        self.assertEqual(self.records(), [])

    def test_invalid_menu_choice_has_clear_error(self):
        result = self.launch(input='9\n')
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('Choose 1, 2, 3 or 4', result.stderr)
