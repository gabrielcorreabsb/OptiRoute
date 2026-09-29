using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;
using OptiRoute.App.Properties;

namespace OptiRoute.App.Windows;

/// <summary>
/// Modal "About" com identidade do app, versão (lida do assembly), descrição,
/// licença MIT e links do repositório/issues. Estilo alinhado ao design system
/// (Tokens/Radius) usado no restante do App.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        VersionText.Text = Strings.About_Version(GetAppVersion());
        CopyrightText.Text = Strings.About_Copyright("Gabriel Correa");
    }

    /// <summary>
    /// Versão exibida: prioriza AssemblyInformationalVersion (remove o sufixo de
    /// metadata "+&lt;commit&gt;" quando presente) e faz fallback para a versão do assembly.
    /// </summary>
    private static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plusIndex = informational.IndexOf('+');
            return plusIndex >= 0 ? informational[..plusIndex] : informational;
        }

        return assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    private void OnLinkClick(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // best-effort: se não houver shell associado, não trava o dialog.
        }
        e.Handled = true;
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => Close();
}
