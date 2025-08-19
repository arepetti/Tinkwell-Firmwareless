#!/usr/bin/env python3

import sys
import base64
import os

def main():
    base_name = os.path.splitext(os.path.basename(__file__))[0]
    if len(sys.argv) != 2:
        print(f"Usage: python {base_name}.py <file_path>")
        sys.exit(1)

    file_path = sys.argv[1]

    try:
        with open(file_path, "rb") as f:
            data = f.read()
            encoded = base64.b64encode(data).decode("utf-8")
            print(encoded)
    except FileNotFoundError:
        print(f"Error: File '{file_path}' not found.")
        sys.exit(1)
    except Exception as e:
        print(f"Error reading file: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()