#!/usr/bin/env python3

import argparse
import os
import sys
import http.client
from urllib.parse import urlparse

COLOR_RESET = "\033[0m"
COLOR_RED = "\033[91m"

def make_connection(host):
    parsed = urlparse(host)
    conn = http.client.HTTPSConnection(parsed.netloc)
    return conn, parsed.path.rstrip("/")

def main():
    parser = argparse.ArgumentParser(description="Obtain information about the authenticated user/API key")
    parser.add_argument("--host", help="API host (e.g. https://your-api-host.com)")
    parser.add_argument("--api-key", help="API key for authentication")
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
    path = f"{base_path}/api/v1/users/me"
    headers = {
        "X-Api-Key": api_key
    }
    
    conn.request("GET", path, headers=headers)
    response = conn.getresponse()
    data = response.read()
    conn.close()

    if response.status >= 400:
        print(f"{COLOR_RED}Error: {response.status} {response.reason}{COLOR_RESET}")

    print(data.decode())
    
if __name__ == "__main__":
    main()