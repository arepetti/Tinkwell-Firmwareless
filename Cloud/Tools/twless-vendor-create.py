#!/usr/bin/env python3

import argparse
import json
import os
import sys
import zipfile
import http.client
from urllib.parse import urlparse

COLOR_RESET = "\033[0m"
COLOR_BLUE = "\033[38;2;138;43;226m"
COLOR_RED = "\033[91m"

def read_certificate(cert_path):
    if cert_path.lower().endswith(".zip"):
        with zipfile.ZipFile(cert_path, "r") as zf:
            for entry in zf.namelist():
                if entry.lower().endswith("public.pem"):
                    return zf.read(entry).decode("utf-8")
        raise FileNotFoundError("public.pem not found in ZIP archive")
    elif cert_path.lower().endswith(".pem"):
        with open(cert_path, "r", encoding="utf-8") as f:
            return f.read()
    else:
        raise ValueError("Unsupported certificate format. Use .zip or .pem")

def make_connection(host):
    parsed = urlparse(host)
    conn = http.client.HTTPSConnection(parsed.netloc)
    return conn, parsed.path.rstrip("/")

def post_vendor(host, api_key, name, certificate):
    conn, base_path = make_connection(host)
    path = f"{base_path}/api/v1/vendors"
    headers = {
        "Content-Type": "application/json",
        "X-Api-Key": api_key
    }
    payload = {
        "name": name,
        "notes": "",
        "certificate": certificate or ""
    }

    body = json.dumps(payload)
    conn.request("POST", path, body=body, headers=headers)
    response = conn.getresponse()
    data = response.read()
    conn.close()

    if response.status < 300:
        result = json.loads(data)
        print(f"Vendor ID: {COLOR_BLUE}{result.get('id')}{COLOR_RESET}")
    else:
        print(f"{COLOR_RED}Error: {response.status} {response.reason}{COLOR_RESET}")
        print(data.decode())

def main():
    parser = argparse.ArgumentParser(description="Create vendor via Admin API")
    parser.add_argument("name", help="Vendor name")
    parser.add_argument("--host", help="API host (e.g. https://your-api-host.com)")
    parser.add_argument("--api-key", help="API key for authentication")
    parser.add_argument("--certificate", help="Path to .pem file or ZIP archive containing public.pem")
    args = parser.parse_args()

    api_key = args.api_key or os.environ.get("TW_REPOSITORY_API_KEY")
    if not api_key:
        print(f"{COLOR_RED}Error: API key must be provided via --api-key or TW_REPOSITORY_API_KEY environment variable.{COLOR_RESET}")
        sys.exit(1)

    host = args.host or os.environ.get("TW_REPOSITORY_HOST")
    if not host:
        print(f"{COLOR_RED}Error: Host must be provided via --host or TW_REPOSITORY_HOST environment variable.{COLOR_RESET}")
        sys.exit(1)
    
    cert_text = ""
    if args.certificate:
        cert_text = read_certificate(args.certificate)

    post_vendor(host, api_key, args.name, cert_text)

if __name__ == "__main__":
    main()