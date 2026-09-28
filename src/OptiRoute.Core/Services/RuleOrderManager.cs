using Microsoft.Extensions.Logging;
using OptiRoute.Core.Interfaces;
using OptiRoute.Core.Models;

namespace OptiRoute.Core.Services;

/// <summary>
/// Implementação de <see cref="IRuleOrderManager"/>.
/// Reordena estritamente as regras pertencentes ao OptiRoute de forma relativa:
/// 1. OVERRIDES (mais específicos por IP)
/// 2. DEFAULTS  (gerais da LAN por DSCP)
/// Ambos posicionados imediatamente ANTES da primeira regra genérica de Internet/Load Balance do usuário.
/// NUNCA altera, renumera ou move regras externas criadas pelo usuário.
/// </summary>
public sealed class RuleOrderManager : IRuleOrderManager
{
    private readonly IOpnsenseClient               _client;
    private readonly ILogger<RuleOrderManager>     _logger;

    public RuleOrderManager(IOpnsenseClient client, ILogger<RuleOrderManager> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<bool> EnsureRelativeOrderAsync(string lanInterface = "lan", CancellationToken ct = default)
    {
        var allRules = await _client.ListAllRulesAsync(lanInterface, ct);

        if (!allRules.Any())
            return true;

        var optiRules = allRules.Where(r => r.IsOptiRouteRule).ToList();
        if (!optiRules.Any())
            return true; // Nenhuma regra OptiRoute cadastrada

        var userRules = allRules.Where(r => !r.IsOptiRouteRule).ToList();

        // Localiza a âncora: primeira regra genérica que captura tráfego para a Internet (ex: LOADBALANCE ou gateway definido)
        var anchorRule = userRules.FirstOrDefault(r =>
            r.Description.Contains("LOADBALANCE", StringComparison.OrdinalIgnoreCase) ||
            r.Description.Contains("Load Balance", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(r.Gateway) && r.Gateway != "*") ||
            (r.DestinationNet == "any" && r.DestinationNot != "1"));

        // Se não houver âncora identificável, usa a última regra de usuário como referência
        var anchorSequence = anchorRule?.Sequence ?? (userRules.Any() ? userRules.Last().Sequence + 10 : 100);

        _logger.LogDebug("[RuleOrderManager] Anchor rule '{Desc}' identified at sequence {Seq}",
            anchorRule?.Description ?? "None", anchorSequence);

        var overrides = optiRules
            .Where(r => r.Descriptor?.RuleType == OptiRouteRuleType.Override)
            .OrderBy(r => r.Sequence)
            .ToList();

        var defaults = optiRules
            .Where(r => r.Descriptor?.RuleType != OptiRouteRuleType.Override)
            .OrderBy(r => r.Sequence)
            .ToList();

        // Verifica se todas as regras já estão ordenadas corretamente:
        // Todas antes da âncora E todos os overrides antes de todos os defaults
        var maxOverrideSeq = overrides.Any() ? overrides.Max(r => r.Sequence) : 0;
        var minDefaultSeq  = defaults.Any()  ? defaults.Min(r => r.Sequence) : int.MaxValue;
        var maxOptiSeq     = optiRules.Max(r => r.Sequence);

        var isCorrectlyOrdered = maxOptiSeq < anchorSequence &&
                                (!overrides.Any() || !defaults.Any() || maxOverrideSeq < minDefaultSeq);

        if (isCorrectlyOrdered)
        {
            _logger.LogDebug("[RuleOrderManager] OptiRoute rules are already in correct relative order.");
            return true;
        }

        _logger.LogInformation("[RuleOrderManager] Reordering OptiRoute rules to be positioned before anchor sequence {Seq}",
            anchorSequence);

        // Atribui sequências relativas em bloco imediatamente antes da âncora
        // Deixamos um espaçamento para que as regras do OptiRoute fiquem ordenadas:
        // Overrides primeiro, depois Defaults
        var totalOpti = overrides.Count + defaults.Count;
        var startSeq  = Math.Max(1, anchorSequence - (totalOpti * 2) - 1);

        var currentSeq = startSeq;
        var hasChanges = false;

        foreach (var rule in overrides)
        {
            if (rule.Sequence != currentSeq)
            {
                await _client.UpdateRuleSequenceAsync(rule.Uuid, currentSeq, ct);
                hasChanges = true;
            }
            currentSeq += 2;
        }

        foreach (var rule in defaults)
        {
            if (rule.Sequence != currentSeq)
            {
                await _client.UpdateRuleSequenceAsync(rule.Uuid, currentSeq, ct);
                hasChanges = true;
            }
            currentSeq += 2;
        }

        if (hasChanges)
        {
            await _client.ApplyRulesAsync(ct);
            _logger.LogInformation("[RuleOrderManager] Applied relative order update for {Count} rules.", totalOpti);
        }

        return true;
    }
}
