#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
if curl --fail --silent http://127.0.0.1:8000/BAIby_v5.3.html >/dev/null; then
  exit 0
fi

preview_dir="$(mktemp -d)"
ln -s "$PWD/legacy/BAIby_v5.3.html" "$preview_dir/BAIby_v5.3.html"
ln -s BAIby_v5.3.html "$preview_dir/index.html"
nohup python3 -m http.server 8000 --bind 0.0.0.0 --directory "$preview_dir" >"$preview_dir/preview.log" 2>&1 </dev/null &
preview_pid=$!
for attempt in {1..30}; do
  if curl --fail --silent http://127.0.0.1:8000/BAIby_v5.3.html >/dev/null; then
    exit 0
  fi
  if ! kill -0 "$preview_pid" 2>/dev/null; then
    cat "$preview_dir/preview.log" >&2
    exit 1
  fi
  sleep 0.2
done
cat "$preview_dir/preview.log" >&2
exit 1
