"""Build repeatable fork release assets from compiled mod DLLs and pinned dependencies."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parent.parent


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def zip_tree(folder, destination):
    with zipfile.ZipFile(destination, 'w', zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(folder.rglob('*')):
            if path.is_file():
                archive.write(path, path.relative_to(folder).as_posix())
    with zipfile.ZipFile(destination) as archive:
        if archive.testzip():
            raise ValueError('Invalid generated archive: ' + str(destination))


def dependency(item, cache):
    archive = cache / item['file']
    if not archive.exists() or digest(archive) != item['sha256']:
        cache.mkdir(parents=True, exist_ok=True)
        temporary = archive.with_suffix('.part')
        try:
            request = urllib.request.Request(item['url'], headers={'User-Agent': 'S1DS-Packager'})
            with urllib.request.urlopen(request, timeout=60) as source, temporary.open('wb') as target:
                shutil.copyfileobj(source, target)
            if digest(temporary) != item['sha256']:
                raise ValueError('Dependency checksum mismatch: ' + item['file'])
            temporary.replace(archive)
        finally:
            temporary.unlink(missing_ok=True)
    return archive


def write_checksums(folder):
    files = sorted(p for p in folder.iterdir() if p.is_file() and p.name != 'SHA256SUMS')
    (folder / 'SHA256SUMS').write_text(''.join(f'{digest(p)}  {p.name}\n' for p in files), encoding='utf-8')


def package(version, channel, runtimes, output, cache):
    source_version = re.search(r'ModVersion = "([^"]+)"', (ROOT / 'API/Version.cs').read_text()).group(1)
    version = version or source_version
    if not re.fullmatch(r'\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?', version):
        raise ValueError('Invalid release version.')
    if version != source_version:
        raise ValueError('Release version must match API/Version.cs.')
    settings = json.loads((ROOT / 'packaging/Installer/installer-settings.json').read_text())
    branch = channel.title()
    tag = channel + '-v' + version
    dest = output / channel
    if dest.exists() and any(dest.iterdir()):
        raise ValueError('Choose an empty output folder; published assets must not be overwritten.')
    dest.mkdir(parents=True, exist_ok=True)
    manifest = {'schema': 1, 'repository': settings['repository'], 'channel': channel, 'tag': tag, 'version': version,
                'commit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
                'game': settings['games'][channel], 'loader': settings['loader'], 'packages': []}
    with tempfile.TemporaryDirectory(prefix='s1ds-packaging-') as temporary:
        stage = Path(temporary)
        for runtime in runtimes:
            for side in ('Client', 'Server'):
                dll = ROOT / f'bin/{branch}/{runtime}_{side}/' / ('net6.0' if runtime == 'Il2cpp' else 'netstandard2.1') / f'DedicatedServerMod_{runtime}_{side}.dll'
                if not dll.is_file():
                    raise ValueError(f'Missing compiled {branch} {runtime} {side} DLL: {dll}')
                folder = stage / f'{runtime}-{side}'
                (folder / 'Mods').mkdir(parents=True)
                shutil.copy2(dll, folder / 'Mods' / dll.name)
                for item in ('LICENSE', 'FORK_NOTES.txt'):
                    shutil.copy2(ROOT / item, folder / item)
                (folder / 'README.md').write_text(f'# S1DS fork {version}\n\n{branch} / {runtime} / {side}. Game {manifest["game"]["version"]}, Steam build {manifest["game"]["build_id"]}.\n\nUse the accompanying installer. Keep game branch and client/server mod versions matched.\n', encoding='utf-8')
                if side == 'Server':
                    shutil.copy2(ROOT / 'packaging/Server/start_server.bat', folder / 'start_server.bat')
                filename = f'S1DS-{branch}-{runtime}-{side}.zip'
                zip_tree(folder, dest / filename)
                manifest['packages'].append({'file': filename, 'runtime': runtime, 'side': side, 'sha256': digest(dest / filename), 'dll_sha256': digest(dll)})
        (dest / 'release-manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
        setup = stage / 'Setup'
        shutil.copytree(ROOT / 'packaging/Installer', setup, ignore=shutil.ignore_patterns('__pycache__', '.downloads', '.runtime*', 'Packages', 'Dependencies'))
        shutil.copy2(ROOT / 'LICENSE', setup / 'S1DS-LICENSE.txt')
        zip_tree(setup, dest / 'Unnamed-Schedule-I-Setup.zip')
        packages = setup / 'Packages' / channel
        packages.mkdir(parents=True)
        for item in manifest['packages']:
            shutil.copy2(dest / item['file'], packages / item['file'])
        shutil.copy2(dest / 'release-manifest.json', packages / 'release-manifest.json')
        deps = setup / 'Dependencies'; deps.mkdir()
        for item in ('loader', 'python'):
            shutil.copy2(dependency(settings[item], cache), deps / settings[item]['file'])
        zip_tree(setup, dest / 'Unnamed-Schedule-I-Client.zip')
    write_checksums(dest)
    print(dest)
    return dest


def bundle(output):
    """A single handout contains both channels for offline switching."""
    with tempfile.TemporaryDirectory(prefix='s1ds-handout-') as temporary:
        stage = Path(temporary)
        with zipfile.ZipFile(output / 'public/Unnamed-Schedule-I-Client.zip') as source:
            source.extractall(stage)
        (stage / 'Packages/beta').mkdir(parents=True)
        for path in (output / 'beta').glob('*'):
            if path.name == 'release-manifest.json' or path.name.startswith('S1DS-') and path.suffix == '.zip':
                shutil.copy2(path, stage / 'Packages/beta' / path.name)
        zip_tree(stage, output / 'Unnamed-Schedule-I-Client.zip')
    # Keep the historical local name and include the same handout in each release.
    for channel in ('public', 'beta'):
        folder = output / channel
        shutil.copy2(output / 'Unnamed-Schedule-I-Client.zip', folder / 'Unnamed-Schedule-I-Public-and-Beta.zip')
        write_checksums(folder)
    print(output / 'Unnamed-Schedule-I-Client.zip')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version', help='Defaults to the current fork version in API/Version.cs.')
    parser.add_argument('--channel', choices=['public', 'beta', 'both'], default='both')
    parser.add_argument('--runtimes', nargs='+', choices=['Il2cpp', 'Mono'], default=['Il2cpp'])
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--dependency-cache', type=Path, default=ROOT / 'artifacts/dependencies')
    args = parser.parse_args()
    for channel in ('public', 'beta') if args.channel == 'both' else (args.channel,):
        package(args.version, channel, args.runtimes, args.output.resolve(), args.dependency_cache.resolve())
    if args.channel == 'both':
        bundle(args.output.resolve())
