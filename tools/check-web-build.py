#!/usr/bin/env python3
"""Check that the uncompressed Unity export can be served for playtesting."""

import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from threading import Thread
from urllib.parse import quote
from urllib.request import urlopen


class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, *args):
        pass


def check_build(root):
    index = (root / "index.html").read_text(encoding="utf-8")
    assets = {}
    for suffix in (".loader.js", ".framework.js", ".data", ".wasm"):
        matches = [
            asset for asset in (root / "Build").glob(f"*{suffix}")
            if asset.name in index
        ]
        if len(matches) != 1 or matches[0].stat().st_size == 0:
            raise ValueError(f"Expected one nonempty {suffix} asset referenced by index.html")
        assets[suffix] = matches[0]

    with assets[".wasm"].open("rb") as stream:
        if stream.read(8) != b"\x00asm\x01\x00\x00\x00":
            raise ValueError("Expected uncompressed WebAssembly version 1")

    handler = partial(QuietHandler, directory=str(root))
    with ThreadingHTTPServer(("127.0.0.1", 0), handler) as server:
        thread = Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            base = f"http://127.0.0.1:{server.server_port}"
            with urlopen(base + "/", timeout=5) as response:
                if response.headers.get_content_type() != "text/html":
                    raise ValueError("The preview index must serve as text/html")
            wasm = quote(assets[".wasm"].relative_to(root).as_posix())
            with urlopen(f"{base}/{wasm}", timeout=5) as response:
                if response.headers.get_content_type() != "application/wasm":
                    raise ValueError("The preview server must serve .wasm as application/wasm")
        finally:
            server.shutdown()
            thread.join()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path, help="Unity export directory containing index.html")
    args = parser.parse_args()
    try:
        check_build(args.directory.resolve())
    except (OSError, ValueError) as error:
        parser.exit(1, f"Invalid browser export: {error}\n")
    print("Unity Web export files and HTTP serving checks passed.")


if __name__ == "__main__":
    main()
