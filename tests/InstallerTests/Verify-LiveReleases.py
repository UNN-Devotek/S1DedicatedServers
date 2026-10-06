"""Opt-in real GitHub download/install smoke test; uses temporary game fixtures only."""
import importlib.util
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import urllib.parse
from types import SimpleNamespace
from unittest.mock import patch

REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('live_installer', REPO / 'packaging/Installer/s1ds_installer.py')
i = importlib.util.module_from_spec(spec); spec.loader.exec_module(i)
settings = i.read_json(i.ROOT / 'installer-settings.json')
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--github-cli-auth', action='store_true', help='Use an existing gh login for API metadata if this machine reached the anonymous rate limit')
args = parser.parse_args()
if args.github_cli_auth:
    original_request = i.request_json
    def request_json(url):
        if urllib.parse.urlparse(url).hostname == 'api.github.com':
            return json.loads(subprocess.check_output(['gh', 'api', url], text=True))
        return original_request(url)
    i.request_json = request_json
results = []
with tempfile.TemporaryDirectory(prefix='s1ds-live-installer-') as directory:
    root = Path(directory)
    i.ROOT = root / 'Installer'; i.ROOT.mkdir()
    i.write_json(i.ROOT / 'installer-settings.json', settings)
    cached_loader = REPO / 'artifacts/dependencies' / settings['loader']['file']
    if cached_loader.is_file():
        cache = i.ROOT / '.downloads'; cache.mkdir()
        shutil.copy2(cached_loader, cache / cached_loader.name)
    # Verify all four original upstream assets, including their real ZIP digests.
    original = None
    for runtime in ('Il2cpp', 'Mono'):
        for side in ('Client', 'Server'):
            staging = root / f'upstream-{runtime}-{side}'; staging.mkdir()
            manifest, dll = i.prepare(settings, 'public', runtime, side, staging, None, False, 'upstream')
            results.append({'action': 'download-verify', 'source': 'upstream', 'runtime': runtime,
                            'side': side, 'tag': manifest['tag'], 'dll_sha256': i.sha256(dll)})
            if runtime == 'Il2cpp' and side == 'Client': original = dll
    game = root / 'Steam Library/steamapps/common/Schedule I'; game.mkdir(parents=True)
    protected = {'Schedule I.exe': b'fixture executable', 'GameAssembly.dll': b'fixture assembly',
                 'Schedule I_Data/sharedassets0.assets': b'base assets',
                 'UserData/save.json': b'player progress', 'Mods/OtherMod.dll': b'unrelated mod'}
    for relative, data in protected.items():
        target = game / relative; target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)
    target = game / 'Mods/DedicatedServerMod_Il2cpp_Client.dll'
    shutil.copy2(original, target); original_hash = i.sha256(original)
    real_run = subprocess.run
    def fixture_process_check(command, *args, **kwargs):
        # A running local test server must not prevent exercising fake game files.
        if command[:2] == ['pgrep', '-x']: return SimpleNamespace(returncode=1)
        if command[:2] == ['powershell.exe', '-NoProfile']: return SimpleNamespace(returncode=0)
        return real_run(command, *args, **kwargs)
    with patch.object(i.subprocess, 'run', side_effect=fixture_process_check):
        # No receipt yet: detect the actual upstream DLL, then adopt/update it.
        (game.parent.parent/'appmanifest_3164500.acf').write_text(f'"buildid" "{settings["games"]["public"]["build_id"]}"')
        assert i.main(['update','--game-directory',str(game)]) == 0
        state = i.load_state(game)
        assert state['source'] == 'upstream' and state['channel'] == 'public'
        results.append({'action':'automatic-manual-upstream','source':state['source'],'tag':state['tag']})
        steps = [('fork', 'public', 'public-v1.1.0-unn.6'), ('fork', 'public', None),
                 ('fork', 'beta', None), ('upstream', 'public', None), ('fork', 'public', None)]
        for source, channel, tag in steps:
            steam_data = f'"buildid" "{settings["games"][channel]["build_id"]}"\n"BetaKey" "{"beta" if channel == "beta" else ""}"\n'
            (game.parent.parent / 'appmanifest_3164500.acf').write_text(steam_data)
            args = ['install', '--game-directory', str(game), '--source', source, '--channel', channel]
            if tag: args += ['--tag', tag]
            assert i.main(args) == 0
            state = i.load_state(game)
            assert state['source'] == source and state['channel'] == channel
            assert i.sha256(target) == state['files']['Mods/' + target.name]['installed_sha256']
            for relative, data in protected.items(): assert (game / relative).read_bytes() == data
            results.append({'action': 'install-update', 'source': source, 'channel': channel,
                            'tag': state['tag'], 'dll_sha256': i.sha256(target), 'protected_files': 'unchanged'})
            before = target.stat().st_mtime_ns
            assert i.main(['update','--game-directory',str(game)]) == 0
            updated = i.load_state(game)
            assert updated['source'] == source and updated['channel'] == channel
            if tag is None: assert target.stat().st_mtime_ns == before, 'Current files should not be rewritten'
            results.append({'action':'automatic-update','source':source,'channel':channel,'tag':updated['tag']})
        # A separate manual beta install, without a receipt, must stay beta.
        beta_game = root/'Manual Beta/steamapps/common/Schedule I'; beta_game.mkdir(parents=True)
        for relative,data in protected.items():
            f=beta_game/relative;f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(data)
        beta_stage=root/'old-manual-beta';beta_stage.mkdir()
        _,beta_dll=i.prepare(settings,'beta','Il2cpp','Client',beta_stage,'beta-v1.1.0-unn.7',False)
        shutil.copy2(beta_dll,beta_game/'Mods'/beta_dll.name)
        (beta_game.parent.parent/'appmanifest_3164500.acf').write_text(f'"BetaKey" "beta" "buildid" "{settings["games"]["beta"]["build_id"]}"')
        assert i.main(['update','--game-directory',str(beta_game)]) == 0
        beta_state=i.load_state(beta_game)
        assert beta_state['source']=='fork' and beta_state['channel']=='beta'
        for relative,data in protected.items():assert (beta_game/relative).read_bytes()==data
        results.append({'action':'automatic-manual-beta','source':'fork','channel':'beta','tag':beta_state['tag'],'protected_files':'unchanged'})
        assert i.main(['uninstall', '--game-directory', str(game), '--restore-previous']) == 0
        assert i.sha256(target) == original_hash
        for relative, data in protected.items(): assert (game / relative).read_bytes() == data
        results.append({'action': 'uninstall-restore', 'original_upstream_sha256': original_hash,
                        'protected_files': 'unchanged'})
print('LIVE_INSTALLER_RESULT=' + json.dumps(results, sort_keys=True))
