using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OptiRoute.Windows.Security;

namespace OptiRoute.App.Services;

/// <summary>
/// Gera e grava um relatório de diagnóstico <b>sanitizado</b> (sem segredos) para
/// suporte. O conteúdo inclui informações do sistema, resumo de configuração
/// (vals. não sensíveis), presença de credenciais (present/missing — nunca os
/// valores) e as últimas 200 linhas do log.
///
/// Sanitização em camadas:
///   1. token Basic (base64 de key:secret) e valores literais de ApiKey/ApiSecret;
///   2. headers Authorization/Bearer e credenciais userinfo em URL;
///   3. pares chave=valor de nomes sensíveis;
///   4. host OPNsense (IP/hostname interno) mascarado.
/// </summary>
public static class DiagnosticsExporter
{
    private const int LogTailLines = 200;
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

    /// <summary>Grava o conteúdo (já sanitizado) no caminho indicado, em UTF-8 sem BOM.</summary>
    public static void Export(string outputPath, string sanitizedContent)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path is required.", nameof(outputPath));

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(outputPath, sanitizedContent ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// Constrói o relatório de diagnóstico completo e sanitizado.
    /// Nunca lança: cada seção é best-effort.
    /// </summary>
    public static string BuildContent()
    {
        var sb = new StringBuilder();

        sb.AppendLine("OptiRoute Diagnostics Export");
        sb.AppendLine($"Generated: {DateTime.UtcNow:O}");
        sb.AppendLine();

        AppendSystemInfo(sb);
        AppendConfigSummary(sb);
        AppendCredentialPresence(sb);
        AppendLogTail(sb);

        // Carrega segredos/host uma única vez e delega à sanitização pura (testável).
        string? apiKey = null, apiSecret = null, host = null;
        try
        {
            var creds = SecretStore.LoadCredentials();
            apiKey    = creds?.ApiKey;
            apiSecret = creds?.ApiSecret;
        }
        catch { /* best-effort */ }
        try { host = AppConfigManager.Load().OpnsenseHost; } catch { /* best-effort */ }

        return Sanitize(sb.ToString(), apiKey, apiSecret, host);
    }

    private static void AppendSystemInfo(StringBuilder sb)
    {
        sb.AppendLine("== System ==");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"OS Architecture: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($".NET Runtime: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Machine Name: {Environment.MachineName}");
        sb.AppendLine($"Current Culture: {CultureInfo.CurrentCulture.Name} ({CultureInfo.CurrentCulture.DisplayName})");
        sb.AppendLine($"UI Culture: {CultureInfo.CurrentUICulture.Name}");
        sb.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
        sb.AppendLine($"Processor Count: {Environment.ProcessorCount}");
        sb.AppendLine();
    }

    private static void AppendConfigSummary(StringBuilder sb)
    {
        sb.AppendLine("== Configuration (sanitized — no secrets) ==");
        try
        {
            var cfg = AppConfigManager.Load();
            // Nunca exportar o host real (IP/hostname interno) — apenas o marcador.
            sb.AppendLine($"OpnsenseHost: {(string.IsNullOrWhiteSpace(cfg.OpnsenseHost) ? "(not set)" : "[OPNSENSE-HOST]")}");
            sb.AppendLine($"LanInterface: {cfg.LanInterface}");
            sb.AppendLine($"Culture: {cfg.Culture}");
            sb.AppendLine($"StartWithWindows: {cfg.StartWithWindows}");
            sb.AppendLine($"MinimizeToTray: {cfg.StartMinimizedToTray}");
            sb.AppendLine($"ShowTechnical: {cfg.ShowTechnicalInformation}");
            // EnableAdvancedDscp não é persistido diretamente: derive do pool não-default.
            sb.AppendLine($"EnableAdvancedDscp: {cfg.DscpPoolStart != 0 || cfg.DscpPoolEnd != 63}");
            sb.AppendLine($"DscpPoolStart: {cfg.DscpPoolStart}");
            sb.AppendLine($"DscpPoolEnd: {cfg.DscpPoolEnd}");
            sb.AppendLine($"LogRetentionDays: {cfg.LogRetentionDays}");
            sb.AppendLine($"GatewayDisplayNames: {cfg.GatewayDisplayNames.Count} entries (values omitted)");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"(failed to read configuration: {ex.Message})");
        }
        sb.AppendLine();
    }

    private static void AppendCredentialPresence(StringBuilder sb)
    {
        sb.AppendLine("== Credentials ==");
        try
        {
            var creds = SecretStore.LoadCredentials();
            sb.AppendLine($"ApiKey: {(string.IsNullOrWhiteSpace(creds?.ApiKey) ? "missing" : "present")}");
            sb.AppendLine($"ApiSecret: {(string.IsNullOrWhiteSpace(creds?.ApiSecret) ? "missing" : "present")}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"(failed to read credential presence: {ex.Message})");
        }
        sb.AppendLine();
    }

    private static void AppendLogTail(StringBuilder sb)
    {
        sb.AppendLine($"== Log tail (last {LogTailLines} lines) — {OptiRoute.App.App.LogPath} ==");
        try
        {
            if (!File.Exists(OptiRoute.App.App.LogPath))
            {
                sb.AppendLine("(log file not found)");
            }
            else
            {
                // Ring buffer: mantém apenas as últimas N linhas sem carregar o arquivo
                // inteiro na memória (File.ReadLines é lazy e streama linha a linha).
                var tail = new Queue<string>(LogTailLines);
                foreach (var line in File.ReadLines(OptiRoute.App.App.LogPath))
                {
                    if (tail.Count == LogTailLines)
                        tail.Dequeue();
                    tail.Enqueue(line);
                }

                foreach (var line in tail)
                    sb.AppendLine(line);
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"(failed to read log: {ex.Message})");
        }
        sb.AppendLine();
    }

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
