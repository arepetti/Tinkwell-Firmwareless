# Tinkwell Firmwareless Cloud API

This document provides an overview of the Tinkwell Firmwareless Cloud API, including available endpoints, required authorization scopes, and common workflows for provisioning, vendors, and hub developers.

## API Scopes

The following scopes define the permissions within the Tinkwell Firmwareless Cloud API:

*   `key.create`: Allows creation of new API keys.
*   `key.read`: Allows reading API key details.
*   `key.revoke`: Allows revoking API keys.
*   `key.delete`: Allows deleting API keys.
*   `vendor.create`: Allows creating new vendors.
*   `vendor.read`: Allows reading vendor details.
*   `vendor.update`: Allows updating vendor details.
*   `vendor.delete`: Allows deleting vendors.
*   `product.create`: Allows creating new products.
*   `product.read`: Allows reading product details.
*   `product.update`: Allows updating product details.
*   `product.delete`: Allows deleting products.
*   `firmware.create`: Allows creating new firmware entries.
*   `firmware.read`: Allows reading firmware details.
*   `firmware.update`: Allows updating firmware details.
*   `firmware.delete`: Allows deleting firmware entries.
*   `firmware.download_all`: Allows downloading firmware files.

## API Endpoints

All authenticated API requests require an API key to be passed in the `X-Api-Key` HTTP header.

### Firmwares Controller (`/api/v1/firmwares`)

*   **`POST /api/v1/firmwares`**
    *   **Description:** Creates a new firmware entry and uploads the firmware file.
    *   **Required Scope:** `firmware.create`
    *   **Parameters (Form Data):**
        *   `ProductId` (Guid): The ID of the product this firmware belongs to.
        *   `Version` (string): The version string of the firmware (e.g., "1.0.0", "1.2.3-beta").
        *   `Compatibility` (string): Compatibility information for the firmware (e.g., "esp32", "armv7").
        *   `Author` (string): The author of the firmware.
        *   `Copyright` (string): Copyright information for the firmware.
        *   `ReleaseNotesUrl` (string): URL to the release notes.
        *   `Type` (FirmwareType): The type of firmware (e.g., `Service`, `Firmlet`, `DeviceRuntime`).
        *   `Status` (FirmwareStatus): The status of the firmware (e.g., `PreRelease`, `Release`, `Deprecated`).
        *   `File` (IFormFile): The firmware binary file.
    *   **Status Codes:**
        *   `201 Created`: Firmware created successfully.
        *   `400 Bad Request`: Invalid input (e.g., validation errors, file too large, invalid content type, invalid version format, attempting to create a deprecated firmware, conflicting version/compatibility).
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action.
        *   `404 Not Found`: Product ID not found.
        *   `500 Internal Server Error`: An unexpected server error occurred.
        *   `503 Service Unavailable`: An error occurred with the compilation server during upload rollback.
*   **`GET /api/v1/firmwares`**
    *   **Description:** Retrieves a paginated list of all firmwares, with optional filtering and sorting.
    *   **Required Scope:** `firmware.read`
    *   **Parameters (Query):**
        *   `pageIndex` (int, optional, default: 0): The page number to retrieve.
        *   `pageLength` (int, optional, default: 20): The number of items per page.
        *   `filter` (string, optional): Filter criteria (see [Filtering and Sorting](#filtering-and-sorting)).
        *   `sort` (string, optional): Sort criteria (see [Filtering and Sorting](#filtering-and-sorting)).
    *   **Status Codes:**
        *   `200 OK`: Successfully retrieved the list of firmwares.
        *   `400 Bad Request`: Invalid query parameters (e.g., `pageIndex` or `pageLength` out of range, invalid filter/sort syntax).
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action.
*   **`GET /api/v1/firmwares/{id:guid}`**
    *   **Description:** Retrieves a specific firmware by its ID.
    *   **Required Scope:** `firmware.read`
    *   **Parameters (URL):**
        *   `id` (Guid): The ID of the firmware to retrieve.
    *   **Status Codes:**
        *   `200 OK`: Successfully retrieved the firmware.
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action.
        *   `404 Not Found`: Firmware with the specified ID not found.
*   **`PUT /api/v1/firmwares`**
    *   **Description:** Updates an existing firmware's status.
    *   **Required Scope:** `firmware.update`
    *   **Parameters (Body):**
        *   `Id` (Guid): The ID of the firmware to update.
        *   `Status` (FirmwareStatus, optional): The new status of the firmware (e.g., `PreRelease`, `Release`, `Deprecated`).
    *   **Status Codes:**
        *   `200 OK`: Firmware updated successfully.
        *   `400 Bad Request`: Invalid input (e.g., validation errors).
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action.
        *   `404 Not Found`: Firmware with the specified ID not found.
        *   `409 Conflict`: Concurrency conflict (e.g., another user modified the firmware simultaneously).
        *   `500 Internal Server Error`: An unexpected server error occurred.
*   **`DELETE /api/v1/firmwares/{id:guid}`**
    *   **Description:** Deletes a firmware by its ID.
    *   **Required Role:** `Admin`
    *   **Required Scope:** `firmware.delete`
    *   **Parameters (URL):**
        *   `id` (Guid): The ID of the firmware to delete.
    *   **Status Codes:**
        *   `204 No Content`: Firmware deleted successfully.
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action (Admin role required).
        *   `404 Not Found`: Firmware with the specified ID not found.
        *   `409 Conflict`: Concurrency conflict.
        *   `500 Internal Server Error`: An unexpected server error occurred.
*   **`POST /api/v1/firmwares/download`**
    *   **Description:** Downloads a firmware file based on vendor, product, type, and hardware version.
    *   **Required Scope:** `firmware.download_all`
    *   **Parameters (Body):**
        *   `VendorId` (Guid): The ID of the vendor.
        *   `ProductId` (Guid): The ID of the product.
        *   `Type` (FirmwareType): The type of firmware.
        *   `HardwareVersion` (string): The hardware version.
        *   `HardwareArchitecture` (string): The hardware architecture.
    *   **Status Codes:**
        *   `200 OK`: Successfully retrieved and returned the firmware file (content type `application/octet-stream`).
        *   `400 Bad Request`: Invalid input (e.g., missing parameters).
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action.
        *   `404 Not Found`: No applicable firmware found for the given criteria.
        *   `503 Service Unavailable`: An error occurred while compiling or retrieving the firmware from the compilation server.

### Keys Controller (`/api/v1/keys`)

*   **`POST /api/v1/keys`**
    *   **Description:** Creates a new API key.
    *   **Required Scope:** `key.create`
    *   **Parameters (Body):**
        *   `VendorId` (Guid?, optional): The ID of the vendor this key belongs to. Null for Admin keys.
        *   `Name` (string): A descriptive name for the API key.
        *   `Role` (string): The role associated with the key ("User" or "Admin").
        *   `DaysValid` (int): The number of days the key is valid for (1 to 365). Use -1 for no expiration.
        *   `Scopes` (string[]): An array of scopes granted to this key.
    *   **Status Codes:**
        *   `201 Created`: API key created successfully.
        *   `400 Bad Request`: Invalid input (e.g., missing name, invalid role, days valid out of range).
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action (e.g., non-admin trying to create admin key, user trying to create key for another vendor).
        *   `404 Not Found`: Vendor ID not found if specified.
        *   `409 Conflict`: Concurrency conflict.
        *   `500 Internal Server Error`: An unexpected server error occurred.
*   **`GET /api/v1/keys`**
    *   **Description:** Retrieves a paginated list of all API keys.
    *   **Required Scope:** `key.read`
    *   **Parameters (Query):**
        *   `pageIndex` (int, optional, default: 0): The page number to retrieve.
        *   `pageLength` (int, optional, default: 20): The number of items per page.
        *   `filter` (string, optional): Filter criteria (see [Filtering and Sorting](#filtering-and-sorting)).
        *   `sort` (string, optional): Sort criteria (see [Filtering and Sorting](#filtering-and-sorting)).
    *   **Status Codes:**
        *   `200 OK`: Successfully retrieved the list of API keys.
        *   `400 Bad Request`: Invalid query parameters.
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action.
*   **`GET /api/v1/keys/{id:guid}`**
    *   **Description:** Retrieves a specific API key by its ID.
    *   **Required Scope:** `key.read`
    *   **Parameters (URL):**
        *   `id` (Guid): The ID of the API key to retrieve.
    *   **Status Codes:**
        *   `200 OK`: Successfully retrieved the API key.
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action.
        *   `404 Not Found`: API key with the specified ID not found (or not accessible by the current user).
*   **`DELETE /api/v1/keys/{id:guid}/revoke`**
    *   **Description:** Revokes an API key by its ID.
    *   **Required Scope:** `key.revoke`
    *   **Parameters (URL):**
        *   `id` (Guid): The ID of the API key to revoke.
    *   **Status Codes:**
        *   `204 No Content`: API key revoked successfully.
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action.
        *   `404 Not Found`: API key with the specified ID not found (or not accessible by the current user).
        *   `409 Conflict`: Concurrency conflict.
        *   `500 Internal Server Error`: An unexpected server error occurred.
*   **`DELETE /api/v1/keys/{id:guid}`**
    *   **Description:** Deletes an API key by its ID.
    *   **Required Scope:** `key.delete`
    *   **Parameters (URL):**
        *   `id` (Guid): The ID of the API key to delete.
    *   **Status Codes:**
        *   `204 No Content`: API key deleted successfully.
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action.
        *   `404 Not Found`: API key with the specified ID not found (or not accessible by the current user).
        *   `409 Conflict`: Concurrency conflict.
        *   `500 Internal Server Error`: An unexpected server error occurred.

### Products Controller (`/api/v1/products`)

*   **`POST /api/v1/products`**
    *   **Description:** Creates a new product.
    *   **Required Scope:** `product.create` (or `Admin` role)
    *   **Parameters (Body):**
        *   `VendorId` (Guid): The ID of the vendor this product belongs to.
        *   `Name` (string): The name of the product.
        *   `Model` (string): The model identifier of the product.
        *   `Status` (ProductStatus): The status of the product (e.g., `Development`, `Production`, `Retired`).
    *   **Status Codes:**
        *   `201 Created`: Product created successfully.
        *   `400 Bad Request`: Invalid input (e.g., missing name or model).
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action (e.g., user trying to create product for another vendor).
        *   `404 Not Found`: Vendor ID not found.
        *   `409 Conflict`: Concurrency conflict.
        *   `500 Internal Server Error`: An unexpected server error occurred.
*   **`GET /api/v1/products`**
    *   **Description:** Retrieves a paginated list of all products, with optional filtering and sorting.
    *   **Required Scope:** `product.read`
    *   **Parameters (Query):**
        *   `pageIndex` (int, optional, default: 0): The page number to retrieve.
        *   `pageLength` (int, optional, default: 20): The number of items per page.
        *   `filter` (string, optional): Filter criteria (see [Filtering and Sorting](#filtering-and-sorting)).
        *   `sort` (string, optional): Sort criteria (see [Filtering and Sorting](#filtering-and-sorting)).
    *   **Status Codes:**
        *   `200 OK`: Successfully retrieved the list of products.
        *   `400 Bad Request`: Invalid query parameters.
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action.
*   **`GET /api/v1/products/{id:guid}`**
    *   **Description:** Retrieves a specific product by its ID.
    *   **Required Scope:** `product.read`
    *   **Parameters (URL):**
        *   `id` (Guid): The ID of the product to retrieve.
    *   **Status Codes:**
        *   `200 OK`: Successfully retrieved the product.
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action.
        *   `404 Not Found`: Product with the specified ID not found (or not accessible by the current user).
*   **`PUT /api/v1/products`**
    *   **Description:** Updates an existing product.
    *   **Required Scope:** `product.update`
    *   **Parameters (Body):**
        *   `Id` (Guid): The ID of the product to update.
        *   `Name` (string, optional): The new name of the product.
        *   `Model` (string, optional): The new model identifier of the product.
        *   `Status` (ProductStatus, optional): The new status of the product.
    *   **Status Codes:**
        *   `200 OK`: Product updated successfully.
        *   `400 Bad Request`: Invalid input.
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action.
        *   `404 Not Found`: Product with the specified ID not found (or not accessible by the current user).
        *   `409 Conflict`: Concurrency conflict.
        *   `500 Internal Server Error`: An unexpected server error occurred.
*   **`DELETE /api/v1/products/{id:guid}`**
    *   **Description:** Deletes a product by its ID.
    *   **Required Scope:** `product.delete`
    *   **Parameters (URL):**
        *   `id` (Guid): The ID of the product to delete.
    *   **Status Codes:**
        *   `204 No Content`: Product deleted successfully.
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action.
        *   `404 Not Found`: Product with the specified ID not found (or not accessible by the current user).
        *   `409 Conflict`: Concurrency conflict.
        *   `500 Internal Server Error`: An unexpected server error occurred.

### Vendors Controller (`/api/v1/vendors`)

*   **`POST /api/v1/vendors`**
    *   **Description:** Creates a new vendor.
    *   **Required Role:** `Admin`
    *   **Required Scope:** `vendor.create`
    *   **Parameters (Body):**
        *   `Name` (string): The name of the vendor.
        *   `Notes` (string): Any additional notes for the vendor.
    *   **Status Codes:**
        *   `201 Created`: Vendor created successfully.
        *   `400 Bad Request`: Invalid input (e.g., missing name).
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action (Admin role required).
        *   `409 Conflict`: Concurrency conflict.
        *   `500 Internal Server Error`: An unexpected server error occurred.
*   **`GET /api/v1/vendors`**
    *   **Description:** Retrieves a paginated list of all vendors, with optional filtering and sorting.
    *   **Required Role:** `Admin`
    *   **Required Scope:** `vendor.read`
    *   **Parameters (Query):**
        *   `pageIndex` (int, optional, default: 0): The page number to retrieve.
        *   `pageLength` (int, optional, default: 20): The number of items per page.
        *   `filter` (string, optional): Filter criteria (see [Filtering and Sorting](#filtering-and-sorting)).
        *   `sort` (string, optional): Sort criteria (see [Filtering and Sorting](#filtering-and-sorting)).
    *   **Status Codes:**
        *   `200 OK`: Successfully retrieved the list of vendors.
        *   `400 Bad Request`: Invalid query parameters.
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action (Admin role required).
*   **`GET /api/v1/vendors/{id:guid}`**
    *   **Description:** Retrieves a specific vendor by its ID.
    *   **Required Scope:** `vendor.read`
    *   **Parameters (URL):**
        *   `id` (Guid): The ID of the vendor to retrieve.
    *   **Status Codes:**
        *   `200 OK`: Successfully retrieved the vendor.
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action.
        *   `404 Not Found`: Vendor with the specified ID not found (or not accessible by the current user).
*   **`PUT /api/v1/vendors`**
    *   **Description:** Updates an existing vendor.
    *   **Required Role:** `Admin`
    *   **Required Scope:** `vendor.update`
    *   **Parameters (Body):**
        *   `Id` (Guid): The ID of the vendor to update.
        *   `Name` (string, optional): The new name of the vendor.
        *   `Notes` (string, optional): Any updated notes for the vendor.
    *   **Status Codes:**
        *   `200 OK`: Vendor updated successfully.
        *   `400 Bad Request`: Invalid input.
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action (Admin role required).
        *   `404 Not Found`: Vendor with the specified ID not found.
        *   `409 Conflict`: Concurrency conflict.
        *   `500 Internal Server Error`: An unexpected server error occurred.
*   **`DELETE /api/v1/vendors/{id:guid}`**
    *   **Description:** Deletes a vendor by its ID.
    *   **Required Role:** `Admin`
    *   **Required Scope:** `vendor.delete`
    *   **Parameters (URL):**
        *   `id` (Guid): The ID of the vendor to delete.
    *   **Status Codes:**
        *   `204 No Content`: Vendor deleted successfully.
        *   `401 Unauthorized`: No API key provided or API key is invalid/expired/revoked.
        *   `403 Forbidden`: Insufficient scope or role to perform the action (Admin role required).
        *   `404 Not Found`: Vendor with the specified ID not found.
        *   `409 Conflict`: Concurrency conflict.
        *   `500 Internal Server Error`: An unexpected server error occurred.

### Status Controller (`/status`)

*   **`GET /status`**
    *   **Description:** Returns "OK" to indicate the service is running.
    *   **Required Scope:** None
    *   **Parameters:** None
    *   **Status Codes:**
        *   `200 OK`: Service is running.

## Filtering and Sorting

For `FindAll()` endpoints, you can use the `filter` and `sort` query parameters to refine your results.

### Filtering (`filter` parameter)

The `filter` parameter accepts a comma-separated list of filter terms. Each term specifies a field, an operator, and a value.

*   **Equality (`==`)**: Filters for an exact match.
    *   Syntax: `fieldName==value`
    *   Example: `name==MyProduct`
*   **Contains (`~`)**: Filters for partial, case-insensitive matches (only for string fields).
    *   Syntax: `fieldName~value`
    *   Example: `name~prod`

Multiple filter terms are combined with a logical AND.

### Sorting (`sort` parameter)

The `sort` parameter accepts a comma-separated list of field names. Results will be sorted by these fields in the order they appear.

*   **Ascending Order (default)**: Simply provide the field name.
    *   Syntax: `fieldName`
    *   Example: `name`
*   **Descending Order**: Prefix the field name with a hyphen (`-`).
    *   Syntax: `-fieldName`
    *   Example: `-createdAt`

Multiple sort terms will apply a secondary sort if the primary sort results in ties.
