# Troubleshooting

Common questions and fixes. When in doubt, check the log file at
`%APPDATA%\OptiRoute\OptiRoute.log` (or use the **📋 Export diagnostics** button
in Settings for a sanitized snapshot).

---

**Q: The app shows no apps in the main window, or apps disappeared.**

A: Click **⚙ Settings → ⚙ Save**, then click **↻ Refresh** in the header to
rebuild the list from OPNsense and Windows. If it stays empty, inspect
`%APPDATA%\OptiRoute\OptiRoute.log` for API errors or an unreachable OPNsense
host.

---

**Q: I get OPNsense API errors, or "401 Unauthorized".**

A: The API key or secret is invalid or was revoked. Regenerate the API key in
OPNsense **System → Access → Users** with the same permissions
(`Firewall Aliases`, `Firewall Rules`, `Firewall Category`), then paste the new
key and secret into **⚙ Settings** and save.

---

**Q: Windows QoS policies don't apply.**

A: QoS policy creation requires elevation. Make sure OptiRoute is running
**as Administrator** (the app manifest requests it — accept the UAC prompt). If
it still fails, check **Event Viewer → Windows Logs → Application** for
`New-NetQosPolicy` errors.

---

**Q: Gateway display names are duplicated, or I can't save.**

A: Each gateway must have a **unique display name** (case-insensitive). Two
gateways cannot share a display name. Reset a row to its OPNsense name using the
**⚙** button in that row to clear the custom name.

---

**Q: DSCP values don't apply to traffic.**

A: Check the firewall rule on OPNsense. The rule must be applied to the **LAN**
interface and match the DSCP value. If the local Windows QoS cache has drifted,
**restart the app** to re-sync local QoS policies.

---

**Q: How do I enable live language change?**

A: It's already enabled. Use the **🌐** combo box in the header to switch between
`en-US` and `pt-BR`. No restart is required.

---

**Q: Where is the log file?**

A: `%APPDATA%\OptiRoute\OptiRoute.log`. Use the **📋 Export diagnostics** button
in Settings to produce a sanitized snapshot (system info + config summary + last
200 log lines) that is safe to attach to a bug report.
