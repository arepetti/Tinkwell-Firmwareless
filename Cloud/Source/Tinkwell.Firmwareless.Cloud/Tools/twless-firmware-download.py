#!/usr/bin/env python3

import argparse
import json
import os
import sys
import urllib.request

def main():
    parser = argparse.ArgumentParser(description="Download compiled firmware from Tinkwell repository.")
    parser.add_argument("host", help="Host address")
    parser.add_argument("vendor_id", help="Vendor ID (GUID)")
    parser.add_argument("product_id", help="Product ID (GUID)")
    parser.add_argument("--hardware", default="linux", help="Hardware architecture (default: linux)")
    parser.add_argument("--hardware-version", default="1.0", help="Hardware version (default: 1.0)")
    parser.add_argument("--type", default="Firmlet", help="Firmware type (default: Firmlet)")
    parser.add_argument("--api-key", help="API key (overrides TW_REPOSITORY_API_KEY env var)")
    parser.add_argument("--output", help="Output file path (default: firmware.bin)")

    args = parser.parse_args()

    api_key = args.api_key or os.environ.get("TW_REPOSITORY_API_KEY")
    if not api_key:
        print("Error: API key must be provided via --api-key or TW_REPOSITORY_API_KEY environment variable.")
        sys.exit(1)

    url = f"{args.host}/api/v1/firmwares/download"
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
        print(f"Downloading firmware from {url}...")
        with urllib.request.urlopen(req) as response:
            output_path = args.output or "firmware.zip"
            with open(output_path, "wb") as out_file:
                out_file.write(response.read())
            print(f"Firmware downloaded to: {output_path}")
    except urllib.error.HTTPError as e:
        print(f"HTTP error: {e.code} {e.reason}")
    except urllib.error.URLError as e:
        print(f"URL error: {e.reason}")

if __name__ == "__main__":
    main()