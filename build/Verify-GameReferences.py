"""Reject release metadata that disagrees with the private compilation references."""
import argparse
import hashlib
import json
from pathlib import Path


def verify(settings, metadata, branch, references):
    expected = settings['games'][branch.lower()]
    if metadata.get('game') != expected:
        raise ValueError(f'{branch} reference game {metadata.get("game")} does not match installer game {expected}')
    assembly = references / 'MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll'
    actual = hashlib.sha256(assembly.read_bytes()).hexdigest()
    if actual != metadata.get('wrapper_sha256'):
        raise ValueError('Generated game wrapper does not match its recorded SHA256')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--branch', required=True, choices=('Public', 'Beta'))
    parser.add_argument('--references', required=True, type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    settings = json.loads((root / 'packaging/Installer/installer-settings.json').read_text())
    metadata = json.loads((args.references / 'reference-info.json').read_text())
    verify(settings, metadata, args.branch, args.references)
    print(f'{args.branch} game references match the installer build and recorded wrapper hash')


if __name__ == '__main__':
    main()
