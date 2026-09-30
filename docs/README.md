# OptiRoute Documentation

OptiRoute routes Windows applications through specific OPNsense WANs using DSCP markers. This folder contains the technical reference for how it works and how to operate it.

For the project overview, latest release, and limitations see the root [`README.md`](../README.md). For what changed in each version, see [`CHANGELOG.md`](../CHANGELOG.md). For how to report a vulnerability, see [`SECURITY.md`](../SECURITY.md).

## How OptiRoute works

```
Windows application (e.g. bf6.exe)
        |
Policy-based QoS (Windows)
        |
Mark packet: DSCP = 33
        |
OPNsense LAN rule matches DSCP = 33
        |
Route through chosen gateway (e.g. WAN_FERNANDO)
        |
Internet via Fernando NET
```

Windows knows which executable originated each packet. OptiRoute uses that knowledge to mark the traffic with a DSCP value, and OPNsense forwards the packet through the right gateway based on that mark.

## Supported environment

| Component | Version |
| --- | --- |
| Windows | 10 or 11, x64 |
| .NET | 10.0 SDK 10.0.400 or newer (development only) |
| OPNsense | 24.x or newer, with API reachable over HTTPS |
| OPNsense plugin | `os-firewall` (required) |
| Privileges | Administrator (Windows QoS policy creation requires elevation) |

## Documentation index

| Document | Purpose |
| --- | --- |
| [`architecture.md`](architecture.md) | High-level architecture, modules, persistence, threading, i18n |
| [`installation.md`](installation.md) | Installing from a release and building from source |
| [`opnsense-setup.md`](opnsense-setup.md) | Configuring OPNsense, rule layout, rule ordering, validation |
| [`windows-qos.md`](windows-qos.md) | How OptiRoute uses Windows QoS, cmdlets, safety rules |
| [`dscp-profiles.md`](dscp-profiles.md) | DSCP pool, DSCP/ToS math, conflict resolution |
| [`api-reference.md`](api-reference.md) | OPNsense REST endpoints and internal .NET contracts |
| [`troubleshooting.md`](troubleshooting.md) | Common issues and fixes |
| [`screenshots/`](screenshots/) | UI screenshots (assets live here; manifest in `screenshots/README.md`) |

## Source layout

```
src/
    OptiRoute.Core/         # Domain models, interfaces, services
    OptiRoute.OPNsense/     # OPNsense REST client and endpoint mappers
    OptiRoute.Windows/      # Windows QoS manager and DPAPI credential store
    OptiRoute.App/          # WPF UI, view models, themes, resources

tests/
    OptiRoute.Core.Tests/
    OptiRoute.Windows.Tests/
```

> The legacy CLI proof of concept (`src/OptiRoute.Poc/`) and internal-only
> docs (`docs/design-system.md`, `docs/ux-copy.md`, `docs/smoke-test-checklist.md`,
> `ROADMAP.md`) are excluded from the public repository via `.gitignore`.

## Runtime files

Everything lives under `%APPDATA%\OptiRoute`:

| File | Contents |
| --- | --- |
| `config.json` | User settings. Written atomically. |
| `credentials.bin` | DPAPI-encrypted OPNsense API key and secret. |
| `OptiRoute.log` | Rolling application log. |