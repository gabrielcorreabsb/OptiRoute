# Changelog

All notable changes to OptiRoute will be documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2026-09-29

### Added

- Single-window SettingsPanel (inline in MainWindow; no wizard modal)
- Live language switching (en-US ↔ pt-BR without restart) via `LString` MarkupExtension
- Auto-test connection in Settings: debounced 500ms after credentials change
- Auto-load gateways on connection success
- Diagnostics Export (sanitized system info + config summary + last 200 log lines)
- About dialog with version, license, GitHub links
- Optional system tray icon (OFF by default; enabled via Settings)
- Inline first-run state (SettingsPanel shows welcome on first launch)
- Phase 2 UX: expanded status bar with connection dot + state + Cancel button; loading stages (Reading rules → Comparing QoS → Building plan → Applying changes)
- File picker for executable selection in the Add Application dialog (persists last used folder)
- Running process scanner with instance count and memory usage (`RunningProcessScanner`)`n- Native file picker, Gateway Refresh button, and heuristic gateway-group filter

### Changed

- Wizard flow removed: app always opens directly into MainWindow
- ShutdownMode changed to `OnMainWindowClose` (only main window close triggers shutdown)
- LogPath is now a process-wide singleton (was `using var` disposed at end of OnStartup)
- MainWindow injection of `IOpnsenseClient` now rebuildable via `RebuildClientAsync()` on credentials change (no app restart required)`n- AddApplicationDialog redesigned as a modal; visual polish removed decorative status symbols and fixed card layout

### Fixed

- App no longer exits silently on first-run wizard Save (was due to `ShutdownMode=OnLastWindowClose` + wizard closing last window before MainWindow.Show)
- `using var loggerFactory` disposed at end of `OnStartup`, muting all subsequent logs — replaced with process-scope logger
- ComboBox `CultureOption { Code = ... }` displayed `ToString()` instead of `DisplayName` — fixed via explicit `ItemTemplate`
- DataGrid headers invisible against dark theme — added `Styles.DataGrid` with `ColumnHeaderStyle`
- Empty `IsCustomized` reset button — button now hidden when display name == OPNsense name
- `NRE` on `ScheduleAutoTest` when timer not yet initialized — timer moved to top of ctor; defensive `?.` guards
- `ArgumentException` in `LocalizationManager.SetCulture` from reflection on get-only `Strings.Culture` — removed reflection (setting `Thread.CurrentUICulture` is sufficient)

### Security

- SettingsWindow.xaml/cs DELETED; SettingsPanel replaces it (removes a window's worth of XAML attack surface)
- DSN file uses random GUID AppId for dedup — collision negligible at scale of home networks



### Security
- TLS validation is secure by default; self-signed certificates require explicit opt-in.
- Diagnostics export redacts Basic/Bearer authorization material and masks the OPNsense host as `[OPNSENSE-HOST]`.

### Changed
- Renamed the published executable from `OptiRoute.App.exe` to `OptiRoute.exe`.

### Fixed
- First-run crash on a clean `%APPDATA%` directory caused by tracing before log-directory creation.
- Blank wizard screen after �Get Started� caused by split visibility state between dependency properties and the view model.
