using System.Net.Http.Headers;
using System.Text;

namespace OptiRoute.OPNsense.Client;

/// <summary>
/// Configurações de conexão com o OPNsense.
/// </summary>
public sealed class OpnsenseSettings
{
    /// <summary>URL base (ex: "https://10.0.0.1").</summary>
    public string Host       { get; set; } = string.Empty;

    /// <summary>API Key gerada em System → Access → Users.</summary>
    public string ApiKey     { get; set; } = string.Empty;

    /// <summary>
    /// Se <c>false</c>, ignora erros de validação do certificado TLS (aceita cert
    /// autoassinado/inválido). Se <c>true</c>, valida o certificado.
    /// <para>
    /// O default de fábrica é <c>false</c> apenas por conveniência de desenvolvimento
    /// (o OPNsense costuma usar certificado autoassinado). O <c>OptiRoute.App</c> SEMPRE
    /// sobrescreve este valor no startup, derivando-o da preferência do usuário:
    /// <c>VerifyTls = !AppConfig.AllowInsecureTls</c>. Ou seja, em produção a validação
    /// fica ligada a menos que o usuário habilite "Allow self-signed certificates".
    /// </para>
    /// </summary>
    public bool   VerifyTls  { get; set; } = false;

    /// <summary>Nome da interface LAN no OPNsense (ex: "lan").</summary>
    public string LanInterface { get; set; } = "lan";

    /// <summary>
    /// IP fixo do computador Windows na LAN (ex: "10.0.0.100").
    /// Usado como source_net nas regras de firewall.
    /// </summary>
    public string PcIpAddress { get; set; } = string.Empty;
}

/// <summary>
/// Factory para construir um <see cref="HttpClient"/> configurado para o OPNsense.
/// Inclui autenticação Basic Auth (ApiKey:ApiSecret) e opção de ignorar TLS.
/// </summary>
public static class OpnsenseHttpClientFactory
{
    /// <summary>
    /// Cria um <see cref="HttpClient"/> pronto para uso com o OPNsense.
    /// </summary>
    /// <param name="settings">Configurações de conexão (Host, ApiKey, VerifyTls).</param>
    /// <param name="apiSecret">
    /// API Secret do OPNsense. Deve ser carregado pelo chamador via SecretStore
    /// ou outro mecanismo seguro. Nunca armazene este valor em texto simples.
    /// </param>
    public static HttpClient Create(OpnsenseSettings settings, string apiSecret)
    {
        if (string.IsNullOrWhiteSpace(apiSecret))
            throw new ArgumentException("API Secret cannot be empty.", nameof(apiSecret));

        var handler = new HttpClientHandler();
        if (!settings.VerifyTls)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(settings.Host.TrimEnd('/') + "/")
        };

        // Basic Auth: ApiKey como username, ApiSecret como password
        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{settings.ApiKey}:{apiSecret}"));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        client.Timeout = TimeSpan.FromSeconds(30);

        return client;
    }
}
