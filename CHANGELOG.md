# Changelog

All notable changes to OptiRoute will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1] - 2026-09-30

### Fixed
- Single-file publish now bundles the WPF native DLLs (`PresentationNative_cor3`, `wpfgfx_cor3`, `D3DCompiler_47_cor3`, `vcruntime140_cor3`, `PenImc_cor3`) into `OptiRoute.exe` via `IncludeNativeLibrariesForSelfExtract=true`. Previously these DLLs were left beside the executable, so the binary was not actually standalone and would only start when copied together with the DLLs.

## [0.1.0] - 2026-09-30

### Note
First **Public Preview**. The internal 2.1.0 series was a private pre-preview; its changelog is preserved below for traceability, but 0.1.0 is the first release published externally.

### Added
- TLS validation enabled by default at startup (`VerifyTls = !config.AllowInsecureTls`)
- Specific error messages in connection test (401, timeout, TLS) with "Allow self-signed" affordance
- Admin-elevation check on startup with restart prompt
- Confirmation dialog before "Apply Repair" listing pending actions
- GitHub issue templates (bug report, feature request) and PR template
- README sections: "Supported OPNsense versions", "Known limitations (v0.1.0)"
- SECURITY section: "Known security limitations"

### Changed
- Default version scheme switches to semver Public Preview (`0.1.0`).

### Security
- App respects `config.AllowInsecureTls` from first boot; no silent fallback to plaintext.

## [2.1.0] - 2026-09-29 — internal pre-preview (not published)

### Added

- Single-window SettingsPanel inline in the main window; no separate wizard modal
- Live language switching between `en-US` and `pt-BR` without restart, via the `LString` markup extension
- Auto-test connection in Settings, debounced 500 ms after credentials change
- Automatic gateway load on successful connection
- Diagnostics Export with sanitized system info, config summary, and the last 200 log lines
- About dialog showing version, license, and GitHub links
- Optional system tray icon, off by default and enabled through Settings
- Inline first-run state: SettingsPanel shows the welcome view on first launch
- Status bar with connection dot, sync state, and cancel button; loading stages reported as `Reading rules`, `Comparing QoS`, `Building plan`, `Applying changes`
- Native file picker for executable selection in the Add Application dialog, with last-used folder persistence
- Running process scanner that reports instance count and working-set memory per executable

### Changed

- Wizard flow removed: the application always opens directly into the main window
- `ShutdownMode` set to `OnMainWindowClose` so only the main window close triggers shutdown
- `LogPath` is now a process-wide singleton; previous `using var` disposed it at the end of `OnStartup`
- `IOpnsenseClient` is rebuilt immediately on credential change through `RebuildClientAsync`, no restart required
- Add Application redesigned as a modal; decorative status symbols removed and card layout corrected

### Fixed

- Silent exit on first-run wizard save caused by `ShutdownMode=OnLastWindowClose` combined with the wizard closing the last window before `MainWindow.Show`
- `using var loggerFactory` disposed at the end of `OnStartup`, muting subsequent logs; replaced with a process-scope logger
- `ComboBox CultureOption` displaying `ToString()` instead of `DisplayName`; fixed through an explicit `ItemTemplate`
- DataGrid headers invisible against the dark theme; added `Styles.DataGrid` with a `ColumnHeaderStyle`
- Empty `IsCustomized` reset button hidden when the display name equals the OPNsense name
- `NullReferenceException` on `ScheduleAutoTest` when the timer was not yet initialized; timer moved to the top of the constructor and defensive `?.` guards added
- `ArgumentException` in `LocalizationManager.SetCulture` from reflection on the get-only `Strings.Culture`; reflection removed, setting `Thread.CurrentUICulture` is sufficient

### Security

- TLS validation enabled by default; self-signed certificates require explicit opt-in
- Diagnostics Export redacts Basic and Bearer authorization material and masks the OPNsense host as `[OPNSENSE-HOST]`
- Dedicated rule AppId uses random GUID for cross-PC deduplication