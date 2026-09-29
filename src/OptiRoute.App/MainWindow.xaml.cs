using System.Windows;
using OptiRoute.App.ViewModels;

namespace OptiRoute.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OptiRoute", "OptiRoute.log"), $"[{DateTime.Now:HH:mm:ss.fff}] [Trace] MainWindow ctor entry{Environment.NewLine}"); } catch { }
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += async (_, _) =>
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OptiRoute", "OptiRoute.log"), $"[{DateTime.Now:HH:mm:ss.fff}] [Trace] MainWindow.Loaded fired{Environment.NewLine}"); } catch { }
            if (_viewModel is not null)
            {
                // Phase 3: cria o NotifyIcon apenas se o usuário já habilitou
                // "Minimize to tray" (persistido). Default = OFF.
                _viewModel.RefreshMinimizeToTray();

                try { System.IO.File.AppendAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OptiRoute", "OptiRoute.log"), $"[{DateTime.Now:HH:mm:ss.fff}] [Trace] MainWindow.Loaded — before InitializeAsync{Environment.NewLine}"); } catch { }
                try
                {
                    await _viewModel.InitializeAsync();
                }
                catch (Exception ex)
                {
                    // async void → sem catch, WPF fecha app silenciosamente. Log + MessageBox.
                    try
                    {
                        var logPath = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "OptiRoute", "OptiRoute.log");
                        System.IO.File.AppendAllText(logPath,
                            $"[{DateTime.Now:HH:mm:ss.fff}] [FATAL] MainWindow.Loaded InitializeAsync: {ex}\n");
                    }
                    catch { /* swallow */ }
                    MessageBox.Show(this,
                        $"Failed to initialize OptiRoute:\n\n{ex.GetType().Name}: {ex.Message}",
                        "OptiRoute", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                }
            }
        };

        // Hook do botão "Settings" do header: MainViewModel apenas sinaliza; o
        // MainWindow exibe o SettingsPanel inline (não abre mais janela modal).
        if (_viewModel is not null)
        {
            _viewModel.OpenSettingsRequested += (_, _) => _viewModel.ShowSettingsPanel();
        }

        // Primeira execução (sem config.json): abre o Welcome panel inline
        // automaticamente — o usuário não precisa clicar em nada. Não há wizard
        // modal separado; tudo acontece dentro da MainWindow.
        if (_viewModel is not null && _viewModel.IsFirstRun)
        {
            _viewModel.ShowSettingsPanel();
        }
    }

    /// <summary>
    /// Cancel do SettingsPanel inline: esconde o painel e volta ao conteúdo normal.
    /// </summary>
    private void OnSettingsPanelCancelled(object sender, RoutedEventArgs e)
    {
        _viewModel?.HideSettingsPanel();
    }

    /// <summary>
    /// Save do SettingsPanel inline: esconde o painel e re-sincroniza para refletir
    /// as novas configurações (host/credenciais/gateways) na lista de aplicativos.
    /// </summary>
    private async void OnSettingsPanelSaved(object sender, RoutedEventArgs e)
    {
        _viewModel?.HideSettingsPanel();

        if (_viewModel is not null)
        {
            // Reconstrói o client com as credenciais/host recém-salvos ANTES de
            // sincronizar — sem isso o SyncAsync usaria o client stale (placeholder
            // de first-run ou credenciais antigas) até o próximo restart.
            await _viewModel.RebuildClientAsync();
            await _viewModel.SyncAsync();

            // Phase 3: reconcilia o tray com a preferência recém-salva
            // (cria/descarta o NotifyIcon conforme o checkbox).
            _viewModel.RefreshMinimizeToTray();
        }
    }
}