using System.Text;
using System.Text.RegularExpressions;

namespace OptiRoute.Core.Diagnostics;

/// <summary>
/// Sanitização pura (sem I/O) de relatórios de diagnóstico: remove segredos
/// (credenciais, tokens) e mascara o host OPNsense. Extraído de
/// <c>OptiRoute.App.Services.DiagnosticsExporter</c> para permitir teste unitário
/// sem dependência de WPF.
///
/// Camadas de redação:
///   0. token Basic (base64 de key:secret) e valores literais de ApiKey/ApiSecret;
///   2. headers Authorization/Bearer e credenciais userinfo em URL;
///   3. pares chave=valor de nomes sensíveis;
///   4. host OPNsense (IP/hostname interno) mascarado.
/// </summary>
public static class DiagnosticsSanitizer
{
    private const string Redacted = "***REDACTED***";

    /// <summary>
    /// Detecta pares <c>chave = valor</c> de nomes sensíveis e os redige.
    /// Cobre api key/secret, password e token. Headers <c>Authorization</c>/<c>Bearer</c>
    /// são tratados por regex dedicadas (ver <see cref="AuthorizationHeaderRegex"/>),
    /// pois o valor do header tem um segundo token (Basic/Bearer) após um espaço.
    /// </summary>
    private static readonly Regex SecretPattern = new(
        @"(?i)\b(api[_-]?key|api[_-]?secret|secret|password|passwd|token)\b\s*[=:]\s*\S+",
        RegexOptions.Compiled);

    /// <summary>
    /// Redige o header de autenticação INTEIRO, incluindo o base64 do Basic ou o token
    /// Bearer após o espaço — que a regex de chave=valor não alcançava.
    /// Ex.: <c>Authorization: Basic YWJjOmRlZg==</c> → <c>Authorization: ***REDACTED***</c>.
    /// </summary>
    private static readonly Regex AuthorizationHeaderRegex = new(
        @"\bAuthorization\s*[:=]\s*(?:Basic|Bearer)\s+[A-Za-z0-9+/=_.-]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Redige tokens Bearer standalone (sem o prefixo Authorization).</summary>
    private static readonly Regex BearerTokenRegex = new(
        @"\bBearer\s+[A-Za-z0-9+/=_.-]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Redige credenciais embutidas em URL (<c>https://user:pass@host</c>).</summary>
    private static readonly Regex UserInfoUrlRegex = new(
        @"(?<scheme>https?://)[^/\s:@]+:[^/\s@]+@",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Sanitiza <paramref name="content"/> removendo segredos e o host OPNsense.
    /// Pura quando os parâmetros são fornecidos (sem I/O) — permite teste unitário.
    /// </summary>
    /// <param name="content">Texto bruto (ex.: relatório de diagnóstico montado).</param>
    /// <param name="apiKey">API key real; redigida literalmente e usada para compor o Basic.</param>
    /// <param name="apiSecret">API secret real; redigido literalmente e usado para compor o Basic.</param>
    /// <param name="host">Host OPNsense real; mascarado como <c>[OPNSENSE-HOST]</c>.</param>
    public static string Sanitize(
        string content,
        string? apiKey = null,
        string? apiSecret = null,
        string? host = null)
    {
        if (string.IsNullOrEmpty(content))
            return content ?? string.Empty;

        var sanitized = content;

        // Camada 0: o token Basic é exatamente base64("key:secret") — redige-o
        // explicitamente, pois pode aparecer em header, log ou curl (qualquer formato).
        if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
        {
            try
            {
                var basicToken = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{apiKey}:{apiSecret}"));
                sanitized = sanitized.Replace(basicToken, Redacted, StringComparison.Ordinal);
            }
            catch
            {
                // best-effort: cai nas camadas seguintes.
            }
        }

        // Camada 1: valores literais de credenciais onde quer que apareçam.
        if (!string.IsNullOrEmpty(apiKey))
            sanitized = sanitized.Replace(apiKey, Redacted, StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(apiSecret))
            sanitized = sanitized.Replace(apiSecret, Redacted, StringComparison.Ordinal);

        // Camada 2: headers/tokens de autenticação (defense-in-depth).
        sanitized = AuthorizationHeaderRegex.Replace(sanitized, $"Authorization: {Redacted}");
        sanitized = BearerTokenRegex.Replace(sanitized, $"Bearer {Redacted}");
        sanitized = UserInfoUrlRegex.Replace(
            sanitized, m => $"{m.Groups["scheme"].Value}{Redacted}@");

        // Camada 3: pares chave=valor de nomes sensíveis (formatos não-header).
        sanitized = SecretPattern.Replace(sanitized, m => $"{m.Groups[1].Value}={Redacted}");

        // Camada 4: host OPNsense (IP/hostname interno) mascarado em todo o conteúdo.
        if (!string.IsNullOrWhiteSpace(host))
            sanitized = sanitized.Replace(host, "[OPNSENSE-HOST]", StringComparison.OrdinalIgnoreCase);

        return sanitized;
    }
}
