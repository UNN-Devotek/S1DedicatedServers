#!/usr/bin/env bash
set -euo pipefail
package_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
command -v python3 >/dev/null || { echo 'Install Python 3.10+ and retry.' >&2; exit 1; }
python3 -c 'import sys; assert sys.version_info >= (3,10), "Python 3.10+ is required"'
python3 "$package_dir/s1ds_installer.py" uninstall "$@"
