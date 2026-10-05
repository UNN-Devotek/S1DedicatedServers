#!/usr/bin/env bash
set -euo pipefail
package_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
command -v python3 >/dev/null || { echo 'Install Python 3.10+ and retry.' >&2; exit 1; }
python3 -c 'import sys; assert sys.version_info >= (3,10), "Python 3.10+ is required"'
game_dir="${1:-}"
channel="${2:-}"
if [[ -z "$channel" ]]; then
    read -r -p 'Install/update public or beta mod files? [public]: ' channel
    channel="${channel:-public}"
fi
if [[ -z "$game_dir" ]]; then
    read -r -p 'Game folder containing Schedule I.exe: ' game_dir
fi
game_dir="${game_dir#\"}"; game_dir="${game_dir%\"}"
extra=(); if [[ $# -gt 2 ]]; then extra=("${@:3}"); fi
python3 "$package_dir/s1ds_installer.py" check --game-directory "$game_dir" --channel "$channel" "${extra[@]}"
if [[ ! -f "$game_dir/version.dll" ]]; then
    if command -v protontricks >/dev/null 2>&1; then
        proton=(protontricks)
    elif command -v flatpak >/dev/null 2>&1 && flatpak info com.github.Matoking.protontricks >/dev/null 2>&1; then
        proton=(flatpak run com.github.Matoking.protontricks)
    else
        echo 'Install Protontricks and launch Schedule I through Proton once before setup.' >&2
        exit 1
    fi
    "${proton[@]}" 3164500 -q dotnetdesktop6
    "${proton[@]}" -c 'wine reg add "HKCU\Software\Wine\DllOverrides" /v version /t REG_SZ /d native,builtin /f' 3164500
fi
python3 "$package_dir/s1ds_installer.py" install --game-directory "$game_dir" --channel "$channel" "${extra[@]}"
