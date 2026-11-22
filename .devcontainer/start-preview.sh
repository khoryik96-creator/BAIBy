#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
port="${BAIBY_PREVIEW_PORT:-8000}"
preview_url="http://127.0.0.1:$port"
if curl --fail --silent "$preview_url/BAIby_v5.3.html" | cmp -s legacy/BAIby_v5.3.html; then
  exit 0
fi

preview_dir="$(mktemp -d)"
mkdir "$preview_dir/public"
ln -s "$PWD/legacy/BAIby_v5.3.html" "$preview_dir/public/BAIby_v5.3.html"
ln -s BAIby_v5.3.html "$preview_dir/public/index.html"
ln -s "$PWD/build/Web" "$preview_dir/public/nursery"

nohup python3 -m http.server "$port" --bind 0.0.0.0 --directory "$preview_dir/public" >"$preview_dir/preview.log" 2>&1 </dev/null &
preview_pid=$!
for attempt in {1..30}; do
  if ! kill -0 "$preview_pid" 2>/dev/null; then
    cat "$preview_dir/preview.log" >&2
    exit 1
  fi
  if curl --fail --silent "$preview_url/BAIby_v5.3.html" | cmp -s legacy/BAIby_v5.3.html; then
    exit 0
  fi
  sleep 0.2
done
kill "$preview_pid" 2>/dev/null || true
cat "$preview_dir/preview.log" >&2
exit 1
