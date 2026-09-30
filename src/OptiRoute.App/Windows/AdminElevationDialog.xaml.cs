using System.Windows;

namespace OptiRoute.App.Windows;

/// <summary>
/// Dialog bloqueante exibido no startup quando o processo NÃO está elevado.
/// Oferece reiniciar com privilégios de Administrador ("Restart elevated") ou
/// continuar mesmo assim (operações de QoS/firewall podem falhar).
/// </summary>
public partial class AdminElevationDialog : Window
{
    /// <summary>
    /// True quando o usuário optou por reiniciar elevado. O chamador (App) é
    /// responsável por relançar o executável com <c>Verb = "runas"</c> e encerrar
    /// esta instância; false = continuar sem privilégios.
    /// </summary>
    public bool RestartRequested { get; private set; }

    public AdminElevationDialog()
    {
        InitializeComponent();
    }

    private void OnRestartClick(object sender, RoutedEventArgs e)
    {
        RestartRequested = true;
        DialogResult = true;
        Close();
    }

    private void OnContinueClick(object sender, RoutedEventArgs e)
    {
        RestartRequested = false;
        DialogResult = true;
        Close();
    }
}
