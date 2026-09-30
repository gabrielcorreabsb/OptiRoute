using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using OptiRoute.Core.Diagnostics;
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

        return DiagnosticsSanitizer.Sanitize(sb.ToString(), apiKey, apiSecret, host);
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
}
