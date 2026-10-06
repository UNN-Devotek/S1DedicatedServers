#!/usr/bin/env bash
set -euo pipefail
package_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
command -v python3 >/dev/null || { echo 'Install Python 3.10+ and retry.' >&2; exit 1; }
python3 -c 'import sys; assert sys.version_info >= (3,10), "Python 3.10+ is required"'
game_dir="${1:-}"
channel="${2:-}"
extra=(); if [[ $# -gt 2 ]]; then extra=("${@:3}"); fi
action=install
interactive=false
release_source=fork
if [[ -z "$channel" ]]; then
    interactive=true
    printf '1: Install/update Fork Public\n2: Install/update Fork Beta\n3: Uninstall\n4: Status\n5: Install/update Original ifBars Public\n'
    read -r -p 'Choose [1]: ' choice
    case "${choice:-1}" in
        1|public) channel=public ;;
        2|beta) channel=beta ;;
        3) action=uninstall ;;
        4) action=status ;;
        5) channel=public; release_source=upstream; extra+=(--source upstream) ;;
        *) echo 'Choose 1, 2, 3, 4 or 5.' >&2; exit 1 ;;
    esac
fi
if [[ -z "$game_dir" ]]; then
    read -r -p 'Game folder containing Schedule I.exe: ' game_dir
fi
game_dir="${game_dir#\"}"; game_dir="${game_dir%\"}"
game_dir="$(python3 -c 'from pathlib import Path; import sys; print(Path(sys.argv[1]).expanduser().resolve())' "$game_dir")"
if [[ "$action" != install ]]; then
    exec python3 "$package_dir/s1ds_installer.py" "$action" --game-directory "$game_dir" "${extra[@]}"
fi
# An explicit source also suppresses the fork's bundled/offline prompt.
for ((index=0; index<${#extra[@]}; index++)); do
    if [[ "${extra[index]}" == --source && $((index+1)) -lt ${#extra[@]} ]]; then
        release_source="${extra[index+1]}"
    elif [[ "${extra[index]}" == --source=* ]]; then
        release_source="${extra[index]#--source=}"
    fi
done
if [[ "$interactive" == true && "$release_source" == fork && -f "$package_dir/Packages/$channel/release-manifest.json" ]]; then
    offline=false
    for arg in "${extra[@]}"; do [[ "$arg" != --offline ]] || offline=true; done
    if [[ "$offline" == false ]]; then
        printf '1: Download latest release\n2: Use bundled files (offline)\n'
        read -r -p 'Choose [1]: ' source
        case "${source:-1}" in
            1) ;;
            2) extra+=(--offline) ;;
            *) echo 'Choose 1 or 2.' >&2; exit 1 ;;
        esac
    fi
fi
python3 "$package_dir/s1ds_installer.py" check --game-directory "$game_dir" --channel "$channel" "${extra[@]}"
# Loader files alone do not prove that this game's Proton prefix is configured.
# Winetricks reuses an installed runtime; always ensure the prefix can load version.dll.
if command -v protontricks >/dev/null 2>&1; then
    proton=(protontricks)
elif command -v flatpak >/dev/null 2>&1 && flatpak info com.github.Matoking.protontricks >/dev/null 2>&1; then
    steam_scope="$game_dir"
    if [[ "$(dirname -- "$game_dir")" == */steamapps/common ]]; then
        steam_scope="$(dirname -- "$(dirname -- "$game_dir")")"
    fi
    proton=(flatpak run "--filesystem=$steam_scope" com.github.Matoking.protontricks)
else
    echo 'Install Protontricks and launch Schedule I through Proton once before setup.' >&2
    echo 'On Steam Deck, use Desktop Mode and install Protontricks from Discover.' >&2
    exit 1
fi
echo 'Preparing Schedule I in Proton: .NET 6 Desktop Runtime and the MelonLoader DLL override...'
if ! "${proton[@]}" 3164500 -q dotnetdesktop6; then
    echo 'Proton runtime setup failed. Launch the game through Proton once, close it, then retry. No mod files were installed.' >&2
    exit 1
fi
if ! "${proton[@]}" -c 'wine reg add "HKCU\Software\Wine\DllOverrides" /v version /t REG_SZ /d native,builtin /f' 3164500; then
    echo 'Proton DLL override setup failed. No mod files were installed.' >&2
    exit 1
fi
python3 "$package_dir/s1ds_installer.py" install --game-directory "$game_dir" --channel "$channel" "${extra[@]}"
