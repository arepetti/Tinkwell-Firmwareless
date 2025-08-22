#!/usr/bin/env python3

import argparse
import json
import os
import sys
import urllib.request

COLOR_RESET = "\033[0m"
COLOR_BLUE = "\033[38;2;138;43;226m"
COLOR_RED = "\033[91m"
COLOR_WHITE = "\033[97m"

def main():
    parser = argparse.ArgumentParser(description="Download compiled firmware from Tinkwell repository.")
    parser.add_argument("vendor_id", help="Vendor ID (GUID)")
    parser.add_argument("product_id", help="Product ID (GUID)")
    parser.add_argument("--host", help="Host address")
    parser.add_argument("--api-key", help="API key (overrides TW_REPOSITORY_API_KEY env var)")
    parser.add_argument("--hardware", default="linux", help="Hardware architecture (default: linux)")
    parser.add_argument("--hardware-version", default="1.0", help="Hardware version (default: 1.0)")
    parser.add_argument("--type", default="Firmlet", help="Firmware type (default: Firmlet)")
    parser.add_argument("--output", help="Output file path (default: <type>-<product id>.zip)")

    args = parser.parse_args()

    api_key = args.api_key or os.environ.get("TW_REPOSITORY_API_KEY")
    if not api_key:
        print(f"{COLOR_RED}Error: API key must be provided via --api-key or TW_REPOSITORY_API_KEY environment variable.{COLOR_RESET}")
        sys.exit(1)

    host = args.host or os.environ.get("TW_REPOSITORY_HOST")
    if not host:
        print(f"{COLOR_RED}Error: Host must be provided via --host or TW_REPOSITORY_HOST environment variable.{COLOR_RESET}")
        sys.exit(1)

    url = f"{host}/api/v1/firmwares/download"
    headers = {
        "Content-Type": "application/json",
        "X-Api-Key": api_key
    }
    payload = {
        "vendorId": args.vendor_id,
        "productId": args.product_id,
        "type": args.type,
        "hardwareVersion": args.hardware_version,
        "hardwareArchitecture": args.hardware
    }

    req = urllib.request.Request(url, data=json.dumps(payload).encode("utf-8"), headers=headers, method="POST")

    try:
        print(f"Downloading firmware from {COLOR_WHITE}{url}{COLOR_RESET}...")
        with urllib.request.urlopen(req) as response:
            output_path = args.output or f"{args.type.lower()}-{args.product_id}.zip"
            with open(output_path, "wb") as out_file:
                out_file.write(response.read())
            print(f"Firmware downloaded to: {COLOR_BLUE}{output_path}{COLOR_RESET}")
    except urllib.error.HTTPError as e:
        print(f"{COLOR_RED}Error: {e.code} {e.reason}{COLOR_RESET}")
    except urllib.error.URLError as e:
        print(f"{COLOR_RED}Error: {e.reason}{COLOR_RESET}")

if __name__ == "__main__":
    main()