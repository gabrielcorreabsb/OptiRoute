namespace OptiRoute.Core.Models;

/// <summary>
/// Regra que associa um executável Windows a um perfil de roteamento.
/// Cada ApplicationRule gera uma QoS Policy no Windows com o DSCP do perfil associado.
/// </summary>
public sealed class ApplicationRule
{
    public Guid   Id               { get; set; } = Guid.NewGuid();

    /// <summary>Nome de exibição amigável (ex: "Battlefield 6").</summary>
    public string DisplayName      { get; set; } = string.Empty;

    /// <summary>Nome do executável apenas (ex: "bf6.exe").</summary>
    public string ExecutableName   { get; set; } = string.Empty;

    /// <summary>Caminho completo do executável, se disponível (ex: "C:\...\bf6.exe").</summary>
    public string? ExecutablePath  { get; set; }

    /// <summary>Perfil de roteamento a ser aplicado.</summary>
    public Guid   RoutingProfileId { get; set; }

    /// <summary>Status atual da regra no sistema.</summary>
    public ApplicationStatus Status   { get; set; } = ApplicationStatus.Configured;

    /// <summary>Descrição do último erro, se houver.</summary>
    public string? LastError          { get; set; }

    public bool   Enabled             { get; set; } = true;
    public DateTime CreatedAt         { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt         { get; set; } = DateTime.UtcNow;
}

public enum ApplicationStatus
{
    /// <summary>QoS policy criada e ativa no Windows.</summary>
    Configured,

    /// <summary>Regra desativada pelo usuário.</summary>
    Disabled,

    /// <summary>Erro ao criar ou manter a QoS policy.</summary>
    Error
}
