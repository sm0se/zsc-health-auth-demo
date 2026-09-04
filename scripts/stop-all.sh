#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f .run/pids ] || { echo "nothing to stop"; exit 0; }
while read -r pid name _; do
  kill "$pid" 2>/dev/null && echo "stopped $name ($pid)" || true
done < .run/pids
rm -f .run/pids
