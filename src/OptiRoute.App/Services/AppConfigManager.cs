using System.IO;
using System.Text.Json;

namespace OptiRoute.App.Services;

/// <summary>
/// Modelo de preferências locais armazenado em %APPDATA%\OptiRoute\config.json.
/// Apenas preferências de interface + cache local. OPNsense é a autoridade do estado global.
///
/// Schema versioning: incrementar <see cref="SchemaVersion"/> quando mudar o shape.
/// Migradores futuros leem a versão antiga e aplicam transforms no Load().
/// </summary>
public sealed class AppConfig
{
    /// <summary>Versão atual do schema. Configs antigos sem esta chave são tratados como v0.</summary>
    public int SchemaVersion { get; set; } = 1;

    // ── Connection ───────────────────────────────────────────────────────────

    public string OpnsenseHost { get; set; } = "https://10.0.0.1";
    public string LanInterface { get; set; } = "lan";

    /// <summary>IP local preferido (manual override). Se vazio, detectado via LocalNetworkDetector.</summary>
    public string PreferredLocalIp { get; set; } = string.Empty;

    // ── Culture / i18n ────────────────────────────────────────────────────────

    /// <summary>Culture code (en-US, pt-BR). Cultura aplicada no startup pelo App.xaml.cs.</summary>
    public string Culture { get; set; } = string.Empty;

    // ── Gateways ──────────────────────────────────────────────────────────────

    /// <summary>Mapeamento {OPNsense gateway name → display name}. Substitui "CustomDisplayNames".</summary>
    public Dictionary<string, string> GatewayDisplayNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ── UI state ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Último diretório usado pelo file picker de "Add Application" (InitialDirectory
    /// do OpenFileDialog). Persistido entre aberturas do dialog. Default: Program Files.
    /// </summary>
    public string LastUsedFolder { get; set; } = DefaultFolder;

    /// <summary>Diretório default quando <see cref="LastUsedFolder"/> está vazio.</summary>
    public static string DefaultFolder =>
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

    // ── Advanced ─────────────────────────────────────────────────────────────

    public bool StartMinimizedToTray { get; set; }
    public bool StartWithWindows { get; set; }

    /// <summary>DSCP pool range managed pelo App. Default 0-63. Avançado — usuário normal não toca.</summary>
    /// <summary>
    /// Default DSCP pool range. Starts at 32 (CS5/AF41–AF43) so the first
    /// allocated DSCP remains the curated OptiRoute default (33) instead of 1.
    /// Range 0–31 stays reserved for CS0/CS1/AFxx/EF-class traffic that other
    /// QoS systems on the host may use. Reservations inside the range
    /// (CS6=48, EF=46) are still skipped by <see cref="DscpRegistry"/>.
    /// </summary>
    public int DscpPoolStart { get; set; } = 32;
    public int DscpPoolEnd   { get; set; } = 63;

    public int LogRetentionDays { get; set; } = 30;
    public bool ShowTechnicalInformation { get; set; }

    /// <summary>
    /// Quando <c>true</c>, o cliente HTTP aceita certificados TLS self-signed/inválidos
    /// ao falar com o OPNsense. Default <c>false</c> — validação TLS ligada — para impedir
    /// que um atacante intercepte as credenciais Basic (key:secret) via MITM com cert falso.
    /// Só deve ser habilitado para um OPNsense com certificado self-signed em rede confiável.
    /// </summary>
    public bool AllowInsecureTls { get; set; }
}

public static class AppConfigManager
{
    public const int CurrentSchemaVersion = 1;

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OptiRoute",
        "config.json");

    private static readonly string ConfigTempPath = ConfigPath + ".tmp";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Indica se <c>config.json</c> existe no disco. Usado pelo App para detectar
    /// primeira execução e lançar o Wizard automaticamente.
    /// </summary>
    public static bool Exists() => File.Exists(ConfigPath);

    /// <summary>
    /// Diretório raiz dos dados locais do app (<c>config.json</c>, <c>credentials.bin</c>,
    /// <c>OptiRoute.log</c>): <c>%APPDATA%\OptiRoute</c>. Usado pelo atalho "Open log folder"
    /// do menu de Diagnostics.
    /// </summary>
    public static string LogFolderPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OptiRoute");

    /// <summary>
    /// Lê config.json. Falhas retornam <c>new AppConfig()</c> (best-effort).
    /// Migrador simples de schema: se <c>SchemaVersion &lt; CurrentSchemaVersion</c>, aplica transforms.
    /// </summary>
    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return new AppConfig();

            var json = File.ReadAllText(ConfigPath);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts) ?? new AppConfig();

            if (cfg.SchemaVersion < CurrentSchemaVersion)
                cfg = Migrate(cfg, cfg.SchemaVersion);

            // Configs antigos (v1) não têm LastUsedFolder — aplica o default.
            if (string.IsNullOrWhiteSpace(cfg.LastUsedFolder))
                cfg.LastUsedFolder = AppConfig.DefaultFolder;

            return cfg;
        }
        catch
        {
            return new AppConfig();
        }
    }

    /// <summary>
    /// Save **atômico**: escreve em <c>config.json.tmp</c> e usa <see cref="File.Replace(string, string, string?)"/>
    /// para trocar em uma operação (não há janela onde o arquivo fica meio escrito).
    /// Falhas são engolidas (best-effort, mesma semântica da versão anterior).
    /// </summary>
    public static void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            config.SchemaVersion = CurrentSchemaVersion;

            // Nunca persistir vazio — mantém um InitialDirectory válido para o picker.
            if (string.IsNullOrWhiteSpace(config.LastUsedFolder))
                config.LastUsedFolder = AppConfig.DefaultFolder;

            var json = JsonSerializer.Serialize(config, JsonOpts);

            File.WriteAllText(ConfigTempPath, json);

            // File.Replace é atômico no NTFS; se o destino não existir ainda, fallback para Move
            if (File.Exists(ConfigPath))
                File.Replace(ConfigTempPath, ConfigPath, destinationBackupFileName: null);
            else
                File.Move(ConfigTempPath, ConfigPath);
        }
        catch
        {
            try { if (File.Exists(ConfigTempPath)) File.Delete(ConfigTempPath); } catch { /* swallow */ }
        }
    }

    private static AppConfig Migrate(AppConfig old, int fromVersion)
    {
        // Placeholder para migradores futuros. Hoje só v0→v1 (no-op além de marcar versão).
        old.SchemaVersion = CurrentSchemaVersion;
        return old;
    }

    /// <summary>
    /// Caminho do arquivo de credenciais (DPAPI). Mesmo diretório do <see cref="ConfigPath"/>.
    /// Mantido em sincronia com <c>OptiRoute.Windows.Security.SecretStore</c>.
    /// </summary>
    private static readonly string CredentialsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OptiRoute",
        "credentials.bin");

    /// <summary>
    /// Caminho do log da aplicação. NUNCA é removido pelo reset (diagnóstico).
    /// </summary>
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OptiRoute",
        "OptiRoute.log");

    /// <summary>
    /// Restaura o app ao estado de primeira execução removendo <c>config.json</c> e
    /// <c>credentials.bin</c> de <c>%APPDATA%\OptiRoute</c>. O <c>OptiRoute.log</c> é
    /// preservado para diagnóstico.
    /// <para>
    /// Best-effort: cada remoção é independente e envolta em try/catch — uma falha em um
    /// arquivo não impede a tentativa no outro. Falhas são registradas como warning no log.
    /// </para>
    /// </summary>
    public static void ResetToDefaults()
    {
        TryDelete(ConfigPath, "config.json");
        TryDelete(CredentialsPath, "credentials.bin");

        // Um Save interrompido pode ter deixado o arquivo temporário para trás.
        TryDelete(ConfigTempPath, "config.json.tmp");
    }

    private static void TryDelete(string path, string label)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                Log("Information", $"Reset: deleted {label} ({path})");
            }
            else
            {
                Log("Information", $"Reset: {label} not found ({path}) — nothing to delete");
            }
        }
        catch (Exception ex)
        {
            Log("Warning", $"Reset: failed to delete {label} ({path}): {ex.Message}");
        }
    }

    /// <summary>
    /// Append best-effort no log do app (mesmo formato do <c>FileLoggerProvider</c>).
    /// Nunca deixa uma falha de logging escapar.
    /// </summary>
    private static void Log(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level,-11}] OptiRoute.App.Services.AppConfigManager: {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging nunca pode quebrar o app.
        }
    }
}
