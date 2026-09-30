# OptiRoute

Multi-PC WAN routing for Windows + OPNsense.

> **SmartScreen on first run.** OptiRoute is not digitally signed (code-signing certificates from a trusted CA cost US$ 200+/year and are not practical for an early-stage open-source project). On Windows 10/11, the first launch shows **Windows protected your PC**. Click **More info** → **Run anyway**. As an alternative, right-click the downloaded `.exe`, choose **Properties**, check **Unblock**, then run it. After the first successful run the warning does not reappear.

## Latest release

**v0.1.1 Public Preview** — single-file `OptiRoute.exe` for Windows 10/11 x64. Download and SHA-256 checksums are on the [GitHub Releases][releases] page. See [CHANGELOG.md](CHANGELOG.md) for what changed in each version.

## Documentation

The full documentation lives in [`docs/`](docs/README.md):

- [Installation](docs/installation.md) — installing from a release, building from source, the OPNsense API user that needs to be created.
- [OPNsense setup](docs/opnsense-setup.md) — firewall rule layout, ordering, validation.
- [Windows QoS](docs/windows-qos.md) — how OptiRoute uses Windows QoS, the cmdlets it shells out to, the `-PolicyStore ActiveStore` rule that keeps policies removable.
- [DSCP profiles](docs/dscp-profiles.md) — DSCP pool, DSCP/ToS math, conflict resolution.
- [Architecture](docs/architecture.md) — modules, persistence, threading, i18n.
- [API reference](docs/api-reference.md) — OPNsense REST endpoints and the internal .NET contracts.
- [Troubleshooting](docs/troubleshooting.md) — common issues and fixes.

## Project status

OptiRoute is in **Public Preview**. End-to-end single-host use is validated against real OPNsense instances; multi-PC dedup of executables is implemented and unit-tested, but real-world validation with two physical PCs is deferred to the v0.2 milestone. The 0.1.x line is otherwise feature-frozen; automatic failover, latency-based routing, and centralized health checks will land in v0.2.

## Known limitations

- **SmartScreen prompt on first run.** The executable is not digitally signed; see the note at the top of this file.
- **Failover is manual.** When a WAN goes down, re-sync the affected app from the main window. Automatic failover is on the v0.2 roadmap.
- **Single-user Windows.** `%APPDATA%\OptiRoute\config.json` and `credentials.bin` are per-user; multi-user Windows installations are not supported.
- **Diagnostics Export can include Windows event log fragments.** Review the export before sharing. Credentials are always redacted.
- **OPNsense `os-firewall` plugin required.** It ships enabled in the standard OPNsense image; confirm it is enabled if you are running a custom image.
- **Code-signing certificates from a trusted CA are out of reach** for this project today. The SmartScreen warning is the only user-visible consequence; see [SECURITY.md](SECURITY.md#unsigned-binary-and-smartscreen).

## Security

Read [SECURITY.md](SECURITY.md) before reporting a vulnerability. TLS validation is enabled by default; the API key and secret are stored with Windows DPAPI; the diagnostic export redacts credentials.

## License

[MIT](LICENSE).

[releases]: https://github.com/gabrielcorreabsb/OptiRoute/releases
