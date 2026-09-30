# OptiRoute

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)
![Build](https://img.shields.io/badge/build-passing-brightgreen)

Route Windows applications through specific OPNsense WANs using DSCP markers.

OptiRoute runs on a Windows PC and assigns each managed application a DSCP value on its outgoing traffic. An OPNsense firewall rule matches that DSCP value and forwards the traffic through the chosen gateway. A single-PC setup works the same way as a multi-PC setup: every machine that runs OptiRoute for the same executable shares the same DSCP value, so the firewall rule applies to all of them.

## Install

Download the latest `OptiRoute.exe` from the [Releases](https://github.com/gabrielcorreabsb/OptiRoute/releases) page and run it as Administrator (the manifest requests elevation). OptiRoute stores configuration and credentials under `%APPDATA%\OptiRoute`.

To build from source, see [`docs/installation.md`](docs/installation.md).

## First-time setup

1. Launch `OptiRoute.exe` and accept the UAC prompt.
2. In Settings, enter the OPNsense host URL, the API key, and the API secret.
3. Wait for the connection test to succeed. Gateways are loaded automatically.
4. Add an executable such as `bf6.exe`, pick a gateway, and save.
5. Traffic from that executable now leaves through the chosen WAN.

## Multi-PC deployment

Install OptiRoute on each PC and add the same executable on every machine. OptiRoute uses a random AppId stored in the OPNsense rule description to deduplicate identities across PCs. When another PC already has a rule for the same executable, OptiRoute adopts the existing identity instead of creating a duplicate.

## Security

- The OPNsense API key and secret are encrypted with Windows DPAPI and stored at `%APPDATA%\OptiRoute\credentials.bin`. They are bound to the current Windows user and never sent anywhere except the configured OPNsense host.
- Diagnostics exports redact credentials and mask the OPNsense host as `[OPNSENSE-HOST]`.
- TLS validation is enabled by default. The self-signed certificates option exists for trusted local networks and is explicitly labelled as insecure.

See [`SECURITY.md`](SECURITY.md) for the threat model, supported versions, and reporting process.

## Documentation

- [`docs/installation.md`](docs/installation.md) — prerequisites, build, and publishing
- [`docs/architecture.md`](docs/architecture.md) — modules, persistence, synchronisation, and i18n
- [`docs/opnsense-setup.md`](docs/opnsense-setup.md) — OPNsense configuration and rule layout
- [`docs/windows-qos.md`](docs/windows-qos.md) — how OptiRoute uses Windows QoS
- [`docs/dscp-profiles.md`](docs/dscp-profiles.md) — DSCP pool, DSCP/ToS mapping, conflict resolution
- [`docs/api-reference.md`](docs/api-reference.md) — OPNsense REST endpoints and internal contracts
- [`docs/troubleshooting.md`](docs/troubleshooting.md) — common issues and fixes

## Contributing

Pull requests are welcome. For large changes, open an issue first.

- Follow the C# StyleCop profile and run `dotnet format` before committing.
- Add XML documentation for public APIs.
- Put user-facing strings in `Strings.resx` and `Strings.pt-BR.resx`. Do not inline them.
- In XAML, use `StaticResource`: `Styles.*` for controls, `Tokens.*` for raw values.
- Avoid new NuGet packages unless necessary and discussed first.

## License

[MIT](LICENSE) — Copyright © 2026 gabrielcorreabsb / OptiRoute contributors.