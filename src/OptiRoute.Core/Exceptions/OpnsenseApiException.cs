namespace OptiRoute.Core.Exceptions;

/// <summary>
/// Exceção lançada quando uma chamada à API do OPNsense falha.
/// </summary>
public sealed class OpnsenseApiException : Exception
{
    public int? StatusCode { get; }

    public OpnsenseApiException(string message) : base(message) { }

    public OpnsenseApiException(string message, int statusCode)
        : base($"HTTP {statusCode}: {message}")
    {
        StatusCode = statusCode;
    }

    public OpnsenseApiException(string message, Exception innerException)
        : base(message, innerException) { }
}
