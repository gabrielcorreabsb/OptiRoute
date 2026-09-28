using OptiRoute.Core.Models;

namespace OptiRoute.Core.Interfaces;

/// <summary>
/// Gerencia políticas QoS baseadas em aplicativo no Windows.
/// Todas as operações são idempotentes.
/// Requer privilégios de Administrador.
/// </summary>
public interface IWindowsQosManager
{
    /// <summary>Verifica se uma política OptiRoute existe para o executável informado.</summary>
    Task<bool> PolicyExistsAsync(string executableName, CancellationToken ct = default);

    /// <summary>
    /// Cria (ou recria) uma política QoS para o executável com o valor DSCP especificado.
    /// Se a política já existir com DSCP diferente, ela é atualizada.
    /// </summary>
    Task CreatePolicyAsync(string executableName, int dscp, CancellationToken ct = default);

    /// <summary>
    /// Remove a política QoS do executável.
    /// Não lança exceção se a política não existir.
    /// </summary>
    Task DeletePolicyAsync(string executableName, CancellationToken ct = default);

    /// <summary>
    /// Remove uma política QoS específica considerando seu Store e Owner.
    /// </summary>
    Task DeletePolicyAsync(LocalQosPolicy policy, CancellationToken ct = default);

    /// <summary>Atualiza o valor DSCP de uma política existente.</summary>
    Task UpdatePolicyAsync(string executableName, int dscp, CancellationToken ct = default);

    /// <summary>Lista todas as políticas QoS criadas pelo OptiRoute (prefixo "OptiRoute-").</summary>
    Task<IReadOnlyList<QosPolicy>> ListOptiRoutePoliciesAsync(CancellationToken ct = default);

    /// <summary>
    /// Lista todas as políticas QoS do Windows gerenciadas pelo OptiRoute,
    /// incluindo informações detalhadas de Owner e PolicyStore.
    /// </summary>
    Task<IReadOnlyList<LocalQosPolicy>> ListLocalPoliciesAsync(CancellationToken ct = default);

    /// <summary>
    /// Sincroniza políticas QoS do Windows com as regras configuradas no banco.
    /// Recria políticas faltantes e remove políticas obsoletas.
    /// </summary>
    Task SyncAllAsync(
        IEnumerable<ApplicationRule> rules,
        IEnumerable<RoutingProfile>  profiles,
        CancellationToken ct = default);
}
