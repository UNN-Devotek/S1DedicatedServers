"""Install, switch, and remove fork builds without replacing a player's game or saves."""
from __future__ import annotations

import argparse
import contextlib
import copy
import hashlib
import json
import ntpath
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import sys
import tempfile
import urllib.parse
import urllib.request
import uuid
import zipfile

ROOT = Path(__file__).resolve().parent
STATE_DIR = '.s1ds-installer'
MOD_PATTERN = re.compile(r'^Mods/DedicatedServerMod_(Mono|Il2cpp)_(Client|Server)\.dll$')


def read_json(path: Path) -> dict:
    value = json.loads(path.read_text(encoding='utf-8-sig'))
    if not isinstance(value, dict):
        raise ValueError(f'Expected a JSON object: {path.name}')
    return value


def write_json(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    try:
        temporary.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def sha256(path: Path) -> str:
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest() if sys.version_info >= (3, 11) else hashlib.sha256(stream.read()).hexdigest()


def safe_path(root: Path, relative: str) -> Path:
    name = relative.replace('\\', '/')
    parts = PurePosixPath(name).parts
    if not parts or name.startswith('/') or ntpath.splitdrive(name)[0] or any(p in ('..', '.', '') or ':' in p for p in parts):
        raise ValueError(f'Unsafe package path: {relative}')
    result = root.joinpath(*parts)
    if not result.resolve().is_relative_to(root.resolve()):
        raise ValueError(f'Path escapes the selected folder: {relative}')
    candidate = result
    while candidate != root:
        if candidate.is_symlink() or (hasattr(candidate, 'is_junction') and candidate.is_junction()):
            raise ValueError(f'Linked install path is not supported: {relative}')
        candidate = candidate.parent
    return result


def managed_path(relative: str) -> bool:
    return bool(MOD_PATTERN.fullmatch(relative)) or relative in ('version.dll', 'dobby.dll') or relative.startswith('MelonLoader/')


def extract(archive: Path, destination: Path) -> None:
    with zipfile.ZipFile(archive) as source:
        for item in source.infolist():
            target = safe_path(destination, item.filename)
            if stat.S_ISLNK(item.external_attr >> 16):
                raise ValueError('A downloaded archive contains a symbolic link.')
            if item.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with source.open(item) as incoming, target.open('wb') as outgoing:
                    shutil.copyfileobj(incoming, outgoing)


def request_json(url: str):
    request = urllib.request.Request(url, headers={'User-Agent': 'S1DS-Fork-Installer', 'Accept': 'application/vnd.github+json'})
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


def download(url: str, target: Path, checksum: str) -> Path:
    if not re.fullmatch(r'[0-9a-fA-F]{64}', checksum or ''):
        raise ValueError('Missing or invalid download checksum.')
    if target.exists() and sha256(target) == checksum.lower():
        return target
    if urllib.parse.urlparse(url).scheme != 'https':
        raise ValueError('Downloads must use HTTPS.')
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_suffix(target.suffix + '.part')
    try:
        request = urllib.request.Request(url, headers={'User-Agent': 'S1DS-Fork-Installer'})
        with urllib.request.urlopen(request, timeout=60) as response, temporary.open('wb') as output:
            shutil.copyfileobj(response, output)
        if sha256(temporary) != checksum.lower():
            raise ValueError(f'Checksum mismatch: {target.name}. Installation stopped.')
        os.replace(temporary, target)
    finally:
        temporary.unlink(missing_ok=True)
    return target


def select_release(releases: list, channel: str) -> dict:
    candidates = [r for r in releases if not r.get('draft') and
                  r.get('tag_name', '').startswith(channel + '-v') and
                  (channel == 'beta' or not r.get('prerelease')) and
                  any(a.get('name') == 'release-manifest.json' for a in r.get('assets', []))]
    if not candidates:
        raise ValueError(f'No published {channel} release with an installer manifest was found.')
    return max(candidates, key=lambda r: r.get('published_at') or '')


def release_manifest(settings: dict, channel: str, tag: str | None = None, offline: bool = False) -> tuple[dict, dict]:
    if offline:
        manifest = read_json(ROOT / 'Packages' / channel / 'release-manifest.json')
        if tag and manifest.get('tag') != tag:
            raise ValueError('The requested tag does not match the offline package.')
        release = {}
    else:
        base = 'https://api.github.com/repos/' + settings['repository'] + '/releases'
        if tag:
            release = request_json(base + '/tags/' + urllib.parse.quote(tag, safe=''))
            release = select_release([release], channel)
        else:
            # Releases are ordered by creation rather than publication; scan every page.
            releases = []
            for page in range(1, 11):
                batch = request_json(base + f'?per_page=100&page={page}')
                releases.extend(batch)
                if len(batch) < 100:
                    break
            release = select_release(releases, channel)
        asset = next(a for a in release['assets'] if a['name'] == 'release-manifest.json')
        manifest = request_json(asset['browser_download_url'])
    if manifest.get('schema') != 1 or manifest.get('channel') != channel or not manifest.get('tag', '').startswith(channel + '-v'):
        raise ValueError('Release manifest does not match the selected channel.')
    if release and manifest['tag'] != release['tag_name']:
        raise ValueError('Release manifest tag does not match the GitHub release.')
    return manifest, release


def prepare_upstream(settings: dict, channel: str, runtime: str, side: str, staging: Path, tag: str | None, offline: bool) -> tuple[dict, Path]:
    """Verify an original ifBars release archive before using its selected mod DLL."""
    if channel != 'public' or offline:
        raise ValueError('Upstream installs require public and online downloads. Use the fork for beta or bundled files.')
    repository = settings['upstream_repository']
    base = 'https://api.github.com/repos/' + repository + '/releases'
    release = request_json(base + ('/tags/' + urllib.parse.quote(tag, safe='') if tag else '/latest'))
    if release.get('draft') or release.get('prerelease') or (tag and release.get('tag_name') != tag):
        raise ValueError('Choose a published stable upstream release.')
    selected_tag = release['tag_name']
    expected = f'{runtime}_{side}.zip'.lower()
    assets = [a for a in release.get('assets', []) if a.get('name', '').replace('-', '_').lower() == expected]
    if len(assets) != 1:
        raise ValueError(f'Upstream release has no unique {runtime} {side} package.')
    asset = assets[0]
    digest = asset.get('digest') or ''
    if not re.fullmatch(r'sha256:[0-9a-fA-F]{64}', digest):
        raise ValueError('Upstream asset has no SHA256 digest. Select a newer release with verified downloads.')
    cache = safe_path(ROOT, '.downloads/upstream/' + selected_tag)
    archive = download(asset['browser_download_url'], safe_path(cache, asset['name']), digest.split(':', 1)[1])
    payload = staging / 'mod'
    extract(archive, payload)
    mod = safe_path(payload, f'Mods/DedicatedServerMod_{runtime}_{side}.dll')
    if not mod.is_file():
        raise ValueError('Upstream package is missing the selected mod DLL.')
    # Upstream publishes archive digests but no fork-style game build manifest.
    # The verified archive covers this DLL; do not infer a Steam build ID.
    manifest = {'schema': 1, 'source': 'upstream', 'repository': repository,
                'channel': 'public', 'tag': selected_tag, 'version': selected_tag.removeprefix('v'),
                'game': {'build_id': None}, 'loader': settings['loader'],
                'packages': [{'file': asset['name'], 'runtime': runtime, 'side': side,
                              'sha256': digest.split(':', 1)[1].lower(), 'dll_sha256': sha256(mod)}]}
    return manifest, mod


def prepare(settings: dict, channel: str, runtime: str, side: str, staging: Path, tag: str | None, offline: bool, source: str = 'fork') -> tuple[dict, Path]:
    if source == 'upstream':
        return prepare_upstream(settings, channel, runtime, side, staging, tag, offline)
    if source != 'fork':
        raise ValueError('Choose fork or upstream as the release source.')
    manifest, release = release_manifest(settings, channel, tag, offline)
    matches = [p for p in manifest['packages'] if p['runtime'] == runtime and p['side'] == side]
    if len(matches) != 1:
        raise ValueError(f'This release has no unique {runtime} {side} package.')
    package = matches[0]
    cache = ROOT / '.downloads' / manifest['tag']
    archive = safe_path(ROOT / 'Packages' / channel, package['file']) if offline else safe_path(cache, package['file'])
    if offline:
        if sha256(archive) != package['sha256']:
            raise ValueError('Offline mod package checksum mismatch.')
    else:
        asset = next((a for a in release['assets'] if a['name'] == package['file']), None)
        if not asset:
            raise ValueError('Release is missing its selected mod package.')
        download(asset['browser_download_url'], archive, package['sha256'])
    payload = staging / 'mod'
    extract(archive, payload)
    relative = f'Mods/DedicatedServerMod_{runtime}_{side}.dll'
    mod = safe_path(payload, relative)
    if not mod.is_file() or sha256(mod) != package['dll_sha256']:
        raise ValueError('The selected mod DLL does not match its release manifest.')
    return manifest, mod


def check_game(game: Path, runtime: str | None = None, build_id: str | None = None, allow_mismatch: bool = False, public_only: bool = False) -> None:
    if not game.is_dir() or not (game / 'Schedule I.exe').is_file():
        raise ValueError('Choose the folder containing Schedule I.exe.')
    safe_path(game, STATE_DIR + '/state.json')
    if runtime == 'Il2cpp' and not (game / 'GameAssembly.dll').is_file():
        raise ValueError('This is an IL2CPP build. Select the matching IL2CPP game branch in Steam first.')
    if runtime == 'Mono' and not (game / 'Schedule I_Data/Managed/Assembly-CSharp.dll').is_file():
        raise ValueError('This is a Mono build. Select the matching Mono game branch in Steam first.')
    if os.name == 'nt':
        result = subprocess.run(['powershell.exe', '-NoProfile', '-Command', "if (Get-Process -Name 'Schedule I' -ErrorAction SilentlyContinue) { exit 1 }"], capture_output=True)
        if result.returncode:
            raise ValueError('Close Schedule I before changing its mod files.')
    elif shutil.which('pgrep'):
        if subprocess.run(['pgrep', '-x', 'Schedule I.exe'], stdout=subprocess.DEVNULL).returncode == 0:
            raise ValueError('Close Schedule I before changing its mod files.')
    steam_manifest = game.parent.parent / 'appmanifest_3164500.acf'
    if steam_manifest.is_file():
        steam_data = steam_manifest.read_text(encoding='utf-8')
        branches = re.findall(r'"BetaKey"\s+"([^"]*)"', steam_data, flags=re.IGNORECASE)
        if public_only and any('beta' in branch.lower() for branch in branches) and not allow_mismatch:
            raise ValueError('Original upstream public files cannot be installed on a detected beta game branch. Switch/update the game in Steam first.')
        found = re.search(r'"buildid"\s+"(\d+)"', steam_data)
        if build_id and found and found.group(1) != str(build_id) and not allow_mismatch:
            raise ValueError(f'Installed Steam build {found.group(1)} does not match required build {build_id}. Switch/update the game in Steam first. Use --allow-game-version-mismatch only for an intentional manual override.')


def load_state(game: Path) -> dict:
    path = safe_path(game, STATE_DIR + '/state.json')
    state = read_json(path) if path.exists() else {'schema': 1, 'files': {}}
    if state.get('schema') != 1 or not isinstance(state.get('files'), dict):
        raise ValueError('Unsupported installer receipt. Keep its backups and contact the host.')
    for relative, entry in state['files'].items():
        if not managed_path(relative) or not isinstance(entry, dict) or not re.fullmatch(r'[0-9a-f]{64}', entry.get('installed_sha256', '')):
            raise ValueError('Invalid installer receipt.')
        safe_path(game, relative)
        if entry.get('original'):
            safe_path(game / STATE_DIR, entry['original'])
    return state


@contextlib.contextmanager
def operation(game: Path):
    home = safe_path(game, STATE_DIR)
    home.mkdir(exist_ok=True)
    lock = home / 'operation.lock'
    try:
        descriptor = os.open(lock, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except FileExistsError:
        raise ValueError('Another installer operation is active. If it crashed, close the game and remove .s1ds-installer/operation.lock before retrying.') from None
    os.close(descriptor)
    transaction = home / ('transaction-' + uuid.uuid4().hex)
    transaction.mkdir()
    snapshots: dict[str, bool] = {}
    rollback_failed = False

    def remember(relative: str) -> Path:
        target = safe_path(game, relative)
        if relative not in snapshots:
            snapshots[relative] = target.exists()
            if target.exists():
                backup = safe_path(transaction, relative)
                backup.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(target, backup)
            write_json(transaction / 'recovery.json', {'files': snapshots})
        return target

    try:
        yield remember
    except BaseException:
        try:
            for relative, existed in reversed(list(snapshots.items())):
                target = safe_path(game, relative)
                if existed:
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(safe_path(transaction, relative), target)
                else:
                    target.unlink(missing_ok=True)
        except OSError as error:
            rollback_failed = True
            raise OSError(f'Rollback could not finish. Recovery copies are retained at {transaction}. Keep the game closed and recover those files before removing the operation lock.') from error
        raise
    finally:
        if not rollback_failed:
            shutil.rmtree(transaction)
            lock.unlink(missing_ok=True)



def favorites(game: Path) -> tuple[Path, dict]:
    path = safe_path(game, 'UserData/DedicatedServerClientServers.json')
    data = read_json(path) if path.exists() else {}
    for name in ('Favorites', 'History'):
        data.setdefault(name, [])
        if data[name] is None:
            data[name] = []
        if not isinstance(data[name], list) or any(not isinstance(e, dict) for e in data[name]):
            raise ValueError('Invalid favorites/history file; repair it before retrying.')
    return path, data


def install(game: Path, manifest: dict, mod: Path, settings: dict, loader: Path | None = None) -> None:
    state = copy.deepcopy(load_state(game))
    files = {f'Mods/{mod.name}': mod}
    if loader:
        for source in loader.rglob('*'):
            if source.is_file():
                relative = source.relative_to(loader).as_posix()
                if not managed_path(relative) or MOD_PATTERN.fullmatch(relative):
                    raise ValueError('Loader package includes an unexpected install path.')
                files[relative] = source
    for relative in files:
        if not managed_path(relative):
            raise ValueError('Unexpected installer file: ' + relative)
        safe_path(game, relative)
    favorite_path, favorite_data = favorites(game) if manifest['side'] == 'Client' else (None, None)
    with operation(game) as remember:
        state = copy.deepcopy(load_state(game))
        for relative, source in files.items():
            target = remember(relative)
            entry = state['files'].get(relative)
            if entry is None:
                original = None
                if target.exists():
                    original = 'originals/' + uuid.uuid4().hex + '/' + relative
                    backup = safe_path(game / STATE_DIR, original)
                    backup.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(target, backup)
                entry = {'original': original, 'original_sha256': sha256(target) if original else None}
            elif target.exists() and sha256(target) != entry['installed_sha256']:
                recovery = safe_path(game / STATE_DIR, 'recovery/' + uuid.uuid4().hex + '/' + relative)
                recovery.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(target, recovery)
                print(f'Preserved locally modified file in {recovery}')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
            entry['installed_sha256'] = sha256(target)
            state['files'][relative] = entry
        if favorite_path:
            entry = settings.get('favorite')
            if entry and not any(e.get('Host') == entry['Host'] and e.get('Port') == entry['Port'] for e in favorite_data['Favorites']):
                added = dict(entry, Id=uuid.uuid4().hex)
                favorite_data['Favorites'].insert(0, added)
                state['added_favorite'] = added
                remember('UserData/DedicatedServerClientServers.json')
                write_json(favorite_path, favorite_data)
        state.update(uninstalled=False, source=manifest.get('source', 'fork'),
                     repository=manifest.get('repository', settings.get('repository')),
                     channel=manifest['channel'], tag=manifest['tag'], runtime=manifest['runtime'],
                     side=manifest['side'], version=manifest['version'])
        remember(STATE_DIR + '/state.json')
        write_json(game / STATE_DIR / 'state.json', state)
    print(f"Installed {state['source']} {state['channel']} {state['runtime']} {state['side']} {state['version']}.")
    print('Original files and recovery copies: ' + str(game / STATE_DIR))


def uninstall(game: Path, keep_loader: bool = False, restore_previous: bool = False) -> list[str]:
    state = copy.deepcopy(load_state(game))
    if not state['files']:
        print('No installer-owned files are recorded. Other files were left in place.')
        return []
    other_mods = [p for folder in ('Mods', 'Plugins') for p in (game / folder).glob('*.dll') if p.relative_to(game).as_posix() not in state['files']]
    keep_loader = keep_loader or bool(other_mods)
    retained = []
    with operation(game) as remember:
        state = copy.deepcopy(load_state(game))
        for relative, entry in list(state['files'].items()):
            if keep_loader and not MOD_PATTERN.fullmatch(relative):
                retained.append(relative)
                continue
            target = safe_path(game, relative)
            if target.exists() and sha256(target) != entry['installed_sha256']:
                retained.append(relative)
                print('Preserved modified file: ' + relative)
                continue
            remember(relative)
            original = entry.get('original')
            if original and (restore_previous or not MOD_PATTERN.fullmatch(relative)):
                backup = safe_path(game / STATE_DIR, original)
                if not backup.is_file() or sha256(backup) != entry.get('original_sha256'):
                    raise ValueError('Missing or changed original backup: ' + relative)
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(backup, target)
            else:
                target.unlink(missing_ok=True)
            del state['files'][relative]
        added = state.get('added_favorite')
        if added:
            path, data = favorites(game)
            remaining = [e for e in data['Favorites'] if not all(e.get(k) == v for k, v in added.items())]
            if len(remaining) != len(data['Favorites']):
                data['Favorites'] = remaining
                remember('UserData/DedicatedServerClientServers.json')
                write_json(path, data)
            state.pop('added_favorite', None)
        remember(STATE_DIR + '/state.json')
        state['uninstalled'] = not any(MOD_PATTERN.fullmatch(p) for p in state['files'])
        write_json(game / STATE_DIR / 'state.json', state)
    print('Recorded uninstall processed. Saves, history, unrelated mods and backups were preserved.')
    if keep_loader:
        print('MelonLoader retained because --keep-loader was selected or other mods/plugins use it.')
    if retained:
        print(f'{len(retained)} loader/modified files retained; the receipt records them for future removal.')
    return retained


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['install', 'uninstall', 'download', 'status', 'check', 'menu'])
    parser.add_argument('--game-directory')
    parser.add_argument('--channel', choices=['public', 'beta'])
    parser.add_argument('--source', choices=['fork', 'upstream'], default='fork')
    parser.add_argument('--runtime', choices=['Il2cpp', 'Mono'], default='Il2cpp')
    parser.add_argument('--side', choices=['Client', 'Server'], default='Client')
    parser.add_argument('--tag')
    parser.add_argument('--offline', action='store_true')
    parser.add_argument('--keep-loader', action='store_true')
    parser.add_argument('--restore-previous', action='store_true')
    parser.add_argument('--allow-game-version-mismatch', action='store_true')
    args = parser.parse_args(argv)
    settings = read_json(ROOT / 'installer-settings.json')
    if args.action == 'menu':
        choice = input('1: Install/update Fork Public\n2: Install/update Fork Beta\n3: Uninstall\n4: Status\n5: Install/update Original ifBars Public\nChoose [1]: ').strip() or '1'
        if choice not in ('1', '2', '3', '4', '5'):
            raise ValueError('Choose 1, 2, 3, 4 or 5.')
        args.action, args.channel = {'1': ('install', 'public'), '2': ('install', 'beta'), '3': ('uninstall', None), '4': ('status', None), '5': ('install', 'public')}[choice]
        if choice == '5':
            args.source = 'upstream'
        elif choice in ('1', '2'):
            args.source = 'fork'
        if args.action == 'install' and args.source == 'fork' and not args.offline and (ROOT / 'Packages' / args.channel / 'release-manifest.json').is_file():
            args.offline = input('1: Download latest release\n2: Use bundled files (offline)\nChoose [1]: ').strip() == '2'
    game = None
    if args.action != 'download':
        raw = args.game_directory or input('Game folder containing Schedule I.exe: ')
        game = Path(raw.strip().strip('"')).expanduser().resolve(strict=True)
        check_game(game)
    if args.action == 'status':
        state = load_state(game)
        print(json.dumps({k: state.get(k) for k in ['source', 'repository', 'channel', 'tag', 'runtime', 'side', 'version', 'uninstalled']}, indent=2))
        return 0
    if args.action == 'uninstall':
        retained = uninstall(game, args.keep_loader, args.restore_previous)
        return 2 if any(MOD_PATTERN.fullmatch(p) for p in retained) else 0
    channel = args.channel or 'public'
    print(f'Selected {args.source} {channel} mod files. Switch Steam to the matching game branch first; the installer does not change Steam game files.')
    with tempfile.TemporaryDirectory(prefix='s1ds-installer-') as temporary:
        staging = Path(temporary)
        manifest, mod = prepare(settings, channel, args.runtime, args.side, staging, args.tag, args.offline, args.source)
        print(f"Release: {manifest.get('repository', settings['repository'])} / {manifest['tag']}")
        if args.source == 'upstream':
            print('Original upstream files replace the fork mod. Use a matching upstream server and the public game branch. Upstream does not publish a Steam build ID; only the game runtime can be checked.')
        selected = dict(manifest, runtime=args.runtime, side=args.side)
        if game:
            check_game(game, args.runtime, manifest['game']['build_id'], args.allow_game_version_mismatch, args.source == 'upstream')
            existing = load_state(game)
            if existing.get('side', args.side) != args.side or existing.get('runtime', args.runtime) != args.runtime:
                raise ValueError('Uninstall the existing runtime/side before changing it. Public/beta switching uses the same runtime/side.')
            if args.side == 'Client':
                favorites(game)
        loader = None
        if not game or not (game / 'version.dll').is_file() or not (game / 'MelonLoader/net6/MelonLoader.dll').is_file():
            dependency = manifest['loader']
            if dependency != settings['loader']:
                raise ValueError('This release needs a different MelonLoader version. Download its updated installer.')
            archive = ROOT / 'Dependencies' / dependency['file'] if args.offline else ROOT / '.downloads' / dependency['file']
            if args.offline:
                if sha256(archive) != dependency['sha256']:
                    raise ValueError('Offline loader checksum mismatch.')
            else:
                download(dependency['url'], archive, dependency['sha256'])
            loader = staging / 'loader'
            extract(archive, loader)
        elif not any(p.startswith('MelonLoader/') for p in existing['files']):
            print('Existing MelonLoader retained. This release requires compatible MelonLoader ' + settings['loader']['version'] + '.')
        if args.action in ('download', 'check'):
            print('Selected downloads and game input verified; no game files changed.')
        else:
            install(game, selected, mod, settings, loader)
            print('Launch through Steam. Clients and server must use matching game/mod channels.')
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (OSError, ValueError, KeyError, TypeError, zipfile.BadZipFile) as error:
        print('S1DS setup failed: ' + str(error), file=sys.stderr)
        sys.exit(1)
