using System.Security.Authentication;

namespace OptiRoute.App.Services;

/// <summary>
/// Detecção <b>conservadora</b> de falhas causadas por validação de certificado TLS.
/// Usada para sugerir a opção "Allow self-signed certificates" sem gerar falsos
/// positivos: só classifica como TLS quando há indício claro — uma exceção de
/// autenticação TLS ou uma mensagem contendo "certificate"/"SSL"/"TLS".
/// Percorre toda a cadeia de <see cref="Exception.InnerException"/>, pois o
/// <c>HttpRequestException</c> do HttpClient normalmente embrulha a causa real.
/// </summary>
public static class TlsErrorDetector
{
    public static bool IsTlsError(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is AuthenticationException)
                return true;

            var message = exception.Message;
            if (!string.IsNullOrEmpty(message)
                && (message.Contains("certificate", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("SSL", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("TLS", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            exception = exception.InnerException;
        }

        return false;
    }
}
