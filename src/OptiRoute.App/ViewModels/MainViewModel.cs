using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using OptiRoute.App.Properties;
using OptiRoute.App.Services;
using OptiRoute.App.Windows;
using OptiRoute.Core.Interfaces;
using OptiRoute.Core.Models;
using ApplicationIdentity = OptiRoute.Core.Models.ApplicationIdentity;

namespace OptiRoute.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IOptiRouteSynchronizer _synchronizer;
    private readonly IHostOverrideManager   _overrideManager;
    private readonly IOpnsenseClient        _client;
    private readonly ILogger<MainViewModel> _logger;

    /// <summary>
    /// Culturas suportadas pelo App (i18n). Exibidas no ComboBox de idioma da header.
    /// Trocar requer reiniciar o App — {x:Static} no XAML é avaliado em parse time.
    /// </summary>
    public IReadOnlyList<CultureOption> AvailableCultures { get; } = new[]
    {
        new CultureOption("en-US", "English"),
        new CultureOption("pt-BR", "Português (BR)")
    };

    private CultureOption _selectedCulture = null!;
    public CultureOption SelectedCulture
    {
        get => _selectedCulture;
        set
        {
            if (SetField(ref _selectedCulture, value))
            {
                // Persiste em config.json via AppConfigManager.Save. App.xaml.cs lê no próximo startup.
                var cfg = AppConfigManager.Load();
                cfg.Culture = value.Code;
                AppConfigManager.Save(cfg);
                try { System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo(value.Code); }
                catch { /* fallback to system culture */ }
                StatusMessage = "Restart required for language change to take effect.";
            }
        }
    }

    private string    _statusMessage = "Ready";
    private bool      _isLoading;
    private int       _progressPercent;
    private string    _localIp = "Detectando...";
    private string    _opnsenseHost = "https://10.0.0.1";
    private bool      _isConnected;
    private IPAddress _currentHostIp = IPAddress.Loopback;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
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

    public ObservableCollection<AppItemViewModel> Applications { get; } = [];
    public ObservableCollection<Gateway>          Gateways     { get; } = [];

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

    /// <summary>
    /// Sinaliza que o usuário clicou em "Settings" no header. MainWindow.xaml.cs
    /// abre a janela modal passando o <see cref="ViewModels.SettingsViewModel"/>
    /// construído pelo MainViewModel (compartilha IOpnsenseClient já configurado).
    /// </summary>
    public event EventHandler<SettingsViewModel>? OpenSettingsRequested;

    public MainViewModel(
        IOptiRouteSynchronizer synchronizer,
        IHostOverrideManager overrideManager,
        IOpnsenseClient client,
        ILogger<MainViewModel> logger)
    {
        _synchronizer    = synchronizer;
        _overrideManager = overrideManager;
        _client          = client;
        _logger          = logger;

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

        var config = AppConfigManager.Load();
        _opnsenseHost = config.OpnsenseHost;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = Strings.Status_Connecting;

        try
        {
            _currentHostIp = LocalNetworkDetector.DetectLocalIp(OpnsenseHost);
            LocalIp = _currentHostIp.ToString();

            IsConnected = await _client.TestConnectionAsync();
            if (!IsConnected)
            {
                StatusMessage = Strings.Status_ConnectionFailed;
                return;
            }

            await SyncAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na inicialização");
            StatusMessage = Strings.Status_GenericError(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SyncAsync()
    {
        IsLoading = true;
        ProgressPercent = 0;
        StatusMessage = Strings.Status_Refreshing;

        try
        {
            // Progress<SyncProgress> marshalla callbacks para a UI thread automaticamente.
            // A UI thread é quem dispara PropertyChanged, então a ProgressBar atualiza sem travar.
            var progress = new Progress<SyncProgress>(p =>
            {
                ProgressPercent = p.Percent;
                StatusMessage   = p.Message;
            });

            var result = await _synchronizer.SyncAsync(_currentHostIp, progress);

            // Atualiza gateways
            Gateways.Clear();
            foreach (var gw in result.Gateways)
                Gateways.Add(gw);

            var gatewayNames = result.Gateways.Select(g => g.Name).ToList();
            if (!gatewayNames.Any())
                gatewayNames = ["WAN2", "WAN_PPPOE", "LB_IPV4"];

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na sincronização");
            StatusMessage = Strings.Status_RefreshFailed(ex.Message);
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
        var openFileDialog = new OpenFileDialog
        {
            Title = "Selecione o Executável do Jogo / Aplicativo",
            Filter = "Executáveis (*.exe)|*.exe|Todos os arquivos (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() != true)
            return;

        var exePath = openFileDialog.FileName;
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
            var defaultGateway = Gateways.FirstOrDefault()?.Name ?? "WAN2";
            var identity = ApplicationIdentity.Create(exePath);

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
    /// Abre a janela de configurações. Constrói um SettingsViewModel reusando o
    /// IOpnsenseClient já configurado (mesma sessão HTTP + logger). Dispara o evento
    /// <see cref="OpenSettingsRequested"/>; MainWindow.xaml.cs é quem exibe a janela
    /// (mantém o VM livre de tipos UI).
    /// </summary>
    private void OpenSettings()
    {
        var settingsVm = new SettingsViewModel(_client);
        OpenSettingsRequested?.Invoke(this, settingsVm);
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
        StatusMessage = Strings.Status_Applying(LatestPlan.ActionCount);
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
