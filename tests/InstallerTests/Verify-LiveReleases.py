"""Opt-in real GitHub download/install smoke test; uses temporary game fixtures only."""
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
from types import SimpleNamespace
from unittest.mock import patch

REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('live_installer', REPO / 'packaging/Installer/s1ds_installer.py')
i = importlib.util.module_from_spec(spec); spec.loader.exec_module(i)
settings = i.read_json(i.ROOT / 'installer-settings.json')
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
        assert i.main(['uninstall', '--game-directory', str(game), '--restore-previous']) == 0
        assert i.sha256(target) == original_hash
        for relative, data in protected.items(): assert (game / relative).read_bytes() == data
        results.append({'action': 'uninstall-restore', 'original_upstream_sha256': original_hash,
                        'protected_files': 'unchanged'})
print('LIVE_INSTALLER_RESULT=' + json.dumps(results, sort_keys=True))
