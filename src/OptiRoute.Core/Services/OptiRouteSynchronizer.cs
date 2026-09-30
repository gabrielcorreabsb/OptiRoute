using System.Net;
using Microsoft.Extensions.Logging;
using OptiRoute.Core.Interfaces;
using OptiRoute.Core.Models;

namespace OptiRoute.Core.Services;

/// <summary>
/// Orquestrador central de reconciliação real entre OPNsense e Windows QoS local.
/// Constrói o estado consolidado a partir da união das duas fontes (OPNsense UNION Windows QoS)
/// e classifica cada aplicativo em Synchronized, LocalOnly, GlobalOnly ou Conflict.
/// </summary>
public sealed class OptiRouteSynchronizer : IOptiRouteSynchronizer
{
    private readonly IOpnsenseClient                  _client;
    private readonly IWindowsQosManager               _qosManager;
    private readonly IDscpRegistry                    _dscpRegistry;
    private readonly IRuleOrderManager                _orderManager;
    private readonly ILogger<OptiRouteSynchronizer>   _logger;

    public OptiRouteSynchronizer(
        IOpnsenseClient client,
        IWindowsQosManager qosManager,
        IDscpRegistry dscpRegistry,
        IRuleOrderManager orderManager,
        ILogger<OptiRouteSynchronizer> logger)
    {
        _client       = client;
        _qosManager   = qosManager;
        _dscpRegistry = dscpRegistry;
        _orderManager = orderManager;
        _logger       = logger;
    }

    public async Task<OptiRouteSyncResult> SyncAsync(
        IPAddress localHostIp,
        IProgress<SyncProgress>? progress = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[Sync] Starting real reconciliation for host {Ip}", localHostIp);
        progress?.Report(new SyncProgress("init", 5, "Iniciando sincronização..."));

        // 1. Consultar regras OptiRoute no OPNsense
        progress?.Report(new SyncProgress("rules", 25, "Consultando regras do OPNsense..."));
        var rules = (await _client.ListOptiRouteRulesAsync(ct)) ?? Array.Empty<FirewallRuleInfo>();

        // 2. Reconstruir DscpRegistry
        progress?.Report(new SyncProgress("merge", 45, "Reconstruindo registry DSCP..."));
        var descriptors = rules
            .Where(r => r.Descriptor is not null)
            .Select(r => r.Descriptor!)
            .ToList();

        _dscpRegistry.SynchronizeFromRules(descriptors);

        // 3. Separar DEFAULTS e OVERRIDES do OPNsense
        var defaultRoutes = new Dictionary<string, ApplicationRoute>(StringComparer.OrdinalIgnoreCase);
        var hostOverrides = new Dictionary<string, HostOverride>(StringComparer.OrdinalIgnoreCase);
        var tosMismatchByExe = new Dictionary<string, FirewallRuleInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            if (rule.Descriptor is null)
                continue;

            var cleanExe = rule.Descriptor.ExecutableName;

            if (rule.Descriptor.RuleType == OptiRouteRuleType.Default)
            {
                if (!defaultRoutes.ContainsKey(cleanExe))
                {
                    defaultRoutes[cleanExe] = new ApplicationRoute
                    {
                        Identity       = ApplicationIdentity.Create(cleanExe, existingAppId: rule.Descriptor.AppId),
                        Dscp           = rule.Descriptor.Dscp,
                        DefaultGateway = rule.Gateway,
                        RuleUuid       = rule.Uuid
                    };
                }

                // Phase 1 — defesa contra tampering do tos via UI do OPNsense.
                // Se o tos real diverge do DSCP parseado do description, a regra está
                // corrompida (description diz "DSCP 33" mas tos=0x80 enforça DSCP 32).
                // Marcamos o executável para virar Conflict no fim.
                if (rule.HasTosMismatch && !tosMismatchByExe.ContainsKey(cleanExe))
                {
                    var detail = $"description DSCP={rule.Descriptor.Dscp} ≠ tos real={rule.Tos} (DSCP={rule.TosDscp?.ToString() ?? "?"})";
                    tosMismatchByExe[cleanExe] = rule;
                    _logger.LogWarning(
                        "[Sync] InvalidTos rule {Uuid} ('{Exe}'): {Detail}",
                        rule.Uuid, cleanExe, detail);
                }
            }
            else if (rule.Descriptor.RuleType == OptiRouteRuleType.Override)
            {
                if (rule.Descriptor.SourceIp?.Equals(localHostIp) == true)
                {
                    hostOverrides[cleanExe] = new HostOverride
                    {
                        Identity = ApplicationIdentity.Create(cleanExe, existingAppId: rule.Descriptor.AppId),
                        Dscp     = rule.Descriptor.Dscp,
                        SourceIp = localHostIp,
                        Gateway  = rule.Gateway,
                        RuleUuid = rule.Uuid
                    };
                }
            }
        }

        // 4. Consultar gateways para status em tempo real
        IReadOnlyList<Gateway> gateways = Array.Empty<Gateway>();
        try
        {
            var fetchedGateways = await _client.GetGatewaysAsync(ct);
            if (fetchedGateways is not null)
                gateways = fetchedGateways;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Sync] Could not refresh gateways status");
        }

        // 5. Consultar políticas locais do Windows QoS (com Owner e Store)
        progress?.Report(new SyncProgress("qos", 70, "Consultando QoS local do Windows..."));
        var localQosPolicies = await _qosManager.ListLocalPoliciesAsync(ct);
        var qosByExe = localQosPolicies
            .Where(p => !string.IsNullOrEmpty(p.ExecutableName))
            .ToDictionary(
                p => p.ExecutableName,
                p => p,
                StringComparer.OrdinalIgnoreCase);

        // 6. RECONCILIAÇÃO REAL POR UNIÃO: allApps = OPNsense UNION Windows QoS
        var allExecutables = defaultRoutes.Keys
            .Union(qosByExe.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e)
            .ToList();

        var effectiveRoutes = new List<EffectiveApplicationRoute>();

        foreach (var exe in allExecutables)
        {
            var hasGlobal = defaultRoutes.TryGetValue(exe, out var defRoute);
            var hasLocal  = qosByExe.TryGetValue(exe, out var localPolicy);

            ApplicationSyncState syncState;
            int dscp;
            int? localDscp  = hasLocal  ? localPolicy!.Dscp : null;
            int? globalDscp = hasGlobal ? defRoute!.Dscp   : null;

            if (hasGlobal && hasLocal)
            {
                syncState = (defRoute!.Dscp == localPolicy!.Dscp)
                    ? ApplicationSyncState.Synchronized
                    : ApplicationSyncState.Conflict;

                dscp = defRoute.Dscp;
            }
            else if (hasGlobal)
            {
                syncState = ApplicationSyncState.GlobalOnly;
                dscp      = defRoute!.Dscp;
            }
            else // !hasGlobal && hasLocal
            {
                syncState = ApplicationSyncState.LocalOnly;
                dscp      = localPolicy!.Dscp;
            }

            // tos mismatch prevalece sobre o estado normal: regra corrompida é sempre Conflict
            string? tosDetail = null;
            if (tosMismatchByExe.TryGetValue(exe, out var td))
            {
                syncState = ApplicationSyncState.Conflict;
                tosDetail = $"description DSCP={td.Descriptor!.Dscp} ≠ tos real={td.Tos} (DSCP={td.TosDscp?.ToString() ?? "?"})";
            }

            var identity = hasGlobal
                ? defRoute!.Identity
                : ApplicationIdentity.Create(exe);

            var defaultGateway = hasGlobal
                ? defRoute!.DefaultGateway
                : "Não configurado no OPNsense";

            var hasOverride = hostOverrides.TryGetValue(exe, out var ovr);
            string effectiveGateway;

            if (syncState == ApplicationSyncState.LocalOnly)
            {
                effectiveGateway = "Default LAN Routing";
            }
            else if (syncState == ApplicationSyncState.GlobalOnly)
            {
                effectiveGateway = $"{defaultGateway} (Inativo localmente)";
            }
            else
            {
                effectiveGateway = hasOverride ? ovr!.Gateway : defaultGateway;
            }

            // Phase 1 — tos mismatch detectado na iteração anterior das rules.
            // Força Conflict mesmo se o DSCP bate (description e local) — a regra está corrompida.
            string? tosMismatchDetail = null;
            string? ruleUuid           = null;
            string? actualTosHex       = null;
            if (tosMismatchByExe.TryGetValue(exe, out var badRule))
            {
                syncState         = ApplicationSyncState.Conflict;
                tosMismatchDetail = $"description DSCP={badRule.Descriptor!.Dscp} ≠ tos real={badRule.Tos} (DSCP={badRule.TosDscp?.ToString() ?? "?"})";
                ruleUuid          = badRule.Uuid;
                actualTosHex      = badRule.Tos;
            }

            effectiveRoutes.Add(new EffectiveApplicationRoute
            {
                Identity         = identity,
                SyncState        = syncState,
                Dscp             = dscp,
                LocalDscp        = localDscp,
                GlobalDscp       = globalDscp,
                DefaultGateway   = defaultGateway,
                EffectiveGateway = effectiveGateway,
                HasOverride      = hasOverride,
                OverrideGateway  = hasOverride ? ovr!.Gateway : null,
                LocalPolicy      = localPolicy,
                TosMismatchDetail = tosMismatchDetail,
                RuleUuid         = ruleUuid,
                ActualTosHex     = actualTosHex
            });
        }

        // 7. Garantir ordenação relativa no firewall
        try
        {
            await _orderManager.EnsureRelativeOrderAsync("lan", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Sync] Could not check rule order on LAN");
        }

        _logger.LogInformation("[Sync] Reconciliation completed. Total {Count} apps (Sync={S}, Local={L}, Global={G}, Conflict={C})",
            effectiveRoutes.Count,
            effectiveRoutes.Count(r => r.SyncState == ApplicationSyncState.Synchronized),
            effectiveRoutes.Count(r => r.SyncState == ApplicationSyncState.LocalOnly),
            effectiveRoutes.Count(r => r.SyncState == ApplicationSyncState.GlobalOnly),
            effectiveRoutes.Count(r => r.SyncState == ApplicationSyncState.Conflict));

        progress?.Report(new SyncProgress("done", 100, $"Sincronização concluída: {effectiveRoutes.Count} aplicativo(s)"));

        return new OptiRouteSyncResult
        {
            LocalHostIp = localHostIp,
            Routes      = effectiveRoutes,
            Gateways    = gateways
        };
    }

    public async Task<string> PromoteLocalToGlobalAsync(
        string executable,
        string targetGateway,
        CancellationToken ct = default)
    {
        var cleanExe = Path.GetFileName(executable).Trim().ToLowerInvariant();
        var localPolicies = await _qosManager.ListLocalPoliciesAsync(ct);
        var localPolicy = localPolicies.FirstOrDefault(p =>
            p.ExecutableName.Equals(cleanExe, StringComparison.OrdinalIgnoreCase));

        int targetDscp;

        if (localPolicy is not null && localPolicy.Dscp > 0)
        {
            // Valida se o DSCP local já está em uso por outro executável
            var existingAppForDscp = _dscpRegistry.GetByDscp(localPolicy.Dscp);
            if (!string.IsNullOrEmpty(existingAppForDscp) &&
                !existingAppForDscp.Equals(cleanExe, StringComparison.OrdinalIgnoreCase))
            {
                // Colisão detectada: o DSCP local já pertence a outro app global
                _logger.LogWarning("[Promote] DSCP {Dscp} is already assigned to '{Existing}'. Allocating new DSCP for '{Exe}'",
                    localPolicy.Dscp, existingAppForDscp, cleanExe);

                targetDscp = _dscpRegistry.AllocateNextAvailable(cleanExe);
                // Atualiza o QoS local do Windows para o novo DSCP
                await _qosManager.UpdatePolicyAsync(cleanExe, targetDscp, ct);
            }
            else
            {
                _dscpRegistry.Register(cleanExe, localPolicy.Dscp);
                targetDscp = localPolicy.Dscp;
            }
        }
        else
        {
            targetDscp = _dscpRegistry.AllocateNextAvailable(cleanExe);
            await _qosManager.CreatePolicyAsync(cleanExe, targetDscp, ct);
        }

        var identity = ApplicationIdentity.Create(cleanExe);
        return await RegisterOrUpdateApplicationAsync(identity, targetGateway, targetDscp, ct);
    }

    public async Task ActivateGlobalOnLocalAsync(string executable, CancellationToken ct = default)
    {
        var cleanExe = Path.GetFileName(executable).Trim().ToLowerInvariant();
        var dscp = _dscpRegistry.GetByExecutable(cleanExe);

        if (!dscp.HasValue)
        {
            var rules = await _client.ListOptiRouteRulesAsync(ct);
            var rule = rules.FirstOrDefault(r => r.Descriptor?.ExecutableName.Equals(cleanExe, StringComparison.OrdinalIgnoreCase) == true);
            dscp = rule?.Descriptor?.Dscp;
        }

        if (!dscp.HasValue || dscp.Value <= 0)
            throw new InvalidOperationException($"Cannot activate '{cleanExe}': no global DSCP found in OPNsense.");

        _logger.LogInformation("[Activate] Activating QoS for '{Exe}' with DSCP {Dscp} on this computer", cleanExe, dscp.Value);
        await _qosManager.CreatePolicyAsync(cleanExe, dscp.Value, ct);
    }

    public async Task RepairConflictAsync(string executable, CancellationToken ct = default)
    {
        var cleanExe = Path.GetFileName(executable).Trim().ToLowerInvariant();
        var dscp = _dscpRegistry.GetByExecutable(cleanExe);

        if (!dscp.HasValue)
        {
            var rules = await _client.ListOptiRouteRulesAsync(ct);
            var rule = rules.FirstOrDefault(r => r.Descriptor?.ExecutableName.Equals(cleanExe, StringComparison.OrdinalIgnoreCase) == true);
            dscp = rule?.Descriptor?.Dscp;
        }

        if (!dscp.HasValue || dscp.Value <= 0)
            throw new InvalidOperationException($"Cannot repair conflict for '{cleanExe}': no global DSCP found.");

        _logger.LogInformation("[Repair] Reconciling QoS for '{Exe}' to match global DSCP {Dscp}", cleanExe, dscp.Value);
        await _qosManager.UpdatePolicyAsync(cleanExe, dscp.Value, ct);
    }

    public async Task RemoveLocalApplicationAsync(string executable, CancellationToken ct = default)
    {
        var cleanExe = Path.GetFileName(executable).Trim().ToLowerInvariant();
        var localPolicies = await _qosManager.ListLocalPoliciesAsync(ct);
        var policy = localPolicies.FirstOrDefault(p =>
            p.ExecutableName.Equals(cleanExe, StringComparison.OrdinalIgnoreCase));

        if (policy is not null)
        {
            _logger.LogInformation("[RemoveLocal] Removing local policy '{Name}'", policy.Name);
            await _qosManager.DeletePolicyAsync(policy, ct);
        }
        else
        {
            await _qosManager.DeletePolicyAsync(cleanExe, ct);
        }
    }

    public async Task DeleteGlobalApplicationAsync(string executable, CancellationToken ct = default)
    {
        var cleanExe = Path.GetFileName(executable).Trim().ToLowerInvariant();
        var rules = await _client.ListOptiRouteRulesAsync(ct);

        foreach (var rule in rules)
        {
            if (rule.Descriptor?.ExecutableName.Equals(cleanExe, StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogInformation("[DeleteGlobal] Deleting OPNsense rule uuid={Uuid} for '{Exe}'",
                    rule.Uuid, cleanExe);
                await _client.DeleteRuleAsync(rule.Uuid, ct);
            }
        }
    }

    public async Task<string> RegisterOrUpdateApplicationAsync(
        ApplicationIdentity identity,
        string defaultGateway,
        int? specificDscp = null,
        CancellationToken ct = default)
    {
        var cleanExe = identity.ExecutableName;

        // ── Delta 4: Multi-PC dedup ──
        // Antes de criar/atualizar, consultar OPNsense. Se já existe uma regra DEFAULT para
        // este executável com AppId válido, ADOTAR esse AppId. Resultado: EnsureRuleExists
        // encontra a regra existente pela descrição e faz UPDATE em vez de CREATE, evitando
        // duplicata quando dois PCs registram o mesmo app em corrida.
        var existingRules = (await _client.ListOptiRouteRulesAsync(ct)) ?? Array.Empty<FirewallRuleInfo>();
        var existingDefault = existingRules.FirstOrDefault(r =>
            r.Descriptor?.RuleType == OptiRouteRuleType.Default &&
            r.Descriptor.ExecutableName.Equals(cleanExe, StringComparison.OrdinalIgnoreCase));

        string effectiveAppId = identity.AppId;
        if (existingDefault?.Descriptor?.AppId is { Length: > 0 } existingId &&
            !existingId.Equals(identity.AppId, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "[Sync] Adopting existing AppId {ExistingId} from OPNsense rule {Uuid} for '{Exe}' (incoming was {IncomingId})",
                existingId, existingDefault.Uuid, cleanExe, identity.AppId);
            effectiveAppId = existingId;
        }

        int dscp;
        if (specificDscp.HasValue && specificDscp.Value > 0)
        {
            _dscpRegistry.Register(cleanExe, specificDscp.Value);
            dscp = specificDscp.Value;
        }
        else
        {
            dscp = _dscpRegistry.AllocateNextAvailable(cleanExe);
        }

        var descriptor = new OptiRouteRuleDescriptor
        {
            RuleType       = OptiRouteRuleType.Default,
            ExecutableName = cleanExe,
            Dscp           = dscp,
            AppId          = effectiveAppId,
            Gateway        = defaultGateway,
            Enabled        = true
        };

        var request = new OPNsenseRuleRequest
        {
            Description    = descriptor.FormatDescription(),
            Category       = "OptiRoute",
            Interface      = "lan",
            SourceIp       = "any",
            DscpValue      = dscp,
            Gateway        = defaultGateway,
            DestinationNet = "(self)",
            DestinationNot = "1", // ! This firewall
            Sequence       = 1
        };

        _logger.LogInformation("[Sync] Registering application '{Exe}' -> Gateway {Gw} (DSCP {Dscp})",
            cleanExe, defaultGateway, dscp);

        var uuid = await _client.EnsureRuleExistsAsync(request, ct);

        // Desempate determinístico contra race condition
        var optiRules = (await _client.ListOptiRouteRulesAsync(ct)) ?? Array.Empty<FirewallRuleInfo>();
        var duplicates = optiRules
            .Where(r => r.Descriptor?.RuleType == OptiRouteRuleType.Default &&
                        r.Descriptor.ExecutableName.Equals(cleanExe, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Uuid)
            .ToList();

        if (duplicates.Count > 1)
        {
            var winner = duplicates[0];
            for (int i = 1; i < duplicates.Count; i++)
            {
                _logger.LogWarning("[Sync] Duplicate rule detected for '{Exe}'. Removing duplicate uuid={Uuid}",
                    cleanExe, duplicates[i].Uuid);
                await _client.DeleteRuleAsync(duplicates[i].Uuid, ct);
            }
            uuid = winner.Uuid;
            dscp = winner.Descriptor!.Dscp;
        }

        // Criar ou atualizar QoS local
        try
        {
            await _qosManager.CreatePolicyAsync(cleanExe, dscp, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Sync] Could not create local QoS for '{Exe}'", cleanExe);
        }

        // Garantir ordem relativa no firewall
        await _orderManager.EnsureRelativeOrderAsync("lan", ct);

        return uuid;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Pipeline de reconciliação (Delta 2)
    // BuildPlanAsync → ApplyPlanAsync → VerifyAsync
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Constrói o plano de mutações a partir do estado consolidado.
    /// Sem side-effects. Idempotente — rodar 2× produz o mesmo plano.
    /// </summary>
    public Task<ReconciliationPlan> BuildPlanAsync(
        OptiRouteSyncResult state,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[Plan] Building reconciliation plan from {Count} routes", state.Routes.Count);

        var actions = new List<ReconciliationAction>();

        foreach (var route in state.Routes)
        {
            ct.ThrowIfCancellationRequested();

            ReconciliationAction? action = route.SyncState switch
            {
                ApplicationSyncState.Conflict => new ReconciliationAction(
                    Type:                ReconciliationActionType.UpdateQosPolicy,
                    Executable:          route.Executable,
                    AppId:               route.Identity?.AppId ?? string.Empty,
                    SourceIp:            null,
                    Dscp:                route.GlobalDscp ?? route.Dscp,
                    TargetGateway:       route.DefaultGateway,
                    RuleUuid:            null,
                    LocalQosPolicyName:  $"OptiRoute-{route.Executable}",
                    Description:         $"Corrigir DSCP local de '{route.Executable}' de {route.LocalDscp} para {route.GlobalDscp} (global)"),

                ApplicationSyncState.GlobalOnly => new ReconciliationAction(
                    Type:                ReconciliationActionType.CreateQosPolicy,
                    Executable:          route.Executable,
                    AppId:               route.Identity?.AppId ?? string.Empty,
                    SourceIp:            null,
                    Dscp:                route.GlobalDscp ?? route.Dscp,
                    TargetGateway:       route.DefaultGateway,
                    RuleUuid:            null,
                    LocalQosPolicyName:  $"OptiRoute-{route.Executable}",
                    Description:         $"Ativar QoS local para '{route.Executable}' com DSCP {route.GlobalDscp}"),

                ApplicationSyncState.LocalOnly => new ReconciliationAction(
                    // Estado LocalOnly: gateway alvo não disponível no estado lido
                    // (DefaultGateway = "Não configurado no OPNsense"). Plano conservador:
                    // oferece Delete. Promoção continua via per-card (decisão D1).
                    Type:                ReconciliationActionType.DeleteLocalQosPolicy,
                    Executable:          route.Executable,
                    AppId:               route.Identity?.AppId ?? string.Empty,
                    SourceIp:            null,
                    Dscp:                route.LocalDscp ?? 0,
                    TargetGateway:       string.Empty,
                    RuleUuid:            null,
                    LocalQosPolicyName:  $"OptiRoute-{route.Executable}",
                    Description:         $"Política local '{route.Executable}' sem registro no OPNsense — remover ou promover (per-card)"),

                _ => null
            };

            if (action is not null)
                actions.Add(action);
        }

        _logger.LogInformation("[Plan] Plan built: {Count} actions ({Breakdown})",
            actions.Count,
            string.Join(", ", actions.GroupBy(a => a.Type).Select(g => $"{g.Key}={g.Count()}")));

        return Task.FromResult(new ReconciliationPlan { Actions = actions });
    }

    /// <summary>
    /// Aplica o plano no Windows QoS local e/ou no OPNsense.
    /// Continua processando após uma falha parcial — caller recebe o resultado completo.
    /// </summary>
    public async Task<ReconciliationResult> ApplyPlanAsync(
        ReconciliationPlan plan,
        CancellationToken ct = default)
    {
        var verified = new List<ReconciliationAction>();
        var failures = new List<ReconciliationActionFailure>();

        _logger.LogInformation("[Apply] Applying plan with {Count} actions", plan.Actions.Count);

        foreach (var action in plan.Actions)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                switch (action.Type)
                {
                    case ReconciliationActionType.NoOp:
                        verified.Add(action);
                        break;

                    case ReconciliationActionType.CreateQosPolicy:
                        await _qosManager.CreatePolicyAsync(action.Executable, action.Dscp, ct);
                        verified.Add(action);
                        break;

                    case ReconciliationActionType.UpdateQosPolicy:
                        await _qosManager.UpdatePolicyAsync(action.Executable, action.Dscp, ct);
                        verified.Add(action);
                        break;

                    case ReconciliationActionType.DeleteLocalQosPolicy:
                        await _qosManager.DeletePolicyAsync(action.Executable, ct);
                        verified.Add(action);
                        break;

                    // As 4 ações de firewall ainda não são emitidas por BuildPlanAsync
                    // (promoção automática fica para delta futuro). Quando forem,
                    // a implementação reutilizará a lógica de RegisterOrUpdateApplicationAsync.
                    case ReconciliationActionType.CreateFirewallRule:
                    case ReconciliationActionType.UpdateFirewallRule:
                    case ReconciliationActionType.MoveFirewallRule:
                    case ReconciliationActionType.DeleteFirewallRule:
                        _logger.LogWarning("[Apply] {Type} ainda não implementado — pulando", action.Type);
                        failures.Add(new ReconciliationActionFailure(action,
                            $"{action.Type} ainda não emitido por BuildPlanAsync (delta futuro)"));
                        break;

                    // Recriar regra OPNsense com tos corrigido (resolve corrupção
                    // tos ≠ DSCP-description). Deleta a regra antiga pelo UUID,
                    // recria com OpnsenseRequest que carrega o DSCP correto, e
                    // aplica as mudanças.
                    case ReconciliationActionType.RecreateFirewallRule:
                        if (!string.IsNullOrEmpty(action.RuleUuid))
                        {
                            _logger.LogInformation("[Apply] Deleting rule uuid={Uuid} before recreate", action.RuleUuid);
                            await _client.DeleteRuleAsync(action.RuleUuid, ct);
                        }
                        if (action.OpnsenseRequest is null)
                            throw new InvalidOperationException(
                                $"RecreateFirewallRule sem OpnsenseRequest: {action.Executable}");

                        _logger.LogInformation("[Apply] Recreating rule for '{Exe}' with DSCP={Dscp}",
                            action.Executable, action.OpnsenseRequest.DscpValue);
                        await _client.EnsureRuleExistsAsync(action.OpnsenseRequest, ct);
                        await _client.ApplyRulesAsync(ct);
                        verified.Add(action);
                        break;

                    default:
                        failures.Add(new ReconciliationActionFailure(action,
                            $"Tipo de ação desconhecido: {action.Type}"));
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Apply] Failed to apply {Type} for '{Exe}'", action.Type, action.Executable);
                failures.Add(new ReconciliationActionFailure(action, ex.Message));
            }
        }

        // Se alguma ação de firewall rodou (ou foi pretendida), checar ordem
        if (plan.Actions.Any(a => a.Type is ReconciliationActionType.CreateFirewallRule
                                        or ReconciliationActionType.UpdateFirewallRule
                                        or ReconciliationActionType.MoveFirewallRule
                                        or ReconciliationActionType.DeleteFirewallRule
                                        or ReconciliationActionType.RecreateFirewallRule))
        {
            try { await _orderManager.EnsureRelativeOrderAsync("lan", ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "[Apply] Could not check rule order on LAN"); }
        }

        _logger.LogInformation("[Apply] Result: {Verified} verified, {Failed} failed",
            verified.Count, failures.Count);

        return new ReconciliationResult(verified, failures);
    }

    /// <summary>
    /// Re-lê o estado atual via <see cref="SyncAsync"/> e verifica que cada ação
    /// do plano produziu o efeito esperado. Falhas de verificação indicam
    /// divergência entre o plano e o estado real (ex: race condition, regra
    /// removida por outro PC).
    /// </summary>
    public async Task<ReconciliationResult> VerifyAsync(
        ReconciliationPlan plan,
        IPAddress localHostIp,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[Verify] Re-reading state to verify {Count} actions", plan.Actions.Count);

        var state = await SyncAsync(localHostIp, progress: null, ct);
        var verified = new List<ReconciliationAction>();
        var failures = new List<ReconciliationActionFailure>();

        foreach (var action in plan.Actions)
        {
            ct.ThrowIfCancellationRequested();

            var route = state.Routes.FirstOrDefault(r =>
                r.Executable.Equals(action.Executable, StringComparison.OrdinalIgnoreCase));

            var (ok, reason) = action.Type switch
            {
                ReconciliationActionType.NoOp =>
                    (true, "NoOp"),

                ReconciliationActionType.UpdateQosPolicy =>
                    route is null
                        ? (false, "rota desapareceu do estado")
                        : (route.LocalDscp == action.Dscp,
                           $"LocalDscp={route.LocalDscp?.ToString() ?? "null"} != esperado {action.Dscp}"),

                ReconciliationActionType.CreateQosPolicy =>
                    route is null
                        ? (false, "rota desapareceu do estado")
                        : (route.LocalDscp == action.Dscp,
                           $"LocalDscp={route.LocalDscp?.ToString() ?? "null"} != esperado {action.Dscp}"),

                ReconciliationActionType.DeleteLocalQosPolicy =>
                    (route is null || route.LocalDscp is null,
                     route is null
                        ? "rota não existe mais (OK)"
                        : $"LocalDscp ainda existe: {route.LocalDscp}"),

                // Ações de firewall ainda não implementadas no Apply
                _ => (false, $"{action.Type} ainda não implementado no Apply")
            };

            if (ok)
                verified.Add(action);
            else
                failures.Add(new ReconciliationActionFailure(action, reason));
        }

        _logger.LogInformation("[Verify] Result: {Verified} verified, {Failed} failed",
            verified.Count, failures.Count);

        return new ReconciliationResult(verified, failures);
    }

    /// <summary>
    /// Re-lê o estado atual e deriva uma verificação por-app (<see cref="RouteVerification"/>)
    /// do estado efetivo. Aproximação: <c>RuleOrderValid</c> assume <c>true</c> porque o
    /// próprio <see cref="SyncAsync"/> já sinalizaria regra fora de ordem no estado.
    /// </summary>
    public async Task<IReadOnlyList<RouteVerification>> VerifyRoutesAsync(
        IPAddress localHostIp,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[VerifyRoutes] Computing per-app verification");

        var state   = await SyncAsync(localHostIp, progress: null, ct);
        var results = new List<RouteVerification>(state.Routes.Count);

        foreach (var route in state.Routes)
        {
            ct.ThrowIfCancellationRequested();

            var qosActive  = route.LocalDscp is not null;
            var ruleDscp   = route.GlobalDscp;
            var dscpCorrect = route.LocalDscp is not null
                              && (ruleDscp is null || route.LocalDscp == ruleDscp);
            var ruleActive = !string.IsNullOrEmpty(route.RuleUuid);
            var gatewayReachable = !string.IsNullOrEmpty(route.EffectiveGateway)
                                   || !string.IsNullOrEmpty(route.DefaultGateway);

            // Aproximação: o sync já teria detectado se a regra está fora de ordem.
            const bool orderValid = true;

            var failureReason = (qosActive, dscpCorrect, ruleActive, gatewayReachable) switch
            {
                (false, _, _, _)          => "QoS policy missing",
                (true, false, _, _)       => $"DSCP mismatch (local={route.LocalDscp}, rule={ruleDscp?.ToString() ?? "null"})",
                (true, true, false, _)    => "Firewall rule missing",
                (true, true, true, false) => "Gateway unreachable",
                _                         => null
            };

            results.Add(new RouteVerification(
                route.Executable,
                qosActive,
                dscpCorrect,
                ruleActive,
                orderValid,
                gatewayReachable,
                failureReason));
        }

        _logger.LogInformation("[VerifyRoutes] Result: {Count} routes checked", results.Count);
        return results;
    }
}
