# Tinkwell Firmwareless

**IMPORTANT**: Tinkwell Firmwareless (and Tinkwell runtime itself) are undergoing a massive refactoring, the current projects might not compile or work as intended. Please be patient until this transition is completed.

**Tinkwell Firmwareless** is the umbrella for a firmware-less IoT stack: product behavior is not baked into per-device firmware images.
Instead, authors ship **portable modules** (WebAssembly **firmlets** and device **applets**), the **registry** signs and compiles them, the **hub** distributes them at the edge, and **devices** download and run them over **OTA** alongside a thin runtime.
That model is explained in context in [Tinkwell firmware-less IoT and lab automation](https://dev.to/adriano-repetti/tinkwell-firmware-less-iot-and-lab-automation-2gef) and in the broader architecture discussion [IoT architectures under pressure (Part 1)](https://dev.to/adriano-repetti/iot-architectures-under-pressure-why-implementation-isnt-as-simple-as-it-seems-part-1-3inn). Note that the modern implementation differ greatly from the original idea.

## Architecture at a glance

Three roles cooperate end to end.

- **[Registry](https://github.com/arepetti/tinkwell-firmwareless-repository/blob/main/README.md)** — Cloud or on-prem **firmlet registry**: authors publish signed packages; the service verifies signatures, stores artifacts, and **ahead-of-time compiles** WASM to native binaries for hub targets (and supports the publish / compile / download workflow).
- **[Hub](https://github.com/arepetti/tinkwell-firmwareless-hub/blob/main/README.md)** — Edge **gateway** built on the main Tinkwell runtime: Tinkwell **runlets** talk to devices (e.g. CoAP), bridge into Docker-hosted **WasmHost** processes for **firmlets**, and integrate provisioning and asset registration with the registry.
- **[Device](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/README.md)** — **MCU-class endpoints** running the PAL-backed SDK (or any stack that speaks the documented wire protocol): download **applets** and firmware **OTA**, heartbeats, and hub-mediated commands.

```text
  Author ──publish (signed firmlet)──► Registry ◄── API / CLI
                                          │
                                          ├── AoT compile + cache
                                          │
Hub ◄──────── download compiled assets ───┘
  │
  ├── Runlets (CoAP, provisioning, proxy, assets) ◄──► Tinkwell ensemble
  │
  └── Firmlets in Docker (WasmHost) ◄──► LAN devices (CoAP, OTA, applets)
```

For hub internals (Supervisor, Router, runlets, Docker boundary), see [Hub architecture](https://github.com/arepetti/tinkwell-firmwareless-hub/blob/main/docs/architecture.md).
For registry service topology, see [Registry architecture](https://github.com/arepetti/tinkwell-firmwareless-repository/blob/main/docs/architecture.md).

## Key concepts

| Term | Meaning |
|------|---------|
| **Firmlet** | Signed **source package** (manifest + WASM modules + config) published to the registry for hub-side execution or downstream compilation. Not a reflashed device image. See [registry README](https://github.com/arepetti/tinkwell-firmwareless-repository/blob/main/README.md). |
| **Applet** | **Compiled portable WASM** pushed to a device (hot-swap, OTA); runs under the device runtime. See [device README](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/README.md) and [applet protocol](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/docs/protocol/applet-protocol.md). |
| **Hub** | Edge stack that hosts firmlets, talks to devices, and connects to the broader Tinkwell coordinator–runner ecosystem. See [hub README](https://github.com/arepetti/tinkwell-firmwareless-hub/blob/main/README.md). |
| **Device** | Field hardware (often ESP32-class) running the SDK or an equivalent protocol implementation. See [device README](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/README.md). |
| **Registry** | Service where firmlets are **published**, **signed**, **stored**, and **compiled** per architecture. Separate from the Tinkwell plugin registry. See [registry README](https://github.com/arepetti/tinkwell-firmwareless-repository/blob/main/README.md). |
| **OTA** | Over-the-air delivery of applet binaries and device firmware payloads from the hub. See [OTA updates](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/docs/guides/ota-updates.md). |
| **Manifest** | Declarative package metadata (for example `package.tw` inside a firmlet ZIP) tying modules, services, and permissions together. Firmlet layout is summarized in [hub README](https://github.com/arepetti/tinkwell-firmwareless-hub/blob/main/README.md#firmlet-package-layout). |
| **Signing** | ECDSA-backed package signing at publish time; registry verification on ingest. Detailed in registry docs and [threat modeling](https://github.com/arepetti/tinkwell-firmwareless-repository/blob/main/docs/threat-modeling.md). |

## Getting started

Use the sub-project guides.

- **Registry** — Local AppHost run and publish / compile / download flows: [Getting started](https://github.com/arepetti/tinkwell-firmwareless-repository/blob/main/docs/getting-started.md) and [registry README](https://github.com/arepetti/tinkwell-firmwareless-repository/blob/main/README.md#quick-start).
- **Device** — Thermostat quick path or applet-first path: [Quick start](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/docs/getting-started/quick-start.md), [Your first applet](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/docs/getting-started/your-first-applet.md), and [device README](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/README.md#quick-start).
- **Hub** — Build, configure, and evaluate locally: [Evaluation guide](https://github.com/arepetti/tinkwell-firmwareless-hub/blob/main/docs/evaluation-guide.md).

## Documentation map

- **Device SDK** — [`device/docs/README.md`](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/docs/README.md) (indexed quick starts, guides, [`architecture/`](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/docs/architecture/overview.md), [`getting-started/`](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/docs/getting-started/quick-start.md), [`guides/`](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/docs/guides/choosing-your-approach.md), [`protocol/`](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/docs/protocol/wire-specification.md), [`reference/`](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/docs/reference/api.md)).
- **Registry** — [`registry/docs/README.md`](https://github.com/arepetti/tinkwell-firmwareless-repository/blob/main/docs/README.md) (links to integration, credentials, permissions, architecture, API, CLI, threat model).
- **Hub** — [`hub/docs/architecture.md`](https://github.com/arepetti/tinkwell-firmwareless-hub/blob/main/docs/architecture.md), [`hub/docs/evaluation-guide.md`](https://github.com/arepetti/tinkwell-firmwareless-hub/blob/main/docs/evaluation-guide.md); UI add-ons live under [`hub/extras/ui/docs/`](https://github.com/arepetti/tinkwell-firmwareless-hub/blob/main/extras/ui/docs/README.md) when present.
- **Examples** — [`device/examples/minimal/`](https://github.com/arepetti/tinkwell-firmwareless-device/tree/main/examples/minimal/), [`device/examples/thermostat/`](https://github.com/arepetti/tinkwell-firmwareless-device/tree/main/examples/thermostat/), [`device/examples/applet-device/`](https://github.com/arepetti/tinkwell-firmwareless-device/tree/main/examples/applet-device/).
- **Scripts** — [`device/scripts/README.md`](https://github.com/arepetti/tinkwell-firmwareless-device/blob/main/scripts/README.md) (demo, provisioning, tooling).

## Security and threat model

The registry’s [Threat model](https://github.com/arepetti/tinkwell-firmwareless-repository/blob/main/docs/threat-modeling.md) covers STRIDE-style analysis for **signing**, **identity** (authors, hubs, organizations), **OTA and download** paths, and operational mitigations.

Hub and device surfaces add CoAP, provisioning, and Docker isolation.
See hub and device docs for protocol- and deployment-specific detail.

## License

[MIT](LICENSE)
