# Architecture

High-level overview of how OptiRoute is wired together. For the deeper design
rationale (DSCP-as-identity, reconciliation states, OPNsense rule ordering), see
the other documents in this folder.

## High-level Diagram

```text
[MainWindow (XAML)]
     ↓ DataContext
[MainViewModel]
     ↓ delegates to
[OptiRouteSynchronizer]
     ↓ uses
[OpnsenseClient] + [WindowsQosManager] + [RuleOrderManager] + [HostOverrideManager]
```

## Modules

| Module | Responsibility |
| --- | --- |
| `OptiRoute.Core` | Domain models and interfaces. No WPF dependencies — pure, testable C#. |
| `OptiRoute.OPNsense` | HTTP client and endpoint mappers for the OPNsense REST API. |
| `OptiRoute.Windows` | Windows QoS manager (PowerShell `NetQosPolicy`) and the credential store (DPAPI). |
| `OptiRoute.App` | WPF UI, view models, and `Themes/Patterns` resources. |

## Persistence

Everything lives under `%APPDATA%\OptiRoute\`:

| File | Contents |
| --- | --- |
| `config.json` | User settings. Written atomically (temp file + `File.Replace`) so a crash cannot corrupt it. |
| `credentials.bin` | DPAPI-encrypted OPNsense API key + secret, bound to the Windows user. |
| `OptiRoute.log` | Rolling application log. |

## Threading

- **UI thread:** the WPF dispatcher owns all UI state.
- **Background:** `Task.Run` drives HTTP calls and other blocking work;
  results are marshaled back with `Dispatcher.Invoke`.
- **Auto-test:** a `DispatcherTimer` runs on the UI thread and triggers the
  debounced (500 ms) connection test after credentials change.

## Internationalization (i18n)

- `LString` is a `MarkupExtension` that reads from `LocalizedStrings.Instance`,
  a singleton implementing `INotifyPropertyChanged`.
- Values resolve through `Strings.ResourceManager.GetString(key, Strings.Culture)`.
- `LocalizationManager.SetCulture(code)` updates `Thread.CurrentUICulture`,
  sets `Strings.Culture`, and fires `OnCultureChanged()`, which refreshes all
  bindings — enabling live switching between `en-US` and `pt-BR` with no restart.

## Design System

- `Themes/Styles.xaml` holds design tokens (colors, spacing, typography) and
  component styles.
- `Themes/Patterns/*.xaml` holds reusable `UserControl`s:
  `InstructionsExpander`, `ValidationField`, and `ValidationSummary`.

## Executable detection and Add Application

`AddApplicationDialog` (`src/OptiRoute.App/Windows/AddApplicationDialog.xaml`) is a modal flow with Browse, Currently running, Gateway, and Refresh controls. It accepts a path from the native picker or a process selected from the scanner, then saves the selected executable and gateway.

`RunningProcessScanner` (`src/OptiRoute.App/Services/RunningProcessScanner.cs`) takes a best-effort snapshot, groups processes by full executable path, and reports instance count and working-set memory. Common system processes and inaccessible modules are skipped.

Gateway groups are excluded by the `IsLikelyGroup` heuristic before gateway choices are displayed.

`AppConfig.AllowInsecureTls` defaults to `false`; TLS validation remains enabled unless explicitly opted in. `DiagnosticsExporter.Sanitize` is public and testable, and redacts credentials and the OPNsense host before export. First-run visibility uses `SettingsViewModel.IsFirstRun`/`IsNotFirstRun` as its single source of truth for welcome, tabs, and Save visibility.
