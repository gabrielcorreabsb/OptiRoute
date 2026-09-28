using System.Net;
using OptiRoute.Core.Models;

namespace OptiRoute.Core.Interfaces;

/// <summary>
/// Representa o estado consolidado e reconciliado de um aplicativo para o computador local.
/// </summary>
public sealed class EffectiveApplicationRoute
{
    public ApplicationIdentity Identity { get; set; } = null!;
    public string Executable => Identity.ExecutableName;
    public string DisplayName => Identity.DisplayName;

    /// <summary>Estado de reconciliação entre OPNsense e Windows QoS local.</summary>
    public ApplicationSyncState SyncState { get; set; } = ApplicationSyncState.Synchronized;

    /// <summary>Valor DSCP oficial (global se existir, ou local se for LocalOnly).</summary>
    public int Dscp { get; set; }

    /// <summary>Valor DSCP ativo no Windows local, se existir.</summary>
    public int? LocalDscp { get; set; }

    /// <summary>Valor DSCP configurado no OPNsense, se existir.</summary>
    public int? GlobalDscp { get; set; }

    /// <summary>Gateway padrão para toda a rede.</summary>
    public string DefaultGateway { get; set; } = string.Empty;

    /// <summary>Gateway efetivo que este computador utilizará.</summary>
    public string EffectiveGateway { get; set; } = string.Empty;

    /// <summary>Indica se este computador possui um override específico ativo.</summary>
    public bool HasOverride { get; set; }

    /// <summary>Gateway do override específico, caso exista.</summary>
    public string? OverrideGateway { get; set; }

    /// <summary>Motivo da rota: "Global Default", "Local Override" ou "Default LAN Routing".</summary>
    public string Reason
    {
        get
        {
            if (SyncState == ApplicationSyncState.LocalOnly)
                return "Default LAN Routing";

            return HasOverride ? "Local Override" : "Global Default";
        }
    }

    /// <summary>Dados da política QoS do Windows local, se encontrada.</summary>
    public LocalQosPolicy? LocalPolicy { get; set; }

    /// <summary>
    /// UUID da regra OPNsense DEFAULT correspondente (populado durante Sync).
    /// Usado pelo modal de InvalidTos para permitir "Forçar Windows → OPNsense".
    /// </summary>
    public string? RuleUuid { get; set; }

    /// <summary>
    /// Tos real (hex) lido da regra OPNsense durante Sync.
    /// Populado somente quando há divergência com o DSCP do description.
    /// </summary>
    public string? ActualTosHex { get; set; }

    /// <summary>
    /// Mensagem de inconsistência entre o DSCP parseado do description e o tos real
    /// da regra no OPNsense. <c>null</c> = regra íntegra ou não-OptiRoute.
    /// Defesa contra tampering manual do tos via UI do OPNsense.
    /// </summary>
    public string? TosMismatchDetail { get; set; }

    public bool IsSynchronized => SyncState == ApplicationSyncState.Synchronized;
    public bool IsLocalOnly    => SyncState == ApplicationSyncState.LocalOnly;
    public bool IsGlobalOnly   => SyncState == ApplicationSyncState.GlobalOnly;
    public bool IsConflict     => SyncState == ApplicationSyncState.Conflict;
    public bool IsQosActive    => LocalDscp.HasValue && LocalDscp.Value > 0;
    public bool IsTosMismatch  => TosMismatchDetail is not null;
}

/// <summary>
/// Resultado da sincronização multi-PC.
/// </summary>
public sealed class OptiRouteSyncResult
{
    public IPAddress LocalHostIp { get; set; } = null!;
    public IReadOnlyList<EffectiveApplicationRoute> Routes { get; set; } = [];
    public IReadOnlyList<Gateway> Gateways { get; set; } = [];
}

/// <summary>
/// Orquestrador de reconciliação real entre OPNsense e Windows QoS local.
/// Constrói a lista de aplicações via união (OPNsense UNION Windows QoS).
/// </summary>
public interface IOptiRouteSynchronizer
{
    /// <summary>
    /// Executa a reconciliação real entre OPNsense e Windows QoS local via união de estados.
    /// Nunca exclui políticas locais automaticamente.
    /// </summary>
    /// <param name="localHostIp">IP da máquina local para identificação de overrides.</param>
    /// <param name="progress">Reporter opcional de progresso (etapas, percentual, mensagem).</param>
    /// <param name="ct">Token de cancelamento.</param>
    Task<OptiRouteSyncResult> SyncAsync(
        IPAddress localHostIp,
        IProgress<SyncProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Promove uma política que existe apenas no Windows local para o OPNsense.
    /// Valida unicidade de DSCP via DscpRegistry; em caso de colisão, aloca um novo DSCP,
    /// atualiza o QoS local do Windows e cria a regra global no OPNsense.
    /// </summary>
    Task<string> PromoteLocalToGlobalAsync(
        string executable,
        string targetGateway,
        CancellationToken ct = default);

    /// <summary>
    /// Ativa uma aplicação existente no OPNsense neste Windows local (cria a política QoS local).
    /// </summary>
    Task ActivateGlobalOnLocalAsync(string executable, CancellationToken ct = default);

    /// <summary>
    /// Repara um conflito de DSCP, ajustando o QoS local do Windows para corresponder ao DSCP global do OPNsense.
    /// </summary>
    Task RepairConflictAsync(string executable, CancellationToken ct = default);

    /// <summary>
    /// Exclui a aplicação APENAS deste computador local (remove a política QoS do Windows sem tocar no OPNsense).
    /// </summary>
    Task RemoveLocalApplicationAsync(string executable, CancellationToken ct = default);

    /// <summary>
    /// Exclui a aplicação GLOBALMENTE no OPNsense (remove a regra DEFAULT e todos os OVERRIDES da rede).
    /// Não remove automaticamente as políticas dos Windows locais (aparecerão como LocalOnly para decisão de cada usuário).
    /// </summary>
    Task DeleteGlobalApplicationAsync(string executable, CancellationToken ct = default);

    /// <summary>
    /// Cadastra um novo aplicativo globalmente ou atualiza sua rota padrão.
    /// </summary>
    Task<string> RegisterOrUpdateApplicationAsync(
        ApplicationIdentity identity,
        string defaultGateway,
        int? specificDscp = null,
        CancellationToken ct = default);

    // ──────────────────────────────────────────────────────────────────────────
    // Pipeline de reconciliação (Delta 2)
    // BuildState → BuildPlan → ApplyPlan → Verify
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Constrói o plano de mutações a partir do estado consolidado.
    /// Sem side-effects. Idempotente.
    /// Rotas em <c>Synchronized</c> são omitidas (nenhuma ação).
    /// </summary>
    Task<ReconciliationPlan> BuildPlanAsync(
        OptiRouteSyncResult state,
        CancellationToken ct = default);

    /// <summary>
    /// Aplica o plano no Windows QoS local e/ou no OPNsense.
    /// Continua processando após uma falha parcial, retornando o resultado completo
    /// (<see cref="ReconciliationResult.Verified"/> + <see cref="ReconciliationResult.Failures"/>).
    /// Não re-sincroniza — cabe ao chamador disparar <see cref="SyncAsync"/> se quiser refresh.
    /// </summary>
    Task<ReconciliationResult> ApplyPlanAsync(
        ReconciliationPlan plan,
        CancellationToken ct = default);

    /// <summary>
    /// Re-lê o estado atual e verifica que cada ação do plano produziu o efeito esperado.
    /// Retorna resultado com Verified (ações confirmadas) e Failures (ações que não bateram).
    /// </summary>
    Task<ReconciliationResult> VerifyAsync(
        ReconciliationPlan plan,
        IPAddress localHostIp,
        CancellationToken ct = default);
}
