using System.Globalization;
using System.Windows;
using OptiRoute.Core.Models;

namespace OptiRoute.App.Windows;

/// <summary>
/// Modal de confirmação exibido antes de aplicar um plano de reconciliação.
/// Agrupa as ações por categoria (Windows QoS / Firewall OPNsense), mostra um
/// resumo contável e exige confirmação explícita (foco inicial no Cancel).
///
/// Uso:
/// <code>
/// var dlg = new ConfirmApplyDialog(plan.Actions);
/// if (dlg.ShowDialog(owner) == true) { /* aplicar */ }
/// </code>
/// </summary>
public partial class ConfirmApplyDialog : Window
{
    /// <summary>Ações que tocam o Windows QoS local.</summary>
    public IReadOnlyList<ReconciliationAction> QosActions { get; }

    /// <summary>Ações que tocam as regras de firewall do OPNsense.</summary>
    public IReadOnlyList<ReconciliationAction> FirewallActions { get; }

    /// <summary>True se há ao menos uma ação de QoS a exibir.</summary>
    public bool HasQosActions => QosActions.Count > 0;

    /// <summary>True se há ao menos uma ação de firewall a exibir.</summary>
    public bool HasFirewallActions => FirewallActions.Count > 0;

    /// <summary>True quando não há nenhuma ação (estado vazio defensivo).</summary>
    public bool HasNoActions => !HasQosActions && !HasFirewallActions;

    /// <summary>Texto resumido: "X QoS changes, Y firewall changes".</summary>
    public string SummaryText { get; }

    public ConfirmApplyDialog(IReadOnlyList<ReconciliationAction> actions)
    {
        InitializeComponent();

        QosActions = actions
            .Where(a => a.Type is ReconciliationActionType.CreateQosPolicy
                           or ReconciliationActionType.UpdateQosPolicy
                           or ReconciliationActionType.DeleteLocalQosPolicy)
            .ToList();

        FirewallActions = actions
            .Where(a => a.Type is ReconciliationActionType.CreateFirewallRule
                           or ReconciliationActionType.UpdateFirewallRule
                           or ReconciliationActionType.MoveFirewallRule
                           or ReconciliationActionType.DeleteFirewallRule
                           or ReconciliationActionType.RecreateFirewallRule)
            .ToList();

        var format = Properties.Strings.ResourceManager.GetString("Card.ConfirmApply.ActionSummary", Properties.Strings.Culture)
                     ?? "{0} QoS changes, {1} firewall changes";
        SummaryText = string.Format(CultureInfo.CurrentCulture, format, QosActions.Count, FirewallActions.Count);

        DataContext = this;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
