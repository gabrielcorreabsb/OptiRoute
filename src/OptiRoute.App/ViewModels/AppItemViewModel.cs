using System.Collections.ObjectModel;
using OptiRoute.App.Properties;
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
    private bool    _isVerified;
    private string? _verificationSummary;
    private string? _verificationFailureReason;

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
        ApplicationSyncState.Synchronized => Strings.Card_SyncStateBadge_Synchronized,
        ApplicationSyncState.LocalOnly    => Strings.Card_SyncStateBadge_LocalOnly,
        ApplicationSyncState.GlobalOnly   => Strings.Card_SyncStateBadge_GlobalOnly,
        ApplicationSyncState.Conflict     => Strings.Card_SyncStateBadge_Conflict,
        _                                => Strings.Card_SyncStateBadge_Unknown
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

    /// <summary>True após uma verificação pós-Apply (<see cref="ApplyVerification"/>) rodar.</summary>
    public bool IsVerified
    {
        get => _isVerified;
        private set => SetField(ref _isVerified, value);
    }

    /// <summary>
    /// Resumo legível dos checks pós-Apply (ex.: "✓ QoS · DSCP · Rule · Order · Gateway"
    /// ou "⚠ DSCP mismatch"). <c>null</c> antes de qualquer verificação.
    /// </summary>
    public string? VerificationSummary
    {
        get => _verificationSummary;
        private set => SetField(ref _verificationSummary, value);
    }

    /// <summary>Motivo da primeira verificação que falhou; <c>null</c> quando tudo OK.</summary>
    public string? VerificationFailureReason
    {
        get => _verificationFailureReason;
        private set => SetField(ref _verificationFailureReason, value);
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

    /// <summary>
    /// Popula o estado de verificação pós-Apply desta rota a partir do resultado de
    /// <c>OptiRouteSynchronizer.VerifyRoutesAsync</c>. Marca <see cref="IsVerified"/> e
    /// deriva <see cref="VerificationSummary"/> / <see cref="VerificationFailureReason"/>.
    /// </summary>
    public void ApplyVerification(RouteVerification v)
    {
        IsVerified = true;
        VerificationFailureReason = v.FailureReason;

        var allOk = v.QosActive && v.DscpCorrect && v.RuleActive && v.RuleOrderValid && v.GatewayReachable;
        if (allOk)
        {
            VerificationSummary = "✓ QoS · DSCP · Rule · Order · Gateway";
            return;
        }

        var failed = new List<string>(5);
        if (!v.QosActive)        failed.Add("QoS");
        if (!v.DscpCorrect)      failed.Add("DSCP");
        if (!v.RuleActive)       failed.Add("Rule");
        if (!v.RuleOrderValid)   failed.Add("Order");
        if (!v.GatewayReachable) failed.Add("Gateway");

        VerificationSummary = $"⚠ {v.FailureReason ?? string.Join(" · ", failed)}";
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
