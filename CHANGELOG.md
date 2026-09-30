# Changelog

All notable changes to OptiRoute will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2026-09-29

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