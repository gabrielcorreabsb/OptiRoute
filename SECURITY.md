# Security Policy

OptiRoute configures network routing on the host machine and talks to the OPNsense firewall using API credentials. Reports of vulnerabilities are taken seriously.

## Supported Versions

OptiRoute is early-stage software. Version **0.1.0 Public Preview** receives security updates. Only the latest released version receives security fixes. There are no long-term support branches.

| Version       | Supported          |
| ------------- | ------------------ |
| 0.1.0 Public Preview | :white_check_mark: |
| older releases | :x:                |

Upgrade before reporting if you are running an older build.

## Reporting a Vulnerability

Do not open a public issue for security problems.

Report privately by email to:

> blogsrto@gmail.com

GitHub Security Advisories are also accepted at
<https://github.com/gabrielcorreabsb/OptiRoute/security/advisories/new>.

Include the following in the report:

- Affected version(s) and Windows build
- OPNsense version
- Steps to reproduce
- Impact assessment
- Relevant lines from `%APPDATA%\OptiRoute\OptiRoute.log` after reviewing them for sensitive data

### Response Timeline

- Acknowledgement: within 72 hours of receipt.
- Fix timeline: 30 days for critical issues (remote code execution, credential disclosure, firewall bypass). Lower-severity issues are scheduled on a best-effort basis.

Reporters are kept informed of progress and credited on request.

## Security Design

OptiRoute runs as a single Windows desktop executable (WPF, .NET 10) with no server component.

- Credentials at rest. The OPNsense API key and secret are encrypted with Windows DPAPI and stored at `%APPDATA%\OptiRoute\credentials.bin`. The blob is bound to the Windows user account that created it and is not portable across machines, users, or the network.
- No telemetry. OptiRoute has no analytics, no crash reporting, and no phone-home behaviour. The only network target is the OPNsense address configured by the user.
- HTTPS to the OPNsense API. API calls use HTTPS. Certificate validation is enabled by default (`AllowInsecureTls = false`). Support for self-signed OPNsense certificates is opt-in through the Allow self-signed certificates setting, which disables validation and is explicitly labelled as insecure because it exposes the API credentials to MITM.
- Atomic configuration writes. `%APPDATA%\OptiRoute\config.json` is written to a temporary file and then atomically replaced using `File.Replace`. A crash mid-write cannot corrupt or truncate the previous configuration.
- Least privilege. Use a dedicated OPNsense API user with only the permissions OptiRoute needs. Never grant it administrator rights.

## Diagnostics and Logs

The Diagnostics Export bundles OS and runtime details, machine name, culture, a non-sensitive configuration summary, credential presence (present or missing, never the values), and the last 200 log lines. The API key and secret are redacted. The OPNsense host (internal IP or hostname) is masked as `[OPNSENSE-HOST]`. Logs may contain the OPNsense host and operational details but must never contain the API key or secret.

Do not publish diagnostic exports publicly. Share them only with trusted support recipients, keep Windows updated, and protect access to the Windows user profile.

## Known Limitations

These are inherent to the design and not bugs:

- Local secret storage. The OPNsense API secret is stored on the local machine. Anyone with physical access to an unlocked session, or with the ability to run as the same Windows user, can read it. Full local control equals full firewall API control.
- Opt-in insecure TLS. TLS validation is on by default. If self-signed certificates are enabled, an attacker who can MITM the network path between OptiRoute and OPNsense could intercept the API key and secret. Keep the setting off and use a trusted certificate on any untrusted network.
- Windows QoS cache drift. OptiRoute keeps a local cache of the QoS policies it created. If policies are changed outside OptiRoute, the cache can drift from the OPNsense state. Pressing Save in Settings re-syncs it.

### Known security limitations

See the [Known limitations](README.md#known-limitations-v010-public-preview) section in the README for the full list. Highlights relevant to the threat model:

- API secret is held as a `string` in process memory until the process exits. Memory dumps can recover it.
- TLS certificate validation is **enabled by default**. Self-signed certificates require explicit opt-in in Settings.
- Diagnostics exports are sanitized: Basic/Bearer authorization headers, URL userinfo, and the OPNsense host are redacted. Always review the export before sharing.

## Out of Scope

- Misconfiguration of the user's own OPNsense firewall rules.
- Physical or administrator-level access to the host machine, which is already a full compromise.
- Vulnerabilities in OPNsense itself. Report those upstream.