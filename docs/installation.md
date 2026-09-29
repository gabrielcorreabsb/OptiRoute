# Installation

This guide covers installing OptiRoute from a published release and building it
from source. It also covers the OPNsense-side API user you need to create.

## Prerequisites

| Requirement | Details |
| --- | --- |
| Windows | Windows 10 or 11, x64 |
| .NET | .NET 10 SDK **10.0.400 or newer** (development only). The published single-file `exe` is self-contained — **no runtime installation is needed** to run it. |
| OPNsense | OPNsense 24.x or newer, with the API reachable at HTTPS |
| Privileges | Administrator. Windows QoS policy creation (`New-NetQosPolicy`) requires elevation. |

## OPNsense Side

OptiRoute manages firewall rules and aliases through the OPNsense API. Create a
dedicated API key rather than reusing an admin login.

1. In OPNsense, go to **System → Access → Users** and create (or pick) a user for
   OptiRoute.
2. Grant that user the following privileges:
   - `Firewall Aliases` (create/read/update/delete)
   - `Firewall Rules` (create/read/update/delete)
   - `Firewall Category` (create/read)
3. Go to **System → Access → Users**, edit the user, and under **API keys**
   click **+** to generate a key/secret pair.
4. Copy the key and secret — the secret is shown only once.

OptiRoute stores the key/secret encrypted with Windows DPAPI (see
[`SECURITY.md`](../SECURITY.md)). Paste them into the app's Settings panel.

## Build from Source

```powershell
# Clone
git clone https://github.com/gabrielcorreabsb/OptiRoute.git
cd OptiRoute

# Restore dependencies
dotnet restore

# Build (Debug)
dotnet build -c Debug

# Run the test suite
dotnet test

# Publish a Release, self-contained, single-file executable
dotnet publish src/OptiRoute.App `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o publish
```

The publish output is `publish/OptiRoute.exe`.

## Run from Publish

The published executable is self-contained and requires no separate .NET
runtime. Right-click `publish/OptiRoute.exe` and choose **Run as
administrator** (or launch from an elevated terminal). The application manifest
requests elevation, so Windows will prompt for UAC consent.

```powershell
.\publish\OptiRoute.exe
```

## Verify

Before using OptiRoute, confirm the machine can reach the OPNsense API. Open
this URL in a browser (adjust the hostname):

```text
http://opnsense/api/core/firmware/status
```

- If you are prompted for credentials, the API is reachable and requires the
  API key/secret (use the pair you generated above).
- If the page does not load, check DNS/hostname resolution and that the firewall
  allows access to the OPNsense web UI/API from this machine.

Once connectivity is confirmed, open OptiRoute → **⚙ Settings**, paste the API
key and secret, and save. The connection auto-tests after a 500 ms debounce; on
success, gateways are loaded automatically.

## First run

Run publish/OptiRoute.exe as administrator. The first-run state opens SettingsPanel in the main window; enter the OPNsense host and API credentials, test the connection, and let the app load gateways. Settings are saved to %APPDATA%\OptiRoute\config.json; credentials are stored separately as DPAPI-protected data. The published build is self-contained and needs no .NET runtime.



TLS certificate validation is enabled by default. Use Settings ? Connection ? Allow self-signed certificates only as an explicit opt-in on a trusted network.

