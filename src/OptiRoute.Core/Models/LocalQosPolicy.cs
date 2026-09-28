namespace OptiRoute.Core.Models;

/// <summary>
/// Representa uma política QoS existente no Windows local,
/// com informações detalhadas de procedência (Owner, PolicyStore).
/// </summary>
public sealed class LocalQosPolicy
{
    public const string ManagedPrefix = "OptiRoute-";

    /// <summary>Nome da política no Windows (ex: "OptiRoute-bf6").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Nome do binário executável associado (ex: "bf6.exe").</summary>
    public string ExecutableName { get; set; } = string.Empty;

    /// <summary>Valor DSCP configurado no Windows.</summary>
    public int Dscp { get; set; }

    /// <summary>Origem / proprietário da política (ex: "Group Policy (Machine)").</summary>
    public string? Owner { get; set; }

    /// <summary>Armazenamento da política (ex: "localhost", "ActiveStore").</summary>
    public string? PolicyStore { get; set; }

    /// <summary>Indica se a política pertence ao OptiRoute (prefixo "OptiRoute-").</summary>
    public bool IsOptiRouteManaged => Name.StartsWith(ManagedPrefix, StringComparison.OrdinalIgnoreCase);
}
