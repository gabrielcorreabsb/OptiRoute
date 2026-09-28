namespace OptiRoute.Core.Models;

/// <summary>Política QoS existente no Windows criada pelo OptiRoute.</summary>
public sealed class QosPolicy
{
    /// <summary>Nome da política (ex: "OptiRoute-bf6").</summary>
    public string Name           { get; set; } = string.Empty;

    /// <summary>Executável alvo (ex: "bf6.exe").</summary>
    public string AppPathName    { get; set; } = string.Empty;

    /// <summary>Valor DSCP configurado (0–63).</summary>
    public int    DscpAction     { get; set; }

    /// <summary>Perfil de rede (All, Domain, Public, Private).</summary>
    public string NetworkProfile { get; set; } = string.Empty;
}
