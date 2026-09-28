# OptiRoute — Roadmap

> Documento vivo. Cada mudança implementada deve ser marcada aqui.
> Espelho operacional do prompt único acordado (`docs/prompt-unico.md` quando publicado).

---

## Legenda

- `[x]` — implementado e validado
- `[~]` — existe mas precisa ser conferido contra a nova spec
- `[ ]` — ainda não implementado
- `[?]` — depende de inventário pré-implementação

---

## 1. Estado atual (alto nível)

| Fase macro | Descrição | Status |
|---|---|---|
| 0 | `OptiRoute.Poc` (CLI) | `[x]` |
| 1 | Core + Windows + OPNsense (headless) | `[~]` |
| 2 | WPF App + Reconciliação 4-estados | `[~]` em curso |
| 3 | Sync automático + Repair | `[ ]` |
| 4 | Monitoramento de gateways | `[ ]` |
| 5 | Failover avançado | `[ ]` |

Esta rodada (= rodada 2.1 abaixo) cobre **somente FASE 1 → FASE 4** da nova especificação.
FASE 5 continua fora de escopo.

---

## 2. Inventário do código existente (pré-implementação)

Resultado da varredura em `src/` + `tests/`. Cada item está marcado como:

- **`[x]`** — conforme a spec nova, sem mudança necessária
- **`[delta N]`** — precisa de mudança pequena; ver §2.6 para a lista numerada de deltas
- **`[ ]`** — não existe; precisa ser criado
- **`[?]`** — depende de inspeção adicional

### 2.1 `OptiRoute.Core`

| Item | Arquivo | Status | Nota |
|---|---|---|---|
| `ApplicationSyncState` | `Models/ApplicationSyncState.cs:6` | `[x]` | enum com `Synchronized/LocalOnly/GlobalOnly/Conflict` exato |
| `ApplicationIdentity` | `Models/ApplicationIdentity.cs:11,26` | `[x]` | `AppId` (string em formato "D") + `Create(...,existingAppId)` preserva GUID entre PCs |
| `DscpRegistry` | `Services/DscpRegistry.cs:11` | `[x]` | pool, reservados, `Get/Allocate/Register/Validate/Synchronize` |
| `OptiRouteRuleDescriptor` | `Models/OptiRouteRuleDescriptor.cs:22` | `[x]` | `|v=1` emitido em `FormatDescription`; `TryParse` aceita v=1 e versões desconhecidas são rejeitadas |
| `RuleOrderManager` | `Services/RuleOrderManager.cs:26` | `[x]` | OVERRIDE > DEFAULT > âncora, idempotente, não toca regra do usuário |
| `IOptiRouteSynchronizer` | `Interfaces/IOptiRouteSynchronizer.cs:75` | `[x]` | agora expõe `BuildPlanAsync / ApplyPlanAsync / VerifyAsync` (Delta 2) |
| `ReconciliationActionType` | `Models/ReconciliationActionType.cs` (novo) | `[x]` | enum canônico: NoOp + 3 QoS + 4 firewall (Delta 2) |
| `ReconciliationPlan` | `Models/ReconciliationPlan.cs` | `[x]` | `Plan / Action / Result / Failure` com 9 campos estruturados (Delta 2) |
| `OptiRouteSynchronizer` | `Services/OptiRouteSynchronizer.cs:328` | `[x]` | `RegisterOrUpdateApplicationAsync` adota AppId existente do OPNsense (Delta 4) |
| `IHostOverrideManager` | `Interfaces/IHostOverrideManager.cs` | `[x]` | `Set/Remove/Get` por (exe, hostIp) |
| `HostOverride` | `Models/HostOverride.cs:9` | `[x]` | `Identity`, `Dscp`, `SourceIp`, `Gateway`, `RuleUuid` |
| Demais models | `Models/*` | `[x]` | OK |

### 2.2 `OptiRoute.OPNsense`

| Item | Arquivo | Status | Nota |
|---|---|---|---|
| `IOpnsenseClient` | (em Core) | `[x]` | OK |
| `OpnsenseClient` | `Client/OpnsenseClient.cs:144` | `[x]` | `searchRule?rowCount=500` em uma única chamada |
| `OpnsenseHttpClientFactory` | `Client/OpnsenseHttpClientFactory.cs:74` | `[x]` | `Timeout = TimeSpan.FromSeconds(30)` já configurado |
| ToS hex | `OpnsenseClient.cs:221-222` | `[x]` | `0x{tosByte:X2}` correto |

### 2.3 `OptiRoute.Windows`

| Item | Arquivo | Status | Nota |
|---|---|---|---|
| `WindowsQosManager` | `QoS/WindowsQosManager.cs:150` | `[x]` | `Get-NetQosPolicy` em uma chamada em lote |
| `SecretStore` | `Security/SecretStore.cs` | `[x]` | DPAPI |
| `OPNsenseKeyFileParser` | `Security/OPNsenseKeyFileParser.cs` | `[x]` | parser do `apikey.txt` |
| PowerShell exec | `WindowsQosManager.cs:235` | `[x]` | `.ps1` temp aleatório (sem Runspace, conforme decisão travada) |
| Cleanup órfão `.ps1` | — | `[delta 6]` | ver §2.6 |

### 2.4 `OptiRoute.App`

| Item | Arquivo | Status | Nota |
|---|---|---|---|
| `MainWindow.xaml` | (XAML inspecionado) | `[x]` | segundo botão aplicado + per-card removidos p/ GlobalOnly/Conflict (opção C) |
| `MainViewModel` | `ViewModels/MainViewModel.cs:73-90` | `[x]` | `LatestPlan` + `ApplyRepairCommand` adicionados; `SyncAsync` popula plano |
| `AppItemViewModel` | `ViewModels/AppItemViewModel.cs` | `[?]` | precisa conferir se renderiza os 4 estados com ações |
| `LocalNetworkDetector` | `Services/LocalNetworkDetector.cs` | `[x]` | detecta IP local |
| `AppConfigManager` | `Services/AppConfigManager.cs` | `[x]` | persistência de config |

### 2.5 Testes

| Item | Status | Nota |
|---|---|---|
| `OptiRouteRuleDescriptorTests` | `[x]` após delta 1 | roundtrip parse/format `v=1` |
| `DscpRegistryTests` | `[?]` | rodar a suíte para confirmar |
| `HostOverrideManagerTests` | `[?]` | rodar a suíte para confirmar |
| `OptiRouteSynchronizerTests` | `[?]` | rodar Cenário A–F |
| `RuleOrderManagerTests` | `[?]` | smoke test real |
| `WindowsQosManagerTests` | `[?]` | leitura em lote já é única chamada |
| `OPNsenseKeyFileParserTests` | `[x]` | parser |
| **Novo:** `ReconciliationPlanTests` | `[ ]` | idempotência — depende do delta 2 |
| **Novo:** smoke `.ps1` órfão | `[ ]` | depende do delta 6 |

### 2.6 Deltas abertos (numerados para referência cruzada)

| # | Descrição | Arquivo(s) | Severidade |
|---|---|---|---|
| 1 | ✅ **FEITO (2026-09-27)** Adicionar `|v=1` no `FormatDescription` + aceitar em `TryParse` (recusar versões desconhecidas) | `OptiRouteRuleDescriptor.cs` + 5 testes novos | média |
| 2 | ✅ **FEITO (2026-09-27)** Tipos canônicos no Core + pipeline BuildState→BuildPlan→ApplyPlan→Verify. `BuildPlanAsync` produz `ReconciliationAction` com enum `ReconciliationActionType` + 9 campos estruturados; `ApplyPlanAsync` mapeia para `IWindowsQosManager`; `VerifyAsync` re-lê estado e valida pós-condições. **18 testes novos** em `OptiRoutePlanTests.cs` | `Core/Models/ReconciliationActionType.cs` (novo) + `Core/Models/ReconciliationPlan.cs` (reescrito) + `Core/Services/OptiRouteSynchronizer.cs` + `Core/Interfaces/IOptiRouteSynchronizer.cs` | alta |
| 3 | ✅ **FEITO (2026-09-27)** UI: separação completa. `ApplyRepairCommand` + segundo botão. Opção C aplicada: per-card buttons removidos para GlobalOnly/Conflict; **LocalOnly mantém Registrar/Remover** (escolha de gateway alvo). `LatestPlan` populado por `SyncAsync`. Hint textual adicionado nos cards sem botão | `MainViewModel.cs`, `MainWindow.xaml`, novo `Core/Models/ReconciliationPlan.cs` | alta |
| 4 | ✅ **FEITO (2026-09-27)** Multi-PC dedup em `RegisterOrUpdateApplicationAsync`: antes de criar, consulta OPNsense. Se já existe regra DEFAULT para o mesmo `Executable` com `AppId` válido, **adota** esse AppId em vez de criar GUID novo. Garante que `EnsureRuleExists` encontra a regra existente e faz UPDATE. Post-dedup safety net continua ativo para race conditions. **4 testes novos** em `OptiRouteSynchronizerTests.cs` | `OptiRouteSynchronizer.cs:328-355` | média |
| 5 | ✅ **FEITO (2026-09-28)** Adicionar `IProgress<SyncProgress>` em `SyncAsync` + propagar para UI (ProgressBar no footer) | `OptiRouteCore/Models/SyncProgress.cs` (novo), `IOptiRouteSynchronizer.cs`, `OptiRouteSynchronizer.cs`, `MainViewModel.cs`, `MainWindow.xaml` | baixa |
| 6 | ✅ **FEITO (2026-09-28)** Cleanup de `.ps1` órfão em `%TEMP%` (idade > 10 min) na inicialização do `WindowsQosManager` | `WindowsQosManager.cs` + 2 testes em `WindowsQosManagerTests.cs` | baixa |
| 7 | ✅ **FEITO (2026-09-28)** Alerta se `searchRule.total > rows.Length` (paginação silenciosa) — log warning | `OpnsenseClient.cs::ListAllRulesAsync` | baixa |
| 8 | ~~verificar timeout no `OpnsenseHttpClientFactory`~~ ✅ já tem `Timeout = 30s` | — | resolvido |

**Saída do inventário:** quando todos os `[?]` rodarem OK e os deltas 2-7 forem fechados, as Fases 1-4 do prompt único estão concluídas.

### 2.7 Decisões em aberto (criadas nesta rodada)

| # | Decisão | Opções | Recomendação |
|---|---|---|---|
| D1 | ✅ **RESOLVIDO (2026-09-27)** Opção **C** escolhida: per-card buttons removidos para GlobalOnly ("Ativar") e Conflict ("Corrigir DSCP"); **mantidos para LocalOnly** ("Registrar no OPNsense" + "Remover do Windows") porque promoção precisa de escolha de gateway alvo que o plano atual ainda não carrega. Hint textual adicionado nos cards órfãos indicando o botão Aplicar reparo. | — | — |

---

## 3. A fazer nesta rodada

Mapeado 1:1 com o prompt único. Cada item é um entregável verificável.

### 3.1 FASE 1 — Consistência (`[ ]`)

Objetivo: o sistema consegue construir a lista de apps nos 4 estados **sem mutar nada**.

- `[?]` Validar `DscpRegistry` contra invariante 2.1 (unicidade bidirecional Exe↔DSCP)
- `[?]` Validar `OptiRouteRuleDescriptor` contra formato `OPTIROUTE|DEFAULT|<exe>|<dscp>|<appId>|v=1`
- `[?]` Garantir que `ApplicationIdentity.AppId` é Guid e é a chave primária lógica
- `[?]` Atualizar `IOptiRouteSynchronizer` se faltar método para devolver lista de estados (read-only)
- `[?]` Smoke test real do `RuleOrderManager` em ambiente com pelo menos uma regra de teste + uma âncora do usuário

**Aceite:** roda a suíte de testes; **todos Synchronized / LocalOnly / GlobalOnly / Conflict** são detectados por leitura pura, sem mutar OPNsense nem Windows QoS.

### 3.2 FASE 2 — Reconciliação idempotente (`[ ]`)

Objetivo: `Sincronizar` lê + dif; `Aplicar reparo` aplica só o necessário; rodar de novo não muda nada.

- `[ ]` Criar `ReconciliationActionType` enum (`NoOp, CreateQos, UpdateQos, DeleteLocalQos, CreateRule, UpdateRule, MoveRule, DeleteOptiRouteRule`)
- `[ ]` Criar `ReconciliationPlan` (record com `IReadOnlyList<ReconciliationAction>`)
- `[ ]` Implementar `BuildPlan()` determinístico (mesma entrada → mesmo plano)
- `[ ]` Implementar `ApplyPlanAsync(ct)` que valida unicidade **antes** de cada mutação
- `[ ]` Implementar `VerifyAsync()` que releio OPNsense após mutações
- `[ ]` Concorrência multi-PC: ao promover `LocalOnly → Global`, **buscar por Executable** no OPNsense antes de criar novo `AppId`; se existir, **adotar** o AppId existente

**Aceite:** Cenários A–F do prompt único passam; rodar `Sincronizar` 2× seguidas resulta em 0 mutações no segundo round.

### 3.3 FASE 3 — Performance básica (`[ ]`)

Sem virtualização, sem cache complexo, sem circuit breaker.

- `[ ]` `WindowsQosManager` lê todas as policies em **uma** chamada (`Get-NetQosPolicy -PolicyStore ActiveStore`)
- `[ ]` `OpnsenseClient` lê todas as regras OptiRoute em **uma** chamada (`searchRule` com `searchPhrase="OPTIROUTE"`)
- `[ ]` Todo o pipeline (`BuildState → BuildPlan → ApplyPlan → Verify`) em `async/await` com `CancellationToken`
- `[ ]` UI não bloqueia durante sync (`IProgress<SyncProgress>` no VM)
- `[ ]` Manter estratégia `.ps1` temp (sem Runspace nesta rodada)

**Aceite:** UI continua responsiva durante sincronização; cancelar funciona; número de chamadas OPNsense e PowerShell não cresce linearmente por app.

### 3.4 FASE 4 — Resiliência mínima (`[ ]`)

- `[ ]` Timeout configurável em todas as chamadas ao OPNsense (sugestão: 10 s)
- `[ ]` Retry simples: até 2–3 tentativas, backoff curto (200 ms / 600 ms)
- `[ ]` Logs estruturados (campos fixos: `op`, `appId`, `executable`, `dscp`, `sourceIp`, `ruleUuid`, `duration`, `result`)
- `[ ]` **Nunca** logar `API Secret`
- `[ ]` Smoke test: `.ps1` órfão em `%TEMP%` (> N minutos) é detectado e limpo no próximo Sync
- `[ ]` Estado inicial vazio: lista vazia com CTA "Registrar primeiro aplicativo"

**Aceite:** OPNsense offline não congela a aplicação; UI mostra erro compreensível; logs não contêm segredo.

---

## 4. UX obrigatória (corte transversal)

- `[ ]` Botão **`[ Sincronizar ]`** — read-only. Constroi plano, nunca muta.
- `[ ]` Botão **`[ Aplicar reparo ]`** — só habilitado se `plan.Actions` contém algo ≠ `NoOp`.
- `[ ]` Card **Synchronized** — exibir DSCP, Default Gateway, Effective Route; permitir trocar Default / criar/remover override.
- `[ ]` Card **LocalOnly** — `[Registrar globalmente]` / `[Excluir deste Windows]`.
- `[ ]` Card **GlobalOnly** — `[Ativar neste PC]`.
- `[ ]` Card **Conflict** — `[Corrigir para DSCP Global]` (global vence).
- `[ ]` Estado vazio inicial — CTA para cadastrar.

---

## 5. Não-objetivos desta rodada (travados)

Reforçando a trava contra overengineering:

```
- Runspace PowerShell persistente
- Circuit breaker sofisticado
- Cache complexo de regras/QoS
- Monitoramento realtime agressivo de gateways
- Failover automático
- Health-check avançado
- Limpeza automática de states do firewall
- WFP / WinDivert / proxy / VPN / packet injection
- Virtualização avançada de cards na UI
- Servidor central / Windows Service
- Migração v2 do descriptor (apenas preparar estrutura ParseV1/ParseV2)
- Refatoração ampla sem necessidade
```

---

## 6. Ordem de execução (sequência real)

```
1. [ ] Inventário pré-implementação (§2) — saída: lista de deltas pequenos
2. [ ] Implementar Fase 1 (§3.1)
3. [ ] Rodar suíte completa de testes
4. [ ] Implementar Fase 2 (§3.2)
5. [ ] Rodar Cenários A–F (§3.2 aceite)
6. [ ] Implementar Fase 3 (§3.3)
7. [ ] Implementar Fase 4 (§3.4)
8. [ ] Aplicar UX (§4)
9. [ ] Validação final: rodar Sync 3×, certificar 0 mutações no 2º e 3º round
```

---

## 7. Definição de pronto da rodada

Esta rodada está pronta **somente** quando:

- [x] Cenário A (Registrar app novo → GlobalOnly→Synchronized) passa — 2026-09-28
- [x] Cenário B (Conflito DSCP → Aplicar reparo) passa — 2026-09-28
- [x] Cenário C (Local órfão → invariante nunca auto-delete) passa — 2026-09-28
- [x] Cenário D (Global não aplicado → Ativar neste PC) passa — 2026-09-28
- [ ] Cenário E (Multi-PC dedup / 2 PCs registram mesmo exe → mesmo AppId) **DEFERRED** — exige 2 máquinas ou simulação multi-user; empurrado para pós-Fase 3
- [ ] Cenário F (Override por IP coexistindo com Default) — pendente
- [ ] Smoke F (Sync idempotente: 0 mutações no 2º round) — pendente
- [ ] Concorrência multi-PC: dois PCs registrando o mesmo `.exe` não geram duplicatas
- [ ] Logs estruturados não vazam `API Secret` em nenhuma hipótese
- [ ] Smoke test do `.ps1` órfão passa
- [ ] UX: dois botões separados conforme §4

---

## 8. Smoke test (compromisso pré-Fase 3)

Antes de avançar para UI avançada, executar **smoke test manual interativo** do App contra OPNsense real + Windows QoS real.

- **Procedimento:** `docs/smoke-test-checklist.md` — 6 cenários A-F, build single-file, limpeza, o que capturar, modos de falha
- **Por que manual e não automatizado:** a esta altura do projeto, o gargalo não é cobertura — é descobrir bugs de interação UI + comportamentos reais do Windows QoS que mocks não reproduzem (PolicyStore `ActiveStore`, refresh de NetworkCondition, permissões)
- **Quem executa:** o usuário (este projeto é single-user)
- **Quando:** antes de começar a Fase 3 do prompt único

### Status do smoke

| Data | Executor | Cenário | Resultado | Notas |
|------|----------|---------|-----------|-------|
| 2026-09-28 | usuário | A: Registrar app novo | ✅ passou | Duas correções: (1) FileLogger sink para diagnóstico; (2) `addRule` esperava UUID da categoria, não nome — fix em `EnsureCategoryExistsAsync` retornando UUID + `EnsureRuleExistsAsync` traduzindo nome→UUID antes do payload. Ver log: `OptiRoute.log` linhas 5-10 confirmam pipeline completo end-to-end |
| 2026-09-28 | usuário | B: Conflito DSCP | ✅ passou | App detectou Conflict=1 após usuário forçar DSCP divergente via Set-NetQosPolicy (provavelmente store diferente de ActiveStore); "Aplicar reparo" disparou pipeline DeletePolicy+CreatePolicy com DSCP=33; re-sync → Sync=1. Bug cosmético nos logs (nome com capital mas Windows cria lowercase) registrado para pós-Fase 3. Ver log linhas 3,4,9 |
| 2026-09-28 | usuário | C: Local órfão (não auto-delete) | ✅ passou | 3 fixes: `ListOptiRoutePoliciesAsync`/`ListLocalPoliciesAsync`/`CreatePolicyAsync` agora usam `-PolicyStore ActiveStore` consistentemente. `DeletePolicyAsync(string)` + `DeletePolicyAsync(LocalQosPolicy)` ganharam verify-after-remove (PowerShell falha silenciosa em GPO-managed). GPO store fallback implementado para cobrir `Owner="Group Policy (Machine)"` |
| 2026-09-28 | usuário | D: Global não aplicado | ✅ passou | App detectou GlobalOnly=1 corretamente sem auto-criar local. "Aplicar reparo" → `Activate Global On Local` criou `optiroute-discord` DSCP=26. Log: linhas 21-26 mostram fluxo completo. Heuristic: OPNsense UI tem campos DSCP e TOS separados que se sobrescrevem (user digitou 0x68 no campo tos → UI converteu pra 0x05) |
| —    | —        | E: Multi-PC dedup | **deferred** | Exige 2 máquinas físicas/VMs. Empurrado pra pós-Fase 3. Implementação do delta 4 já está no código (`RegisterOrUpdateApplicationAsync` adota AppId existente) — falta apenas validar contra infra real |
| —    | —        | F: Ordem das regras | pendente | Verificar `OVERRIDE > DEFAULT > LAN` precedence após múltiplas adições/edições |



## 8.1. Open Bugs / Future Fixes (registrados durante smoke test)

Bugs **não-bloqueantes** encontrados durante validação Cenário B/C/D. Acompanham pra resolver antes da Fase 3.

| # | Severidade | Descrição | Workaround atual | Fix sugerido |
|---|---|---|---|---|
| BUG-1 | baixa (cosmético) | Log do `WindowsQosManager` mostra `OptiRoute-bf6` (capital O, R) mas o nome real da policy no Windows é `optiroute-bf6` (lowercase) — Windows PowerShell normaliza pra lowercase internamente | Logs só são confusing pro dev, usuário final não vê | `BuildPolicyName()` deve retornar lowercase consistentemente, ou capturar o nome real após `New-NetQosPolicy` e usar esse no log |
| BUG-2 | média (UX) | OPNsense UI tem campos `DSCP` e `tos` separados que se sobrescrevem silenciosamente. User digitou `0x68` no campo `tos` mas UI converteu pra `0x05` (DSCP=1) | App detectou via defesa tos (`description DSCP=26 ≠ tos real=0x05`) e ofereceu modal "Adotar tos como verdade" — funcionou | Documentar em `opnsense-setup.md` que user deve usar o campo **DSCP** (=26), não **tos** (=0x68), pois OPNsense deriva um do outro mas pode haver diferença |
| BUG-3 | baixa (resolvido em publish2) | `CreatePolicyAsync` rodava `New-NetQosPolicy` SEM `-PolicyStore ActiveStore` → gravava em default store, virando GPO-managed (Owner = "Group Policy (Machine)") e removível só via gpedit.msc ou `GPO:$env:COMPUTERNAME` | User usou `-PolicyStore "GPO:$env:COMPUTERNAME"` no PowerShell; App após fix usa ActiveStore explicitamente | **Aplicado:** `-PolicyStore ActiveStore` em `CreatePolicyAsync` (e deletes) — policies novas vêm com Owner = "PowerShell / WMI" removíveis |
| BUG-4 | baixa (resolvido em publish2) | `DeletePolicyAsync(string)` e `DeletePolicyAsync(LocalQosPolicy)` não tentavam fallback pro GPO store | User escolheu gpedit.msc manualmente | **Aplicado:** `TryDeletePolicyFromStoreAsync` helper com fallback chain ActiveStore → GPO:$env:COMPUTERNAME. Mensagem de erro final menciona PS + gpedit |
| BUG-5 | média (delta futuro) | `ActualTosHex` em `EffectiveApplicationRoute` agora é populado, mas o modal ainda pode exibir "0x00" se SyncAsync rodar antes da propagação completa | Modal mostra valor aproximado, escolha ainda funciona | Garantir ordem de propagação em `SyncAsync` antes de construir routes; ou adicionar teste explícito |
| BUG-6 | baixa (WIP) | Cenário E (multi-PC dedup) deferred — código pronto mas falta validação | Implementação delta 4 (`RegisterOrUpdateApplicationAsync` adota AppId) já cobre o caso | Validar com 2 PCs ou simulação (criar rule com description `OPTIROUTE|DEFAULT\|<exe>\|<dscp>\|<guid-X>\|v=1` antes do sync, ver se App adota guid-X) |



## 9. Diário de bordo

```
2026-09-27 — [INVENTÁRIO] varredura de src/ + tests/ cruzando contra o prompt único.
              Saída: 7 deltas abertos (delta 8 já OK com Timeout=30s no factory).
              Arquivos lidos: 16 (Core/Models, Core/Services, Core/Interfaces,
              OPNsense/Client, Windows/QoS, App/ViewModels, App/MainWindow).

2026-09-27 — [DELTA 1 ✅] OptiRouteRuleDescriptor: marcador `|v=1` emitido em
              FormatDescription; TryParse aceita o sufixo e recusa versões
              desconhecidas (v=2, v=3…). Roundtrip preservado.
              Testes: 6 novos (Format+AppId sem v=1, Parse com v=1+AppId,
              Parse com v=1 sem AppId, v=2 rejeitado, roundtrip) + 2 atualizados
              para esperar o sufixo. Doc comment OPNsenseModels atualizado.
              Files: OptiRouteRuleDescriptor.cs, OptiRouteRuleDescriptorTests.cs,
              HostOverrideManagerTests.cs (assertion ajustada), OPNsenseModels.cs.
              Resultado: 28/28 testes do Core passam.

2026-09-27 — [DELTA 3 ✅ parcial] Separação dos botões UI:
              - Core/Models/ReconciliationPlan.cs novo (placeholder para Delta 2):
                ReconciliationPlan { Actions[], HasActions, ActionCount } + ReconciliationAction(Type, Executable, Description).
              - MainViewModel.cs: propriedade LatestPlan + HasPendingRepair + PendingRepairCount +
                ApplyRepairCommand (canExecute = HasPendingRepair). SyncAsync agora popula
                LatestPlan via BuildPlanFromResult() no fim. StatusMessage inclui contagem
                de pendentes.
              - MainWindow.xaml: segundo botão "✓ Aplicar reparo" ao lado de "↻ Sincronizar",
                IsEnabled ligado a HasPendingRepair, ToolTip explica a semântica.
              - ApplyRepairAsync itera plano e chama RepairConflictAsync/ActivateGlobalOnLocalAsync.
                PromoteLocal fica em log warning (precisa de gateway alvo, plano atual não carrega).
              Build limpo (0 erros, 0 warnings). Testes: 28/28 OK.
              DECISÃO PENDENTE: per-card action buttons (Registrar/Remover/Ativar/Corrigir)
              continuam coexistindo com "Aplicar reparo" — viola "single source of mutation"
              da spec. Ver §2.7 (D1) para opções.

2026-09-27 — [D1 → C] Per-card buttons removidos para GlobalOnly e Conflict; LocalOnly
              mantém Registrar/Remover (gateway alvo necessário). Hint textual adicionado
              nos cards órfãos. Botão ✕ de exclusão por app mantido (decisão fora do escopo D1).
              Relays no VM (ActivateGlobalCommand, RepairConflictCommand) ficam orfãos do XAML
              mas presentes no VM — código morto intencional até o delta 2 consolidar tudo
              em BuildPlan/ApplyPlan. Build limpo, testes 28/28 OK.

2026-09-27 — [DELTA 2 ✅] Pipeline BuildState→BuildPlan→ApplyPlan→Verify no Core.
              - Models/ReconciliationActionType.cs NOVO: enum (NoOp, CreateQosPolicy,
                UpdateQosPolicy, DeleteLocalQosPolicy, CreateFirewallRule, UpdateFirewallRule,
                MoveFirewallRule, DeleteFirewallRule).
              - Models/ReconciliationPlan.cs REESCRITO: 3 records no arquivo.
                ReconciliationAction agora tem 9 campos estruturados (Type enum, Executable,
                AppId, SourceIp?, Dscp, TargetGateway, RuleUuid?, LocalQosPolicyName?, Description).
                Adicionados ReconciliationResult(Verified, Failures) + ReconciliationActionFailure.
              - IOptiRouteSynchronizer.cs: 3 métodos novos no pipeline
                (BuildPlanAsync, ApplyPlanAsync, VerifyAsync) com doc comments completos.
              - OptiRouteSynchronizer.cs: implementações:
                * BuildPlanAsync: switch sobre SyncState emitindo UpdateQosPolicy/CreateQosPolicy/
                  DeleteLocalQosPolicy. LocalOnly conservador (D1). Sem side-effects, idempotente.
                * ApplyPlanAsync: switch por enum mapeando para IWindowsQosManager.
                  Firewall types vão para Failures com warning "ainda não emitido".
                  EnsureRelativeOrderAsync chamado apenas se houve firewall action.
                  try/catch por ação → Failures preserva ex.Message.
                * VerifyAsync: chama SyncAsync(localHostIp) e valida pós-condições
                  (UpdateQoS: route.LocalDscp == action.Dscp; DeleteLocal: route==null||LocalDscp==null).
              - MainViewModel.cs: reconcilia com novo tipo (switch no enum).
              - Tests: 18 novos em OptiRoutePlanTests.cs (6 BuildPlan, 6 ApplyPlan, 4 Verify,
                2 idempotência/edge cases). Resultado: 46/46 OK.

2026-09-27 — [DELTA 4 ✅] Multi-PC dedup em RegisterOrUpdateApplicationAsync.
              - OptiRouteSynchronizer.cs: ANTES de alocar DSCP/criar regra, consulta OPNsense.
                Se existe regra DEFAULT para o mesmo Executable com AppId válido, ADOTA
                esse AppId. EnsureRuleExists encontra a regra existente (mesma description
                após adoption) e faz UPDATE em vez de CREATE. Post-dedup safety net
                continua para race conditions verdadeiras.
              - Tests: 4 novos em OptiRouteSynchronizerTests.cs (adoption, sem regra existente,
                regra legada sem AppId, mesmo AppId). Resultado: 50/50 OK.

2026-09-28 — [DELTA 5 ✅] IProgress<SyncProgress>:
              - Novo `Core/Models/SyncProgress.cs` (record Stage/Percent/Message).
              - `IOptiRouteSynchronizer.SyncAsync` agora aceita `IProgress<SyncProgress>?`.
              - SyncAsync reporta 5 etapas: init(5)→rules(25)→merge(45)→qos(70)→done(100).
              - MainViewModel: novo `ProgressPercent` + Progress<SyncProgress> que atualiza
                StatusMessage via UI thread automaticamente.
              - MainWindow.xaml: ProgressBar no footer, visibilidade ligada a IsLoading.
              - VerifyAsync consertado para usar `progress: null` na nova assinatura.
              Build limpo, 50/50 Core tests OK.

2026-09-28 — [DELTA 6 ✅] Cleanup .ps1 órfão:
              - `WindowsQosManager.CleanupOrphanedScripts` estático: varre %TEMP% por
                `optiroute_*.ps1` com `LastWriteTimeUtc < (now - 10min)` e remove.
                Todos os parâmetros opcionais para testes determinísticos.
              - Chamado no construtor do manager (best-effort, swallow + log em falha).
              - 2 testes novos: RemoveOnlyOldMatchingFiles + NonExistentDirectoryReturnsZero.
              - Resultado Windows tests: 18/20 OK (2 falhas = testes [Trait(Integration)]
                pré-existentes que requerem Admin + Windows real, não regressões).

2026-09-28 — [DELTA 7 ✅] Alerta de paginação silenciosa:
              - `OpnsenseClient.ListAllRulesAsync`: após parsear `SearchRuleResponse`,
                checa `payload.Total > rows.Count` e loga warning explícito citando
                rowCount e o delta futuro que implementaria paginação.
              - Sem alteração de assinatura — interno.
              - Não exposto à UI nesta rodada (delta 5 já dá feedback de progresso).
              Build limpo.

2026-09-28 — [SMOKE DOC] Decisão: smoke será **manual interativo** no App WPF contra
              OPNsense real + Windows QoS real (não automatizado com mocks nesta fase).
              Documentado em `docs/smoke-test-checklist.md`:
              - Pré-requisitos (OPNsense + Admin + 2 gateways)
              - Build single-file (`dotnet publish -c Release -r win-x64 --self-contained
                -p:PublishSingleFile=true`)
              - Procedimento de limpeza antes/depois
              - 6 cenários A-F: Registrar novo, Conflito DSCP, Local órfão (não
                auto-delete), Global não aplicado, Multi-PC dedup, Ordem das regras
              - Modos de falha comuns mapeados a sintomas
              - Logs e diagnóstico (console, log file, OPNsense logs, Get-NetQosPolicy)
              - Critério de aprovação
              - ROADMAP §8 (Smoke) e §9 (Diário) com tabela de status por cenário.

**ESTADO GERAL: todos os 8 deltas do inventário fechados (1, 2, 3, 4, 5, 6, 7, 8).**
Próximo marco comprometido: smoke test E2E manual (rodar 6 cenários A-F) antes da Fase 3 do prompt.

2026-09-28 — [BUGFIX-1: log sink] App.xaml.cs usava NullLogger em TODOS os componentes —
              logs iam para o void. Criei `src/OptiRoute.App/Logging/FileLogger.cs`
              (provider + logger, sem dep extra, lock compartilhado, swallow de falhas),
              caminho padrão `%APPDATA%\OptiRoute\OptiRoute.log`. Trunca a cada startup
              com linha "session started". Build limpo, publish em `publish2/`.

2026-09-28 — [BUGFIX-2: addRule categories] OPNsense `<categories>` é `ModelRelationField` —
              validado por UUID na escrita, não por nome (display=name só na leitura).
              Hipótese inicial errada: achei que categoria não existia. Após FileLogger
              expor o payload, vi `"categories":"OptiRoute"` (nome) → `Related category not found`.
              Fix: `EnsureCategoryExistsAsync` agora retorna `Task<string?>` (UUID),
              `HashSet<string>` virou `Dictionary<string,string>` (nome→uuid), e
              `EnsureRuleExistsAsync` muta `request.Category = uuid` antes do BuildRulePayload.
              Source: `src/opnsense/mvc/app/models/OPNsense/Firewall/Filter.xml`.
              Cenário A validado end-to-end via log do usuário.
```

---

## 10. GitHub-readiness — Fases 0 a 4

Plano pós-smoke-test (2026-09-28). Antes de divulgação pública no
`https://github.com/gabrielcorreabsb/OptiRoute`, o App precisa passar por 5 fases.

### Fase 0 — Copy/UX contract ✅ FEITO (2026-09-28)

**Saída:** `docs/ux-copy.md` com ~70 strings catalogadas em en-US + pt-BR.

**Decisões:**
- `Gateway` (não "Network" nem "WAN")
- `Refresh` (não "Sync")
- `Out of sync` (não "Conflict")
- `This PC only` / `Available in OPNsense` (não "LocalOnly"/"GlobalOnly")
- TOS visível só no dialog InvalidTos, escondido em runtime
- i18n via `.resx` (en-US fallback + pt-BR) desde o início
- Resource keys: `<Scope>.<Screen>.<Element>[.<Qualifier>]`

### Fase 1 — Settings UI + First Run Wizard + Language picker

Bloqueador de release. Sem isso, GitHub release é tóxico (user tem que editar JSON).

**Estrutura:**
- **Connection**: Host + Detect Local IP + Test Connection (autenticação real via `/api/firewall/filter/searchRule?rowCount=1`, **não** `/api/core/firmware/info` que é público)
- **Credentials**: paste **ou** Import `.txt` file (`OPNsenseKeyFileParser` já existe)
- **Gateways**: lista `Display name | OPNsense name | Reset` (sem Description — usuário OPNsense é avançado)
- **Advanced**: DSCP pool (atrás de toggle "Show advanced DSCP configuration"), log level, log retention, TLS, Language picker
- **First-run Wizard**: Welcome → Connection → Credentials → Computer → Gateways → Validation → Finish
  - Finish só habilita com 4 ✓: OPNsense reachable + credentials valid + local IPv4 detected + at least one gateway
- **Persistencia**: `config.json` + DPAPI creds separados (nunca misturar)
- **schemaVersion: 1** no config.json + write transacional (.tmp + rename)

### Fase 2 — UX diária (uso diário)

- **Status bar expandida**: OPNsense: Connected | Last sync: 23:42 | Issues: 0; erros com [Details] [Copy], sem stack trace na tela principal
- **Loading textual por stage**: "Reading OPNsense rules" → "Reading Windows QoS" → "Comparing configuration" (já temos `IProgress<SyncProgress>` da delta 5)
- **Empty states distintos**:
  - "No applications yet" (CTA: Add Application)
  - "OptiRoute isn't connected yet" (CTA: Open Settings)

### Fase 3 — Release ready

- **Diagnostics Export** (com sanitização de Key/Secret/tokens) — obrigatório
- **About dialog**: versão, licença, link docs, link GitHub
- **Tray icon opcional** (OFF por default em Settings)
- **Tutorial overlay** = P1 opcional, não bloqueador

### Fase 4 — Release Hygiene (repo)

- `LICENSE` (MIT), `SECURITY.md`, `CHANGELOG.md`
- `docs/installation.md` (novo), `docs/troubleshooting.md` (novo)
- GitHub Actions CI: `restore → build → test → publish`
- Screenshots no README
- `.gitignore` ✅ já feito (2026-09-28)

### Critério final (gold standard)

**Smoke test em Windows limpo:**

```
baixar OptiRoute → abrir → configurar OPNsense → importar Key/Secret →
detectar PC → nomear gateways → adicionar .exe → selecionar WAN →
tráfego sair pela WAN correta
```

**sem editar JSON, sem PowerShell manual, sem UI OPNsense além de criar API key.**

### Ordem de execução

```
Fase 0 ✅ Copy/UX contract (i18n-aware)
  ↓
Refator: extrair strings atuais para .resx  [1-2h, novo round]
  ↓
Fase 1 — Settings + First Run + Language picker  [bloqueador]
  ↓
Smoke test E (multi-PC dedup, agora com config UI)
  ↓
Fase 2 — Daily UX polish
  ↓
Fase 3 — Diagnostics/About/Tray
  ↓
Fase 4 — Release Hygiene
  ↓
Public Preview no GitHub
```
