#!/usr/bin/env python3
import sys
import os
import argparse
import mimetypes
import uuid
import urllib.request
import urllib.parse
import json

COLOR_RESET = "\033[0m"
COLOR_BLUE = "\033[38;2;138;43;226m"
COLOR_RED = "\033[91m"
COLOR_WHITE = "\033[97m"
COLOR_GREEN = "\033[92m"

def select_product(api_host, api_key):
    products = []
    page_index = 0
    headers = {"X-Api-Key": api_key}

    while True:
        print("Fetching existing products...")
        query = urllib.parse.urlencode({"pageIndex": page_index})
        url = f"{api_host}/api/v1/products?{query}"

        req = urllib.request.Request(url, headers=headers)
        with urllib.request.urlopen(req) as resp:
            data = resp.read().decode("utf-8")
            page_data = json.loads(data)

        products.extend(page_data["items"])

        if not page_data["hasMore"]:
            break

        page_index += 1

    if not products:
        print(f"{COLOR_RED}No products found.{COLOR_RESET}")

    if len(products) == 1:
        print(f"Only one product found: {COLOR_WHITE}{products[0]['name']}{COLOR_RESET}")
        return products[0]['id']

    print("\nAvailable products:")
    for idx, prod in enumerate(products):
        print(f"{COLOR_GREEN}{idx}{COLOR_RESET}: {prod['name']}")

    while True:
        choice = input("Select a product index: ").strip()
        if choice.isdigit():
            choice = int(choice)
            if 0 <= choice < len(products):
                return products[choice]['id']
        print("Invalid selection. Please try again.")

def main():
    parser = argparse.ArgumentParser(description="Upload firmware via multipart/form-data")
    parser.add_argument("product_id", nargs="?", help="Product ID (GUID)")
    parser.add_argument("file", nargs="?", default="firmware.zip",
                        help="Path to firmware file to upload (default: firmware.zip)")
    parser.add_argument("--host", help="Base host address (e.g. https://api.example.com)")
    parser.add_argument("--version", required=True, help="Firmware version (required)")
    parser.add_argument("--compatibility", default="any", help="Compatibility info")
    parser.add_argument("--type", default="Firmlet", help="Firmware type")
    parser.add_argument("--status", default="Release", help="Firmware status")
    parser.add_argument("--author", default="", help="Author name (default: empty string)")
    parser.add_argument("--copyright", default="", help="Copyright (default: empty string)")
    parser.add_argument("--release-notes-url", default="", help="Release notes URL (default: empty string)")
    parser.add_argument("--api-key", help="API key (overrides TW_REPOSITORY_API_KEY env var)")
    parser.add_argument("-o", "--output", help="Optional path to save server response")

    args = parser.parse_args()

    api_key = args.api_key or os.environ.get("TW_REPOSITORY_API_KEY")
    if not api_key:
        print(f"{COLOR_RED}Error: API key must be provided via --api-key or TW_REPOSITORY_API_KEY environment variable.{COLOR_RESET}")
        sys.exit(1)

    host = args.host or os.environ.get("TW_REPOSITORY_HOST")
    if not host:
        print(f"{COLOR_RED}Error: Host must be provided via --host or TW_REPOSITORY_HOST environment variable.{COLOR_RESET}")
        sys.exit(1)

    if not os.path.isfile(args.file):
        print(f"{COLOR_RED}Error: File '{args.file}' not found.{COLOR_RESET}")
        sys.exit(1)

    if args.product_id is None:
        args.product_id = select_product(host.rstrip('/'), api_key)
        print(f"Using product {COLOR_WHITE}{args.product_id}{COLOR_RESET}")

    boundary = uuid.uuid4().hex
    body_parts = []

    def add_field(name, value):
        body_parts.append(f"--{boundary}\r\n".encode())
        body_parts.append(f'Content-Disposition: form-data; name="{name}"\r\n\r\n'.encode())
        body_parts.append(f"{value}\r\n".encode())

    def add_file_field(name, filepath):
        filename = os.path.basename(filepath)
        ctype = mimetypes.guess_type(filename)[0] or "application/octet-stream"
        body_parts.append(f"--{boundary}\r\n".encode())
        body_parts.append(f'Content-Disposition: form-data; name="{name}"; filename="{filename}"\r\n'.encode())
        body_parts.append(f"Content-Type: {ctype}\r\n\r\n".encode())
        with open(filepath, "rb") as f:
            body_parts.append(f.read())
        body_parts.append(b"\r\n")

    add_field("productId", args.product_id)
    add_field("version", args.version)
    add_field("compatibility", args.compatibility)
    add_field("type", args.type)
    add_field("status", args.status)
    add_field("author", args.author)
    add_field("copyright", args.copyright)
    add_field("releaseNotesUrl", args.release_notes_url)

    add_file_field("file", args.file)

    body_parts.append(f"--{boundary}--\r\n".encode())
    body = b"".join(body_parts)

    url = f"{host.rstrip('/')}/api/v1/firmwares"
    req = urllib.request.Request(url, data=body, method="POST")
    req.add_header("Content-Type", f"multipart/form-data; boundary={boundary}")
    req.add_header("Content-Length", str(len(body)))
    req.add_header("X-Api-Key", api_key)

    try:
        with urllib.request.urlopen(req) as resp:
            resp_data = resp.read()
            if args.output:
                with open(args.output, "wb") as f:
                    f.write(resp_data)
                print(f"Response saved to {COLOR_BLUE}{args.output}{COLOR_BLUE}")
            else:
                result = json.loads(resp_data)
                print(f"ID: {COLOR_BLUE}{result.get('id')}{COLOR_RESET}")
    except urllib.error.HTTPError as e:
        print(f"{COLOR_RED}Error {e.code}: {e.reason}{COLOR_RESET}")
        print(e.read().decode(errors="ignore"))
    except urllib.error.URLError as e:
        print(f"{COLOR_RED}Connection error: {e.reason}{COLOR_RESET}")

if __name__ == "__main__":
    main()