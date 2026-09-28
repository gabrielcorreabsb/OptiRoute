namespace OptiRoute.Core.Models;

/// <summary>
/// Perfil de roteamento associado a um valor DSCP.
/// Cada perfil representa uma intenção de roteamento (ex: "Fernando NET", "Load Balance").
/// Uma regra no OPNsense é criada por perfil — não por aplicativo.
/// </summary>
public sealed class RoutingProfile
{
    public Guid   Id                  { get; set; } = Guid.NewGuid();
    public string Name                { get; set; } = string.Empty;

    /// <summary>
    /// Valor DSCP (0–63) que identifica este perfil.
    /// DSCP 0 é reservado para tráfego padrão (sem política OptiRoute).
    /// </summary>
    public int         DscpValue          { get; set; }
    public RoutingMode Mode               { get; set; } = RoutingMode.Default;

    /// <summary>Nome exato do gateway no OPNsense (ex: "WAN_FERNANDO").</summary>
    public string? Gateway           { get; set; }

    /// <summary>Nome exato do grupo de gateways no OPNsense (ex: "MULTIWAN_LB").</summary>
    public string? GatewayGroup      { get; set; }

    /// <summary>UUID da regra criada no OPNsense para este perfil.</summary>
    public string? OPNsenseRuleUuid  { get; set; }

    /// <summary>
    /// Se true, ao alterar o perfil de um aplicativo o OptiRoute solicitará
    /// reset das conexões existentes no OPNsense (feature V0.2).
    /// </summary>
    public bool   ClearStatesOnChange { get; set; }
    public bool   Enabled             { get; set; } = true;
    public DateTime CreatedAt         { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt         { get; set; } = DateTime.UtcNow;
}

public enum RoutingMode
{
    /// <summary>Sem política específica — segue rota padrão do OPNsense.</summary>
    Default,

    /// <summary>Direciona para um único gateway específico.</summary>
    SingleGateway,

    /// <summary>Direciona para um grupo de gateways (load balance ou failover).</summary>
    GatewayGroup
}
