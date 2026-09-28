# OptiRoute

> Mark Windows packets with DSCP by executable. OPNsense routes them by gateway.
> Multi-PC orchestration without a central server.

**Status:** Active development · v0.x · [Smoke testing](docs/smoke-test-checklist.md) in progress

---

## What is it?

OptiRoute keeps multiple Windows PCs each routing specific executables through specific
WAN gateways on a shared OPNsense firewall — without touching individual router
configs or running a Windows Service.

You register `bf6.exe → WAN2`. Every packet from that executable gets DSCP 26 marked
on the Windows side. OPNsense sees the DSCP, matches a firewall rule, and routes
through WAN2. Other traffic from other apps flows normally.

```
┌──────────────┐                  ┌──────────────┐
│  PC-A        │                  │  PC-B        │
│ bf6.exe ─────┼─► DSCP 26 ─────► │              │
│ (browser)    │                  │              │
└──────────────┘                  └──────┬───────┘
       │                                 │
       └────────────┬────────────────────┘
                    ▼
            ┌──────────────┐
            │   OPNsense   │
            │   Firewall   │
            │ ──────────── │
            │ DSCP 26 → WAN2│
            │ DSCP 33 → WAN1│
            │ Other → LAN   │
            └──────────────┘
```

## Features

- ✅ Per-executable DSCP marking via PowerShell `NetQosPolicy`
- ✅ OPNsense firewall rules auto-generated with category `OptiRoute`
- ✅ Multi-PC deduplication (same `.exe` on multiple PCs shares the same `AppId`)
- ✅ Per-host override (PC-A can route bf6 through WAN2 while PC-B uses WAN1)
- ✅ Conflict detection: catches tampering with either the OPNsense rule `tos` or the
  Windows QoS DSCP
- ✅ Self-healing on `tos/description` corruption (interactive dialog)
- ✅ Idempotent: Sincronizar can be run repeatedly with no side-effects
- ✅ DPAPI-encrypted credentials (never plaintext on disk)

## Requirements

- Windows 10/11 x64 with Administrator rights
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for development)
- OPNsense 24.x or newer with API access enabled
- A second machine (physical or VM) to validate multi-PC scenarios

## Quick Start (Development)

```powershell
# Clone
git clone https://github.com/gabrielcorreabsb/OptiRoute.git
cd OptiRoute

# Restore + build
dotnet restore
dotnet build -c Release

# Run tests
dotnet test

# Launch the App (requires Admin)
dotnet run --project src/OptiRoute.App -c Release
```

End-user single-file release:

```powershell
dotnet publish src/OptiRoute.App `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o publish
```

Output: `publish/OptiRoute.App.exe` (~80 MB).

## Documentation

| Doc | Purpose |
|---|---|
| [docs/architecture.md](docs/architecture.md) | DSCP=identity principle, reconciliation states, layering |
| [docs/api-reference.md](docs/api-reference.md) | OPNsense REST endpoints + internal .NET contracts |
| [docs/dscp-profiles.md](docs/dscp-profiles.md) | DSCP pool + ToS math |
| [docs/opnsense-setup.md](docs/opnsense-setup.md) | OPNsense plugin/API setup |
| [docs/windows-qos.md](docs/windows-qos.md) | Windows PowerShell NetQoS + `OptiRoute-` prefix |
| [docs/smoke-test-checklist.md](docs/smoke-test-checklist.md) | Manual smoke test scenarios (A–F) |
| [ROADMAP.md](ROADMAP.md) | High-level development status and open deltas |

## Project Layout

```
OptiRoute/
├── src/
│   ├── OptiRoute.Core/          # Models, interfaces, sync orchestrator
│   ├── OptiRoute.OPNsense/     # REST client (OpnsenseClient)
│   ├── OptiRoute.Windows/       # Windows QoS PowerShell wrapper
│   └── OptiRoute.App/           # WPF MVVM UI
├── tests/
│   ├── OptiRoute.Core.Tests/    # 50+ unit tests
│   └── OptiRoute.Windows.Tests/ # PowerShell integration tests
├── docs/                        # Architecture, API, smoke test
├── ROADMAP.md                   # Status of every open delta
└── OptiRoute.slnx               # Solution
```

## Architecture in 30 seconds

```
        ┌────────────────────────────────────────────┐
        │ OptiRoute.App (WPF MVVM)                   │
        │   MainViewModel → ApplyRepairCommand       │
        └────────────────┬───────────────────────────┘
                         │
        ┌────────────────▼───────────────────────────┐
        │ OptiRoute.Core (pure C#, no IO)            │
        │   IOptiRouteSynchronizer                   │
        │     SyncAsync → BuildPlanAsync →           │
        │     ApplyPlanAsync → VerifyAsync           │
        └─────┬──────────────────┬───────────────────┘
              │                  │
   ┌──────────▼───────┐  ┌───────▼──────────┐
   │ WindowsQosManager│  │  OpnsenseClient  │
   │ (PowerShell .ps1)│  │  (HttpClient)    │
   └──────────────────┘  └──────────────────┘
```

The **pipeline** is non-trivial: every mutation flows through
`BuildState → BuildPlan → user reviews → ApplyPlan → Verify`. Only `ApplyPlan` writes.

## Contributing

Pull requests welcome for:
- Bug reports (with log excerpt from `%APPDATA%\OptiRoute\OptiRoute.log`)
- Additional smoke test scenarios (E, F still pending)
- Localization (we plan en-US + pt-BR; more welcome)
- Documentation improvements

This is an early-stage project. Major refactors happen. Open an issue before sending
significant changes.

## Security

Report vulnerabilities via [SECURITY.md](SECURITY.md) (or as GitHub Security Advisories).

Credentials are stored with Windows DPAPI. The App runs only as Administrator.

## License

MIT — see [LICENSE](LICENSE).

## Author

Gabriel Correa — https://github.com/gabrielcorreabsb
