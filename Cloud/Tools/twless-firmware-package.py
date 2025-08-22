#!/usr/bin/env python3

import argparse
import json
import sys
import os
import zipfile
import hashlib
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding

COLOR_RESET = "\033[0m"
COLOR_BLUE = "\033[38;2;138;43;226m"
COLOR_RED = "\033[91m"

def sha512_hex(data: bytes) -> str:
    return hashlib.sha512(data).hexdigest()

def sign_manifest(manifest_bytes: bytes, cert_zip_path: str) -> bytes:
    with zipfile.ZipFile(cert_zip_path, "r") as cert_zip:
        for entry in cert_zip.namelist():
            if entry.lower().endswith("private.pem"):
                key_data = cert_zip.read(entry)
                private_key = serialization.load_pem_private_key(
                    key_data,
                    password=None
                )
                return private_key.sign(
                    manifest_bytes,
                    padding.PKCS1v15(),
                    hashes.SHA512()
                )
    print(f"{COLOR_RED}No private.pem found in certificate archive{COLOR_RESET}")
    sys.exit(1)

def main():
    parser = argparse.ArgumentParser(description="Build firmware ZIP with firmware.json and .wasm files")
    parser.add_argument("wasm_files", nargs="+", help="List of .wasm files")
    parser.add_argument("--enable-multi-thread", action="store_true", help="Enable multi-threading")
    parser.add_argument("--enable-tail-call", action="store_true", help="Enable tail call optimization")
    parser.add_argument("--enable-gc", action="store_true", help="Enable garbage collection")
    parser.add_argument("--certificate", help="Path to ZIP archive containing signing certificate (private.pem)")
    parser.add_argument("-o", "--output", default="firmware.zip", help="Output ZIP file name")

    args = parser.parse_args()

    config = {
        "EnableMultiThread": args.enable_multi_thread,
        "EnableTailCall": args.enable_tail_call,
        "EnableGarbageCollection": args.enable_gc,
        "CompilationUnits": [os.path.basename(f) for f in args.wasm_files]
    }

    manifest_lines = []

    with zipfile.ZipFile(args.output, "w", zipfile.ZIP_DEFLATED) as zipf:
        # Add firmware.json
        json_bytes = json.dumps(config, indent=2).encode("utf-8")
        zipf.writestr("firmware.json", json_bytes)
        manifest_lines.append(f'"firmware.json" SHA512 {sha512_hex(json_bytes)}')

        # Add .wasm files
        for wasm_file in args.wasm_files:
            filename = os.path.basename(wasm_file)
            with open(wasm_file, "rb") as f:
                data = f.read()
                zipf.writestr(filename, data)
                manifest_lines.append(f'"{filename}" SHA512 {sha512_hex(data)}')

        # Add manifest
        manifest_content = "\n".join(manifest_lines).encode("utf-8")
        zipf.writestr("integrity/manifest.txt", manifest_content)

        # Optionally sign manifest
        if args.certificate:
            signature = sign_manifest(manifest_content, args.certificate)
            zipf.writestr("integrity/manifest.sig", signature)

    print(f"Firmware archive created: {COLOR_BLUE}{args.output}{COLOR_RESET}")

if __name__ == "__main__":
    main()