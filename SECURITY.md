# Security Policy

OptiRoute configures network routing on your machine and talks to your OPNsense
firewall with API credentials. We take security reports seriously.

## Supported Versions

OptiRoute is early-stage software. Only the **latest released `v0.x.y`** receives
security fixes. There are no LTS branches.

| Version       | Supported          |
| ------------- | ------------------ |
| latest `v0.x.y` | :white_check_mark: |
| older releases  | :x:                |

If you are on an older build, upgrade before reporting.

## Threat Model

OptiRoute is a **single-user, local Windows desktop application**. It talks only
to the OPNsense host configured by the user and has **no central service, no
telemetry, and no remote backend**. The primary assets to protect are the
OPNsense API key/secret (which grant full firewall API control) and the local
configuration.

## Reporting a Vulnerability

**Please do not open a public issue for security problems.**

Report privately by email to:

> blogsrto@gmail.com

You may also use [GitHub Security Advisories](https://github.com/gabrielcorreabsb/OptiRoute/security/advisories/new)
if you prefer.

Please include:

- Affected version(s) and Windows build
- OPNsense version
- Steps to reproduce
- Impact assessment (what an attacker gains)
- Any logs from `%APPDATA%\OptiRoute\OptiRoute.log` (review them first — see below)

### Response Timeline

- **Acknowledgement:** within **72 hours** of receipt.
- **Fix timeline:** **30 days** for critical issues (remote code execution,
  credential disclosure, firewall bypass). Lower-severity issues are scheduled
  on a best-effort basis.

We will keep you informed of progress and credit reporters who want attribution.

## Security Design

OptiRoute runs as a single desktop executable (WPF, .NET 10) with no server
component. The security model is deliberately simple:

- **Credentials at rest.** The OPNsense API key and secret are encrypted with
  Windows **DPAPI** and written to `%APPDATA%\OptiRoute\credentials.bin`. The
  blob is bound to the Windows user account that created it and is **not
  portable across machines or users**, nor reachable over the network.
- **No telemetry.** OptiRoute has no analytics, no crash reporting, and no
  "phone home" behavior. It only contacts the OPNsense address you configure.
- **HTTPS to the OPNsense API.** API calls use HTTPS. TLS certificate
  validation is **enabled by default** (`AllowInsecureTls = false`). Support for
  self-signed OPNsense certificates is **opt-in** via the "Allow self-signed
  certificates (insecure)" setting, which disables validation and is explicitly
  labelled as insecure because it exposes the API credentials to MITM.
- **Atomic config writes.** `%APPDATA%\OptiRoute\config.json` is written using a
  temp file plus `File.Replace`, so a crash mid-write cannot corrupt or truncate
  the existing configuration.
- **Least privilege.** Use a dedicated OPNsense API user with only the
  permissions OptiRoute needs, never an administrator account.

## Diagnostics & Logs

The Diagnostics Export bundles OS/runtime details, machine name, culture, a
**non-sensitive** configuration summary, credential *presence* (present/missing —
never the values), and the last 200 log lines. The API key and secret are
redacted, and the OPNsense host (internal IP/hostname) is masked as
`[OPNSENSE-HOST]`. Logs may contain the OPNsense host and operational details,
but must never contain the API key or secret.

**Never publish diagnostic exports publicly.** Share them only with trusted
support recipients, keep Windows updated, and protect access to the Windows user
profile.

## Known Limitations

These are inherent to the current design, not bugs:

- **Local secret storage.** The OPNsense API secret is stored on the local
  machine. Anyone with physical access to an unlocked session — or the ability
  to run as the same Windows user — can use it. Full local control equals full
  firewall API control.
- **Opt-in insecure TLS.** TLS validation is on by default. If the user enables
  "Allow self-signed certificates (insecure)", an attacker who can MITM the
  network path between OptiRoute and OPNsense could intercept the API
  key/secret. Keep the setting off and use a trusted certificate on any
  untrusted network.
- **Windows QoS cache drift.** OptiRoute keeps a local cache of the QoS policies
  it created. If policies are changed outside OptiRoute, the cache can drift
  from the OPNsense state. Pressing **Save** in Settings re-syncs it.

## Out of Scope

- Misconfiguration of your own OPNsense firewall rules.
- Physical or administrator-level access on the host machine (already a full
  compromise).
- Vulnerabilities in OPNsense itself — report those upstream.

### Security details
TLS validation is enabled by default. Self-signed certificates require explicit opt-in via Settings ? Connection ? Allow self-signed certificates (insecure). Diagnostic exports mask the host as `[OPNSENSE-HOST]`, fully remove Authorization headers, and redact Basic `key:secret` base64 material and URL credentials. Do not enable the self-signed option on untrusted networks.
