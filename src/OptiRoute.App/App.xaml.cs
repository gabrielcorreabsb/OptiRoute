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

        // Garante que %APPDATA%\OptiRoute existe ANTES de qualquer Trace/AppendAllText.
        // Em máquina limpa o diretório não existe e File.AppendAllText lançaria
        // DirectoryNotFoundException, abortando o OnStartup (janela nunca aparece).
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        }
        catch { /* best-effort: sem diretório, o Trace abaixo vira no-op */ }

        // ─── Global exception handlers ────────────────────────────────
        // Escrevem DIRETO no LogPath via File.AppendAllText (sem loggerFactory)
        // para garantir captura mesmo se o factory estiver disposed ou async void
        // handler tiver exception que escapa antes do try/catch local.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}] [FATAL-AppDomain] {args.ExceptionObject}{Environment.NewLine}");
            }
            catch { /* swallow */ }
        };
        System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += (_, args) =>
        {
            try
            {
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}] [FATAL-Dispatcher] {args.Exception}{Environment.NewLine}");
            }
            catch { /* swallow */ }
            args.Handled = true; // previne WPF shutdown por exception no dispatcher
        };

        // Strategic trace logs — gravam direto no LogPath via File.AppendAllText.
        // Cada ponto confirma que o processo passou por ali. Se o processo morre, a última
        // linha escrita antes do death isola o step que crashou.
        void Trace(string stage)
        {
            // Best-effort: logging NUNCA pode derrubar o startup (disco cheio,
            // permissões, diretório ausente). Engole exceções silenciosamente.
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] [Trace] {stage}{Environment.NewLine}");
            }
            catch { /* swallow */ }
        }

        Trace("OnStartup start");

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

        // Static para sobreviver ao fim do OnStartup (initializer estático roda na primeira referência).
// `using var` descartava o factory antes de InitializeAsync rodar, silenciando logs.
        var loggerFactory = LoggerFactory.Create(b => b
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
        // Single-window UX: não há mais wizard modal. O App abre direto na
        // MainWindow. O estado de primeira execução (config.json ausente) é
        // detectado pelo MainViewModel (IsFirstRun), que exibe o SettingsPanel
        // inline com o Welcome panel automaticamente. O opnClient é construído
        // com credenciais vazias (fallback) e fica null-safe até o usuário
        // salvar as configurações.
        // ─────────────────────────────────────────────────────────────────
        try
        {
            Trace("building opnsense settings");
            var opnsenseSettings = new OpnsenseSettings
            {
                Host         = config.OpnsenseHost,
                ApiKey       = creds?.ApiKey ?? string.Empty,
                VerifyTls    = false,
                LanInterface = config.LanInterface
            };

            Trace("creating HttpClient");
            // First-run (sem config.json): creds vazias. OpnsenseHttpClientFactory
            // rejeita secret vazio, então usamos um placeholder apenas para o app
            // conseguir abrir na MainWindow. Este client NÃO autentica; o
            // SettingsPanel inline (Welcome) constrói clientes efêmeros a partir das
            // credenciais digitadas. Após salvar, um restart constrói o client real.
            var apiSecret = creds?.ApiSecret ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiSecret))
            {
                Trace("no credentials — using placeholder secret (first-run)");
                apiSecret = "first-run-placeholder";
            }
            var httpClient = OpnsenseHttpClientFactory.Create(opnsenseSettings, apiSecret);
            Trace("creating OpnsenseClient");
            var opnClient = new OpnsenseClient(httpClient, loggerFactory.CreateLogger<OpnsenseClient>());

            Trace("creating WindowsQosManager");
            var qosManager     = new WindowsQosManager(loggerFactory.CreateLogger<WindowsQosManager>());
            Trace("creating managers (dscp/order/override/synchronizer)");
            var dscpRegistry   = new DscpRegistry();
            var orderManager   = new RuleOrderManager(opnClient, loggerFactory.CreateLogger<RuleOrderManager>());
            var overrideManager = new HostOverrideManager(
                opnClient, dscpRegistry, orderManager, loggerFactory.CreateLogger<HostOverrideManager>());
            var synchronizer = new OptiRouteSynchronizer(
                opnClient, qosManager, dscpRegistry, orderManager, loggerFactory.CreateLogger<OptiRouteSynchronizer>());

            Trace("creating MainViewModel");
            var mainVm = new MainViewModel(synchronizer, overrideManager, opnClient, loggerFactory.CreateLogger<MainViewModel>(), loggerFactory, qosManager, dscpRegistry);
            Trace("setting SelectedCulture");
            // Sincroniza o ComboBox de idioma com a cultura persistida (ou fallback en-US).
            mainVm.SelectedCulture = mainVm.AvailableCultures.FirstOrDefault(c => c.Code == config.Culture)
                                    ?? mainVm.AvailableCultures.FirstOrDefault(c => c.Code == "en-US")
                                    ?? mainVm.AvailableCultures.First();

            Trace("creating MainWindow");
            var mainWindow = new MainWindow(mainVm);
            // Force visible position: quando o user lança via PowerShell/cmd, a MainWindow
            // pode aparecer atrás do terminal (z-order). WindowStartupLocation=Manual + Left/Top
            // garantem que a window sempre aparece no centro do primary monitor, não off-screen.
            mainWindow.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
            mainWindow.Left = SystemParameters.PrimaryScreenWidth / 2 - mainWindow.Width / 2;
            mainWindow.Top = SystemParameters.PrimaryScreenHeight / 2 - mainWindow.Height / 2;
            if (mainWindow.Left < 0) mainWindow.Left = 0;
            if (mainWindow.Top < 0) mainWindow.Top = 0;
            mainWindow.ShowInTaskbar = true;
            Trace("MainWindow.Show()");
            mainWindow.Show();
            mainWindow.Activate();
            mainWindow.WindowState = System.Windows.WindowState.Normal;
            mainWindow.Topmost = true;
            mainWindow.Topmost = false;
            mainWindow.Focus();
            Trace("MainWindow shown + activated — OnStartup ending normally");
        }
        catch (Exception ex)
        {
            // Log diagnóstico: expõe a causa raiz de falhas ao construir a MainWindow.
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "OptiRoute", "OptiRoute.log");
                System.IO.File.AppendAllText(logPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}] [FATAL] OnStartup failed: {ex}\n");
            }
            catch { /* swallow — log path pode não estar acessível */ }

            System.Windows.MessageBox.Show(
                $"Failed to start OptiRoute:\n\n{ex.GetType().Name}: {ex.Message}\n\nDetails logged to %APPDATA%\\OptiRoute\\OptiRoute.log",
                "OptiRoute",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown();
        }
    }
}
