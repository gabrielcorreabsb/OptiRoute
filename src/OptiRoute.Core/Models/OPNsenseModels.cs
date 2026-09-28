namespace OptiRoute.Core.Models;

/// <summary>
/// Solicitação de criação/atualização de regra de firewall no OPNsense.
/// </summary>
public sealed class OPNsenseRuleRequest
{
    /// <summary>
    /// Descrição estruturada da regra, sempre com marcador de versão v=1 ao final.
    /// Exemplo: <c>OPTIROUTE|DEFAULT|bf6.exe|33|f47ac10b-...|v=1</c>.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Categoria da regra no OPNsense (padrão: "OptiRoute").</summary>
    public string Category { get; set; } = "OptiRoute";

    /// <summary>Nome da interface no OPNsense (ex: "lan").</summary>
    public string Interface { get; set; } = "lan";

    /// <summary>IP de origem (ex: "10.0.0.122" para OVERRIDE, ou "any"/"lan" para DEFAULT).</summary>
    public string SourceIp { get; set; } = "any";

    /// <summary>Valor DSCP a ser correspondido (ex: 33).</summary>
    public int DscpValue { get; set; }

    /// <summary>
    /// Destino da regra. Padrão: "(self)" com <see cref="DestinationNot"/> = "1",
    /// gerando "! Este firewall" para nunca desviar tráfego de DNS/GUI/API locais.
    /// </summary>
    public string DestinationNet { get; set; } = "(self)";

    /// <summary>Inverter destino (1 = sim, gerando "! destination_net"). Padrão: 1.</summary>
    public string DestinationNot { get; set; } = "1";

    /// <summary>Nome exato do gateway no OPNsense (ex: "WAN2", "WAN_PPPOE", "LB_IPV4").</summary>
    public string Gateway { get; set; } = string.Empty;

    /// <summary>Número de sequência para ordenação relativa.</summary>
    public int Sequence { get; set; } = 1;
}

/// <summary>
/// Informação detalhada sobre uma regra de firewall lida do OPNsense.
/// </summary>
public sealed class FirewallRuleInfo
{
    public string Uuid { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int Sequence { get; set; }
    public string Interface { get; set; } = string.Empty;
    public string SourceNet { get; set; } = string.Empty;
    public string DestinationNet { get; set; } = string.Empty;
    public string DestinationNot { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Valor TOS (Type of Service) como string hex retornado pelo OPNsense.
    /// Ex.: <c>"0x68"</c> = DSCP 26 (26 * 4 = 104 = 0x68).
    /// Defesa contra tampering manual: precisa bater com o DSCP do <see cref="Descriptor"/>.
    /// </summary>
    public string Tos { get; set; } = string.Empty;

    /// <summary>
    /// Descritor parseado caso seja uma regra estruturada do OptiRoute.
    /// </summary>
    public OptiRouteRuleDescriptor? Descriptor =>
        OptiRouteRuleDescriptor.TryParse(Description, out var d) ? d : null;

    /// <summary>Indica se a regra pertence ao OptiRoute.</summary>
    public bool IsOptiRouteRule =>
        Category.Equals("OptiRoute", StringComparison.OrdinalIgnoreCase) ||
        Description.StartsWith(OptiRouteRuleDescriptor.Prefix, StringComparison.OrdinalIgnoreCase) ||
        Description.StartsWith("OPTIRoute_", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// DSCP efetivo lido do campo <see cref="Tos"/> (TOS byte = DSCP × 4).
    /// Retorna <c>null</c> se <see cref="Tos"/> estiver vazio ou não for hex válido.
    /// </summary>
    public int? TosDscp
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Tos)) return null;
            var s = Tos.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s[2..];
            else if (s.StartsWith("0X"))
                s = s[2..];
            if (!byte.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var b))
                return null;
            return b >> 2; // ToS byte = DSCP × 4
        }
    }

    /// <summary>
    /// <c>true</c> quando a regra é OptiRoute E tem <see cref="Tos"/> parseável
    /// E esse DSCP não bate com o do <see cref="Descriptor"/>.
    /// Indica tampering manual ou corrupção — App deve tratar como conflito especial.
    /// </summary>
    public bool HasTosMismatch
    {
        get
        {
            if (Descriptor is null) return false;
            var tosDscp = TosDscp;
            if (tosDscp is null) return false; // sem tos legível → sem julgamento
            return tosDscp.Value != Descriptor.Dscp;
        }
    }
}
