using OptiRoute.Core.Models;

namespace OptiRoute.Core.Interfaces;

/// <summary>
/// Gerenciador de precedência e ordenação relativa das regras do OptiRoute no firewall.
/// Garante que:
/// OVERRIDES venham antes de DEFAULTS, e DEFAULTS venham imediatamente antes
/// da primeira regra genérica de saída/Load Balance da LAN, sem jamais mover regras externas do usuário.
/// </summary>
public interface IRuleOrderManager
{
    /// <summary>
    /// Inspeciona o ruleset da interface LAN no OPNsense e reordena relativamente
    /// apenas as regras do OptiRoute caso alguma esteja mal posicionada.
    /// </summary>
    Task<bool> EnsureRelativeOrderAsync(string lanInterface = "lan", CancellationToken ct = default);
}
