using System.Collections.ObjectModel;
using OptiRoute.Core.Interfaces;
using OptiRoute.Core.Models;
using ApplicationIdentity = OptiRoute.Core.Models.ApplicationIdentity;

namespace OptiRoute.App.ViewModels;

public sealed class AppItemViewModel : ViewModelBase
{
    private string _displayName = string.Empty;
    private string _defaultGateway = string.Empty;
    private bool   _useGlobalDefault = true;
    private string _selectedOverrideGateway = string.Empty;
    private string _effectiveGateway = string.Empty;
    private bool   _isQosActive;
    private ApplicationSyncState _syncState = ApplicationSyncState.Synchronized;

    public ApplicationIdentity Identity { get; }
    public string Executable => Identity.ExecutableName;
    public int Dscp { get; }
    public int? LocalDscp { get; }
    public int? GlobalDscp { get; }
    public LocalQosPolicy? LocalPolicy { get; }

    public ApplicationSyncState SyncState
    {
        get => _syncState;
        set
        {
            if (SetField(ref _syncState, value))
            {
                OnPropertyChanged(nameof(IsSynchronized));
                OnPropertyChanged(nameof(IsLocalOnly));
                OnPropertyChanged(nameof(IsGlobalOnly));
                OnPropertyChanged(nameof(IsConflict));
                OnPropertyChanged(nameof(SyncStateBadge));
                OnPropertyChanged(nameof(Reason));
            }
        }
    }

    public bool IsSynchronized => SyncState == ApplicationSyncState.Synchronized;
    public bool IsLocalOnly    => SyncState == ApplicationSyncState.LocalOnly;
    public bool IsGlobalOnly   => SyncState == ApplicationSyncState.GlobalOnly;
    public bool IsConflict     => SyncState == ApplicationSyncState.Conflict;

    public string SyncStateBadge => SyncState switch
    {
        ApplicationSyncState.Synchronized => "● Sincronizado",
        ApplicationSyncState.LocalOnly    => "⚠ Somente neste Windows",
        ApplicationSyncState.GlobalOnly   => "○ Disponível globalmente",
        ApplicationSyncState.Conflict     => "⚠ Conflito de DSCP",
        _ => "Desconhecido"
    };

    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value);
    }

    public string DefaultGateway
    {
        get => _defaultGateway;
        set
        {
            if (SetField(ref _defaultGateway, value))
            {
                UpdateEffectiveGateway();
                if (IsSynchronized)
                    OnDefaultGatewayChanged?.Invoke(this, value);
            }
        }
    }

    public bool UseGlobalDefault
    {
        get => _useGlobalDefault;
        set
        {
            if (SetField(ref _useGlobalDefault, value))
            {
                OnPropertyChanged(nameof(UseLocalOverride));
                UpdateEffectiveGateway();
                if (value && IsSynchronized)
                    OnOverrideRemoved?.Invoke(this);
            }
        }
    }

    public bool UseLocalOverride
    {
        get => !_useGlobalDefault;
        set
        {
            UseGlobalDefault = !value;
            if (value && !string.IsNullOrEmpty(SelectedOverrideGateway) && IsSynchronized)
                OnOverrideChanged?.Invoke(this, SelectedOverrideGateway);
        }
    }

    public string SelectedOverrideGateway
    {
        get => _selectedOverrideGateway;
        set
        {
            if (SetField(ref _selectedOverrideGateway, value))
            {
                UpdateEffectiveGateway();
                if (!_useGlobalDefault && !string.IsNullOrEmpty(value) && IsSynchronized)
                    OnOverrideChanged?.Invoke(this, value);
            }
        }
    }

    public string EffectiveGateway
    {
        get => _effectiveGateway;
        private set => SetField(ref _effectiveGateway, value);
    }

    public string Reason => SyncState switch
    {
        ApplicationSyncState.LocalOnly => "Default LAN Routing",
        ApplicationSyncState.GlobalOnly => "Inativo neste PC",
        _ => UseGlobalDefault ? "Global Default" : "Local Override"
    };

    public bool IsQosActive
    {
        get => _isQosActive;
        set => SetField(ref _isQosActive, value);
    }

    public ObservableCollection<string> AvailableGateways { get; } = [];

    // Eventos disparados para o MainViewModel
    public event Action<AppItemViewModel, string>? OnDefaultGatewayChanged;
    public event Action<AppItemViewModel, string>? OnOverrideChanged;
    public event Action<AppItemViewModel>?         OnOverrideRemoved;

    public AppItemViewModel(
        EffectiveApplicationRoute route,
        IEnumerable<string> availableGateways)
    {
        Identity                 = route.Identity;
        DisplayName              = route.DisplayName;
        Dscp                     = route.Dscp;
        LocalDscp                = route.LocalDscp;
        GlobalDscp               = route.GlobalDscp;
        LocalPolicy              = route.LocalPolicy;
        _syncState               = route.SyncState;
        _defaultGateway          = route.DefaultGateway;
        _useGlobalDefault        = !route.HasOverride;
        _selectedOverrideGateway = route.OverrideGateway ?? (availableGateways.FirstOrDefault() ?? string.Empty);
        _isQosActive             = route.IsQosActive;

        foreach (var gw in availableGateways)
            AvailableGateways.Add(gw);

        UpdateEffectiveGateway();
    }

    public void UpdateGatewaysList(IEnumerable<string> gateways)
    {
        AvailableGateways.Clear();
        foreach (var gw in gateways)
            AvailableGateways.Add(gw);

        if (string.IsNullOrEmpty(_selectedOverrideGateway))
            SelectedOverrideGateway = AvailableGateways.FirstOrDefault() ?? string.Empty;
    }

    private void UpdateEffectiveGateway()
    {
        if (IsLocalOnly)
        {
            EffectiveGateway = "Default LAN Routing";
        }
        else if (IsGlobalOnly)
        {
            EffectiveGateway = $"{DefaultGateway} (Inativo localmente)";
        }
        else
        {
            EffectiveGateway = UseGlobalDefault ? DefaultGateway : SelectedOverrideGateway;
        }

        OnPropertyChanged(nameof(Reason));
    }
}
