# Tinkwell Firmwareless CLI

This document describes the command-line tools available for interacting with the Tinkwell Firmwareless cloud platform.

## Wrapper Scripts

The primary way to use these tools is through the `twless.ps1` (PowerShell) or `twless.sh` (Bash) wrapper scripts. These scripts automatically manage a Python virtual environment and execute the appropriate Python script based on the domain and verb you provide.

### Usage

**PowerShell:**
```powershell
./twless.ps1 <domain> <verb> [arguments...]
```

**Bash:**
```bash
./twless.sh <domain> <verb> [arguments...]
```

### Environment Variables

All scripts use the following environment variables for configuration. They can also be passed as command-line arguments.

*   `TW_REPOSITORY_HOST`: The URL of the Tinkwell Firmwareless repository (e.g., `https://api.example.com`). This can be overridden by the `--host` argument.
*   `TW_REPOSITORY_API_KEY`: The API key for authentication. This can be overridden by the `--api-key` argument.

---

## Command Reference

### Admin

#### `admin bootstrap`

Bootstraps the environment by creating a permanent Admin API key and then revoking the temporary key used for the process.

**Usage:**
```
twless admin bootstrap [--host <host>] [--api-key <temp_key>]
```

**Arguments:**
*   `--host`: The API host. Overrides the `TW_REPOSITORY_HOST` environment variable.
*   `--api-key`: The temporary Admin API key. Overrides the `TW_REPOSITORY_API_KEY` environment variable.

---

### Vendor

#### `vendor create`

Creates a new vendor.

**Usage:**
```
twless vendor create <name> [--host <host>] [--api-key <key>] [--certificate <cert>]
```

**Arguments:**
*   `name`: The name of the vendor.
*   `--host`: The API host. Overrides the `TW_REPOSITORY_HOST` environment variable.
*   `--api-key`: The API key. Overrides the `TW_REPOSITORY_API_KEY` environment variable.
*   `--certificate`: Path to the vendor's public key `.pem` file or a `.zip` archive containing it.

#### `vendor create-key`

Creates a new API key.

**Usage:**
```
twless vendor create-key [--host <host>] [--api-key <key>] [--vendor <id>] [--role <role>] [--scopes <scopes...>] [--name <name>] [--validity <days>]
```

**Arguments:**
*   `--host`: The API host. Overrides the `TW_REPOSITORY_HOST` environment variable.
*   `--api-key`: The API key used to perform the creation. Overrides the `TW_REPOSITORY_API_KEY` environment variable.
*   `--vendor`: The Vendor ID to associate the key with.
*   `--role`: The role for the key (`Admin` or `User`). Default: `User`.
*   `--scopes`: A space-separated list of scopes to grant.
*   `--name`: An informative name for the key. Default: `API Key`.
*   `--validity`: The number of days the key is valid for. Default: `365`.

---

### Product

#### `product create`

Creates a new product associated with a vendor.

**Usage:**
```
twless product create <vendor_id> <name> <model> [--host <host>] [--api-key <key>] [--status <status>]
```

**Arguments:**
*   `vendor_id`: The GUID of the vendor.
*   `name`: The name of the product.
*   `model`: The model name/number of the product.
*   `--host`: The API host. Overrides the `TW_REPOSITORY_HOST` environment variable.
*   `--api-key`: The API key. Overrides the `TW_REPOSITORY_API_KEY` environment variable.
*   `--status`: The product status (`Development`, `Production`, or `Retired`). Default: `Development`.

---

### Firmware

#### `firmware download`

Downloads a compiled firmware package from the repository.

**Usage:**
```
twless firmware download <vendor_id> <product_id> [--host <host>] [--api-key <key>] [--hardware <arch>] [--hardware-version <ver>] [--type <type>] [--output <file>]
```

**Arguments:**
*   `vendor_id`: The GUID of the vendor.
*   `product_id`: The GUID of the product.
*   `--host`: The API host. Overrides the `TW_REPOSITORY_HOST` environment variable.
*   `--api-key`: The API key. Overrides the `TW_REPOSITORY_API_KEY` environment variable.
*   `--hardware`: The hardware architecture (default: `linux`).
*   `--hardware-version`: The hardware version (default: `1.0`).
*   `--type`: The firmware type (default: `Firmlet`).
*   `--output`: The output file path (default: `<type>-<product_id>.zip`).

#### `firmware package`

Packages `.wasm` files and a `firmware.json` manifest into a single ZIP archive for uploading.

**Usage:**
```
twless firmware package <wasm_files...> [--enable-multi-thread] [--enable-tail-call] [--enable-gc] [--certificate <cert.zip>] [-o <output.zip>]
```

**Arguments:**
*   `wasm_files`: One or more `.wasm` files to include.
*   `--enable-multi-thread`: Enable multi-threading support in the runtime.
*   `--enable-tail-call`: Enable tail call optimization.
*   `--enable-gc`: Enable garbage collection.
*   `--certificate`: Path to the vendor certificate ZIP archive to sign the manifest.
*   `-o, --output`: The output ZIP file name (default: `firmware.zip`).

#### `firmware upload`

Uploads a firmware package to the repository.

**Usage:**
```
twless firmware upload [product_id] [file] --version <ver> [--host <host>] [--api-key <key>] [...]
```

**Arguments:**
*   `product_id`: The GUID of the product. If omitted, you will be prompted to select from a list of available products.
*   `file`: Path to the firmware ZIP file (default: `firmware.zip`).
*   `--version`: **(Required)** The firmware version string.
*   `--compatibility`: Compatibility information (default: `any`).
*   `--type`: The firmware type (default: `Firmlet`).
*   `--status`: The firmware status (default: `Release`).
*   `--author`: The author's name.
*   `--copyright`: Copyright information.
*   `--release-notes-url`: URL to the release notes.
*   `--api-key`: The API key. Overrides the `TW_REPOSITORY_API_KEY` environment variable.
*   `--host`: The API host. Overrides the `TW_REPOSITORY_HOST` environment variable.
*   `-o, --output`: Optional path to save the server's JSON response.

#### `firmware validate`

Validates the integrity and signature of compiled firmware archive.

**Usage:**
```
twless firmware validate <zipfile_path> [--public-key <key.pem>] [--host <host>]
```

**Arguments:**
*   `zipfile_path`: Path to the firmware ZIP archive.
*   `--public-key`: Path to the public key PEM file for signature verification.
*   `--host`: The host to fetch the public key from if `--public-key` is not provided. Overrides the `TW_REPOSITORY_HOST` environment variable.

---

### Base64

#### `base64 encode`

A simple utility to Base64 encode a file and print the result to standard output.

**Usage:**
```
twless base64 encode <file_path>
```

---

### Certificate

#### `certificate create`

Generates a 2048-bit RSA key pair and saves the private and public keys into a ZIP archive.

**Usage:**
```
twless certificate create [-o <output.zip>] [--public-key-output <pubkey.pem>]
```

**Arguments:**
*   `-o, --output`: The name of the output ZIP file (default: `vendor-certificate.zip`).
*   `--public-key-output`: Optional path to save the public key to a separate `.pem` file.

---

### Repository

#### `repository get-public-key`

Retrieves the repository's public key, which is used to verify firmware signatures.

**Usage:**
```
twless repository get-public-key [--host <host>]
```

**Arguments:**
*   `--host`: The API host. Overrides the `TW_REPOSITORY_HOST` environment variable.

---

### Users

#### `users me`

Retrieves information about the currently authenticated user or API key.

**Usage:**
```
twless users me [--host <host>] [--api-key <key>]
```

**Arguments:**
*   `--host`: The API host. Overrides the `TW_REPOSITORY_HOST` environment variable.
*   `--api-key`: The API key. Overrides the `TW_REPOSITORY_API_KEY` environment variable.