using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using OptiRoute.App.Properties;
using OptiRoute.App.Services;
using OptiRoute.Core.Interfaces;
using OptiRoute.OPNsense.Client;
using OptiRoute.Windows.Security;

namespace OptiRoute.App.ViewModels;

/// <summary>
/// ViewModel da <c>SettingsWindow.xaml</c>. Suporta modo normal (4 tabs) e modo first-run wizard
/// (passos sequenciais). Persistência transacional via <see cref="AppConfigManager.Save"/>.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly IOpnsenseClient _opnsenseClient;
    private string _apiKey = string.Empty;
    private string _apiSecret = string.Empty;
    private bool _isSecretVisible;
    private bool _isFirstRun;

    /// <summary>
    /// Indica se a janela está em modo wizard de primeira execução.
    /// Quando true, a UI mostra o Welcome panel em vez das 4 abas; alterna para false
    /// quando o usuário clica "Get Started" no Welcome panel.
    ///
    /// IMPORTANTE: o setter dispara <see cref="OnPropertyChanged"/> também para
    /// <see cref="IsNotFirstRun"/>. <c>IsNotFirstRun</c> é propriedade computed
    /// (get-only) usada em bindings XAML — WPF não tem como detectar mudanças em
    /// computed properties automaticamente, então temos que notificar manualmente.
    /// Sem isso, o TabControl ficaria stale no estado inicial (Visible desde o
    /// DataContext set, antes do ctor flipar IsFirstRun para true).
    /// </summary>
    public bool IsFirstRun
    {
        get => _isFirstRun;
        set
        {
            if (SetField(ref _isFirstRun, value))
                OnPropertyChanged(nameof(IsNotFirstRun));
        }
    }

    /// <summary>Conveniência para bindings XAML: visível quando NÃO é first-run.</summary>
    public bool IsNotFirstRun => !_isFirstRun;

    public SettingsViewModel(IOpnsenseClient opnsenseClient, bool isFirstRun = false)
    {
        _opnsenseClient = opnsenseClient;
        _isFirstRun     = isFirstRun;

        // Auto-test timer: DEVE ser criado ANTES de qualquer setter de OpnsenseHost/ApiKey/ApiSecret
        // que disparem ScheduleAutoTest() — caso contrário NRE no startup.
        _autoTestTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _autoTestTimer.Tick += (_, _) =>
        {
            _autoTestTimer.Stop();
            _ = RunAutoTestAsync();
        };

        var cfg = AppConfigManager.Load();
        OpnsenseHost      = cfg.OpnsenseHost;
        PreferredLocalIp  = cfg.PreferredLocalIp;
        ApiKey            = LoadApiKey();
        GatewayDisplayNames = new Dictionary<string, string>(cfg.GatewayDisplayNames, StringComparer.OrdinalIgnoreCase);
        StartWithWindows  = cfg.StartWithWindows;
        MinimizeToTray    = cfg.StartMinimizedToTray;
        ShowTechnical     = cfg.ShowTechnicalInformation;
        DscpPoolStart     = cfg.DscpPoolStart;
        DscpPoolEnd       = cfg.DscpPoolEnd;
        LogRetentionDays  = cfg.LogRetentionDays;

        AvailableCultures = new ObservableCollection<CultureOption>
        {
            new("en-US", "English"),
            new("pt-BR", "Português (BR)")
        };
        SelectedCulture = AvailableCultures.FirstOrDefault(c => c.Code == cfg.Culture)
                          ?? AvailableCultures.FirstOrDefault(c => c.Code == System.Globalization.CultureInfo.CurrentUICulture.Name)
                          ?? AvailableCultures.First();

        DetectIpCommand        = new RelayCommand(DetectIp);
        SaveCommand            = new RelayCommand(Save, () => CanSave);
        ResetDisplayNameCommand = new RelayCommand(p =>
        {
            if (p is GatewayDisplayName g) g.Reset();
        });
    }

    // ── Connection tab ───────────────────────────────────────────────────

    private string _opnsenseHost = string.Empty;
    public string OpnsenseHost
    {
        get => _opnsenseHost;
        set
        {
            if (SetField(ref _opnsenseHost, value))
            {
                OnPropertyChanged(nameof(OpnsenseHostError));
                OnPropertyChanged(nameof(HasErrors));
                OnPropertyChanged(nameof(CanSave));
                ScheduleAutoTest();
            }
        }
    }

    private string _preferredLocalIp = string.Empty;
    public string PreferredLocalIp
    {
        get => _preferredLocalIp;
        set
        {
            if (SetField(ref _preferredLocalIp, value))
            {
                OnPropertyChanged(nameof(PreferredIpError));
                OnPropertyChanged(nameof(HasErrors));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    private string _detectedIp = string.Empty;
    public string DetectedIp { get => _detectedIp; set => SetField(ref _detectedIp, value); }

    public ICommand DetectIpCommand { get; }

    private void DetectIp()
    {
        // LocalNetworkDetector é static
        DetectedIp = LocalNetworkDetector.DetectLocalIp()?.ToString() ?? "(no network detected)";
    }

    // ── Auto-test connection (debounced 500ms após typing) ──────────────

    private readonly DispatcherTimer _autoTestTimer;
    private TestStatus _testStatus = TestStatus.Idle;
    private string _testStatusText = Strings.Settings_Connection_AutoTestStatus_Idle;
    private string _testStatusTooltip = string.Empty;

    private static readonly SolidColorBrush SuccessBrush  = CreateFrozenBrush(Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly SolidColorBrush FailedBrush   = CreateFrozenBrush(Color.FromRgb(0xEF, 0x44, 0x44));
    private static readonly SolidColorBrush CheckingBrush = CreateFrozenBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly SolidColorBrush IdleBrush     = CreateFrozenBrush(Color.FromRgb(0xA1, 0xA1, 0xAA));

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var b = new SolidColorBrush(color);
        b.Freeze();
        return b;
    }

    public TestStatus TestStatus
    {
        get => _testStatus;
        private set
        {
            if (SetField(ref _testStatus, value))
            {
                OnPropertyChanged(nameof(TestStatusText));
                OnPropertyChanged(nameof(TestStatusBrush));
                OnPropertyChanged(nameof(TestStatusIcon));
            }
        }
    }

    public string TestStatusText
    {
        get => _testStatusText;
        private set => SetField(ref _testStatusText, value);
    }

    public Brush TestStatusBrush => _testStatus switch
    {
        TestStatus.Success  => SuccessBrush,
        TestStatus.Failed   => FailedBrush,
        TestStatus.Checking => CheckingBrush,
        _                   => IdleBrush,
    };

    public string TestStatusIcon => _testStatus switch
    {
        TestStatus.Success  => "✓",
        TestStatus.Failed   => "✗",
        TestStatus.Checking => "⟳",
        _                   => "○",
    };

    public string TestStatusTooltip
    {
        get => _testStatusTooltip;
        private set => SetField(ref _testStatusTooltip, value);
    }

    /// <summary>
    /// Agenda um auto-test 500ms após a última mudança em OpnsenseHost/ApiKey/ApiSecret.
    /// Cancela testes pendentes se o usuário continuar digitando.
    /// Belt-and-suspenders: `?.` no timer cobre cenários onde o VM ainda não terminou de
    /// inicializar (ex: algum caminho edge que dispare antes do ctor finalizar).
    /// </summary>
    private void ScheduleAutoTest()
    {
        TestStatus = TestStatus.Checking;
        TestStatusText = Strings.Settings_Connection_AutoTestStatus_Checking;
        _autoTestTimer?.Stop();
        _autoTestTimer?.Start();
    }

    private async Task RunAutoTestAsync()
    {
        if (string.IsNullOrWhiteSpace(OpnsenseHost)
            || string.IsNullOrWhiteSpace(ApiKey)
            || string.IsNullOrWhiteSpace(ApiSecret))
        {
            TestStatus = TestStatus.Idle;
            TestStatusText = Strings.Settings_Connection_AutoTestStatus_Idle;
            TestStatusTooltip = string.Empty;
            Gateways.Clear();
            GatewaysStatus = Strings.Settings_Gateways_WaitingForCredentials;
            return;
        }
        try
        {
            var settings = new OpnsenseSettings { Host = OpnsenseHost, ApiKey = ApiKey, VerifyTls = false };
            using var http = OpnsenseHttpClientFactory.Create(settings, ApiSecret);
            var opn = new OpnsenseClient(http,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<OptiRoute.OPNsense.Client.OpnsenseClient>.Instance);
            var version = await opn.GetVersionAsync();
            TestStatus = TestStatus.Success;
            TestStatusText = Strings.Settings_Connection_AutoTestStatus_Success;
            TestStatusTooltip = Strings.Settings_Connection_TestSuccess(version, "—");
            // Auto-load gateways ao validar credenciais (mesmo client que passou no test).
            await LoadGatewaysFromClientAsync(opn);
        }
        catch (HttpRequestException ex)
        {
            TestStatus = TestStatus.Failed;
            TestStatusText = Strings.Settings_Connection_AutoTestStatus_Failed;
            TestStatusTooltip = $"{Strings.Settings_Connection_TestFailedNetwork} ({ex.Message})";
            Gateways.Clear();
            GatewaysStatus = Strings.Settings_Gateways_WaitingForCredentials;
        }
        catch (Exception ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            TestStatus = TestStatus.Failed;
            TestStatusText = Strings.Settings_Connection_AutoTestStatus_Failed;
            TestStatusTooltip = Strings.Settings_Connection_TestFailedAuth;
            Gateways.Clear();
            GatewaysStatus = Strings.Settings_Gateways_WaitingForCredentials;
        }
        catch (Exception ex)
        {
            TestStatus = TestStatus.Failed;
            TestStatusText = Strings.Settings_Connection_AutoTestStatus_Failed;
            TestStatusTooltip = ex.Message;
            Gateways.Clear();
            GatewaysStatus = Strings.Settings_Gateways_WaitingForCredentials;
        }
    }

    // ── Credentials tab ─────────────────────────────────────────────────

    public string ApiKey
    {
        get => _apiKey;
        set
        {
            if (SetField(ref _apiKey, value))
            {
                OnPropertyChanged(nameof(ApiKeyError));
                OnPropertyChanged(nameof(HasErrors));
                OnPropertyChanged(nameof(CanSave));
                ScheduleAutoTest();
            }
        }
    }
    public string ApiSecret
    {
        get => _apiSecret;
        set
        {
            if (SetField(ref _apiSecret, value))
            {
                OnPropertyChanged(nameof(ApiSecretError));
                OnPropertyChanged(nameof(HasErrors));
                OnPropertyChanged(nameof(CanSave));
                ScheduleAutoTest();
            }
        }
    }
    public bool IsSecretVisible { get => _isSecretVisible; set => SetField(ref _isSecretVisible, value); }

    public string SecretBoxActualValue
    {
        get => ApiSecret;
        set { ApiSecret = value; }
    }

    public string ImportStatus { get; set; } = string.Empty;

    private string LoadApiKey()
    {
        var creds = SecretStore.LoadCredentials();
        return creds?.ApiKey ?? string.Empty;
    }

    public void OnSecretBoxChanged(string newValue) => ApiSecret = newValue;

    /// <summary>Lê arquivo .txt do OPNsense (key=...\nsecret=...) e popula ApiKey/ApiSecret.</summary>
    public bool ImportKeyFile(string path)
    {
        try
        {
            var creds = OPNsenseKeyFileParser.ParseContent(File.ReadAllText(path));
            if (creds is null) { ImportStatus = Strings.Settings_Credentials_ImportFailed; return false; }
            ApiKey = creds.ApiKey;
            ApiSecret = creds.ApiSecret;
            ImportStatus = Strings.Settings_Credentials_ImportSuccess;
            return true;
        }
        catch
        {
            ImportStatus = Strings.Settings_Credentials_ImportFailed;
            return false;
        }
    }

    // ── Gateways tab ─────────────────────────────────────────────────────

    public Dictionary<string, string> GatewayDisplayNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<GatewayDisplayName> Gateways { get; } = new();

    public string GatewaysStatus { get; set; } = Strings.Settings_Gateways_Loading;

    public ICommand ResetDisplayNameCommand { get; }

    /// <summary>
    /// Carrega lista de gateways do OPNsense e mescla com display names existentes.
    /// Em modo wizard (first-run) onde <see cref="_opnsenseClient"/> ainda é null, constrói
    /// um cliente efêmero com os valores já digitados (mesmo padrão do auto-test).
    /// </summary>
    public async Task LoadGatewaysAsync(CancellationToken ct = default)
    {
        GatewaysStatus = Strings.Settings_Gateways_Loading;

        IOpnsenseClient? client = _opnsenseClient;
        if (client is null)
        {
            if (string.IsNullOrWhiteSpace(OpnsenseHost)
                || string.IsNullOrWhiteSpace(ApiKey)
                || string.IsNullOrWhiteSpace(ApiSecret))
            {
                GatewaysStatus = Strings.Settings_Gateways_WaitingForCredentials;
                return;
            }
            var settings = new OpnsenseSettings { Host = OpnsenseHost, ApiKey = ApiKey, VerifyTls = false };
            var http = OpnsenseHttpClientFactory.Create(settings, ApiSecret);
            client = new OpnsenseClient(http,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<OptiRoute.OPNsense.Client.OpnsenseClient>.Instance);
        }

        await LoadGatewaysFromClientAsync(client, ct);
    }

    /// <summary>
    /// Helper compartilhado entre <see cref="LoadGatewaysAsync"/> (entrada via UI) e
    /// <see cref="RunAutoTestAsync"/> (auto-load após o auto-test passar).
    /// </summary>
    private async Task LoadGatewaysFromClientAsync(IOpnsenseClient client, CancellationToken ct = default)
    {
        try
        {
            var list = await client.GetGatewaysAsync(ct);
            Gateways.Clear();
            foreach (var gw in list)
            {
                Gateways.Add(new GatewayDisplayName(gw.Name, gw.Status.ToString())
                {
                    DisplayName = GatewayDisplayNames.TryGetValue(gw.Name, out var dn) ? dn : gw.Name
                });
            }
            GatewaysStatus = list.Count == 0 ? Strings.Settings_Gateways_NoneFound : string.Empty;
        }
        catch (Exception ex)
        {
            GatewaysStatus = $"{Strings.Settings_Gateways_NoneFound} ({ex.Message})";
        }
    }

    // ── Advanced tab ────────────────────────────────────────────────────

    public ObservableCollection<CultureOption> AvailableCultures { get; }
    private CultureOption? _selectedCulture;
    public CultureOption? SelectedCulture { get => _selectedCulture; set => SetField(ref _selectedCulture, value); }

    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray   { get; set; }
    public bool ShowTechnical    { get; set; }
    public bool EnableAdvancedDscp { get; set; }

    private int _dscpPoolStart;
    public int DscpPoolStart
    {
        get => _dscpPoolStart;
        set
        {
            if (SetField(ref _dscpPoolStart, value))
            {
                OnPropertyChanged(nameof(DscpError));
                OnPropertyChanged(nameof(HasErrors));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    private int _dscpPoolEnd;
    public int DscpPoolEnd
    {
        get => _dscpPoolEnd;
        set
        {
            if (SetField(ref _dscpPoolEnd, value))
            {
                OnPropertyChanged(nameof(DscpError));
                OnPropertyChanged(nameof(HasErrors));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    private int _logRetentionDays;
    public int LogRetentionDays
    {
        get => _logRetentionDays;
        set
        {
            if (SetField(ref _logRetentionDays, value))
            {
                OnPropertyChanged(nameof(LogRetentionError));
                OnPropertyChanged(nameof(HasErrors));
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    // ── Validation ───────────────────────────────────────────────────────

    /// <summary>Mensagem de erro do campo OPNsense host (vazia = válido).</summary>
    public string OpnsenseHostError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(OpnsenseHost))
                return Strings.Settings_Validation_HostRequired;
            if (!Uri.TryCreate(OpnsenseHost, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return Strings.Settings_Validation_HostInvalid;
            return string.Empty;
        }
    }

    /// <summary>Mensagem de erro do campo preferred local IP (vazia = válido ou não preenchido).</summary>
    public string PreferredIpError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PreferredLocalIp)) return string.Empty;
            return System.Net.IPAddress.TryParse(PreferredLocalIp, out var ip)
                   && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                ? string.Empty
                : Strings.Settings_Validation_PreferredIpInvalid;
        }
    }

    /// <summary>Mensagem de erro do campo API key (vazia = válido).</summary>
    public string ApiKeyError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ApiKey)) return Strings.Settings_Validation_ApiKeyRequired;
            if (ApiKey.Length < 20) return Strings.Settings_Validation_ApiKeyTooShort;
            return string.Empty;
        }
    }

    /// <summary>Mensagem de erro do campo API secret (vazia = válido).</summary>
    public string ApiSecretError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ApiSecret)) return Strings.Settings_Validation_ApiSecretRequired;
            if (ApiSecret.Length < 20) return Strings.Settings_Validation_ApiSecretTooShort;
            return string.Empty;
        }
    }

    /// <summary>Mensagem de erro do pool DSCP (vazia = válido).</summary>
    public string DscpError
    {
        get
        {
            if (DscpPoolStart < 0 || DscpPoolStart > 63) return Strings.Settings_Validation_DscpRangeInvalid;
            if (DscpPoolEnd   < 0 || DscpPoolEnd   > 63) return Strings.Settings_Validation_DscpRangeInvalid;
            if (DscpPoolStart > DscpPoolEnd)             return Strings.Settings_Validation_DscpRangeInvalid;
            return string.Empty;
        }
    }

    /// <summary>Mensagem de erro da retenção de log (vazia = válido).</summary>
    public string LogRetentionError
    {
        get => (LogRetentionDays < 1 || LogRetentionDays > 365)
            ? Strings.Settings_Validation_LogRetentionInvalid
            : string.Empty;
    }

    /// <summary>True se qualquer regra de validação falhou.</summary>
    public bool HasErrors =>
        !string.IsNullOrEmpty(OpnsenseHostError)
     || !string.IsNullOrEmpty(PreferredIpError)
     || !string.IsNullOrEmpty(ApiKeyError)
     || !string.IsNullOrEmpty(ApiSecretError)
     || !string.IsNullOrEmpty(DscpError)
     || !string.IsNullOrEmpty(LogRetentionError);

    /// <summary>True se o Save pode ser habilitado. Usado pelo SaveCommand.CanExecute.</summary>
    public bool CanSave => !HasErrors;

    /// <summary>Lista de mensagens de erro (para ValidationSummary pattern).</summary>
    public System.Collections.Generic.IEnumerable<string> ValidationErrors
    {
        get
        {
            if (!string.IsNullOrEmpty(OpnsenseHostError))   yield return OpnsenseHostError;
            if (!string.IsNullOrEmpty(PreferredIpError))    yield return PreferredIpError;
            if (!string.IsNullOrEmpty(ApiKeyError))         yield return ApiKeyError;
            if (!string.IsNullOrEmpty(ApiSecretError))      yield return ApiSecretError;
            if (!string.IsNullOrEmpty(DscpError))           yield return DscpError;
            if (!string.IsNullOrEmpty(LogRetentionError))   yield return LogRetentionError;
        }
    }

    // ── Save ────────────────────────────────────────────────────────────

    public ICommand SaveCommand { get; }

    private void Save()
    {
        var cfg = AppConfigManager.Load();
        cfg.OpnsenseHost             = OpnsenseHost;
        cfg.PreferredLocalIp         = PreferredLocalIp;
        cfg.GatewayDisplayNames      = new Dictionary<string, string>(GatewayDisplayNames);
        cfg.StartWithWindows         = StartWithWindows;
        cfg.StartMinimizedToTray     = MinimizeToTray;
        cfg.ShowTechnicalInformation = ShowTechnical;
        cfg.DscpPoolStart            = DscpPoolStart;
        cfg.DscpPoolEnd              = DscpPoolEnd;
        cfg.LogRetentionDays         = LogRetentionDays;
        if (SelectedCulture is not null) cfg.Culture = SelectedCulture.Code;

        AppConfigManager.Save(cfg);
        SecretStore.SaveCredentials(new OpnsenseCredentials(ApiKey, ApiSecret));

        // Sinaliza para o code-behind fechar a janela
        Saved?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Saved;
}

/// <summary>
/// Status do auto-test de conexão. Idle = sem credenciais; Checking = typing/waiting;
/// Success = API respondeu; Failed = erro de rede/auth/timeout.
/// </summary>
public enum TestStatus
{
    Idle,
    Checking,
    Success,
    Failed
}

public sealed class GatewayDisplayName : ViewModelBase
{
    public string OpnsenseName { get; }
    public string Status { get; }

    private string _displayName = string.Empty;
    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetField(ref _displayName, value))
                OnPropertyChanged(nameof(IsCustomized));
        }
    }

    /// <summary>
    /// True quando o usuário customizou o DisplayName para algo diferente do OpnsenseName.
    /// Drives Visibility do botão Reset na coluna Ações (esconde quando não há customização
    /// a reverter — evita confusão de "botão visível que não faz nada").
    /// </summary>
    public bool IsCustomized => !string.Equals(_displayName, OpnsenseName, StringComparison.Ordinal);

    public GatewayDisplayName(string opnsenseName, string status)
    {
        OpnsenseName = opnsenseName;
        Status = status;
        _displayName = opnsenseName;
    }

    public void Reset() => DisplayName = OpnsenseName;
}
