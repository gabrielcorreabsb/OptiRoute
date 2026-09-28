# Smoke Test Manual — OptiRoute v2.0

Teste interativo do App WPF contra OPNsense real + Windows QoS real.
**Não automatizado** — clicar e observar.

---

## 1. Pré-requisitos

- **OPNsense real** acessível (LAN do Windows host): URL base + API key + API secret
- **Windows 10/11** com privilégios de Administrador
- **.NET 10 SDK** instalado (`dotnet --version` → 10.0.x)
- **2 gateways** configurados no OPNsense (ex: `WAN1` + `WAN2`) para validar escolha de gateway
- OPNsense com categoria `OptiRoute` disponível. **Não precisa criar manualmente** — o App auto-cria via `POST /api/firewall/category/add_item` no primeiro `addRule`, e cacheia o UUID em memória. Se a categoria já existir, faz lookup por nome (`search_item`) e reusa.

---

## 2. Build do executável single-file

```powershell
dotnet publish src/OptiRoute.App `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o E:\Projetos\OptiRoute\publish
```

Resultado: `E:\Projetos\OptiRoute\publish\OptiRoute.exe` (~80 MB self-contained).

---

## 3. Limpeza antes/depois de cada cenário

```powershell
# Limpar QoS locais criadas pelo OptiRoute
Get-NetQosPolicy -PolicyStore ActiveStore |
    Where-Object { $_.Name -like "OptiRoute-*" } |
    Remove-NetQosPolicy -Confirm:$false

# Limpar regras OptiRoute no OPNsense
# (via UI: Firewall → Rules → LAN → filtro Category=OptiRoute → delete todas)

# Limpar temp scripts órfãos
Remove-Item "$env:TEMP\optiroute_*.ps1" -ErrorAction SilentlyContinue
```

---

## 4. Cenários

Cada cenário = clique-through da UI + validação de estado final.
**Capture por cenário:** 1 screenshot da UI + log + estado do OPNsense + `Get-NetQosPolicy`.

### Cenário A — Registrar app novo (GlobalOnly → Synchronized)

1. Limpar tudo (§3)
2. Abrir OptiRoute.exe → tela principal vazia
3. Clicar **+ Adicionar Aplicativo** → preencher Executable=`bf6.exe`, DisplayName=`Battlefield 6`, escolher Gateway=`WAN1`
4. **Observar:** botão "Sincronizar" desabilitado durante loading
5. Após criar: progresso → tela mostra 1 app em estado `Synchronized`
6. **Verificar OPNsense:** 1 regra na LAN com description `OPTIROUTE|DEFAULT|bf6.exe|<dscp>|<appId>|v=1`
7. **Verificar Windows:** `Get-NetQosPolicy -PolicyStore ActiveStore` → `OptiRoute-bf6.exe` com DSCP correto

**Pós-condição esperada:**
- 1 regra OPNsense Default
- 1 policy QoS local
- App card verde (Synchronized)

### Cenário B — Conflito DSCP

1. Partir do estado do Cenário A (já sincronizado)
2. **Forçar DSCP divergente pelo lado OPNsense** (mais realista — Set-NetQosPolicy em
   ActiveStore falha com ERROR_NOT_FOUND quando a policy foi criada em PersistentStore):
   - Abrir OPNsense UI: **Firewall → Rules → LAN**
   - Editar a regra `OPTIROUTE|DEFAULT|bf6.exe|...` (criada no Cenário A)
   - Trocar o campo **DSCP** de `33` para `26` (ou outro valor, ex: `cs3 = 24`)
   - Clicar **Apply Changes** no topo da página
   - **Por que isso:** reflete o caso real de conflito (alguém editou a regra no firewall)
     e exercita o mesmo caminho de detecção no OptiRoute
3. Voltar ao OptiRoute → clicar **↻ Sincronizar**
4. **Observar:** app passa para estado **Conflict** (vermelho), com hint "→ Aplique pelo botão '✓ Aplicar reparo'"
5. Clicar **✓ Aplicar reparo** (habilitado porque plano ≠ vazio)
6. **Observar:** progresso, depois app volta para `Synchronized`
7. **Verificar Windows:** `Get-NetQosPolicy -Name "optiroute-bf6" -PolicyStore ActiveStore | Format-List Name,DSCPValue` → DSCP de volta ao global
8. **Verificar OPNsense:** nenhuma mudança

### Cenário C — Local órfão (não auto-delete)

1. Limpar tudo (§3)
2. Criar policy QoS local "manualmente" sem registrar no OPNsense:
   ```powershell
   New-NetQosPolicy -Name "OptiRoute-orphan.exe" -AppPathNameMatchCondition "orphan.exe" -DSCPValue 40 -PolicyStore ActiveStore
   ```
3. Abrir OptiRoute → **↻ Sincronizar**
4. **Observar:** app aparece em estado **LocalOnly** (roxo) com botões `+ Registrar no OPNsense` e `Remover do Windows`
5. **NÃO clicar** em nada. Fechar OptiRoute.
6. Reabrir OptiRoute → **↻ Sincronizar** novamente
7. **Observar:** orphan.exe continua lá (não foi auto-deletado) — **invariante crítica**

### Cenário D — Global não aplicado (ActivateGlobal)

1. Limpar tudo (§3)
2. Criar **só** a regra no OPNsense (manualmente, ou via outro PC de teste):
   - Description: `OPTIROUTE|DEFAULT|cod.exe|35|<algum-guid>|v=1`
   - Gateway: `WAN1`
3. NÃO criar policy local
4. Abrir OptiRoute → **↻ Sincronizar**
5. **Observar:** app aparece em estado **GlobalOnly** (cinza-azulado), com hint "→ Aplique pelo botão '✓ Aplicar reparo'"
6. Clicar **✓ Aplicar reparo**
7. **Verificar Windows:** `OptiRoute-cod.exe` criada com DSCP 35

### Cenário E — Multi-PC dedup (delta 4)

1. Partir do estado do Cenário A (1 app sincronizado)
2. Em **outro PC** (ou em outra sessão do App no mesmo PC — simular):
   - Adicionar mesmo `bf6.exe` com AppId **diferente** (gerar GUID novo)
3. **Observar no App:** log mostra `"[Sync] Adopting existing AppId <guid-original> from OPNsense rule <uuid>"`
4. **Verificar OPNsense:** continua existindo **1** regra Default para bf6.exe (não 2)
5. **Verificar Windows:** QoS local atualizada para o DSCP do registro existente (sem mudança se já igual)

### Cenário F — Ordem das regras

1. Limpar tudo (§3)
2. Criar 2 apps no OPNsense: `bf6.exe` (Default, WAN1) e `cod.exe` (Default, WAN2)
3. Em outro PC fictício, criar Override para `bf6.exe` em 10.0.0.122 → WAN2
4. No PC local: sincronizar
5. **Verificar OPNsense:** sequência das regras tem Override ANTES de Default
6. **Verificar:** `EnsureRelativeOrderAsync("lan")` foi chamado (ver log)

---

## 5. Modos de falha a observar

| Sintoma | Provável causa |
|---|---|
| "OPNsense host não configurado" ao iniciar | `AppConfigManager` não tem URL salva — preencher no primeiro launch |
| Botão Sincronizar desabilitado permanentemente | AppId vazio na identidade do app — verificar UI de adição |
| ApplyRepair fica desabilitado mesmo com Conflict | `BuildPlanAsync` retornou plano vazio — ver log `[Plan] Plan built:` |
| QoS local criada mas regra OPNsense sumiu | race condition no dedup — verificar log `[Sync] Duplicate rule detected` |
| ProgressBar trava em X% | thread sync — verificar se UI thread está recebendo callbacks |
| `.ps1` órfão em %TEMP% | cleanup não rodou — verificar logs de `CleanupOrphanedScripts` |

---

## 6. Logs e diagnóstico

- **Console:** stdout do .exe (PowerShell host: `OptiRoute.exe > log.txt 2>&1`)
- **Arquivo de log:** `%APPDATA%\OptiRoute\OptiRoute.log` (sempre escrito, truncado a cada startup)
- **Tail em tempo real:** `Get-Content $env:APPDATA\OptiRoute\OptiRoute.log -Tail 50 -Wait`
- **OPNsense:** System → Log → Firewall (filtrar por categoria `OptiRoute`)
- **Windows:** `Get-NetQosPolicy -PolicyStore ActiveStore | Format-List *`

---

## 7. Critério de aprovação

Todos os 6 cenários A-F passam + invariantes mantidas:
- Cenário C provou que local-only não é auto-deletado
- Cenário E provou que multi-PC não cria duplicatas
- Cenário F provou que ordem das regras é respeitada
- 0 exceções não-tratadas nos logs
- 0 arquivos órfãos em `%TEMP%` após 10min
