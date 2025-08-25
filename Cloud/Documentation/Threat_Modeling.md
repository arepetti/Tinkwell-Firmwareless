# Threat Modeling: Tinkwell Firmwareless Public Repository & Compilation Server

This document provides a threat modeling analysis for the Tinkwell Firmwareless cloud components, specifically the `PublicRepository` and the `CompilationServer`. The goal is to identify potential security risks, assess their impact, and propose concrete mitigation strategies.

---

## 1. Malicious Firmware Upload & Execution

This is the most critical threat to the platform, as it directly affects the end-user devices (hubs) that will run the firmware.

- **Threat Description:** An attacker, either by posing as a legitimate vendor or by compromising a vendor's account, uploads a crafted WebAssembly (`.wasm`) file. This file is not a benign firmware but a malicious payload designed to exploit vulnerabilities in either the AOT compiler on the `CompilationServer` or the WASM runtime on the end-user's hub.
- **STRIDE Category:** Tampering, Elevation of Privilege.
- **Impact:** High. A successful exploit could lead to remote code execution (RCE) on the compilation server or, more likely, on the entire fleet of hubs that download the compromised firmware. This could result in a massive botnet, data theft from end-users, or physical disruption of IoT devices.
- **Exploit Difficulty:** Medium. Requires crafting a sophisticated WASM payload that targets a specific vulnerability in the `wamrc` compiler or a common WASM runtime.
- **Reward for Attacker:** High. Control over a large number of IoT devices is a significant asset.
- **Likelihood:** Medium. The high reward makes this an attractive target for skilled attackers.

#### Current Mitigations:
1.  **Vendor Authentication:** The `PublicRepository` requires an `X-Api-Key` for all upload operations, preventing anonymous uploads. (Ref: `ApiKeyAuthHandler.cs`)
2.  **Package Integrity Verification:** The `FirmwareSourcePackageValidator.cs` implements a robust check. It requires that the uploaded ZIP contains a manifest of file hashes and that this manifest is signed with the vendor's private key. The server verifies this using the vendor's public key. This is an excellent mitigation against tampering in transit and ensures the vendor's identity.
3.  **WASM validation:** `CompilationServer` generates a compilation script; the first step, before invoking `wamrc`, is the validation on the `.wasm` modules using `wasm-validate`.

#### Additional Mitigations:
1.  **Resource Sandboxing for Compilation:** The `CompilationServer` already uses Docker, which is a good first step. This should be hardened by running each compilation job with the strictest possible constraints:
    - A dedicated, non-root user.
    - A read-only filesystem, except for specific input/output directories.
    - Disabled network access for the compiler process itself.
2.  **Compiler Version Management:** Ensure the `wamrc` compiler (and any other tool in the chain) is always kept up-to-date with the latest security patches. This falls under **OWASP Top 10 A06:2021 - Vulnerable and Outdated Components**.
3.  **Static Analysis of WASM:** Before compilation, the `CompilationServer` performs static analysis on the `.wasm` modules. Tools can inspect the module's import/export sections to ensure it only requests legitimate, expected host functions. It can also scan for known malicious code patterns.

---

## 2. Denial of Service (DoS)

- **Threat Description:** An attacker could overwhelm the service, making it unavailable for legitimate users. This can be targeted at either the `PublicRepository` or the `CompilationServer`.
- **STRIDE Category:** Denial of Service.
- **Impact:** High. A successful DoS attack would prevent hubs from downloading new firmware or updates, potentially disrupting critical device functions.
- **Exploit Difficulty:** Low.
- **Reward for Attacker:** Medium. Primarily disruptive.
- **Likelihood:** High. This is one of the easiest attacks to mount.

#### Specific DoS Vectors & Mitigations:

### 2.1. Volumetric DoS on Public Endpoints
- **Vector:** An attacker floods the API endpoints (`/api/v1/firmwares/upload`, `/api/v1/firmwares/download`) with a high volume of requests.
- **Current Mitigations:** None.
- **Additional Mitigations:**
    - **Rate Limiting:** Implement strict rate limiting on all public-facing endpoints. This can be done per-IP address for anonymous requests and per-API-key for authenticated requests. ASP.NET Core provides built-in middleware for this (`AddRateLimiter`).
    - **Request Size Limits:** Enforce reasonable request size limits at the web server level (e.g., in Kestrel configuration) to prevent trivial resource exhaustion from overly large HTTP requests.

### 2.2. Resource Exhaustion via "Compilation Bomb"
- **Vector:** An attacker uploads a small but computationally complex `.wasm` file designed to consume excessive CPU time or memory during the AOT compilation phase, effectively starving the `CompilationServer` of resources. This is a specialized form of DoS.
- **Current Mitigations:** The `CompilationProxyService.cs` has a hardcoded 1-minute timeout, which is a good safety measure and resources available to the `wamrc` docker image are strictly limited.
- **Additional Mitigations:**
    - **Input Validation:** Before sending a file to the compiler, perform more checks than just the file type. Analyze the WASM module's structure (number of functions, complexity) to reject files that are clearly designed to be "compiler bombs".
    - **Queueing and Worker Management:** Implement a proper job queue for the `CompilationServer`. This allows you to control the number of concurrent compilations, preventing a flood of requests from overwhelming the system.

---

## 3. Authentication & Authorization Flaws

- **Threat Description:** An attacker bypasses authentication or authorization mechanisms to gain unauthorized access.
- **STRIDE Category:** Spoofing, Elevation of Privilege.
- **Impact:** High. Could allow an attacker to upload malicious firmware under a legitimate vendor's name, or delete/modify products and firmwares.
- **Exploit Difficulty:** Medium. Depends on finding a flaw in the key management or validation logic.
- **Reward for Attacker:** High.
- **Likelihood:** Low to Medium.

#### Current Mitigations:
1.  **API Key Authentication:** The system uses a custom `ApiKeyAuthHandler`.
2.  **Key Hashing:** Keys are hashed and salted before being stored in the database (standard practice). An additional secure hash is also included in the plain API key to allow a quick key validation without hitting the DB.
3.  **Authorization Policies:** all actions need authentication and individually they check for granular permissions.

#### Additional Mitigations:
1.  **Key Rotation and Revocation:** Implement a mechanism for vendors to revoke a compromised key and generate a new one. Introduce a policy that encourages or enforces periodic key rotation.
2.  **Logging and Monitoring:** Log all authentication attempts (successful and failed). Set up alerts for high rates of failed login attempts from a specific IP or against a specific vendor account, as this could indicate a brute-force or password spraying attack. This relates to **OWASP Top 10 A09:2021 - Security Logging and Monitoring Failures**.

---

## 4. Information Disclosure

- **Threat Description:** The application leaks sensitive information that could aid an attacker.
- **STRIDE Category:** Information Disclosure.
- **Impact:** Low to Medium. Leaked information (e.g., stack traces, internal file paths, library versions) can provide a roadmap for an attacker to mount more targeted attacks.
- **Exploit Difficulty:** Low. Often requires just sending invalid requests.
- **Reward for Attacker:** Medium.
- **Likelihood:** Medium.

#### Current Mitigations:
1.  **Default Exception Handling:** Standard ASP.NET Core templates provide good defaults that prevent leaking stack traces in production environments. The `CompilerController` returns generic error messages for unexpected exceptions.
2. **Secure Secret Management:** in production, all secrets are stored securely in the Azure Key Vault.
3. **Prevent Directory Traversal in Compilation Job**: user provided strings are never used directly to build a file name or compiler options (see `CompilerOptionsBuilder`) or they're validated for correctness (see `FirmwareSourcePackage.IsSafeRelativePath`).
