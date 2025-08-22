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
    parser = argparse.ArgumentParser(description="Create a new product")
    parser.add_argument("--host", help="API host (e.g. https://your-api-host.com)")
    parser.add_argument("--api-key", help="API key")
    parser.add_argument("vendor", help="Vendor ID")
    parser.add_argument("name", help="Product name")
    parser.add_argument("model", help="Model name")
    parser.add_argument("--status", default="Development", help="Product status (Development, Production or Retired)")
    args = parser.parse_args()

    api_key = args.api_key or os.environ.get("TW_REPOSITORY_API_KEY")
    if not api_key:
        print(f"{COLOR_RED}Error: API key must be provided via --api-key or TW_REPOSITORY_API_KEY environment variable.{COLOR_RESET}")
        sys.exit(1)

    host = args.host or os.environ.get("TW_REPOSITORY_HOST")
    if not host:
        print(f"{COLOR_RED}Error: Host must be provided via --host or TW_REPOSITORY_HOST environment variable.{COLOR_RESET}")
        sys.exit(1)

    conn, base_path = make_connection(host)
    path = f"{base_path}/api/v1/products"
    headers = {
        "Content-Type": "application/json",
        "X-Api-Key": api_key
    }
    payload = {
        "vendorId": args.vendor,
        "name": args.name,
        "model": args.model,
        "status": args.status            
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

if __name__ == "__main__":
    main()