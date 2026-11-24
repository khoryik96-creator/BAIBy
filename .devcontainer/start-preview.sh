#!/usr/bin/env bash
set -euo pipefail

# Serves the browser preview on port 8000:
#   /           opens the Unity build in build/Web, or explains how to download one
#   /nursery/   the Unity build (build/Web)
#   /legacy/    the archived HTML prototype (also kept at /BAIby_v5.3.html for old links)
cd "$(dirname "$0")/.."
port="${BAIBY_PREVIEW_PORT:-8000}"
preview_url="http://127.0.0.1:$port"
layout="2"

serving_this_layout() {
  [ "$(curl --fail --silent "$preview_url/preview-layout.txt" 2>/dev/null)" = "$layout" ] &&
    curl --fail --silent "$preview_url/legacy/" | cmp -s legacy/BAIby_v5.3.html
}

if serving_this_layout; then
  exit 0
fi

# A preview from an older layout (the HTML prototype at the root) still holds the port: replace it.
if curl --silent --output /dev/null "$preview_url/BAIby_v5.3.html"; then
  pkill -f "http.server $port" 2>/dev/null || true
  for attempt in {1..25}; do
    curl --silent --output /dev/null "$preview_url/" || break
    sleep 0.2
  done
fi

preview_dir="$(mktemp -d)"
public="$preview_dir/public"
mkdir -p "$public/legacy"
ln -s "$PWD/.devcontainer/preview-home.html" "$public/index.html"
ln -s "$PWD/build/Web" "$public/nursery"
ln -s "$PWD/legacy/BAIby_v5.3.html" "$public/legacy/index.html"
ln -s "$PWD/legacy/BAIby_v5.3.html" "$public/BAIby_v5.3.html"
printf '%s' "$layout" > "$public/preview-layout.txt"

nohup python3 -m http.server "$port" --bind 0.0.0.0 --directory "$public" >"$preview_dir/preview.log" 2>&1 </dev/null &
preview_pid=$!
for attempt in {1..30}; do
  if ! kill -0 "$preview_pid" 2>/dev/null; then
    cat "$preview_dir/preview.log" >&2
    exit 1
  fi
  if serving_this_layout; then
    exit 0
  fi
  sleep 0.2
done
kill "$preview_pid" 2>/dev/null || true
cat "$preview_dir/preview.log" >&2
exit 1
