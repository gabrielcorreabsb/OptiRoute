using System.Windows;

namespace OptiRoute.App.Windows;

/// <summary>
/// Modal de confirmação destrutiva exibido antes de restaurar o OptiRoute aos padrões
/// de fábrica (apaga <c>config.json</c> + <c>credentials.bin</c> e reinicia o app).
/// Foco inicial no Cancel; o botão Reset usa o estilo destrutivo.
///
/// Uso:
/// <code>
/// var dlg = new ConfirmResetDialog { Owner = owner };
/// if (dlg.ShowDialog() == true) { /* reset */ }
/// </code>
/// </summary>
public partial class ConfirmResetDialog : Window
{
    public ConfirmResetDialog()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
