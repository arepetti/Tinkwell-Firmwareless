#!/bin/bash

set -e

if [ "$#" -lt 2 ]; then
    echo "Usage: $0 <domain> <verb> [args...]"
    echo "Example: $0 vendor create --name 'My Vendor'"
    exit 1
fi

DOMAIN=$1
VERB=$2
shift 2
OTHER_ARGS=("$@")

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
TARGET_SCRIPT="$SCRIPT_DIR/twless-$DOMAIN-$VERB.py"
if [ ! -f "$TARGET_SCRIPT" ]; then
    echo "Error: script 'twless-$DOMAIN-$VERB.py' not found in '$SCRIPT_DIR'" >&2
    exit 1
fi

ENV_DIR="$SCRIPT_DIR/.penv"
if [ ! -d "$ENV_DIR" ]; then
    echo "Creating Python virtual environment in $ENV_DIR..."
    python -m venv "$ENV_DIR"

    source "$ENV_DIR/bin/activate"
    echo "Installing dependencies..."
    pip install --upgrade pip
    pip install -r "$SCRIPT_DIR/requirements.txt"
    echo "Virtual environment is now active"
else
    source "$ENV_DIR/bin/activate"
fi

exec python "$TARGET_SCRIPT" "${OTHER_ARGS[@]}"
