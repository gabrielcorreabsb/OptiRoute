namespace OptiRoute.Core.Models;

/// <summary>
/// Reporte de progresso emitido durante a sincronização entre OPNsense e Windows QoS.
/// Consumido pela UI via <see cref="System.IProgress{T}"/> para atualizar barra e status.
/// </summary>
/// <param name="Stage">Identificador curto da etapa (ex: "rules", "qos", "merge", "classify", "done").</param>
/// <param name="Percent">Progresso de 0 a 100. A UI pode ignorar fora desse intervalo.</param>
/// <param name="Message">Mensagem legível exibida na status bar.</param>
public sealed record SyncProgress(string Stage, int Percent, string Message);
