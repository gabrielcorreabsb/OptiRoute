using OptiRoute.Core.Models;

namespace OptiRoute.Core.Interfaces;

/// <summary>
/// Cliente de integração com a API REST do OPNsense.
/// Gerencia gateways e regras de firewall (Automation/Filter API).
/// </summary>
public interface IOpnsenseClient
{
    /// <summary>Testa a conectividade e autenticação com o OPNsense.</summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);

    /// <summary>Retorna a versão do OPNsense.</summary>
    Task<string> GetVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// Retorna a lista de gateways e grupos com seu status em tempo real.
    /// Usa GET /api/routes/gateway/status.
    /// </summary>
    Task<IReadOnlyList<Gateway>> GetGatewaysAsync(CancellationToken ct = default);

    /// <summary>
    /// Cria ou atualiza uma regra de firewall no OPNsense.
    /// Idempotente: busca por descrição antes de criar.
    /// Garante que a categoria referenciada em <paramref name="request"/>.Category
    /// exista no firewall antes de tentar criar a regra (auto-cria se faltar).
    /// Retorna o UUID da regra.
    /// </summary>
    Task<string> EnsureRuleExistsAsync(OPNsenseRuleRequest request, CancellationToken ct = default);

    /// <summary>
    /// Garante que uma categoria de firewall exista no OPNsense e retorna o UUID dela.
    /// Auto-cria no primeiro uso; chamadas subsequentes retornam do cache em memória.
    /// Pré-condição para <see cref="EnsureRuleExistsAsync"/>, que a invoca automaticamente
    /// para traduzir o nome da categoria em UUID antes do addRule (ModelRelationField exige UUID).
    /// </summary>
    /// <returns>UUID da categoria, ou <c>null</c> se <paramref name="name"/> for vazio.</returns>
    Task<string?> EnsureCategoryExistsAsync(string name, CancellationToken ct = default);

    /// <summary>Remove uma regra de firewall pelo UUID.</summary>
    Task DeleteRuleAsync(string uuid, CancellationToken ct = default);

    /// <summary>
    /// Aplica as alterações pendentes no OPNsense (POST /api/firewall/filter/apply).
    /// Deve ser chamado após qualquer criação/atualização/remoção de regras.
    /// </summary>
    Task ApplyRulesAsync(CancellationToken ct = default);

    /// <summary>Lista todas as regras de firewall pertencentes ao OptiRoute.</summary>
    Task<IReadOnlyList<FirewallRuleInfo>> ListOptiRouteRulesAsync(CancellationToken ct = default);

    /// <summary>
    /// Lista todas as regras de uma interface para análise de precedência e âncora.
    /// </summary>
    Task<IReadOnlyList<FirewallRuleInfo>> ListAllRulesAsync(string? interfaceName = null, CancellationToken ct = default);

    /// <summary>Atualiza o número de sequência de uma regra existente.</summary>
    Task UpdateRuleSequenceAsync(string uuid, int sequence, CancellationToken ct = default);
}
