"""Original upstream release selection and migration through the shipping installer."""
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('upstream_installer', ROOT / 'packaging/Installer/s1ds_installer.py')
i = importlib.util.module_from_spec(spec); spec.loader.exec_module(i)


class UpstreamInstallerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.game = self.root / 'Steam Library/steamapps/common/Schedule I'; self.game.mkdir(parents=True)
        for name in ('Schedule I.exe', 'GameAssembly.dll'):
            (self.game / name).write_bytes(name.encode())
        (self.game / 'UserData').mkdir()
        (self.game / 'UserData/save.json').write_text('progress')
        (self.game / 'Mods').mkdir()
        (self.game / 'Mods/OtherMod.dll').write_bytes(b'other')
        self.settings = {'repository': 'owner/fork', 'upstream_repository': 'ifBars/S1DedicatedServers',
                         'loader': {'version': '0.7.2'}, 'favorite': {}}
        self.release = {'tag_name': 'v1.1.0', 'draft': False, 'prerelease': False,
                        'html_url': 'https://github.com/ifBars/S1DedicatedServers/releases/tag/v1.1.0', 'assets': []}
        self.payloads = {}
        for runtime in ('Il2cpp', 'Mono'):
            for side in ('Client', 'Server'):
                name = f'{runtime}_{side}.zip' if runtime == 'Il2cpp' else f'{runtime}-{side}.zip'
                contents = io.BytesIO()
                with zipfile.ZipFile(contents, 'w') as z:
                    z.writestr(f'Mods/DedicatedServerMod_{runtime}_{side}.dll', f'{runtime} {side} upstream'.encode())
                    z.writestr('README.md', 'upstream readme')
                url = 'https://example.invalid/' + name
                self.payloads[url] = contents.getvalue()
                self.release['assets'].append({'name': name, 'browser_download_url': url,
                                              'digest': 'sha256:' + hashlib.sha256(contents.getvalue()).hexdigest()})
        self.root_patch = patch.object(i, 'ROOT', self.root); self.root_patch.start(); self.addCleanup(self.root_patch.stop)
        api_patch = patch.object(i, 'request_json', return_value=self.release)
        self.api = api_patch.start(); self.addCleanup(api_patch.stop)
        network_patch = patch.object(i.urllib.request, 'urlopen', side_effect=lambda request, **kwargs: io.BytesIO(self.payloads[request.full_url]))
        self.network = network_patch.start(); self.addCleanup(network_patch.stop)

    def prepare(self, runtime='Il2cpp', side='Client', channel='public', tag=None, offline=False):
        staging = self.root / f'stage-{runtime}-{side}-{len(list(self.root.glob("stage-*")))}'; staging.mkdir()
        return i.prepare(self.settings, channel, runtime, side, staging, tag, offline, source='upstream')

    def test_upstream_latest_supports_all_published_runtime_side_packages(self):
        for runtime in ('Il2cpp', 'Mono'):
            for side in ('Client', 'Server'):
                m, dll = self.prepare(runtime, side)
                self.assertEqual(dll.read_bytes(), f'{runtime} {side} upstream'.encode())
                self.assertEqual(m['repository'], 'ifBars/S1DedicatedServers')
                self.assertEqual(m['source'], 'upstream')
                self.assertEqual(m['tag'], 'v1.1.0')
                self.assertIsNone(m['game']['build_id'], 'Do not claim fork game-build metadata applies to upstream')
        self.api.assert_called_with('https://api.github.com/repos/ifBars/S1DedicatedServers/releases/latest')

    def test_upstream_never_substitutes_for_beta_or_offline_fork_files(self):
        for channel, offline in [('beta', False), ('public', True)]:
            with self.subTest(channel=channel, offline=offline), self.assertRaisesRegex(ValueError, 'upstream|Upstream'):
                self.prepare(channel=channel, offline=offline)
        self.api.assert_not_called(); self.network.assert_not_called()

    def test_upstream_drafts_prereleases_and_wrong_explicit_tags_are_rejected(self):
        for field in ('draft', 'prerelease'):
            self.release[field] = True
            with self.assertRaises(ValueError): self.prepare()
            self.release[field] = False
        with self.assertRaises(ValueError): self.prepare(tag='v9.9.9')
        self.network.assert_not_called()

    def test_upstream_digest_missing_or_wrong_stops_before_install(self):
        asset = self.release['assets'][0]
        for digest in ('', 'sha256:' + '0'*64):
            asset['digest'] = digest
            with self.subTest(digest=digest), self.assertRaisesRegex(ValueError, 'checksum|Checksum|digest'):
                self.prepare()
        self.assertFalse((self.game / 'Mods/DedicatedServerMod_Il2cpp_Client.dll').exists())

    def test_upstream_missing_or_ambiguous_asset_is_rejected(self):
        asset = self.release['assets'].pop(0)
        with self.assertRaises(ValueError): self.prepare()
        self.release['assets'].extend([asset, dict(asset, name='Il2cpp-Client.zip')])
        with self.assertRaises(ValueError): self.prepare()
        self.network.assert_not_called()

    def test_explicit_upstream_tag_uses_that_release(self):
        self.prepare(tag='v1.1.0')
        self.api.assert_called_once_with('https://api.github.com/repos/ifBars/S1DedicatedServers/releases/tags/v1.1.0')

    def test_upstream_archive_cannot_write_outside_staging(self):
        asset = self.release['assets'][0]
        contents = io.BytesIO()
        with zipfile.ZipFile(contents, 'w') as z:
            z.writestr('../escape.dll', b'bad')
        self.payloads[asset['browser_download_url']] = contents.getvalue()
        asset['digest'] = 'sha256:' + hashlib.sha256(contents.getvalue()).hexdigest()
        with self.assertRaisesRegex(ValueError, 'Unsafe'):
            self.prepare()
        self.assertFalse((self.root / 'escape.dll').exists())

    def test_upstream_rejects_archive_without_the_selected_dll(self):
        asset = self.release['assets'][0]
        contents = io.BytesIO()
        with zipfile.ZipFile(contents, 'w') as z: z.writestr('README.md', 'no mod')
        self.payloads[asset['browser_download_url']] = contents.getvalue()
        asset['digest'] = 'sha256:' + hashlib.sha256(contents.getvalue()).hexdigest()
        with self.assertRaisesRegex(ValueError, 'missing'):
            self.prepare()

    def test_fork_upstream_fork_and_restore_preserve_original_and_save(self):
        target = self.game / 'Mods/DedicatedServerMod_Il2cpp_Client.dll'; target.write_bytes(b'original upstream')
        local = self.root / target.name; local.write_bytes(b'fork')
        manifest = {'source': 'fork', 'repository': 'owner/fork', 'channel': 'public', 'tag': 'public-v1',
                    'version': '1', 'runtime': 'Il2cpp', 'side': 'Client'}
        i.install(self.game, manifest, local, self.settings)
        upstream, dll = self.prepare()
        i.install(self.game, dict(upstream, runtime='Il2cpp', side='Client'), dll, self.settings)
        self.assertEqual(i.load_state(self.game)['source'], 'upstream')
        self.assertEqual(target.read_bytes(), b'Il2cpp Client upstream')
        i.install(self.game, manifest, local, self.settings)
        self.assertEqual(i.load_state(self.game)['source'], 'fork')
        i.uninstall(self.game, restore_previous=True)
        self.assertEqual(target.read_bytes(), b'original upstream')
        self.assertEqual((self.game / 'UserData/save.json').read_text(), 'progress')
        self.assertEqual((self.game / 'Mods/OtherMod.dll').read_bytes(), b'other')
        self.assertEqual((self.game / 'Schedule I.exe').read_bytes(), b'Schedule I.exe')

    def test_upstream_public_refuses_detected_beta_game_branch(self):
        steam_manifest = self.game.parent.parent / 'appmanifest_3164500.acf'
        with patch.object(i.subprocess, 'run') as run:
            run.return_value.returncode = 0 if i.os.name == 'nt' else 1
            for branch in ('beta', 'alternate-beta'):
                steam_manifest.write_text(f'"BetaKey" "{branch}"')
                with self.subTest(branch=branch), self.assertRaisesRegex(ValueError, 'detected beta'):
                    i.check_game(self.game, 'Il2cpp', None, public_only=True)
            steam_manifest.write_text('"BetaKey" "alternate"')
            i.check_game(self.game, 'Il2cpp', None, public_only=True)


if __name__ == '__main__':
    unittest.main()
