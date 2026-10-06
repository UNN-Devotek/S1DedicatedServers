#!/usr/bin/env bash
set -euo pipefail
package_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
game_dir="${1:-}"
if [[ $# -gt 0 ]]; then shift; fi
exec bash "$package_dir/Install-Linux.sh" "$game_dir" auto "$@"
