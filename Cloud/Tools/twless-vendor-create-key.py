#!/usr/bin/env python3

import argparse
import json
import os
import sys
import http.client
from urllib.parse import urlparse

COLOR_RESET = "\033[0m"
COLOR_BLUE = "\033[38;2;138;43;226m"
COLOR_RED = "\033[91m"

def make_connection(host):
    parsed = urlparse(host)
    print(f"Connecting to {host}...")
    conn = http.client.HTTPSConnection(parsed.netloc)
    base_path = parsed.path.rstrip('/')
    return conn, base_path

def main():
    parser = argparse.ArgumentParser(description="Create a new API key")
    parser.add_argument("--host", help="API host (e.g. https://your-api-host.com)")
    parser.add_argument("--api-key", help="API key")
    parser.add_argument("--vendor", help="Vendor ID (unspecified for Admin keys)")
    parser.add_argument("--role", default="User", help="Role (Admin or User)")
    parser.add_argument("--scopes", nargs="*", help="Assigned scopes (omit for default scopes)")
    parser.add_argument("--name", default="API Key", help="API Key name (informative)")
    parser.add_argument("--validity", type=int, default=365, help="API Key validity (in days)")
    args = parser.parse_args()

    api_key = args.api_key or os.environ.get("TW_REPOSITORY_API_KEY")
    if not api_key:
        print(f"{COLOR_RED}Error: API key must be provided via --api-key or TW_REPOSITORY_API_KEY environment variable.{COLOR_RESET}")
        sys.exit(1)

    host = args.host or os.environ.get("TW_REPOSITORY_HOST")
    if not host:
        print(f"{COLOR_RED}Error: Host must be provided via --host or TW_REPOSITORY_HOST environment variable.{COLOR_RESET}")
        sys.exit(1)

    scopes = args.scopes
    if not scopes:
        scopes = [
            "key.create", "key.read", "key.revoke",
            "vendor.read",
            "product.create", "product.read", "product.update", "product.delete",
            "firmware.create", "firmware.read", "firmware.update",
            "firmware.download_all"
        ]
        
    conn, base_path = make_connection(host)
    path = f"{base_path}/api/v1/keys"
    headers = {
        "Content-Type": "application/json",
        "X-Api-Key": api_key
    }
    payload = {
        "vendorId": args.vendor,
        "name": args.name,
        "role": args.role,
        "daysValid": args.validity,
        "scopes": scopes            
    }

    conn.request("POST", path, body=json.dumps(payload), headers=headers)
    response = conn.getresponse()
    data = response.read()
    conn.close()

    if response.status != 201:
        print(f"{COLOR_RED}Error: {response.status} {response.reason}{COLOR_RESET}")
        print(data.decode())
    else:
        result = json.loads(data)
        print(f"ID: {COLOR_BLUE}{result.get('id')}{COLOR_RESET}")
        print(f"API Key: {COLOR_BLUE}{result.get('text')}{COLOR_RESET}")

if __name__ == "__main__":
    main()