namespace OptiRoute.Core.Models;

/// <summary>
/// Tipos canônicos de ações que o pipeline de reconciliação pode emitir.
/// Mapeamento 1:1 com o switch em <c>OptiRouteSynchronizer.ApplyPlanAsync</c>.
/// Os 3 primeiros tocam o Windows QoS local; os 5 últimos tocam o firewall do OPNsense.
/// </summary>
public enum ReconciliationActionType
{
    /// <summary>Nenhuma ação necessária — incluído para exaustividade do switch.</summary>
    NoOp,

    // ── Windows QoS (lado local) ─────────────────────────────────────────────
    /// <summary>Criar uma nova NetQosPolicy para o executável.</summary>
    CreateQosPolicy,

    /// <summary>Atualizar o DSCP de uma NetQosPolicy existente (resolver conflito).</summary>
    UpdateQosPolicy,

    /// <summary>Remover uma NetQosPolicy local órfã (sem regra global).</summary>
    DeleteLocalQosPolicy,

    // ── OPNsense firewall (lado global) ──────────────────────────────────────
    /// <summary>Criar uma regra DEFAULT ou OVERRIDE no OPNsense.</summary>
    CreateFirewallRule,

    /// <summary>Atualizar DSCP e/ou gateway de uma regra existente.</summary>
    UpdateFirewallRule,

    /// <summary>Reordenar a regra para garantir precedência OVERRIDE &gt; DEFAULT &gt; LAN.</summary>
    MoveFirewallRule,

    /// <summary>Remover uma regra DEFAULT ou OVERRIDE do OPNsense.</summary>
    DeleteFirewallRule,

    /// <summary>
    /// Deletar e recriar uma regra do OPNsense cujo campo <c>tos</c> não bate
    /// com o DSCP declarado no description (regra corrompida por edição manual).
    /// A ação carrega um <see cref="OPNsenseRuleRequest"/> completo em
    /// <c>ReconciliationAction.OpnsenseRequest</c>.
    /// </summary>
    RecreateFirewallRule
}
