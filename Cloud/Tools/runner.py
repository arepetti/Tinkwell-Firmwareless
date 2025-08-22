#!/usr/bin/env python3

import sys
import os
import subprocess

def main():
    if len(sys.argv) < 3:
        print(f"Usage: twless <domain> <verb> [other-options...]")
        sys.exit(1)

    domain = sys.argv[1]
    verb = sys.argv[2]
    other_args = sys.argv[3:]

    target_script = f"twless-{domain}-{verb}.py"
    script_path = os.path.join(os.path.dirname(__file__), target_script)

    if not os.path.isfile(script_path):
        print(f"Error: script '{target_script}' not found in current directory.")
        sys.exit(1)

    cmd = [sys.executable, script_path] + other_args
    subprocess.run(cmd)

if __name__ == "__main__":
    main()