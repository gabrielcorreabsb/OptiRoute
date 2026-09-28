namespace OptiRoute.Core.Models;

/// <summary>Gateway do OPNsense com seu status em tempo real.</summary>
public sealed class Gateway
{
    /// <summary>Nome exato do gateway no OPNsense (ex: "WAN_FERNANDO").</summary>
    public string Name      { get; set; } = string.Empty;

    public GatewayStatus Status    { get; set; } = GatewayStatus.Unknown;

    /// <summary>Endereço IP do gateway.</summary>
    public string? Address  { get; set; }

    /// <summary>Latência em ms (ex: "12.4 ms").</summary>
    public string? DelayMs  { get; set; }

    /// <summary>Perda de pacotes (ex: "0.0 %").</summary>
    public string? PacketLoss { get; set; }

    /// <summary>Interface física do OPNsense (ex: "igb0").</summary>
    public string? Interface  { get; set; }

    public bool IsDefault   { get; set; }
    public bool IsGroup     { get; set; }
}

public enum GatewayStatus
{
    /// <summary>Gateway respondendo normalmente.</summary>
    Online,

    /// <summary>Gateway com perda de pacotes ou sem resposta.</summary>
    Offline,

    /// <summary>Status desconhecido (não consultado ainda).</summary>
    Unknown
}
