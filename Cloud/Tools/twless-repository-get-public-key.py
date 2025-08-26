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
    parser = argparse.ArgumentParser(description="Get the public key used to sign artifacts")
    parser.add_argument("--host", help="API host (e.g. https://your-api-host.com)")
    args = parser.parse_args()

    host = args.host or os.environ.get("TW_REPOSITORY_HOST")
    if not host:
        print(f"{COLOR_RED}Error: Host must be provided via --host or TW_REPOSITORY_HOST environment variable.{COLOR_RESET}")
        sys.exit(1)

    conn, base_path = make_connection(host)
    conn.request("GET", f"{base_path}/api/v1/repository/identity")
    response = conn.getresponse()
    data = response.read()
    conn.close()

    if response.status >= 400:
        print(f"{COLOR_RED}Error: {response.status} {response.reason}{COLOR_RESET}")

    print(data.decode())
    
if __name__ == "__main__":
    main()