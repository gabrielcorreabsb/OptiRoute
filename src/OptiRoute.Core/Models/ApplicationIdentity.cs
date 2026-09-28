namespace OptiRoute.Core.Models;

/// <summary>
/// Identidade lógica de um aplicativo no OptiRoute.
/// Possui um AppId único estável (GUID) criado no primeiro cadastro,
/// permitindo futuramente acrescentar hash, publisher ou Steam AppId sem quebrar o protocolo global.
/// </summary>
public sealed record ApplicationIdentity
{
    /// <summary>Identificador único global estável (GUID gerado no primeiro cadastro).</summary>
    public string AppId { get; init; } = Guid.NewGuid().ToString("D");

    /// <summary>Nome do binário executável para Windows QoS (ex: "bf6.exe").</summary>
    public string ExecutableName { get; init; } = string.Empty;

    /// <summary>Nome amigável exibido na interface (ex: "Battlefield 6").</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Caminho de instalação local opcional (específico de cada PC).</summary>
    public string? LocalInstallPath { get; init; }

    /// <summary>
    /// Cria uma nova identidade de aplicativo. Se <paramref name="existingAppId"/> for fornecido,
    /// preserva o identificador global estável existente.
    /// </summary>
    public static ApplicationIdentity Create(string exeName, string? displayName = null, string? existingAppId = null)
    {
        var cleanExe = Path.GetFileName(exeName).Trim().ToLowerInvariant();
        var defaultName = Path.GetFileNameWithoutExtension(cleanExe);

        return new ApplicationIdentity
        {
            AppId            = string.IsNullOrWhiteSpace(existingAppId) ? Guid.NewGuid().ToString("D") : existingAppId,
            ExecutableName   = cleanExe,
            DisplayName      = string.IsNullOrWhiteSpace(displayName) ? defaultName : displayName,
            LocalInstallPath = Path.IsPathRooted(exeName) ? exeName : null
        };
    }
}
