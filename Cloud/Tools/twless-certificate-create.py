#!/usr/bin/env python3

import argparse
import zipfile
from pathlib import Path
from cryptography.hazmat.primitives import serialization
from cryptography.hazmat.primitives.asymmetric import rsa

COLOR_RESET = "\033[0m"
COLOR_BLUE = "\033[38;2;138;43;226m"
COLOR_RED = "\033[91m"

def generate_key_pair():
    private_key = rsa.generate_private_key(
        public_exponent=65537,
        key_size=2048
    )
    public_key = private_key.public_key()
    return private_key, public_key

def serialize_keys(private_key, public_key):
    private_pem = private_key.private_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PrivateFormat.TraditionalOpenSSL,
        encryption_algorithm=serialization.NoEncryption()
    )
    public_pem = public_key.public_bytes(
        encoding=serialization.Encoding.PEM,
        format=serialization.PublicFormat.SubjectPublicKeyInfo
    )
    return private_pem, public_pem

def write_zip(zip_path, private_pem, public_pem):
    with zipfile.ZipFile(zip_path, 'w') as zf:
        zf.writestr('private.pem', private_pem)
        zf.writestr('public.pem', public_pem)

def main():
    parser = argparse.ArgumentParser(description="Generate PEM certificate archive")
    parser.add_argument('-o', '--output', default='vendor-certificate.zip', help='Output ZIP file')
    parser.add_argument('-pub', '--public-key-output', help='Optional path to save public key as .pem')
    args = parser.parse_args()

    private_key, public_key = generate_key_pair()
    private_pem, public_pem = serialize_keys(private_key, public_key)

    write_zip(args.output, private_pem, public_pem)

    if args.public_key_output:
        Path(args.public_key_output).write_bytes(public_pem)
        print(f"Public key saved to: {COLOR_BLUE}{args.public_key_output}{COLOR_RESET}")
    else:
        print("Public key:")
        print(public_pem.decode())

    print(f"Certificate archive created: {COLOR_BLUE}{args.output}{COLOR_RESET}")

if __name__ == "__main__":
    main()