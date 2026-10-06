"""Verify upload-ready release ZIPs contain the installer and both game channels."""
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from unittest.mock import patch
import zipfile

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('packager', ROOT / 'build/Package-Release.py')
p = importlib.util.module_from_spec(spec); spec.loader.exec_module(p)


class ReleasePackagingTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='s1ds-package-test-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / 'Source'; self.root.mkdir()
        (self.root / 'API').mkdir()
        (self.root / 'API/Version.cs').write_text('ModVersion = "1.2.3";')
        for name in ('LICENSE', 'FORK_NOTES.txt'):
            shutil.copy2(ROOT / name, self.root / name)
        shutil.copytree(ROOT / 'packaging/Installer', self.root / 'packaging/Installer',
                        ignore=shutil.ignore_patterns('__pycache__', '.downloads', '.runtime*'))
        (self.root / 'packaging/Server').mkdir()
        shutil.copy2(ROOT / 'packaging/Server/start_server.bat', self.root / 'packaging/Server/start_server.bat')
        self.cache = Path(self.temp.name) / 'Dependencies'; self.cache.mkdir()
        self.output = Path(self.temp.name) / 'Release'
        settings_path = self.root / 'packaging/Installer/installer-settings.json'
        settings = json.loads(settings_path.read_text())
        for name in ('loader', 'python'):
            archive = self.cache / settings[name]['file']
            with zipfile.ZipFile(archive, 'w') as z:
                z.writestr('LICENSE.txt', 'dependency license')
            settings[name]['sha256'] = p.digest(archive)
        settings_path.write_text(json.dumps(settings))
        for branch in ('Public', 'Beta'):
            for side in ('Client', 'Server'):
                folder = self.root / f'bin/{branch}/Il2cpp_{side}/net6.0'
                folder.mkdir(parents=True)
                (folder / f'DedicatedServerMod_Il2cpp_{side}.dll').write_bytes(f'{branch} {side}'.encode())

    def package_both(self):
        with patch.object(p, 'ROOT', self.root), patch.object(p.subprocess, 'check_output', return_value='fixture-commit\n'):
            for channel in ('public', 'beta'):
                p.package('1.2.3', channel, ['Il2cpp'], self.output, self.cache)
            p.bundle(self.output)

    def test_combined_handout_is_upload_ready_for_both_releases(self):
        self.package_both()
        for channel in ('public', 'beta'):
            folder = self.output / channel
            handout = folder / 'Unnamed-Schedule-I-Public-and-Beta.zip'
            self.assertTrue(handout.is_file(), 'Combined handout must be included in the upload-ready channel folder.')
            self.assertEqual(p.digest(handout), p.digest(self.output / 'Unnamed-Schedule-I-Client.zip'))
            checksums = dict(line.split('  ', 1)[::-1] for line in (folder / 'SHA256SUMS').read_text().splitlines())
            self.assertEqual(checksums[handout.name], p.digest(handout))
            self.assertNotIn('SHA256SUMS', checksums)
            for filename, digest in checksums.items():
                self.assertEqual(p.digest(folder / filename), digest)
            with zipfile.ZipFile(handout) as z:
                self.assertIsNone(z.testzip())
                self.assertTrue({'Install-Windows.cmd', 'Install-Linux.sh', 'Uninstall-Windows.cmd',
                                 'Uninstall-Linux.sh', 'Update-Windows.cmd', 'Update-Linux.sh',
                                 's1ds_installer.py'} <= set(z.namelist()))
                for bundled_channel in ('public', 'beta'):
                    manifest = json.loads(z.read(f'Packages/{bundled_channel}/release-manifest.json'))
                    self.assertEqual(manifest['version'], '1.2.3')
                    self.assertEqual(manifest['channel'], bundled_channel)
                    self.assertEqual(manifest['commit'], 'fixture-commit')
                    for item in manifest['packages']:
                        content = z.read(f'Packages/{bundled_channel}/' + item['file'])
                        self.assertEqual(hashlib.sha256(content).hexdigest(), item['sha256'])

    def test_online_setup_contains_current_scripts_without_bundled_mods(self):
        self.package_both()
        with zipfile.ZipFile(self.output / 'public/Unnamed-Schedule-I-Setup.zip') as z:
            self.assertEqual(z.read('s1ds_installer.py'), (ROOT / 'packaging/Installer/s1ds_installer.py').read_bytes())
            self.assertFalse(any(name.startswith('Packages/') for name in z.namelist()))
            self.assertFalse(any(name.endswith('.dll') for name in z.namelist()))

    def test_package_defaults_to_current_source_version_and_rejects_an_old_override(self):
        with patch.object(p, 'ROOT', self.root), patch.object(p.subprocess, 'check_output', return_value='fixture-commit\n'):
            with self.assertRaisesRegex(ValueError, 'must match'):
                p.package('1.2.2', 'public', ['Il2cpp'], self.output, self.cache)
            p.package(None, 'public', ['Il2cpp'], self.output, self.cache)
        manifest = json.loads((self.output / 'public/release-manifest.json').read_text())
        self.assertEqual(manifest['version'], '1.2.3')
        self.assertEqual(manifest['tag'], 'public-v1.2.3')
