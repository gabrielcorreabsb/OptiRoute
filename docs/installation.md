# Installation

This guide covers installing OptiRoute from a published release and building it from source. It also covers the OPNsense-side API user that needs to be created.

## Prerequisites

| Requirement | Details |
| --- | --- |
| Windows | Windows 10 or 11, x64 |
| .NET | .NET 10 SDK 10.0.400 or newer (development only). The published single-file executable is self-contained; no runtime installation is needed to run it. The repository targets `net10.0`. |
| OPNsense | OPNsense 24.x or newer, with the API reachable over HTTPS. The `os-firewall` plugin must be enabled (it ships enabled in the standard image). |
| Privileges | Administrator. Windows QoS policy creation (`New-NetQosPolicy`) requires elevation. |

## OPNsense side

OptiRoute manages firewall rules and aliases through the OPNsense API. Create a dedicated API key rather than reusing an administrator login.

1. In OPNsense, open **System → Access → Users** and create or pick a user for OptiRoute.
2. Grant that user the following privileges:
   - `Firewall Aliases` (create, read, update, delete)
   - `Firewall Rules` (create, read, update, delete)
   - `Firewall Category` (create, read)
3. Edit the user and under **API keys** click **+** to generate a key and secret pair.
4. Copy the key and secret. The secret is shown only once.

The key and secret are stored encrypted with Windows DPAPI. See [`SECURITY.md`](../SECURITY.md). Paste them into the Settings panel of the app.

## Build from source

```powershell
git clone https://github.com/gabrielcorreabsb/OptiRoute.git
cd OptiRoute

dotnet restore
dotnet build -c Debug
dotnet test

dotnet publish src/OptiRoute.App `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o publish
```

The publish output is `publish/OptiRoute.exe`. When built by the release workflow (`.github/workflows/release.yml`), the binary is renamed to `OptiRoute-v$VERSION.exe` and uploaded to the GitHub Release alongside its SHA-256 checksum; the local `publish/OptiRoute.exe` keeps the unversioned name.

## Run from publish

The published executable is self-contained and requires no separate .NET runtime. Right-click `publish/OptiRoute.exe` (or `OptiRoute-v$VERSION.exe` from a release) and choose **Run as administrator**, or launch from an elevated terminal. The application manifest requests elevation and Windows prompts for consent.

```powershell
# Local build:
.\publish\OptiRoute.exe

# Or a downloaded release asset:
.\OptiRoute-v0.1.1.exe
```

## Verify connectivity

Before using OptiRoute, confirm the machine can reach the OPNsense API. Open this URL in a browser (adjust the hostname):

```
https://opnsense/api/core/firmware/status
```

This endpoint is unauthenticated. If the browser shows a JSON payload (firmware version, status, upgrade info), the API host is reachable from this network. If the page does not load, check DNS resolution and confirm the firewall allows access to the OPNsense API from this machine.

> **Note:** OptiRoute itself never calls `/api/core/firmware/status` for authentication. The connection test in **Settings** hits `/api/firewall/filter/searchRule?rowCount=1` with the API key and secret so that the test reflects real authorization. The firmware endpoint above is only useful as a quick "is the host reachable?" smoke check before you have the credentials handy.

Once connectivity is confirmed, open OptiRoute → **Settings**, paste the API key and secret, and save. The connection auto-tests after a 500 ms debounce; on success, gateways are loaded automatically.

## First run

Run `publish/OptiRoute.exe` as Administrator. SettingsPanel opens in the main window on first launch. Enter the OPNsense host and API credentials, run the connection test, and let the app load the gateways. Settings persist to `%APPDATA%\OptiRoute\config.json` and credentials are stored separately as DPAPI-protected data. The published build is self-contained and needs no .NET runtime.