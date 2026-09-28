namespace OptiRoute.Core.Models;

/// <summary>
/// Plano de mutações pendentes produzido pela fase "Build Plan" do pipeline
/// de reconciliação (BuildState → BuildPlan → ApplyPlan → Verify).
///
/// Esta é a versão canônica do tipo introduzida no Delta 2:
/// <list type="bullet">
///   <item><c>ReconciliationActionType</c> substituindo o <c>string Type</c> da versão
///         placeholder do Delta 3 — type-safe e exaustivo via switch.</item>
///   <item>Campos estruturados (<c>Dscp</c>, <c>TargetGateway</c>, <c>RuleUuid</c>,
///         <c>LocalQosPolicyName</c>) para que <c>ApplyPlanAsync</c> não precise
///         re-descobrir o estado para cada ação.</item>
///   <item><see cref="ReconciliationResult"/> e <see cref="ReconciliationActionFailure"/>
///         para reportar sucesso/falha de Apply e Verify de forma uniforme.</item>
/// </list>
/// </summary>
public sealed record ReconciliationPlan
{
    /// <summary>Ações a executar. Pode ser vazio (estado já sincronizado).</summary>
    public IReadOnlyList<ReconciliationAction> Actions { get; init; } = Array.Empty<ReconciliationAction>();

    /// <summary>Indica se há ao menos uma ação a aplicar.</summary>
    public bool HasActions => Actions.Count > 0;

    /// <summary>Número total de ações no plano.</summary>
    public int ActionCount => Actions.Count;

    /// <summary>Plano vazio — conveniência para "nada a fazer".</summary>
    public static readonly ReconciliationPlan Empty = new();
}

/// <summary>
/// Ação individual de um plano de reconciliação. Representa uma mutação unitária
/// que será aplicada ao Windows QoS local e/ou ao OPNsense pelo
/// <c>OptiRouteSynchronizer.ApplyPlanAsync</c>.
/// </summary>
/// <param name="Type">
/// Tipo da ação. Define o dispatch no switch de <c>ApplyPlanAsync</c>.
/// </param>
/// <param name="Executable">
/// Nome do executável alvo (case-insensitive, já normalizado por
/// <see cref="System.IO.Path.GetFileName"/> + <c>ToLowerInvariant</c>).
/// </param>
/// <param name="AppId">
/// GUID da aplicação em formato "D" (ex: <c>f47ac10b-58cc-4372-a567-0e02b2c3d479</c>).
/// Pode estar vazio para compatibilidade legada.
/// </param>
/// <param name="SourceIp">
/// Endereço IP de origem. <c>null</c> para regra DEFAULT; preenchido para OVERRIDE.
/// </param>
/// <param name="Dscp">Valor DSCP alvo após a ação ser aplicada.</param>
/// <param name="TargetGateway">Nome do gateway no OPNsense (ex: <c>"WAN2"</c>).</param>
/// <param name="RuleUuid">
/// UUID da regra OPNsense existente. <c>null</c> para <c>CreateFirewallRule</c>;
/// preenchido para <c>UpdateFirewallRule</c>, <c>MoveFirewallRule</c>,
/// <c>DeleteFirewallRule</c>.
/// </param>
/// <param name="LocalQosPolicyName">
/// Nome da política QoS local (ex: <c>"OptiRoute-bf6"</c>). Preenchido para
/// todas as ações que tocam QoS — útil para idempotência e logging.
/// </param>
/// <param name="Description">Descrição human-readable para a UI / log.</param>
/// <param name="OpnsenseRequest">
/// Request OPNsense completo, usado por <see cref="ReconciliationActionType.RecreateFirewallRule"/>
/// para regerar uma regra corrompida (tos ≠ DSCP). <c>null</c> para ações que não tocam firewall.
/// </param>
public sealed record ReconciliationAction(
    ReconciliationActionType Type,
    string                   Executable,
    string                   AppId,
    string?                  SourceIp,
    int                      Dscp,
    string                   TargetGateway,
    string?                  RuleUuid,
    string?                  LocalQosPolicyName,
    string                   Description,
    OPNsenseRuleRequest?     OpnsenseRequest = null);

/// <summary>
/// Resultado de uma operação de <c>ApplyPlanAsync</c> ou <c>VerifyAsync</c>.
/// </summary>
/// <param name="Verified">
/// Ações processadas com sucesso. Após Apply: ações executadas; após Verify:
/// ações cujo pós-estado foi confirmado.
/// </param>
/// <param name="Failures">Ações que falharam, com o motivo preservado.</param>
public sealed record ReconciliationResult(
    IReadOnlyList<ReconciliationAction>         Verified,
    IReadOnlyList<ReconciliationActionFailure>  Failures)
{
    /// <summary>True se nenhuma ação falhou.</summary>
    public bool AllSucceeded => Failures.Count == 0;

    /// <summary>Quantidade de ações verificadas com sucesso.</summary>
    public int VerifiedCount => Verified.Count;

    /// <summary>Quantidade de ações que falharam.</summary>
    public int FailedCount => Failures.Count;

    /// <summary>Resultado vazio — conveniência para o caso trivial.</summary>
    public static readonly ReconciliationResult Empty = new(
        Array.Empty<ReconciliationAction>(),
        Array.Empty<ReconciliationActionFailure>());
}

/// <summary>
/// Falha individual durante Apply ou Verify, preservando a ação original e o motivo.
/// </summary>
/// <param name="Action">A ação que falhou.</param>
/// <param name="Reason">Descrição human-readable do motivo (ex: "DSCP divergente do esperado").</param>
public sealed record ReconciliationActionFailure(
    ReconciliationAction Action,
    string               Reason);
