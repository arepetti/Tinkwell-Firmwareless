#!/usr/bin/env python3

import argparse
import json
import os
import zipfile

def main():
    parser = argparse.ArgumentParser(description="Build firmware ZIP with firmware.json and .wasm files")
    parser.add_argument("wasm_files", nargs="+", help="List of .wasm files")
    parser.add_argument("--enable-multi-thread", action="store_true", help="Enable multi-threading")
    parser.add_argument("--enable-tail-call", action="store_true", help="Enable tail call optimization")
    parser.add_argument("--enable-gc", action="store_true", help="Enable garbage collection")
    parser.add_argument("-o", "--output", default="firmware.zip", help="Output ZIP file name")

    args = parser.parse_args()

    config = {
        "EnableMultiThread": args.enable_multi_thread,
        "EnableTailCall": args.enable_tail_call,
        "EnableGarbageCollection": args.enable_gc,
        "CompilationUnits": [os.path.basename(f) for f in args.wasm_files]
    }

    json_bytes = json.dumps(config, indent=2).encode("utf-8")

    with zipfile.ZipFile(args.output, "w", zipfile.ZIP_DEFLATED) as zipf:
        zipf.writestr("firmware.json", json_bytes)
        for wasm_file in args.wasm_files:
            zipf.write(wasm_file, arcname=os.path.basename(wasm_file))

    print(f"Firmware archive created: {args.output}")

if __name__ == "__main__":
    main()