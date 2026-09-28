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

    // ── Advanced ─────────────────────────────────────────────────────────────

    public bool StartMinimizedToTray { get; set; }
    public bool StartWithWindows { get; set; }

    /// <summary>DSCP pool range managed pelo App. Default 0-63. Avançado — usuário normal não toca.</summary>
    public int DscpPoolStart { get; set; } = 0;
    public int DscpPoolEnd { get; set; } = 63;

    public int LogRetentionDays { get; set; } = 30;
    public bool ShowTechnicalInformation { get; set; }
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
}
