namespace OptiRoute.Core.Exceptions;

/// <summary>
/// Exceção lançada quando uma operação de política QoS do Windows falha.
/// </summary>
public sealed class QosPolicyException : Exception
{
    public QosPolicyException(string message) : base(message) { }
    public QosPolicyException(string message, Exception innerException) : base(message, innerException) { }
}
