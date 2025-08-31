#!/usr/bin/env python3
import argparse
import sys
import zipfile
import hashlib
import os
from pathlib import Path
import urllib.request
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding
from cryptography.exceptions import InvalidSignature

COLOR_RESET = "\033[0m"
COLOR_RED = "\033[91m"
COLOR_GREEN = "\033[92m"

def compute_sha512(data: bytes) -> str:
    return hashlib.sha512(data).hexdigest()

def load_public_key_from_file(path: Path):
    try:
        with open(path, "rb") as f:
            return serialization.load_pem_public_key(f.read())
    except:
        print(f"{COLOR_RED}Error: could not read public key file{COLOR_RESET}")
        sys.exit(1)

def load_public_key_from_host(host: str):
    try:
        url = f"{host.rstrip('/')}/apu/v1/repository/identity"
        with urllib.request.urlopen(url, timeout=10) as resp:
            if resp.status != 200:
                print(f"{COLOR_RED}Error: failed to fetch key from host, status {resp.status}{COLOR_RESET}")
                sys.exit(1)
            pem_data = resp.read()
        return serialization.load_pem_public_key(pem_data)
    except:
        print("{COLOR_RED}Error: failed to fetch or parse public key from host{COLOR_RESET}")
        sys.exit(1)

def verify_signature(public_key, data: bytes, signature: bytes) -> bool:
    try:
        public_key.verify(
            signature,
            data,
            padding.PKCS1v15(),
            hashes.SHA512()
        )
        return True
    except InvalidSignature:
        return False
    except:
        return False

def main():
    parser = argparse.ArgumentParser(description="Validate ZIP archive integrity and manifest signature")
    parser.add_argument("zipfile_path", type=Path, help="Path to the ZIP archive")
    parser.add_argument("--public-key", type=Path, help="Path to public key PEM file")
    parser.add_argument("--host", help="Host to obtain the PEM key from if public key is not provided")
    args = parser.parse_args()

    if args.public_key:
        public_key = load_public_key_from_file(args.public_key)
    else:
        host = args.host or os.environ.get("TW_REPOSITORY_HOST")
        if not host:
            print("{COLOR_RED}Error: no public key or host provided{COLOR_RESET}")
            sys.exit(1)
        public_key = load_public_key_from_host(host)

    try:
        with zipfile.ZipFile(args.zipfile_path, "r") as z:
            try:
                with z.open("integrity/manifest.txt") as f:
                    manifest_data = f.read()
            except:
                print("{COLOR_RED}Error: missing integrity/manifest.txt in ZIP{COLOR_RESET}")
                sys.exit(1)

            manifest_lines = manifest_data.decode("utf-8").splitlines()

            for line in manifest_lines:
                parts = line.strip().split()
                if len(parts) != 3 or parts[1] != "SHA512":
                    print("{COLOR_RED}Error: invalid manifest format{COLOR_RESET}")
                    sys.exit(1)
                entry_name, _, expected_hash = parts
                try:
                    with z.open(entry_name) as ef:
                        actual_hash = compute_sha512(ef.read())
                except:
                    print(f"{COLOR_RED}Error: could not read {entry_name} from ZIP{COLOR_RESET}")
                    sys.exit(1)
                if actual_hash.lower() != expected_hash.lower():
                    print(f"{COLOR_RED}Error: hash mismatch for {entry_name}{COLOR_RESET}")
                    print("not valid")
                    sys.exit(1)

            try:
                with z.open("integrity/manifest.sig") as f:
                    signature = f.read()
            except:
                print("{COLOR_RED}Error: missing integrity/manifest.sig in ZIP{COLOR_RESET}")
                sys.exit(1)

            if not verify_signature(public_key, manifest_data, signature):
                print("{COLOR_RED}Signature verification failed{COLOR_RESET}")
                print("not valid")
                sys.exit(1)

    except:
        print("{COLOR_RED}Error: unable to open ZIP archive{COLOR_RESET}")
        sys.exit(1)

    print(f"{COLOR_GREEN}valid{COLOR_RESET}")
    sys.exit(0)

if __name__ == "__main__":
    main()