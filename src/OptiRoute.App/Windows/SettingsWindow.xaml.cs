using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Win32;
using OptiRoute.App.Services;
using OptiRoute.App.ViewModels;

namespace OptiRoute.App.Windows;

public partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow(SettingsViewModel viewModel, bool isFirstRun = false)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        viewModel.IsFirstRun = isFirstRun;
        ViewModel.Saved += (_, _) => { DialogResult = true; Close(); };
        Loaded += async (_, _) => await ViewModel.LoadGatewaysAsync();
    }

    /// <summary>
    /// Avança do Welcome panel para o TabControl (modo first-run wizard).
    /// </summary>
    private void OnWizardNextClick(object sender, RoutedEventArgs e)
    {
        ViewModel.IsFirstRun = false;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnToggleSecretClick(object sender, RoutedEventArgs e)
    {
        // Toggle PasswordBox <-> TextBox (simples: alterna o conteúdo visível no header da tab)
        // Implementação básica: mostra MessageBox com o secret (alternativa simples ao TextBox com PasswordChar).
        if (string.IsNullOrEmpty(ViewModel.ApiSecret))
        {
            MessageBox.Show(this, "(empty)", "API secret", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(this, ViewModel.ApiSecret, "API secret", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnImportKeyFileClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select OPNsense API key file",
            Filter = "OPNsense key files (*.txt)|*.txt|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == true)
        {
            ViewModel.ImportKeyFile(dlg.FileName);
        }
    }

    private void OnHowToCreateClick(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void SecretBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        // Hook: conecta PasswordBox.Password ao ViewModel.ApiSecret
        if (sender is System.Windows.Controls.PasswordBox pb)
            ViewModel.OnSecretBoxChanged(pb.Password);
    }
}
