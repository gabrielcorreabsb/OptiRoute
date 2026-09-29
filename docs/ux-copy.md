# UX Copy & Internationalization Contract

> Single source of truth for every user-facing string in the OptiRoute App.
> Adding UI? Add a row to the table below **before** coding. Changing copy? Update here
> first, then propagate.

**Status:** Phase 0 — pending designer/Fase 1 review
**Locales:** `en-US` (default fallback) + `pt-BR`
**Last updated:** 2026-09-28

---

## 1. Methodology

### 1.1 Tone

| Locale | Tone |
|---|---|
| **en-US** | Direct, slightly technical. OPNsense users are advanced — no hand-holding. |
| **pt-BR** | Same tone, natural Brazilian Portuguese (not literal translation). |

### 1.2 Show technical terms, hide jargon

| Always visible (user-facing) | Hidden (logs only) |
|---|---|
| `DSCP`, `Gateway`, `OPNsense`, `Application`, `Refresh`, `Apply` | `RecreateFirewallRule`, `TosMismatchDetail`, `source_net`, `destination_not`, `EffectiveApplicationRoute` |

### 1.3 Locale handling

- **Detection:** System culture on first launch.
- **Override:** Settings → Advanced → Language (`System` / `English` / `Português (BR)`).
- **Storage:** `config.json` key `culture` (e.g., `"pt-BR"`, `"en-US"`).
- **Log messages:** Always `en-US`. Devs troubleshooting across locales should read the same log.

---

## 2. Canonical terminology

| Concept | en-US | pt-BR | Internal term (NOT shown) |
|---|---|---|---|
| App name | `OptiRoute` | `OptiRoute` | — |
| Tagline (window subtitle) | `Multi-PC WAN Router` | `Roteador Multi-PC por WAN` | — |
| External firewall | `OPNsense` | `OPNsense` | — |
| Network interface gateway | `Gateway` | `Gateway` | `Gateway` |
| QoS marking value | `DSCP` | `DSCP` | `Dscp` |
| Low-level packet field | `TOS byte` (dialog only) | `byte TOS` (dialog only) | `Tos` |
| Windows QoS policy name prefix | `OptiRoute-` (read-only, Windows lowercases) | `OptiRoute-` | `optiroute-` |
| OPNsense firewall rule category | `OptiRoute` (read-only) | `OptiRoute` | — |
| Sync action | `Refresh` | `Sincronizar` | `SyncAsync` |
| Apply pending repairs | `Apply Changes` | `Aplicar reparos` | `ApplyRepairAsync` |
| Add executable to manage | `Add Application` | `Adicionar Aplicativo` | `RegisterOrUpdateApplicationAsync` |
| Conflict state | `Out of sync` | `Fora de sincronia` | `Conflict` |
| PC-only state | `This PC only` | `Apenas neste PC` | `LocalOnly` |
| Network-only state | `Available in OPNsense` | `Disponível no OPNsense` | `GlobalOnly` |
| Synced state | `In sync` | `Em sincronia` | `Synchronized` |
| Multi-PC architecture badge | `Multi-PC Architecture` | `Arquitetura Multi-PC` | — |

---

## 3. String inventory — `MainWindow.xaml`

Every row below **must** be in `Properties/Strings.resx` (en-US) and
`Properties/Strings.pt-BR.resx` before Fase 1 begins.

| Resource Key | Location | en-US | pt-BR |
|---|---|---|---|
| `MainWindow.Title` | Window title bar | `OptiRoute — Multi-PC WAN Router` | `OptiRoute — Roteador Multi-PC por WAN` |
| `MainWindow.BrandName` | Header logo | `⚡ OptiRoute` | `⚡ OptiRoute` |
| `MainWindow.ArchitectureBadge` | Header badge | `Multi-PC Architecture` | `Arquitetura Multi-PC` |
| `MainWindow.OpnsenseLabel` | Status row | `OPNsense:` | `OPNsense:` |
| `MainWindow.LocalComputerLabel` | Status row | `This Computer:` | `Este Computador:` |
| `MainWindow.RefreshButton` | Top bar | `↻ Refresh` | `↻ Sincronizar` |
| `MainWindow.ApplyChangesButton` | Top bar | `✓ Apply Changes` | `✓ Aplicar reparos` |
| `MainWindow.AddApplicationButton` | Top bar | `+ Add Application` | `+ Adicionar Aplicativo` |
| `MainWindow.ActiveGatewaysLabel` | Gateways bar | `Active Gateways:` | `Gateways Ativos:` |
| `MainWindow.EmptyState.Title` | No apps state | `No managed applications yet` | `Nenhum aplicativo gerenciado no momento` |
| `MainWindow.EmptyState.Body` | No apps state | `Click below to add a game or executable to OptiRoute.` | `Clique no botão abaixo para adicionar um jogo ou executável ao OptiRoute.` |
| `MainWindow.EmptyState.Action` | No apps state | `+ Add Application` | `+ Adicionar Aplicativo` |
| `MainWindow.NotConnectedState.Title` | No config state | `OptiRoute isn't connected to OPNsense yet.` | `OptiRoute ainda não está conectado ao OPNsense.` |
| `MainWindow.NotConnectedState.Action` | No config state | `Open Settings` | `Abrir Configurações` |
| `MainWindow.VersionBadge` | Footer | `OptiRoute v2.1.0 (Multi-PC)` | `OptiRoute v2.1.0 (Multi-PC)` |

---

## 4. String inventory — application cards (per-state panels)

| Resource Key | Context | en-US | pt-BR |
|---|---|---|---|
| `Card.Column.RouteEffective` | Column header | `Effective Route:` | `Rota Efetiva:` |
| `Card.DeleteButton.Tooltip` | Delete `✕` button | `Delete application` | `Excluir aplicativo` |
| `Card.Synced.GlobalDefault` | Default gateway label | `Global Default:` | `Padrão global:` |
| `Card.Synced.GlobalDefaultHint` | Sub-label | `Default for the entire network` | `Padrão para toda a rede` |
| `Card.Synced.ThisComputer` | Local override label | `This Computer:` | `Este Computador:` |
| `Card.Synced.FollowGlobal` | Radio button | `Follow Global` | `Seguir Global` |
| `Card.Synced.OverrideLocal` | Radio button | `Override local:` | `Override local:` |
| `Card.Synced.OverrideLocalHint` | Sub-label | `Routes through a different WAN for this computer only` | `Muda a WAN exclusivamente para este computador` |
| `Card.LocalOnly.Title` | LocalOnly card | `⚠ Policy exists only on this Windows machine` | `⚠ Política existente apenas no Windows local` |
| `Card.LocalOnly.Body` | LocalOnly card | `This application has no global rule in OPNsense.` | `Esta aplicação não possui regra global cadastrada no OPNsense.` |
| `Card.LocalOnly.RegisterButton` | LocalOnly card | `+ Register in OPNsense` | `+ Registrar no OPNsense` |
| `Card.LocalOnly.RemoveButton` | LocalOnly card | `Remove from Windows` | `Remover do Windows` |
| `Card.GlobalOnly.Title` | GlobalOnly card | `○ Available in OPNsense` | `○ Disponível no OPNsense` |
| `Card.GlobalOnly.Body` | GlobalOnly card | `QoS not yet configured for this game on this computer.` | `QoS ainda não configurado para este jogo neste computador.` |
| `Card.GlobalOnly.Hint` | Hint (italic) | `→ Apply via the '✓ Apply Changes' button in the top bar.` | `→ Aplique pelo botão '✓ Aplicar reparos' na barra superior.` |
| `Card.Conflict.Title` | Conflict card | `⚠ DSCP mismatch detected` | `⚠ Divergência de DSCP detectada` |
| `Card.Conflict.Body` | Conflict card | `The Windows local DSCP differs from the OPNsense global DSCP.` | `O DSCP local do Windows diverge do DSCP global registrado no OPNsense.` |
| `Card.Conflict.Hint` | Hint (italic) | `→ Apply via the '✓ Apply Changes' button in the top bar.` | `→ Aplique pelo botão '✓ Aplicar reparos' na barra superior.` |

---

## 5. String inventory — `InvalidTosDialog.xaml`

| Resource Key | en-US | pt-BR |
|---|---|---|
| `InvalidTos.Window.Title` | `OPNsense Rule Corrupted` | `Regra OptiRoute corrompida no OPNsense` |
| `InvalidTos.Header.Title` | `⚠ OPNsense OptiRoute Rule Corrupted` | `⚠ Regra OptiRoute corrompida no OPNsense` |
| `InvalidTos.DescriptionLabel` | `'{0}'` (placeholder for rule description) | `'{0}'` |
| `InvalidTos.DescriptionSays` | `description says:` | `description diz:` |
| `InvalidTos.ActualTosSays` | `actual TOS says:` | `tos real diz:` |
| `InvalidTos.CancelButton` | `Cancel` | `Cancelar` |
| `InvalidTos.UseWindowsDscpButton` | `Use Windows DSCP (regenerate OPNsense rule with tos=localDscp<<2)` | `Usar DSCP do Windows (regenerar regra OPNsense com tos=localDscp<<2)` |
| `InvalidTos.UseOpnsenseTosButton` | `Use OPNsense TOS (update Windows + description to tos/4)` | `Usar tos do OPNsense (atualizar Windows + description para tos/4)` |

---

## 6. String inventory — `SettingsWindow.xaml` (Fase 1 — pending implementation)

These will be added during Fase 1 implementation but are listed here so the contract
is complete up-front.

### 6.1 Window chrome

| Resource Key | en-US | pt-BR |
|---|---|---|
| `Settings.Window.Title` | `OptiRoute Settings` | `Configurações do OptiRoute` |
| `Settings.Tabs.Connection` | `Connection` | `Conexão` |
| `Settings.Tabs.Credentials` | `Credentials` | `Credenciais` |
| `Settings.Tabs.Gateways` | `Gateways` | `Gateways` |
| `Settings.Tabs.Advanced` | `Advanced` | `Avançado` |

### 6.2 Connection tab

| Resource Key | en-US | pt-BR |
|---|---|---|
| `Settings.Connection.OpnsenseAddress` | `OPNsense Address` | `Endereço do OPNsense` |
| `Settings.Connection.LocalComputer` | `Local Computer` | `Computador Local` |
| `Settings.Connection.Adapter` | `Adapter:` | `Adaptador:` |
| `Settings.Connection.Ipv4` | `IPv4:` | `IPv4:` |
| `Settings.Connection.DetectButton` | `Detect Again` | `Detectar novamente` |
| `Settings.Connection.TestButton` | `Test Connection` | `Testar conexão` |
| `Settings.Connection.TestSuccess` | `✓ Connected to OPNsense\nOPNsense: {0} | Authentication: OK | Latency: {1}ms` | `✓ Conectado ao OPNsense\nOPNsense: {0} | Autenticação: OK | Latência: {1}ms` |
| `Settings.Connection.TestAuthFailed` | `✗ Authentication failed — check API key and secret` | `✗ Falha de autenticação — verifique key e secret` |
| `Settings.Connection.TestUnreachable` | `✗ Cannot reach OPNsense at {0}` | `✗ Não foi possível alcançar OPNsense em {0}` |

### 6.3 Credentials tab

| Resource Key | en-US | pt-BR |
|---|---|---|
| `Settings.Credentials.ApiKey` | `API Key` | `API Key` |
| `Settings.Credentials.ApiSecret` | `API Secret` | `API Secret` |
| `Settings.Credentials.RevealSecret` | `Show` | `Mostrar` |
| `Settings.Credentials.HideSecret` | `Hide` | `Ocultar` |
| `Settings.Credentials.ImportFile` | `Import OPNsense API key file` | `Importar arquivo de chave da API` |
| `Settings.Credentials.SelectFileButton` | `Select file…` | `Selecionar arquivo…` |
| `Settings.Credentials.HowToCreate` | `How to create an API key` | `Como criar uma chave de API` |
| `Settings.Credentials.PasteHere` | `…or paste contents here` | `…ou cole o conteúdo aqui` |

### 6.4 Gateways tab

| Resource Key | en-US | pt-BR |
|---|---|---|
| `Settings.Gateways.Header.DisplayName` | `Display Name` | `Nome exibido` |
| `Settings.Gateways.Header.OpnsenseName` | `OPNsense Name` | `Nome no OPNsense` |
| `Settings.Gateways.Header.Status` | `Status` | `Status` |
| `Settings.Gateways.ResetButton` | `Reset to OPNsense name` | `Restaurar nome do OPNsense` |
| `Settings.Gateways.Placeholder` | `No gateways detected.` | `Nenhum gateway detectado.` |

### 6.5 Advanced tab

| Resource Key | en-US | pt-BR |
|---|---|---|
| `Settings.Advanced.EnableAdvancedDscp` | `Enable advanced DSCP configuration` | `Habilitar configuração avançada de DSCP` |
| `Settings.Advanced.ManagedDscpPool` | `Managed DSCP Pool` | `Pool de DSCP gerenciado` |
| `Settings.Advanced.LoggingLevel` | `Logging level` | `Nível de log` |
| `Settings.Advanced.LogRetention` | `Log retention (days)` | `Retenção de log (dias)` |
| `Settings.Advanced.TlsSettings` | `TLS certificate settings` | `Configurações de certificado TLS` |
| `Settings.Advanced.Language` | `Language` | `Idioma` |
| `Settings.Advanced.LanguageSystem` | `System` | `Sistema` |
| `Settings.Advanced.LanguageEnglish` | `English` | `Inglês` |
| `Settings.Advanced.LanguagePortuguese` | `Português (BR)` | `Português (BR)` |
| `Settings.Advanced.ShowTechnicalInfo` | `Show technical information` | `Mostrar informações técnicas` |
| `Settings.Advanced.MinimizeToTray` | `Minimize to tray when closing` | `Minimizar para bandeja ao fechar` |
| `Settings.Advanced.StartWithWindows` | `Start OptiRoute with Windows` | `Iniciar OptiRoute com o Windows` |

### 6.6 First-run wizard

| Resource Key | en-US | pt-BR |
|---|---|---|
| `Wizard.Welcome.Title` | `Welcome to OptiRoute` | `Bem-vindo ao OptiRoute` |
| `Wizard.Welcome.Body` | `Let's connect OptiRoute to your OPNsense firewall in a few steps.` | `Vamos conectar o OptiRoute ao seu firewall OPNsense em alguns passos.` |
| `Wizard.Step.Connection` | `Connection` | `Conexão` |
| `Wizard.Step.Credentials` | `Credentials` | `Credenciais` |
| `Wizard.Step.Computer` | `Computer` | `Computador` |
| `Wizard.Step.Gateways` | `Gateways` | `Gateways` |
| `Wizard.Step.Validation` | `Validation` | `Validação` |
| `Wizard.Step.Finish` | `Finish` | `Conclusão` |
| `Wizard.Button.Back` | `← Back` | `← Voltar` |
| `Wizard.Button.Next` | `Next →` | `Próximo →` |
| `Wizard.Button.Finish` | `Launch OptiRoute` | `Iniciar OptiRoute` |
| `Wizard.Validation.Required` | `OPNsense reachable` | `OPNsense acessível` |
| `Wizard.Validation.Credentials` | `Credentials valid` | `Credenciais válidas` |
| `Wizard.Validation.LocalIp` | `Local IPv4 detected` | `IPv4 local detectado` |
| `Wizard.Validation.Gateway` | `At least one usable gateway` | `Ao menos um gateway utilizável` |
| `Wizard.Validation.Persisted` | `Settings persisted successfully` | `Configurações salvas com sucesso` |

### 6.7 Common buttons

| Resource Key | en-US | pt-BR |
|---|---|---|
| `Common.Save` | `Save` | `Salvar` |
| `Common.Cancel` | `Cancel` | `Cancelar` |
| `Common.Close` | `Close` | `Fechar` |
| `Common.Loading` | `Loading…` | `Carregando…` |

---

## 7. Resource key naming convention

Pattern: `<Scope>.<Screen>.<Element>[.<Qualifier>]`

- **Scope:** `MainWindow`, `Settings`, `Card`, `InvalidTos`, `Wizard`, `Common`
- **Screen:** Tab or sub-form (`Connection`, `Credentials`, `Gateways`, `Advanced`)
- **Element:** UI element type (`Title`, `Button`, `Label`, `Header`, `Tooltip`, `Hint`, `Body`)
- **Qualifier:** optional, disambiguates same-named elements

Examples:
- `Settings.Connection.TestButton` → "Test Connection" button in Connection tab
- `Card.LocalOnly.RegisterButton` → "+ Register in OPNsense" button in LocalOnly card
- `Common.Cancel` → generic cancel button (reuse everywhere)

---

## 8. Validation & status messages (Fase 2)

These live in `MainViewModel.StatusMessage` and `_logger` calls. To be added in Fase 2
when the status bar is expanded:

| Resource Key | en-US | pt-BR |
|---|---|---|
| `Status.Idle` | `Ready.` | `Pronto.` |
| `Status.Syncing` | `Synchronizing…` | `Sincronizando…` |
| `Status.SyncProgress.Ready` | `Reading OPNsense rules…` | `Lendo regras do OPNsense…` |
| `Status.SyncProgress.Rules` | `Reading OPNsense rules…` | `Lendo regras do OPNsense…` |
| `Status.SyncProgress.Qos` | `Reading Windows QoS policies…` | `Lendo políticas QoS do Windows…` |
| `Status.SyncProgress.Done` | `Comparing configuration…` | `Comparando configuração…` |
| `Status.SyncComplete` | `Sync completed. {Sync} in sync, {Local} PC only, {Global} OPNsense only, {Conflict} out of sync.` | `Sincronização concluída. {Sync} em sincronia, {Local} apenas no PC, {Global} apenas no OPNsense, {Conflict} fora de sincronia.` |
| `Status.NothingToApply` | `Nothing to apply.` | `Nada a aplicar.` |
| `Status.Applying` | `Applying {0} change(s)…` | `Aplicando {0} reparo(s)…` |
| `Status.ApplySuccess` | `{0} change(s) applied successfully.` | `{0} reparo(s) aplicado(s) com sucesso.` |
| `Status.ApplyPartial` | `{0} applied, {1} failed — see log.` | `{0} aplicado(s), {1} falharam — ver log.` |
| `Status.Error` | `Error: {0}` | `Erro: {0}` |

---

## 9. Implementation order

1. **Create** `src/OptiRoute.App/Properties/Strings.resx` (en-US) with all rows above.
2. **Create** `src/OptiRoute.App/Properties/Strings.pt-BR.resx` mirroring keys.
3. **Wire** `Settings.Connection.Language` picker → `CultureInfo` propagation.
4. **Refactor** every hardcoded XAML string into `{x:Static p:Strings.X}` bindings.
5. **Refactor** every `MainViewModel.StatusMessage = "..."` into `Strings.X` access.
6. **Verify** by switching language in Settings → all visible text changes (re-launch app).

---

## 10. Acceptance criteria (Fase 0 done)

- [ ] Every XAML hardcoded user string replaced by `{x:Static p:Strings.X}`.
- [ ] Every `StatusMessage` and dialog label sourced from `.resx`.
- [ ] Both `Strings.resx` and `Strings.pt-BR.resx` contain identical keys.
- [ ] Language picker in Settings changes UI text after restart.
- [ ] No English string leaks into pt-BR UI and vice-versa.
- [ ] Log messages stay en-US regardless of UI locale.

---

## 11. String inventory — Add Application dialog

| Resource Key | Context | en-US | pt-BR |
|---|---|---|---|
| `AddApplication.Title` | Window title | `Add Application` | `Adicionar Aplicativo` |
| `AddApplication.ExecutableLabel` | Executable field | `Executable` | `Executável` |
| `AddApplication.DisplayNameLabel` | Display-name field | `Display name` | `Nome de exibição` |
| `AddApplication.BrowseButton` | File picker | `Browse...` | `Procurar...` |
| `AddApplication.RunningApps` | Running-processes header | `Currently running` | `Em execução` |
| `AddApplication.NoRunningApps` | Empty scan state | `(none detected)` | `(nenhum detectado)` |
| `AddApplication.RefreshButton` | Rescan button | `Refresh` | `Atualizar` |
| `AddApplication.OkButton` | Confirm button | `Add` | `Adicionar` |
| `AddApplication.CancelButton` | Cancel button | `Cancel` | `Cancelar` |
| `AddApplication.ExecutableRequired` | Validation message | `Executable is required` | `Executável é obrigatório` |
| `AddApplication.GatewayLabel` | Default gateway field | `Gateway` | `Gateway` |

## Recent copy changes

The resource files remain the authority and must keep identical keys in English and pt-BR.

| Key | Current change |
|---|---|
| `MainWindow.BrandName` | `?` changed to `?` in both locales. |
| `MainWindow.EmptyState.Subtitle` | Decorative `?` removed. |
| `Card.GlobalOnly.Subtitle` | Uses �executable� instead of �game�. |
| `Card.SyncStateBadge.*` | Decorative `?`, `?`, and `?` removed. |
| `AddApplication.*` | Added for the modal dialog, native picker, running-process list, gateway selection, and refresh actions. |

The current visual language uses neutral badges and avoids decorative symbols in user-facing status strings.

## 12. Final visual and connection copy

| Resource Key | en-US | pt-BR |
|---|---|---|
| `Settings.Connection.AllowInsecureTls` | `Allow self-signed certificates (insecure)` | `Permitir certificados autoassinados (inseguro)` |
| `Settings.Connection.AllowInsecureTlsTooltip` | `Disables TLS validation; enable only on a trusted network because of MITM risk.` | `Desativa a valida��o TLS; habilite apenas em rede confi�vel devido ao risco de MITM.` |

The header brand is `? OptiRoute`; badges are neutral and decorative symbols are omitted from status strings.
