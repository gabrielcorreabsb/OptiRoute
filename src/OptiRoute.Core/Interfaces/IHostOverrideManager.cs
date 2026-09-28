using System.Net;
using OptiRoute.Core.Models;

namespace OptiRoute.Core.Interfaces;

/// <summary>
/// Gerencia overrides de rota por IP para computadores específicos da rede.
/// </summary>
public interface IHostOverrideManager
{
    /// <summary>
    /// Cria ou atualiza um override de rota para um computador específico.
    /// </summary>
    Task<string> SetOverrideAsync(
        ApplicationIdentity identity,
        IPAddress hostIp,
        string targetGateway,
        CancellationToken ct = default);

    /// <summary>
    /// Remove um override existente para um computador, revertendo-o à rota padrão global.
    /// </summary>
    Task<bool> RemoveOverrideAsync(
        string executable,
        IPAddress hostIp,
        CancellationToken ct = default);

    /// <summary>
    /// Consulta se há override ativo para um executável e IP de computador.
    /// </summary>
    Task<HostOverride?> GetOverrideAsync(
        string executable,
        IPAddress hostIp,
        CancellationToken ct = default);
}
