# OptiRoute

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)
![Build](https://img.shields.io/badge/build-passing-brightgreen)

Multi-PC DSCP marker for OPNsense and Windows QoS. Each application binary (`bf6.exe`, `cod.exe`, and so on) receives a DSCP value on its outgoing Windows traffic; an OPNsense firewall rule then routes that traffic through the selected gateway. A single-PC rollout works too.

## How we do things in this app

This is the load-bearing section. If you only read one part of this README, read this.

### 1. No telemetry

OptiRoute makes network calls only to the OPNsense API configured by the user. There is no analytics, crash reporting, or phone-home service. The diagnostics export records the network endpoints contacted during the session; inspect it or verify the traffic with Wireshark.

### 2. Credentials stay local and encrypted

The OPNsense API key and secret are protected with Windows DPAPI and stored at `%APPDATA%\OptiRoute\credentials.bin`. They are bound to the current Windows user and are not sent anywhere except the configured OPNsense host. Diagnostics exports redact both values.

### 3. Configuration writes are atomic

Configuration saves write a temporary file and replace the existing file atomically (`AppConfigManager.Save`). A crash during a save leaves the previous configuration available.

### 4. Language changes are live

The language selector switches between English and Portuguese without restarting. The `LString` markup extension re-evaluates bindings when the culture changes through `LocalizedStrings`.

### 5. Settings stay in the main window

Settings are presented inline rather than in a separate modal window, keeping application lifetime and shutdown behavior predictable. See [`docs/architecture.md`](docs/architecture.md).

### 6. OPNsense is the source of truth

Windows QoS is the local projection of the state managed in OPNsense. Refresh and sync operations fetch canonical firewall state, reconcile local drift, and surface conflicts when a rule or DSCP value no longer matches. Only the apply stage mutates managed state.

### 7. The DSCP pool is conservative

The default pool is `0-7`, below commonly reserved values such as EF (`46`) and CS6/CS7 (`48-63`). The pool is validated to stay within `0-63` and avoid reserved values. Advanced settings can widen it, with a warning when reserved ranges would be crossed.

### 8. Multi-PC operation is safe by construction

Each application identity carries an `AppId` in the OPNsense rule description. When another PC already has a rule for the same executable, OptiRoute adopts the existing identity instead of creating a duplicate. Host-specific overrides remain available for different gateways.

### 9. Inputs are validated before saving

- OPNsense host: valid HTTP or HTTPS URL
- Preferred local IP: valid IPv4 address when provided
- API key and secret: at least 20 characters
- DSCP pool: `0-63`, start less than or equal to end
- Log retention: 1-365 days

The Save command is disabled (`CanSave == false`) while validation errors remain.

### 10. Saving credentials rebuilds the client

When credentials change, the `OpnsenseClient` and dependent synchronizer/managers are rebuilt immediately. A restart is not required before the next connection or synchronization.

## Install

See [`docs/installation.md`](docs/installation.md) for Windows 10/11, .NET 10 SDK, OPNsense 24.x+, Administrator requirements, and build/publish commands. That guide is planned if it is not yet present in this checkout.

```powershell
git clone https://github.com/gabrielcorreabsb/OptiRoute.git
cd OptiRoute
dotnet publish src/OptiRoute.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish
.\publish\OptiRoute.exe
```

## Usage

1. Run the executable; use **Run as administrator** the first time.
2. On first launch, enter the OPNsense host, API key, and API secret.
3. Select **Test Connection** (or wait for the connection test after editing fields).
4. Add an executable such as `bf6.exe` and select its gateway.
5. Select **Save** to push the rule to OPNsense and create the local QoS policy.

For multi-PC setups, install OptiRoute on each PC and add the same executable. Existing managed identities are deduplicated automatically.

## Documentation

- [`docs/installation.md`](docs/installation.md) � prerequisites, build, and publishing
- [`docs/troubleshooting.md`](docs/troubleshooting.md) � common issues and fixes (planned)
- [`docs/architecture.md`](docs/architecture.md) � modules, persistence, synchronization, and i18n
- [`SECURITY.md`](SECURITY.md) � credential handling and vulnerability reporting
- [`CHANGELOG.md`](CHANGELOG.md) � version history (planned)
- [`ROADMAP.md`](ROADMAP.md) � current work and future plans

Additional notes are available in [`docs/`](docs/), including OPNsense setup, Windows QoS, DSCP profiles, API reference, and smoke-test scenarios.

## Contributing

Pull requests are welcome. For large changes, open an issue first.

- Follow the C# StyleCop profile and run `dotnet format` before committing.
- Add XML documentation for public APIs.
- Put user-facing strings in `Strings.resx` and `Strings.pt-BR.resx`; do not inline them.
- Avoid new NuGet packages unless necessary and discussed first.
- In XAML, use `StaticResource`; use `Styles.*` for controls and `Tokens.*` for raw values.

## License

[MIT](LICENSE) � Copyright � 2026 gabrielcorreabsb / OptiRoute contributors.

## Acknowledgments

- [OPNsense](https://opnsense.org/) for the firewall API
- Microsoft PowerShell SDK for Windows QoS management
- `System.Windows.Forms.NotifyIcon` for the optional tray icon


## Features

- Multi-PC WAN routing with a gateway per application
- OPNsense API integration
- Native file picker and running-process scanner for executables
- Per-PC local overrides and effective-route display
- English/Portuguese (Brazil) live language switching
- Dark-theme UI with neutral badges and modal Add Application flow

## Security & Data Storage

Preferences are stored in `%APPDATA%\OptiRoute\config.json`; API credentials are DPAPI-encrypted in `%APPDATA%\OptiRoute\credentials.bin`. Logs are in `%APPDATA%\OptiRoute\OptiRoute.log`. Diagnostics contain system details, a non-sensitive config summary, credential presence, and the last 200 log lines; secrets are redacted. See [SECURITY.md](SECURITY.md).

## Building from source

```powershell
dotnet build src/OptiRoute.App/OptiRoute.App.csproj -c Debug
dotnet publish src/OptiRoute.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

