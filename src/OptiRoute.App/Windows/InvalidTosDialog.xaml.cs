using System.Windows;

namespace OptiRoute.App.Windows;

/// <summary>
/// Resultado da decisão do usuário no dialog de InvalidTos.
/// </summary>
public enum InvalidTosResolution
{
    /// <summary>Não aplicar nada — usuário cancelou.</summary>
    Cancel,
    /// <summary>Deletar regra OPNsense e recriar com tos correto (do Windows local).</summary>
    ForceWindowsToOpnsense,
    /// <summary>Manter regra OPNsense (tos atual) e ajustar QoS local para bater.</summary>
    ForceOpnsenseToWindows
}

/// <summary>
/// Modal apresentado quando uma regra do OptiRoute tem tos ≠ DSCP do description
/// (regra corrompida por edição manual na UI do OPNsense). Oferece 3 opções:
/// cancelar, forçar Windows para OPNsense (recreate), ou forçar OPNsense para Windows (default).
/// </summary>
public partial class InvalidTosDialog : Window
{
    public InvalidTosResolution Resolution { get; private set; } = InvalidTosResolution.Cancel;

    public InvalidTosDialog(
        string ruleDescription,
        int    descriptionDscp,
        int    actualTosDscp,
        string actualTosHex)
    {
        InitializeComponent();
        RuleDescriptionText.Text   = $"'{ruleDescription}'";
        DescriptionDscpText.Text    = $"DSCP {descriptionDscp}";
        ActualTosText.Text          = $"{actualTosHex} (DSCP {actualTosDscp})";
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Resolution = InvalidTosResolution.Cancel;
        DialogResult = false;
        Close();
    }

    private void OnForceWindowsClick(object sender, RoutedEventArgs e)
    {
        Resolution = InvalidTosResolution.ForceWindowsToOpnsense;
        DialogResult = true;
        Close();
    }

    private void OnForceOpnsenseClick(object sender, RoutedEventArgs e)
    {
        Resolution = InvalidTosResolution.ForceOpnsenseToWindows;
        DialogResult = true;
        Close();
    }
}
