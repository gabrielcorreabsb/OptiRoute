namespace OptiRoute.Core.Models;

/// <summary>
/// Rota padrão de um aplicativo cadastrado no OptiRoute.
/// Define a WAN padrão para toda a rede quando não houver override local.
/// </summary>
public sealed class ApplicationRoute
{
    public ApplicationIdentity Identity { get; set; } = null!;

    public string Executable => Identity.ExecutableName;

    /// <summary>Valor DSCP associado globalmente a este aplicativo.</summary>
    public int Dscp { get; set; }

    /// <summary>Gateway padrão (ex: "WAN2", "WAN_PPPOE", "LB_IPV4").</summary>
    public string DefaultGateway { get; set; } = string.Empty;

    /// <summary>UUID da regra no OPNsense, se já persistida.</summary>
    public string? RuleUuid { get; set; }
}
