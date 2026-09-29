using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using OptiRoute.App.Properties;
using OptiRoute.App.Services;
using OptiRoute.App.Views;
using OptiRoute.App.Windows;
using OptiRoute.Core.Interfaces;
using OptiRoute.Core.Models;
using OptiRoute.Core.Services;
using OptiRoute.OPNsense.Client;
using OptiRoute.Windows.QoS;
using OptiRoute.Windows.Security;
using ApplicationIdentity = OptiRoute.Core.Models.ApplicationIdentity;

namespace OptiRoute.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    // Mutáveis: RebuildClientAsync substitui estes três quando o usuário salva
    // novas credenciais/host no SettingsPanel (sem exigir restart do App).
    private IOptiRouteSynchronizer _synchronizer;
    private IHostOverrideManager   _overrideManager;
    private IOpnsenseClient        _client;

    // Reutilizados no rebuild (não dependem do OPNsense client).
    private readonly IWindowsQosManager     _qosManager;
    private readonly IDscpRegistry          _dscpRegistry;

    private readonly ILogger<MainViewModel> _logger;
    private readonly ILoggerFactory         _loggerFactory;

    // ── Tray opcional (OFF por padrão) ──────────────────────────────────────
    private TrayIcon? _trayIcon;

    /// <summary>
    /// True quando "Minimize to tray when closing" está habilitado e o tray foi
    /// efetivamente criado. Só vira true por ação explícita do usuário (Settings) —
    /// nunca no startup default.
    /// </summary>
    public bool IsMinimizeToTrayEnabled { get; private set; }

    /// <summary>
    /// Setado quando o usuário pede shutdown explícito (menu "Quit" do tray).
    /// Enquanto false, fechar a janela com o tray ativo apenas a esconde.
    /// </summary>
    public bool IsExplicitShutdown { get; set; }

    /// <summary>
    /// Culturas suportadas pelo App (i18n). Exibidas no ComboBox de idioma da header.
    /// Trocar requer reiniciar o App — {x:Static} no XAML é avaliado em parse time.
    /// </summary>
    public IReadOnlyList<CultureOption> AvailableCultures { get; } = new[]
    {
        new CultureOption("en-US", "English"),
        new CultureOption("pt-BR", "Português (BR)")
    };

    /// <summary>
    /// Nomes das etapas de sincronização exibidos na barra de status (footer).
    /// Lê as chaves <c>MainWindow.LoadingStage.*</c> do resx a cada acesso, de forma
    /// que acompanha a cultura atual (en-US / pt-BR) sem cache obsoleto. O custo de
    /// reconstrução é desprezível (4 leituras de recurso).
    /// </summary>
    public static IReadOnlyList<string> LoadingStages => new[]
    {
        Strings.MainWindow_LoadingStage_ReadingRules,
        Strings.MainWindow_LoadingStage_ComparingQos,
        Strings.MainWindow_LoadingStage_BuildingPlan,
        Strings.MainWindow_LoadingStage_ApplyingChanges,
    };

    private CultureOption _selectedCulture = null!;
    public CultureOption SelectedCulture
    {
        get => _selectedCulture;
        set
        {
            if (SetField(ref _selectedCulture, value) && value is not null)
            {
                // Persiste em config.json (próximo startup lê daqui se user não trocou antes de fechar)
                var cfg = AppConfigManager.Load();
                cfg.Culture = value.Code;
                AppConfigManager.Save(cfg);
                // Aplica imediatamente: troca a cultura + dispara refresh de todos os bindings indexer
                LocalizationManager.SetCulture(value.Code);
            }
        }
    }

    private string    _statusMessage = "Ready";
    private bool      _isLoading;
    private int       _progressPercent;
    private string    _localIp = "Detectando...";
    private string    _opnsenseHost = "https://10.0.0.1";
    private bool      _isConnected;
    private string?   _connectionError;
    private IPAddress _currentHostIp = IPAddress.Loopback;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    /// <summary>
    /// Detalhe do último erro de conexão/sincronização. Nulo quando não há erro.
    /// Exibido como tooltip do indicador de conexão no footer.
    /// </summary>
    public string? ConnectionError
    {
        get => _connectionError;
        set => SetField(ref _connectionError, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    /// <summary>
    /// Progresso da sincronização atual (0-100). Ligado à ProgressBar no footer.
    /// Resetado no início de cada <see cref="SyncAsync"/>.
    /// </summary>
    public int ProgressPercent
    {
        get => _progressPercent;
        set => SetField(ref _progressPercent, value);
    }

    public string LocalIp
    {
        get => _localIp;
        set => SetField(ref _localIp, value);
    }

    public string OpnsenseHost
    {
        get => _opnsenseHost;
        set => SetField(ref _opnsenseHost, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetField(ref _isConnected, value);
    }

    private bool _isSettingsPanelVisible;

    /// <summary>
    /// Quando true, o MainWindow exibe o <c>SettingsPanel</c> inline ocupando
    /// toda a área, escondendo o conteúdo normal (Header/Gateways/Apps/Footer).
    /// </summary>
    public bool IsSettingsPanelVisible
    {
        get => _isSettingsPanelVisible;
        set
        {
            if (SetField(ref _isSettingsPanelVisible, value))
                OnPropertyChanged(nameof(IsMainContentVisible));
        }
    }

    /// <summary>Conveniência para bindings XAML: visível quando o painel está escondido.</summary>
    public bool IsMainContentVisible => !_isSettingsPanelVisible;

    private readonly bool _isFirstRun;

    /// <summary>
    /// Detectado uma única vez no construtor: true quando <c>config.json</c> ainda
    /// não existe. Nesse caso a MainWindow abre com o <c>SettingsPanel</c> inline
    /// (Welcome panel) automaticamente — sem wizard modal separado.
    /// </summary>
    public bool IsFirstRun => _isFirstRun;

    private SettingsViewModel? _settingsVm;

    /// <summary>
    /// ViewModel do <c>SettingsPanel</c> inline. Criado por <see cref="ShowSettingsPanel"/>
    /// e descartado por <see cref="HideSettingsPanel"/>.
    /// </summary>
    public SettingsViewModel? SettingsVm
    {
        get => _settingsVm;
        private set => SetField(ref _settingsVm, value);
    }

    public ObservableCollection<AppItemViewModel> Applications { get; } = [];
    public ObservableCollection<Gateway>          Gateways     { get; } = [];

    /// <summary>
    /// Nomes de gateway usados quando o OPNsense não retorna nenhum (config recém-criada
    /// ou API indisponível). Mantém cache, UI e dialog de "Add Application" consistentes.
    /// </summary>
    private static readonly string[] FallbackGatewayNames = ["WAN2", "WAN_PPPOE", "LB_IPV4"];

    /// <summary>
    /// Heurística temporária para detectar gateway groups (ex.: load-balance) pelo nome.
    /// O DTO do OPNsense ainda não popula <c>Gateway.IsGroup</c>; quando popular, trocar por
    /// <c>!g.IsGroup</c>. Enquanto isso: prefixo "LB_" ou substring "GROUP".
    /// </summary>
    private static bool IsLikelyGroup(string name) =>
        name.StartsWith("LB_", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("GROUP", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Nomes de gateway utilizáveis na seleção por aplicativo: exclui groups (heurística)
    /// e cai no fallback filtrado quando a lista resultante fica vazia — mantendo card e
    /// dialog "Add Application" consistentes.
    /// </summary>
    private static List<string> SelectableGatewayNames(IEnumerable<Gateway> gateways)
    {
        var names = gateways
            .Select(g => g.Name)
            .Where(n => !IsLikelyGroup(n))
            .ToList();

        if (names.Count == 0)
            names = FallbackGatewayNames.Where(n => !IsLikelyGroup(n)).ToList();

        return names;
    }

    public bool HasApplications => Applications.Count > 0;
    public bool HasNoApplications => Applications.Count == 0;

    /// <summary>
    /// Plano de reparo produzido pela última sincronização. Nulo/vazio até a
    /// primeira sincronização bem-sucedida. Usado pelo botão "Aplicar reparo".
    /// </summary>
    public ReconciliationPlan LatestPlan { get; private set; } = ReconciliationPlan.Empty;

    /// <summary>Habilita o botão "Aplicar reparo" na barra superior.</summary>
    public bool HasPendingRepair => LatestPlan.HasActions;

    /// <summary>Contagem de ações pendentes — exibida no rótulo do botão.</summary>
    public int PendingRepairCount => LatestPlan.ActionCount;

    /// <summary>Último resultado de sincronização — usado pelo modal InvalidTos.</summary>
    public OptiRouteSyncResult? LatestResult { get; private set; }

    public ICommand SyncCommand { get; }
    public ICommand AddApplicationCommand { get; }
    public ICommand DeleteApplicationCommand { get; }
    public ICommand RemoveLocalCommand { get; }
    public ICommand DeleteGlobalCommand { get; }
    public ICommand PromoteLocalCommand { get; }
    public ICommand ActivateGlobalCommand { get; }
    public ICommand RepairConflictCommand { get; }
    public ICommand ApplyRepairCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    /// <summary>Gera um arquivo de diagnóstico sanitizado (logs + sistema + config, sem segredos).</summary>
    public ICommand ExportDiagnosticsCommand { get; }

    /// <summary>Abre o modal "About" centralizado na MainWindow.</summary>
    public ICommand ShowAboutCommand { get; }

    /// <summary>
    /// Cancela (melhor esforço) a sincronização em andamento. Nesta fase apenas
    /// encerra o estado de carregamento — o cancelamento cooperativo via
    /// <see cref="System.Threading.CancellationToken"/> será ligado numa fase futura.
    /// </summary>
    public ICommand CancelSyncCommand { get; }

    /// <summary>
    /// Sinaliza que o usuário clicou em "Settings" no header. MainWindow.xaml.cs
    /// responde chamando <see cref="ShowSettingsPanel"/>, que constrói o
    /// <see cref="ViewModels.SettingsViewModel"/> e o publica inline (sem janela modal).
    /// </summary>
    public event EventHandler? OpenSettingsRequested;

    public MainViewModel(
        IOptiRouteSynchronizer synchronizer,
        IHostOverrideManager overrideManager,
        IOpnsenseClient client,
        ILogger<MainViewModel> logger,
        ILoggerFactory loggerFactory,
        IWindowsQosManager qosManager,
        IDscpRegistry dscpRegistry)
    {
        _synchronizer    = synchronizer;
        _overrideManager = overrideManager;
        _client          = client;
        _logger          = logger;
        _loggerFactory   = loggerFactory;
        _qosManager      = qosManager;
        _dscpRegistry    = dscpRegistry;

        SyncCommand              = new AsyncRelayCommand(SyncAsync);
        AddApplicationCommand    = new AsyncRelayCommand(AddApplicationAsync);
        DeleteApplicationCommand = new RelayCommand(DeleteApplication);
        RemoveLocalCommand       = new RelayCommand(RemoveLocalApplication);
        DeleteGlobalCommand      = new RelayCommand(DeleteGlobalApplication);
        PromoteLocalCommand      = new RelayCommand(PromoteLocalApplication);
        ActivateGlobalCommand    = new RelayCommand(ActivateGlobalApplication);
        RepairConflictCommand    = new RelayCommand(RepairConflictApplication);
        ApplyRepairCommand       = new AsyncRelayCommand(ApplyRepairAsync, () => HasPendingRepair);
        OpenSettingsCommand      = new RelayCommand(OpenSettings);
        ExportDiagnosticsCommand = new RelayCommand(ExportDiagnostics);
        ShowAboutCommand         = new RelayCommand(ShowAbout);
        CancelSyncCommand        = new RelayCommand(CancelSync);

        var config = AppConfigManager.Load();
        _opnsenseHost = config.OpnsenseHost;

        // Primeira execução: sem config.json. A MainWindow consulta esta flag
        // para exibir o Welcome panel inline automaticamente.
        _isFirstRun = !AppConfigManager.Exists();
    }

    public async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = Strings.Status_Connecting;
            ConnectionError = null;

            _currentHostIp = LocalNetworkDetector.DetectLocalIp(OpnsenseHost);
            LocalIp = _currentHostIp.ToString();

            IsConnected = await _client.TestConnectionAsync();
            if (!IsConnected)
            {
                StatusMessage = Strings.Status_ConnectionFailed;
                ConnectionError = Strings.Status_ConnectionFailed;
                return;
            }

            await SyncAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na inicialização");
            StatusMessage = Strings.Status_GenericError(ex.Message);
            ConnectionError = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Reconstrói o <c>OpnsenseClient</c> (e os managers que dele dependem) com as
    /// credenciais/host recém-salvos no SettingsPanel. Chamado pelo MainWindow logo
    /// após <c>HideSettingsPanel()</c> e ANTES de <see cref="SyncAsync"/>, para que a
    /// sincronização use as novas credenciais sem exigir restart do App.
    ///
    /// Reutiliza <c>qosManager</c>/<c>dscpRegistry</c> (não dependem do client) e
    /// recria <c>RuleOrderManager</c>/<c>HostOverrideManager</c>/<c>OptiRouteSynchronizer</c>.
    /// Sem credenciais ou host válidos, apenas registra o status e retorna sem rebuild.
    /// </summary>
    public async Task RebuildClientAsync()
    {
        try
        {
            var config = AppConfigManager.Load();
            var creds  = SecretStore.LoadCredentials();

            if (creds is null
                || string.IsNullOrWhiteSpace(creds.ApiKey)
                || string.IsNullOrWhiteSpace(creds.ApiSecret)
                || string.IsNullOrWhiteSpace(config.OpnsenseHost))
            {
                _logger.LogInformation(
                    "Rebuild do client ignorado: credenciais ou host OPNsense ausentes.");
                IsConnected = false;
                StatusMessage = Strings.Settings_Connection_AutoTestStatus_Idle;
                return;
            }

            var settings = new OpnsenseSettings
            {
                Host         = config.OpnsenseHost,
                ApiKey       = creds.ApiKey,
                VerifyTls    = !config.AllowInsecureTls,
                LanInterface = config.LanInterface
            };

            var httpClient   = OpnsenseHttpClientFactory.Create(settings, creds.ApiSecret);
            var newClient    = new OpnsenseClient(httpClient, _loggerFactory.CreateLogger<OpnsenseClient>());
            var orderManager = new RuleOrderManager(newClient, _loggerFactory.CreateLogger<RuleOrderManager>());
            var overrideMgr  = new HostOverrideManager(
                newClient, _dscpRegistry, orderManager, _loggerFactory.CreateLogger<HostOverrideManager>());
            var synchronizer = new OptiRouteSynchronizer(
                newClient, _qosManager, _dscpRegistry, orderManager, _loggerFactory.CreateLogger<OptiRouteSynchronizer>());

            _client          = newClient;
            _overrideManager = overrideMgr;
            _synchronizer    = synchronizer;

            OpnsenseHost = config.OpnsenseHost;

            IsConnected = await newClient.TestConnectionAsync();
            StatusMessage = IsConnected
                ? Strings.Settings_Connection_AutoTestStatus_Success
                : Strings.Settings_Connection_AutoTestStatus_Failed;

            _logger.LogInformation("Client OPNsense reconstruído. Conectado={Connected}", IsConnected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao reconstruir o client OPNsense");
            IsConnected = false;
            StatusMessage = Strings.Settings_Connection_AutoTestStatus_Failed;
        }
    }

    public async Task SyncAsync()
    {
        IsLoading = true;
        ProgressPercent = 0;
        ConnectionError = null;
        StatusMessage = LoadingStages[0]; // "Reading OPNsense rules..."

        try
        {
            // Progress<SyncProgress> marshalla callbacks para a UI thread automaticamente.
            // A UI thread é quem dispara PropertyChanged, então a ProgressBar atualiza sem travar.
            // O Stage reportado pelo synchronizer é mapeado para os nomes de etapa exibidos
            // na barra de status (footer) — texto específico por fase em vez de "Refreshing…".
            var progress = new Progress<SyncProgress>(p =>
            {
                ProgressPercent = p.Percent;
                StatusMessage = p.Stage switch
                {
                    "init" or "rules" => LoadingStages[0], // Reading OPNsense rules...
                    "merge" or "qos"  => LoadingStages[1], // Comparing with local QoS...
                    "done"            => LoadingStages[2], // Building plan...
                    _                 => LoadingStages[0]
                };
            });

            var result = await _synchronizer.SyncAsync(_currentHostIp, progress);

            // Etapa final antes de montar o plano de reparo na UI.
            StatusMessage = LoadingStages[2]; // Building plan...

            // Atualiza gateways. Quando o OPNsense não retorna nenhum, popula a coleção
            // com o fallback para que cache, UI e o dialog de "Add Application" fiquem
            // consistentes (evita gateway picker vazio na 1ª abertura).
            Gateways.Clear();
            if (result.Gateways.Count > 0)
            {
                foreach (var gw in result.Gateways)
                    Gateways.Add(gw);
            }
            else
            {
                foreach (var name in FallbackGatewayNames)
                    Gateways.Add(new Gateway { Name = name });
            }

            var gatewayNames = SelectableGatewayNames(Gateways);

            // Atualiza lista de aplicativos
            Applications.Clear();
            foreach (var route in result.Routes)
            {
                var itemVm = new AppItemViewModel(route, gatewayNames);
                itemVm.OnDefaultGatewayChanged += HandleDefaultGatewayChanged;
                itemVm.OnOverrideChanged       += HandleOverrideChanged;
                itemVm.OnOverrideRemoved       += HandleOverrideRemoved;
                Applications.Add(itemVm);
            }

            OnPropertyChanged(nameof(HasApplications));
            OnPropertyChanged(nameof(HasNoApplications));

            // Constrói o plano de reparo a partir do estado recém-lido.
            // Mantém a invariante "Sincronizar nunca muta" — só aplica quando o
            // usuário clicar em "Aplicar reparo".
            LatestResult = result;
            LatestPlan   = BuildPlanFromResult(result.Routes);
            OnPropertyChanged(nameof(LatestPlan));
            OnPropertyChanged(nameof(HasPendingRepair));
            OnPropertyChanged(nameof(PendingRepairCount));

            StatusMessage = HasPendingRepair
                ? $"Sincronizado! {Applications.Count} aplicativo(s), {PendingRepairCount} reparo(s) pendente(s)."
                : $"Sincronizado! {Applications.Count} aplicativos ativos na rede.";
            IsConnected = true;
            ConnectionError = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na sincronização");
            var message = Strings.Status_RefreshFailed(ex.Message);

            // Falha de TLS em runtime: sugere o toggle (só enquanto ele está desligado,
            // para não dar conselho enganoso a quem já o habilitou).
            if (!AppConfigManager.Load().AllowInsecureTls && TlsErrorDetector.IsTlsError(ex))
                message = $"{message} · {Strings.MainWindow_StatusBar_TlsHint}";

            StatusMessage = message;
            ConnectionError = ex.Message;
        }
        finally
        {
            IsLoading = false;
            // Mantém em 100% brevemente para o usuário ver o "OK" antes de voltar para 0
            // na próxima sincronização. Sem reset aqui: próximo SyncAsync zera no início.
        }
    }

    private async Task AddApplicationAsync()
    {
        // Gateways utilizáveis: exclui groups (heurística de nome) e cai no fallback
        // filtrado quando a coleção está vazia (dialog aberto antes de sincronizar).
        var gatewayNames = SelectableGatewayNames(Gateways);

        var dialog = new AddApplicationDialog(
            gatewayNames,
            async () =>
            {
                // Re-sincroniza com o OPNsense e devolve a lista utilizável atualizada.
                // SyncAsync() engole falhas (mantém Gateways) — o dialog nunca esvazia.
                await SyncAsync();
                return SelectableGatewayNames(Gateways);
            });
        if (Application.Current?.MainWindow is { } owner && !ReferenceEquals(owner, dialog))
            dialog.Owner = owner;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        if (dialog.ShowDialog() != true)
            return;

        var exePath = dialog.Executable;
        var exeName = Path.GetFileName(exePath);

        // Se já existir na lista, avisa
        if (Applications.Any(a => a.Executable.Equals(exeName, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = Strings.Status_AlreadyRegistered(exeName);
            return;
        }

        IsLoading = true;
        StatusMessage = Strings.Status_Registering(exeName);

        try
        {
            var defaultGateway = string.IsNullOrWhiteSpace(dialog.Gateway)
                ? (Gateways.FirstOrDefault()?.Name ?? "WAN2")
                : dialog.Gateway;
            var identity = ApplicationIdentity.Create(exePath, dialog.DisplayName);

            await _synchronizer.RegisterOrUpdateApplicationAsync(identity, defaultGateway);
            await SyncAsync();

            StatusMessage = Strings.Status_RegisterSuccess(exeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar aplicativo");
            StatusMessage = $"Erro ao adicionar: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void DeleteApplication(object? parameter)
    {
        if (parameter is not AppItemViewModel item)
            return;

        IsLoading = true;
        StatusMessage = $"Removendo '{item.DisplayName}'...";

        try
        {
            if (item.IsLocalOnly)
            {
                await _synchronizer.RemoveLocalApplicationAsync(item.Executable);
                StatusMessage = $"Política local '{item.Executable}' removida do Windows.";
            }
            else
            {
                await _synchronizer.DeleteGlobalApplicationAsync(item.Executable);
                StatusMessage = $"'{item.DisplayName}' excluído globalmente do OPNsense.";
            }

            await SyncAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover aplicativo");
            StatusMessage = $"Erro ao remover: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void HandleDefaultGatewayChanged(AppItemViewModel item, string newGateway)
    {
        try
        {
            StatusMessage = $"Atualizando rota padrão global de '{item.DisplayName}' para {newGateway}...";
            await _synchronizer.RegisterOrUpdateApplicationAsync(item.Identity, newGateway, item.Dscp);
            StatusMessage = $"Rota padrão de '{item.DisplayName}' atualizada para {newGateway}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar rota padrão");
            StatusMessage = $"Erro ao atualizar rota padrão: {ex.Message}";
        }
    }

    private async void HandleOverrideChanged(AppItemViewModel item, string targetGateway)
    {
        try
        {
            StatusMessage = $"Criando override para este computador em '{item.DisplayName}' -> {targetGateway}...";
            await _overrideManager.SetOverrideAsync(item.Identity, _currentHostIp, targetGateway);
            StatusMessage = $"Override ativado! Este PC utilizará {targetGateway} para '{item.DisplayName}'.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao aplicar override");
            StatusMessage = $"Erro ao aplicar override: {ex.Message}";
        }
    }

    private async void HandleOverrideRemoved(AppItemViewModel item)
    {
        try
        {
            StatusMessage = $"Removendo override de '{item.DisplayName}' deste PC...";
            await _overrideManager.RemoveOverrideAsync(item.Executable, _currentHostIp);
            StatusMessage = $"Override removido. Este PC voltou a usar o padrão global ({item.DefaultGateway}).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover override");
            StatusMessage = $"Erro ao remover override: {ex.Message}";
        }
    }

    private async void RemoveLocalApplication(object? parameter)
    {
        if (parameter is not AppItemViewModel item)
            return;

        IsLoading = true;
        StatusMessage = $"Removendo política local '{item.Executable}' deste Windows...";

        try
        {
            await _synchronizer.RemoveLocalApplicationAsync(item.Executable);
            await SyncAsync();
            StatusMessage = $"Política '{item.Executable}' removida deste computador com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover política local");
            StatusMessage = $"Erro ao remover do Windows: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void DeleteGlobalApplication(object? parameter)
    {
        if (parameter is not AppItemViewModel item)
            return;

        IsLoading = true;
        StatusMessage = $"Excluindo regra global de '{item.DisplayName}' no OPNsense...";

        try
        {
            await _synchronizer.DeleteGlobalApplicationAsync(item.Executable);
            await SyncAsync();
            StatusMessage = $"Regra global de '{item.DisplayName}' excluída do OPNsense.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir globalmente");
            StatusMessage = $"Erro ao excluir no OPNsense: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void PromoteLocalApplication(object? parameter)
    {
        if (parameter is not AppItemViewModel item)
            return;

        IsLoading = true;
        StatusMessage = $"Promovendo '{item.DisplayName}' para o OPNsense...";

        try
        {
            var targetGw = item.AvailableGateways.FirstOrDefault(g => !g.Contains("DHCP6", StringComparison.OrdinalIgnoreCase)) ?? "WAN2";
            await _synchronizer.PromoteLocalToGlobalAsync(item.Executable, targetGw);
            await SyncAsync();
            StatusMessage = $"'{item.DisplayName}' registrado com sucesso no OPNsense!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar no OPNsense");
            StatusMessage = $"Erro ao registrar: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void ActivateGlobalApplication(object? parameter)
    {
        if (parameter is not AppItemViewModel item)
            return;

        IsLoading = true;
        StatusMessage = $"Ativando QoS para '{item.DisplayName}' neste Windows...";

        try
        {
            await _synchronizer.ActivateGlobalOnLocalAsync(item.Executable);
            await SyncAsync();
            StatusMessage = $"QoS ativado com sucesso para '{item.DisplayName}'!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ativar QoS local");
            StatusMessage = $"Erro ao ativar QoS: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void RepairConflictApplication(object? parameter)
    {
        if (parameter is not AppItemViewModel item)
            return;

        IsLoading = true;
        StatusMessage = $"Reparando conflito de DSCP para '{item.DisplayName}'...";

        try
        {
            await _synchronizer.RepairConflictAsync(item.Executable);
            await SyncAsync();
            StatusMessage = $"Conflito reparado! '{item.DisplayName}' agora utiliza o DSCP global.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reparar conflito");
            StatusMessage = $"Erro ao reparar conflito: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Sinaliza que o usuário clicou em "Settings" no header. Em primeira execução
    /// (sem config.json) força o <see cref="ShowSettingsPanel"/> diretamente — o
    /// Welcome panel aparece sem depender de handler da Window. Caso contrário
    /// apenas levanta <see cref="OpenSettingsRequested"/> para o MainWindow exibir
    /// o painel inline.
    /// </summary>
    private void OpenSettings()
    {
        if (IsFirstRun && !AppConfigManager.Exists())
        {
            ShowSettingsPanel();
            return;
        }

        OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Command do botão "Cancelar" da barra de status. Nesta fase o cancelamento
    /// é apenas visual: se não há sincronização em andamento é no-op; caso
    /// contrário encerra o estado de carregamento. Cancelamento cooperativo real
    /// (CancellationToken propagado ao <c>SyncAsync</c>) fica para uma fase futura.
    /// </summary>
    private void CancelSync()
    {
        if (!IsLoading)
            return;

        _logger.LogInformation("Sincronização cancelada pelo usuário (visual).");
        IsLoading = false;
        ProgressPercent = 0;
    }

    // ── Phase 3: Diagnostics export ─────────────────────────────────────────

    /// <summary>
    /// Exporta um relatório de diagnóstico sanitizado. Abre um SaveFileDialog com
    /// nome default contendo timestamp e, ao confirmar, grava o conteúdo gerado
    /// por <see cref="DiagnosticsExporter.BuildContent"/>.
    /// </summary>
    private void ExportDiagnostics()
    {
        var dialog = new SaveFileDialog
        {
            Title = Strings.MainWindow_Button_ExportDiagnostics,
            Filter = "Text files (*.txt)|*.txt",
            DefaultExt = ".txt",
            FileName = $"OptiRoute-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var content = DiagnosticsExporter.BuildContent();
            DiagnosticsExporter.Export(dialog.FileName, content);

            MessageBox.Show(
                Strings.DiagnosticsExport_Success(dialog.FileName),
                "OptiRoute",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao exportar diagnóstico");
            MessageBox.Show(
                Strings.DiagnosticsExport_Failed(ex.Message),
                "OptiRoute",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    // ── Phase 3: About dialog ───────────────────────────────────────────────

    private void ShowAbout()
    {
        var window = new AboutWindow();
        if (Application.Current?.MainWindow is { } owner && !ReferenceEquals(owner, window))
            window.Owner = owner;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.ShowDialog();
    }

    // ── Phase 3: Tray opcional (OFF por padrão) ─────────────────────────────

    /// <summary>
    /// Reconcilia o estado do tray com a preferência persistida
    /// (<c>StartMinimizedToTray</c>). Chamado pelo MainWindow no startup e após
    /// salvar as Settings. É no-op quando o estado não muda e <b>nunca</b> cria um
    /// <c>NotifyIcon</c> sem opt-in explícito do usuário.
    /// </summary>
    public void RefreshMinimizeToTray()
    {
        if (AppConfigManager.Load().StartMinimizedToTray)
            EnableTray();
        else
            DisableTray();
    }

    private void EnableTray()
    {
        if (_trayIcon is not null)
            return;

        if (Application.Current?.MainWindow is not MainWindow mainWindow)
            return;

        _trayIcon = new TrayIcon(mainWindow);
        _trayIcon.ShowRequested += (_, _) => RestoreMainWindow(mainWindow);
        _trayIcon.QuitRequested += (_, _) =>
        {
            IsExplicitShutdown = true;
            Application.Current.Shutdown();
        };
        mainWindow.Closing += OnMainWindowClosing;
        Application.Current.SessionEnding += OnSessionEnding;

        IsMinimizeToTrayEnabled = true;
    }

    private void DisableTray()
    {
        if (_trayIcon is null)
            return;

        if (Application.Current?.MainWindow is MainWindow mainWindow)
            mainWindow.Closing -= OnMainWindowClosing;
        if (Application.Current is not null)
            Application.Current.SessionEnding -= OnSessionEnding;

        _trayIcon.Dispose();
        _trayIcon = null;
        IsMinimizeToTrayEnabled = false;
    }

    /// <summary>
    /// Com o tray ativo, fechamentos iniciados pelo usuário (X) apenas escondem a
    /// janela. O "Quit" explícito do tray e o encerramento de sessão do Windows
    /// marcam <see cref="IsExplicitShutdown"/> e prosseguem normalmente.
    /// (WPF não expõe CloseReason em <c>Window.Closing</c>.)
    /// </summary>
    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (IsExplicitShutdown || _trayIcon is null)
            return;

        e.Cancel = true;
        if (sender is Window window)
            window.Hide();
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
        => IsExplicitShutdown = true;

    private static void RestoreMainWindow(Window window)
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }

    /// <summary>
    /// Exibe o <c>SettingsPanel</c> inline no MainWindow. Constrói um
    /// <see cref="SettingsViewModel"/> novo reusando o <c>IOpnsenseClient</c> já
    /// configurado e zera <see cref="IsLoading"/> (cancela o overlay de sincronização
    /// em andamento). Idempotente: se já visível, apenas garante a visibilidade.
    /// Quando ainda não há config.json, abre em modo first-run (Welcome panel).
    /// </summary>
    public void ShowSettingsPanel()
    {
        if (SettingsVm is null)
        {
            IsLoading = false;
            var firstRun = !AppConfigManager.Exists();
            // First-run: passa client null para o SettingsViewModel construir clientes
            // efêmeros a partir dos valores digitados (mesmo caminho do antigo wizard).
            // Normal: reusa o IOpnsenseClient já configurado no MainViewModel.
            SettingsVm = firstRun
                ? new SettingsViewModel(opnsenseClient: null!, isFirstRun: true)
                : new SettingsViewModel(_client, isFirstRun: false);
        }

        IsSettingsPanelVisible = true;
    }

    /// <summary>
    /// Esconde o <c>SettingsPanel</c> inline e descarta o <see cref="SettingsVm"/>
    /// (um novo é criado na próxima abertura).
    /// </summary>
    public void HideSettingsPanel()
    {
        SettingsVm = null;
        IsSettingsPanelVisible = false;
    }

    /// <summary>
    /// Aplica o plano de reparo construído pela última sincronização.
    /// Itera sobre as ações e chama o método correspondente do sincronizador.
    /// Re-sincroniza ao final para refletir o estado pós-mutação.
    /// </summary>
    private async Task ApplyRepairAsync()
    {
        if (!LatestPlan.HasActions)
        {
            StatusMessage = Strings.Status_NothingToRepair;
            return;
        }

        IsLoading = true;
        StatusMessage = LoadingStages[3]; // "Applying changes..."
        var applied = 0;
        var errors  = 0;

        try
        {
            // Resolve conflitos tos/description via modal ANTES de aplicar
            var plan = await ResolvePlanWithTosDialogAsync(LatestPlan);

            foreach (var action in plan.Actions)
            {
                try
                {
                    switch (action.Type)
                    {
                        case ReconciliationActionType.UpdateQosPolicy:
                            await _synchronizer.RepairConflictAsync(action.Executable);
                            applied++;
                            break;

                        case ReconciliationActionType.CreateQosPolicy:
                            await _synchronizer.ActivateGlobalOnLocalAsync(action.Executable);
                            applied++;
                            break;

                        case ReconciliationActionType.DeleteLocalQosPolicy:
                            await _synchronizer.RemoveLocalApplicationAsync(action.Executable);
                            applied++;
                            break;

                        case ReconciliationActionType.RecreateFirewallRule:
                            // Dispatch via ApplyPlanAsync (full request) para Delete + Create + Apply
                            var result = await _synchronizer.ApplyPlanAsync(
                                new ReconciliationPlan { Actions = new[] { action } });
                            if (result.AllSucceeded)
                                applied++;
                            else
                                errors++;
                            break;

                        // Os demais tipos (CreateFirewallRule / UpdateFirewallRule /
                        // MoveFirewallRule / DeleteFirewallRule) ainda não são emitidos
                        // por BuildPlanFromResult; quando forem (delta futuro com promoção
                        // automática), entram aqui com chamadas equivalentes no sincronizador.
                        default:
                            _logger.LogWarning(
                                "Tipo de ação {Type} ainda não implementado no dispatcher (delta 2 prepara o terreno)",
                                action.Type);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    _logger.LogError(ex,
                        "Falha ao aplicar ação {Type} para '{Exe}'",
                        action.Type, action.Executable);
                }
            }

            // Re-sincroniza para refletir o resultado e reconstruir o plano
            // (deve ficar vazio se as mutações foram bem-sucedidas).
            await SyncAsync();

            StatusMessage = errors == 0
                ? Strings.Status_ApplySuccess(applied)
                : Strings.Status_ApplyPartialFailure(applied, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro geral ao aplicar reparos");
            StatusMessage = Strings.Status_ApplyFailed(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Abre dialog para cada rota com TosMismatchDetail, deixa usuário escolher direção.
    /// Mutates o plano conforme escolha: UpdateQosPolicy → mantém, ou RecreateFirewallRule → substitui.
    /// </summary>
    private async Task<ReconciliationPlan> ResolvePlanWithTosDialogAsync(ReconciliationPlan plan)
    {
        var routesByExe = plan.Actions
            .Select(a => a.Executable)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e, e => LatestResult?.Routes.FirstOrDefault(r =>
                r.Executable.Equals(e, StringComparison.OrdinalIgnoreCase)),
                StringComparer.OrdinalIgnoreCase);

        var resolutions = new Dictionary<string, InvalidTosResolution>(StringComparer.OrdinalIgnoreCase);

        foreach (var (exe, route) in routesByExe)
        {
            if (route?.TosMismatchDetail is null) continue;

            var dialog = new InvalidTosDialog(
                ruleDescription:  route.DescriptorString(),
                descriptionDscp:   route.GlobalDscp ?? 0,
                actualTosDscp:     route.ActualTosDscp() ?? 0,
                actualTosHex:      route.ActualTosHex() ?? "0x00");
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
            resolutions[exe] = dialog.Resolution;
        }

        var resolved = new List<ReconciliationAction>();
        foreach (var action in plan.Actions)
        {
            if (action.Type != ReconciliationActionType.UpdateQosPolicy ||
                !resolutions.TryGetValue(action.Executable, out var res))
            {
                resolved.Add(action);
                continue;
            }

            var route = routesByExe.TryGetValue(action.Executable, out var r) ? r : null;

            switch (res)
            {
                case InvalidTosResolution.Cancel:
                    _logger.LogWarning("Usuário cancelou reparo de '{Exe}' (TosMismatch).", action.Executable);
                    continue;

                case InvalidTosResolution.ForceWindowsToOpnsense:
                    if (route?.LocalDscp.HasValue == true && !string.IsNullOrEmpty(route.RuleUuid))
                    {
                        resolved.Add(new ReconciliationAction(
                            Type:               ReconciliationActionType.RecreateFirewallRule,
                            Executable:         action.Executable,
                            AppId:              action.AppId,
                            SourceIp:           null,
                            Dscp:               route.LocalDscp.Value,
                            TargetGateway:      action.TargetGateway,
                            RuleUuid:           route.RuleUuid,
                            LocalQosPolicyName: action.LocalQosPolicyName,
                            Description:        $"Tos divergente → recriar regra OPNsense com DSCP {route.LocalDscp} (do Windows local)",
                            OpnsenseRequest:    BuildOpnsenseRequest(route, route.LocalDscp.Value)));
                    }
                    else
                    {
                        resolved.Add(action);
                    }
                    break;

                case InvalidTosResolution.ForceOpnsenseToWindows:
                {
                    if (route is null) { resolved.Add(action); break; }

                    // "Adotar tos como verdade": o que o usuário setou no OPNsense VENCE.
                    // Windows + description passam a refletir tos/4; tos fica como está.
                    var tosAdoptedDscp = route.ActualTosDscp() ?? route.GlobalDscp ?? 0;

                    // 1. Recriar regra OPNsense com description atualizada
                    if (!string.IsNullOrEmpty(route.RuleUuid))
                    {
                        resolved.Add(new ReconciliationAction(
                            Type:               ReconciliationActionType.RecreateFirewallRule,
                            Executable:         action.Executable,
                            AppId:              action.AppId,
                            SourceIp:           null,
                            Dscp:               tosAdoptedDscp,
                            TargetGateway:      action.TargetGateway,
                            RuleUuid:           route.RuleUuid,
                            LocalQosPolicyName: action.LocalQosPolicyName,
                            Description:        $"Adotar tos real como verdade → recriar regra com DSCP {tosAdoptedDscp}",
                            OpnsenseRequest:    BuildOpnsenseRequest(route, tosAdoptedDscp)));
                    }

                    // 2. Consertar Windows QoS para o DSCP adotado
                    resolved.Add(new ReconciliationAction(
                        Type:               ReconciliationActionType.UpdateQosPolicy,
                        Executable:         action.Executable,
                        AppId:              action.AppId,
                        SourceIp:           null,
                        Dscp:               tosAdoptedDscp,
                        TargetGateway:      action.TargetGateway,
                        RuleUuid:           route.RuleUuid,
                        LocalQosPolicyName: action.LocalQosPolicyName,
                        Description:        $"Reconciliar Windows QoS para DSCP {tosAdoptedDscp} (tos adotado)"));
                    break;
                }

                default:
                    resolved.Add(action);
                    break;
            }
        }

        return await Task.FromResult(new ReconciliationPlan { Actions = resolved });
    }

    /// <summary>
    /// Constrói o plano de reparo a partir do resultado da sincronização.
    /// Cada rota que não está em <c>Synchronized</c> gera uma ação correspondente.
    ///
    /// Nota: a versão canônica (delta 2) vive em
    /// <c>OptiRouteSynchronizer.BuildPlanAsync</c>. Esta implementação permanece
    /// aqui apenas como fallback caso a sincronização seja contornada e a UI precise
    /// re-construir o plano a partir de uma lista em memória.
    /// </summary>
    private static ReconciliationPlan BuildPlanFromResult(IEnumerable<EffectiveApplicationRoute> routes)
    {
        var actions = new List<ReconciliationAction>();
        foreach (var r in routes)
        {
            ReconciliationAction? action = r.SyncState switch
            {
                ApplicationSyncState.Conflict => new ReconciliationAction(
                    Type:                ReconciliationActionType.UpdateQosPolicy,
                    Executable:          r.Executable,
                    AppId:               r.Identity?.AppId ?? string.Empty,
                    SourceIp:            null,
                    Dscp:                r.GlobalDscp ?? r.Dscp,
                    TargetGateway:       r.DefaultGateway,
                    RuleUuid:            r.RuleUuid,
                    LocalQosPolicyName:  $"OptiRoute-{r.Executable}",
                    Description:         r.TosMismatchDetail is not null
                                         ? $"Corrigir DSCP local de '{r.Executable}' (tos divergente: {r.TosMismatchDetail})"
                                         : $"Corrigir DSCP local de '{r.Executable}' para {r.GlobalDscp} (global)"),

                ApplicationSyncState.GlobalOnly => new ReconciliationAction(
                    Type:                ReconciliationActionType.CreateQosPolicy,
                    Executable:          r.Executable,
                    AppId:               r.Identity?.AppId ?? string.Empty,
                    SourceIp:            null,
                    Dscp:                r.GlobalDscp ?? r.Dscp,
                    TargetGateway:       r.DefaultGateway,
                    RuleUuid:            null,
                    LocalQosPolicyName:  $"OptiRoute-{r.Executable}",
                    Description:         $"Ativar QoS local para '{r.Executable}' com DSCP {r.GlobalDscp}"),

                ApplicationSyncState.LocalOnly => new ReconciliationAction(
                    // LocalOnly: o gateway alvo não está no estado (DefaultGateway="Não configurado no OPNsense").
                    // Plano conservador: oferecer Delete. Promoção continua via per-card (D1).
                    Type:                ReconciliationActionType.DeleteLocalQosPolicy,
                    Executable:          r.Executable,
                    AppId:               r.Identity?.AppId ?? string.Empty,
                    SourceIp:            null,
                    Dscp:                r.LocalDscp ?? 0,
                    TargetGateway:       string.Empty,
                    RuleUuid:            null,
                    LocalQosPolicyName:  $"OptiRoute-{r.Executable}",
                    Description:         $"Política local '{r.Executable}' sem registro no OPNsense — remover ou promover (per-card)"),

                _ => null
            };

            if (action is not null)
                actions.Add(action);
        }

        return new ReconciliationPlan { Actions = actions };
    }

    private static OPNsenseRuleRequest BuildOpnsenseRequest(EffectiveApplicationRoute route, int newDscp)
        => new()
        {
            Description    = route.DescriptorString(),
            Category       = "OptiRoute",
            Interface      = "lan",
            SourceIp       = route.HasOverride ? route.OverrideGateway ?? "any" : "any",
            DscpValue      = newDscp,
            Gateway        = route.DefaultGateway,
            DestinationNet = "(self)",
            DestinationNot = "1",
            Sequence       = 1
        };
}

internal static class EffectiveApplicationRouteExtensions
{
    public static string DescriptorString(this EffectiveApplicationRoute r)
        => $"OPTIROUTE|DEFAULT|{r.Executable}|{r.GlobalDscp ?? 0}|{r.Identity?.AppId ?? ""}|v=1";

    public static string? ActualTosHex(this EffectiveApplicationRoute r)
        => r.ActualTosHex;

    public static int? ActualTosDscp(this EffectiveApplicationRoute r)
    {
        var hex = r.ActualTosHex;
        if (string.IsNullOrEmpty(hex) || !hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return null;
        if (!int.TryParse(hex.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var tosByte))
            return null;
        return tosByte >> 2;
    }
}
