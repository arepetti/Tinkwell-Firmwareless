#!/usr/bin/env python3

import argparse
import os
import sys
import json
import http.client
from urllib.parse import urlparse

COLOR_RESET = "\033[0m"
COLOR_BLUE = "\033[38;2;138;43;226m"
COLOR_RED = "\033[91m"

def prompt_if_missing(value, prompt_text):
    if value:
        return value
    return input(prompt_text)

def make_connection(host):
    parsed = urlparse(host)
    print(f"Connecting to {host}...")
    conn = http.client.HTTPSConnection(parsed.netloc)
    base_path = parsed.path.rstrip('/')
    return conn, base_path

def create_admin_key(host, api_key):
    conn, base_path = make_connection(host)
    path = f"{base_path}/api/v1/keys"
    headers = {
        "Content-Type": "application/json",
        "X-Api-Key": api_key
    }
    payload = {
        "Name": "Admin key",
        "Role": "Admin",
        "DaysValid": -1,
        "Scopes": [
            "key.create", "key.read", "key.revoke", "key.delete",
            "vendor.create", "vendor.read", "vendor.update", "vendor.delete",
            "product.create", "product.read", "product.update", "product.delete",
            "firmware.create", "firmware.read", "firmware.update", "firmware.delete",
            "firmware.download_all"
        ]
    }

    conn.request("POST", path, body=json.dumps(payload), headers=headers)
    response = conn.getresponse()
    data = response.read()
    conn.close()

    if response.status != 201:
        print(f"{COLOR_RED}Failed to create key: {response.status} {response.reason}{COLOR_RESET}")
        sys.exit(1)

    result = json.loads(data)
    print(f"New Admin API Key: {COLOR_BLUE}{result.get('text')}{COLOR_RESET}")
    return result.get('text')

def revoke_key(host, api_key):
    conn, base_path = make_connection(host)
    path = f"{base_path}/api/v1/keys/this/revoke"
    headers = {
        "X-Api-Key": api_key
    }

    conn.request("POST", path, headers=headers)
    response = conn.getresponse()
    conn.close()

    if response.status != 204:
        print(f"{COLOR_RED}Failed to revoke key: {response.status} {response.reason}{COLOR_RESET}")
        sys.exit(1)

    print("Temporary Admin API key revoked")

def main():
    parser = argparse.ArgumentParser(description="Create and revoke Admin API key")
    parser.add_argument("--host", help="API host (e.g. https://your-api-host.com)")
    parser.add_argument("--api-key", help="Temporary Admin API key")
    args = parser.parse_args()

    host = prompt_if_missing(args.host or os.environ.get("TW_REPOSITORY_HOST"), "Enter API host: ")
    temp_key = prompt_if_missing(args.api_key or os.environ.get("TW_REPOSITORY_API_KEY"), "Enter temporary Admin API key: ")

    create_admin_key(host, temp_key)
    revoke_key(host, temp_key)

if __name__ == "__main__":
    main()