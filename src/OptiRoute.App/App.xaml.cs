using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OptiRoute.App.Logging;
using OptiRoute.App.Services;
using OptiRoute.App.ViewModels;
using OptiRoute.Core.Interfaces;
using OptiRoute.Core.Services;
using OptiRoute.OPNsense.Client;
using OptiRoute.Windows.QoS;
using OptiRoute.Windows.Security;

namespace OptiRoute.App;

public partial class App : Application
{
    /// <summary>
    /// Caminho padrão do log: %APPDATA%\OptiRoute\OptiRoute.log.
    /// Apêndice-only; rotação manual (smoke tests não geram volume).
    /// </summary>
    public static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OptiRoute",
        "OptiRoute.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Trunca log no startup para o smoke test ficar limpo. Em produção futura,
        // considerar rotação por tamanho.
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.WriteAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Information] OptiRoute: session started{Environment.NewLine}");
        }
        catch
        {
            // Se nem isso funcionar, segue sem log.
        }

        using var loggerFactory = LoggerFactory.Create(b => b
            .SetMinimumLevel(LogLevel.Information)
            .AddProvider(new FileLoggerProvider(LogPath)));

        var config = AppConfigManager.Load();

        // Locale detection (i18n): tenta persistência do usuário primeiro (config.json → Culture);
        // fallback para cultura do sistema. {x:Static} é avaliado em XAML parse-time, então
        // CurrentUICulture precisa estar setada antes da MainWindow ser construída.
        var persistedCulture = config.Culture;
        var initialCulture = !string.IsNullOrWhiteSpace(persistedCulture)
            ? new CultureInfo(persistedCulture)
            : CultureInfo.CurrentUICulture;
        Thread.CurrentThread.CurrentUICulture = initialCulture;
        Thread.CurrentThread.CurrentCulture     = initialCulture;
        CultureInfo.DefaultThreadCurrentUICulture = initialCulture;
        CultureInfo.DefaultThreadCurrentCulture     = initialCulture;

        var creds = SecretStore.LoadCredentials();

        // ─────────────────────────────────────────────────────────────────
        // First-run wizard: se config.json não existe, lança a SettingsWindow
        // em modo wizard ANTES de construir o opnClient (que precisa de credenciais
        // válidas — OpnsenseHttpClientFactory rejeita apiSecret vazio).
        // TestConnection do VM cria seu próprio client; LoadGatewaysAsync fica
        // null-safe via guard. Cancelar = Shutdown().
        // ─────────────────────────────────────────────────────────────────
        if (!AppConfigManager.Exists())
        {
            var wizardVm = new OptiRoute.App.ViewModels.SettingsViewModel(opnsenseClient: null!, isFirstRun: true);
            var wizard = new Windows.SettingsWindow(wizardVm, isFirstRun: true);
            if (wizard.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            // Reload config + creds após o wizard — host/secret podem ter mudado.
            config = AppConfigManager.Load();
            creds  = SecretStore.LoadCredentials();
        }

        var opnsenseSettings = new OpnsenseSettings
        {
            Host         = config.OpnsenseHost,
            ApiKey       = creds?.ApiKey ?? string.Empty,
            VerifyTls    = false,
            LanInterface = config.LanInterface
        };

        var httpClient = OpnsenseHttpClientFactory.Create(opnsenseSettings, creds?.ApiSecret ?? string.Empty);
        var opnClient = new OpnsenseClient(httpClient, loggerFactory.CreateLogger<OpnsenseClient>());

        var qosManager     = new WindowsQosManager(loggerFactory.CreateLogger<WindowsQosManager>());
        var dscpRegistry   = new DscpRegistry();
        var orderManager   = new RuleOrderManager(opnClient, loggerFactory.CreateLogger<RuleOrderManager>());
        var overrideManager = new HostOverrideManager(
            opnClient, dscpRegistry, orderManager, loggerFactory.CreateLogger<HostOverrideManager>());
        var synchronizer = new OptiRouteSynchronizer(
            opnClient, qosManager, dscpRegistry, orderManager, loggerFactory.CreateLogger<OptiRouteSynchronizer>());

        var mainVm = new MainViewModel(synchronizer, overrideManager, opnClient, loggerFactory.CreateLogger<MainViewModel>());
        // Sincroniza o ComboBox de idioma com a cultura persistida (ou a do sistema se primeira execução).
        mainVm.SelectedCulture = mainVm.AvailableCultures.FirstOrDefault(c => c.Code == initialCulture.Name)
                                ?? mainVm.AvailableCultures.First();

        var mainWindow = new MainWindow(mainVm);
        mainWindow.Show();
    }
}
