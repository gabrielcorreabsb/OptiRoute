namespace OptiRoute.Core.Models;

/// <summary>
/// Resultado da verificação pós-Apply de uma única rota (aplicativo), derivado do
/// estado relido por <c>OptiRouteSynchronizer.VerifyRoutesAsync</c>.
///
/// Complementa <see cref="ReconciliationResult"/>: enquanto este reporta falhas por
/// ação do plano, <see cref="RouteVerification"/> descreve o estado efetivo de cada
/// rota para renderização por-app na UI (badge ✓/⚠ no card).
/// </summary>
/// <param name="Executable">Nome do executável (case-insensitive, chave do app).</param>
/// <param name="QosActive">True quando existe política QoS local ativa (LocalDscp presente).</param>
/// <param name="DscpCorrect">True quando o DSCP local bate com o DSCP esperado/da regra.</param>
/// <param name="RuleActive">True quando há regra OPNsense associada (RuleUuid presente).</param>
/// <param name="RuleOrderValid">True quando a ordem relativa da regra está válida (aproximação pós-sync).</param>
/// <param name="GatewayReachable">True quando o gateway efetivo está configurado.</param>
/// <param name="FailureReason">Motivo legível da primeira verificação que falhou; <c>null</c> se tudo OK.</param>
public sealed record RouteVerification(
    string  Executable,
    bool    QosActive,
    bool    DscpCorrect,
    bool    RuleActive,
    bool    RuleOrderValid,
    bool    GatewayReachable,
    string? FailureReason);
