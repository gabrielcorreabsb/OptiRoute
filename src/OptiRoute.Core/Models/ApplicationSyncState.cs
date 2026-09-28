namespace OptiRoute.Core.Models;

/// <summary>
/// Estado de sincronização e reconciliação entre OPNsense e Windows QoS local.
/// </summary>
public enum ApplicationSyncState
{
    /// <summary>Existe no OPNsense e no Windows local, com DSCP idêntico.</summary>
    Synchronized,

    /// <summary>Existe apenas no Windows QoS local (sem regra global no OPNsense).</summary>
    LocalOnly,

    /// <summary>Existe no OPNsense globalmente, mas ainda não foi ativado neste computador.</summary>
    GlobalOnly,

    /// <summary>Existe em ambos, mas o valor DSCP diverge entre Windows e OPNsense.</summary>
    Conflict
}
