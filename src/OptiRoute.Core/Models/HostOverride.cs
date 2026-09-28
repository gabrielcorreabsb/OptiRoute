using System.Net;

namespace OptiRoute.Core.Models;

/// <summary>
/// Override de rota específico para um IP na rede.
/// Permite que um computador específico redirecione um aplicativo para uma WAN diferente da padrão global.
/// </summary>
public sealed class HostOverride
{
    public ApplicationIdentity Identity { get; set; } = null!;

    public string Executable => Identity.ExecutableName;

    /// <summary>Valor DSCP associado globalmente a este aplicativo.</summary>
    public int Dscp { get; set; }

    /// <summary>IP de origem específico (ex: 10.0.0.122).</summary>
    public IPAddress SourceIp { get; set; } = null!;

    /// <summary>Gateway específico deste host (ex: "WAN1").</summary>
    public string Gateway { get; set; } = string.Empty;

    /// <summary>UUID da regra no OPNsense, se já persistida.</summary>
    public string? RuleUuid { get; set; }
}
