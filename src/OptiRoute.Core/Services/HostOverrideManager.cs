using System.Net;
using Microsoft.Extensions.Logging;
using OptiRoute.Core.Interfaces;
using OptiRoute.Core.Models;

namespace OptiRoute.Core.Services;

/// <summary>
/// Implementação de <see cref="IHostOverrideManager"/>.
/// Cria regras OVERRIDE no OPNsense garantindo DSCP global compartilhado e prioridade relativa.
/// </summary>
public sealed class HostOverrideManager : IHostOverrideManager
{
    private readonly IOpnsenseClient               _client;
    private readonly IDscpRegistry                 _dscpRegistry;
    private readonly IRuleOrderManager             _orderManager;
    private readonly ILogger<HostOverrideManager>  _logger;

    public HostOverrideManager(
        IOpnsenseClient client,
        IDscpRegistry dscpRegistry,
        IRuleOrderManager orderManager,
        ILogger<HostOverrideManager> logger)
    {
        _client       = client;
        _dscpRegistry = dscpRegistry;
        _orderManager = orderManager;
        _logger       = logger;
    }

    public async Task<string> SetOverrideAsync(
        ApplicationIdentity identity,
        IPAddress hostIp,
        string targetGateway,
        CancellationToken ct = default)
    {
        // 1. Garante que o executável possui um DSCP global oficial
        var dscp = _dscpRegistry.AllocateNextAvailable(identity.ExecutableName);

        var descriptor = new OptiRouteRuleDescriptor
        {
            RuleType       = OptiRouteRuleType.Override,
            ExecutableName = identity.ExecutableName,
            Dscp           = dscp,
            SourceIp       = hostIp,
            AppId          = identity.AppId,
            Gateway        = targetGateway,
            Enabled        = true
        };

        var request = new OPNsenseRuleRequest
        {
            Description    = descriptor.FormatDescription(),
            Category       = "OptiRoute",
            Interface      = "lan",
            SourceIp       = hostIp.ToString(),
            DscpValue      = dscp,
            Gateway        = targetGateway,
            DestinationNet = "(self)",
            DestinationNot = "1", // ! This firewall
            Sequence       = 1
        };

        _logger.LogInformation("[HostOverrideManager] Setting override for '{Exe}' on {Ip} -> {Gw} (DSCP {Dscp})",
            identity.ExecutableName, hostIp, targetGateway, dscp);

        var uuid = await _client.EnsureRuleExistsAsync(request, ct);

        // Garante que a regra OVERRIDE fique acima de DEFAULTS e do LOADBALANCE
        await _orderManager.EnsureRelativeOrderAsync("lan", ct);

        return uuid;
    }

    public async Task<bool> RemoveOverrideAsync(
        string executable,
        IPAddress hostIp,
        CancellationToken ct = default)
    {
        var existing = await GetOverrideAsync(executable, hostIp, ct);
        if (existing is null || string.IsNullOrEmpty(existing.RuleUuid))
        {
            _logger.LogDebug("[HostOverrideManager] No active override found to remove for '{Exe}' on {Ip}",
                executable, hostIp);
            return false;
        }

        _logger.LogInformation("[HostOverrideManager] Removing override uuid={Uuid} for '{Exe}' on {Ip}",
            existing.RuleUuid, executable, hostIp);

        await _client.DeleteRuleAsync(existing.RuleUuid, ct);
        return true;
    }

    public async Task<HostOverride?> GetOverrideAsync(
        string executable,
        IPAddress hostIp,
        CancellationToken ct = default)
    {
        var cleanExe = Path.GetFileName(executable).Trim().ToLowerInvariant();
        var rules = await _client.ListOptiRouteRulesAsync(ct);

        foreach (var rule in rules)
        {
            if (rule.Descriptor is null || rule.Descriptor.RuleType != OptiRouteRuleType.Override)
                continue;

            if (rule.Descriptor.ExecutableName.Equals(cleanExe, StringComparison.OrdinalIgnoreCase) &&
                rule.Descriptor.SourceIp?.Equals(hostIp) == true)
            {
                return new HostOverride
                {
                    Identity = ApplicationIdentity.Create(cleanExe, existingAppId: rule.Descriptor.AppId),
                    Dscp     = rule.Descriptor.Dscp,
                    SourceIp = hostIp,
                    Gateway  = rule.Gateway,
                    RuleUuid = rule.Uuid
                };
            }
        }

        return null;
    }
}
