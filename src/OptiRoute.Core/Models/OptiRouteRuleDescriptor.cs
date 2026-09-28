using System.Net;

namespace OptiRoute.Core.Models;

/// <summary>
/// Tipo da regra do OptiRoute no firewall.
/// </summary>
public enum OptiRouteRuleType
{
    Default,
    Override
}

/// <summary>
/// Descritor de regra estruturada do OptiRoute para o OPNsense.
/// Formato padronizado (sempre com marcador de versão no final):
/// <code>
/// DEFAULT:  OPTIROUTE|DEFAULT|bf6.exe|33|&lt;AppId&gt;|v=1
/// OVERRIDE: OPTIROUTE|OVERRIDE|bf6.exe|33|10.0.0.122|&lt;AppId&gt;|v=1
/// </code>
/// </summary>
public sealed record OptiRouteRuleDescriptor
{
    public const string Prefix        = "OPTIROUTE|";
    public const string CurrentVersion = "v=1";

    public OptiRouteRuleType RuleType { get; init; }
    public string ExecutableName { get; init; } = string.Empty;
    public int Dscp { get; init; }
    public string AppId { get; init; } = string.Empty;
    public IPAddress? SourceIp { get; init; }
    public string? Uuid { get; init; }
    public string Gateway { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
    public int Sequence { get; init; } = 1;

    /// <summary>
    /// Converte este descritor para o texto de descrição estruturada salvo no OPNsense.
    /// Sempre inclui o marcador <c>|v=1</c> como último campo.
    /// </summary>
    public string FormatDescription()
    {
        var cleanExe = Path.GetFileName(ExecutableName).Trim().ToLowerInvariant();

        if (RuleType == OptiRouteRuleType.Default)
        {
            return string.IsNullOrWhiteSpace(AppId)
                ? $"OPTIROUTE|DEFAULT|{cleanExe}|{Dscp}|{CurrentVersion}"
                : $"OPTIROUTE|DEFAULT|{cleanExe}|{Dscp}|{AppId}|{CurrentVersion}";
        }

        var ipStr = SourceIp?.ToString() ?? "any";
        return string.IsNullOrWhiteSpace(AppId)
            ? $"OPTIROUTE|OVERRIDE|{cleanExe}|{Dscp}|{ipStr}|{CurrentVersion}"
            : $"OPTIROUTE|OVERRIDE|{cleanExe}|{Dscp}|{ipStr}|{AppId}|{CurrentVersion}";
    }

    /// <summary>
    /// Tenta fazer o parse de uma descrição do OPNsense para um <see cref="OptiRouteRuleDescriptor"/>.
    /// Suporta formatos estruturados e compatibilidade retroativa com regras legadas.
    /// </summary>
    public static bool TryParse(string description, out OptiRouteRuleDescriptor? descriptor)
    {
        descriptor = null;
        if (string.IsNullOrWhiteSpace(description))
            return false;

        var trimmed = description.Trim();

        // 1. Formato Estruturado: OPTIROUTE|...
        if (trimmed.StartsWith("OPTIROUTE|", StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed.Split('|');
            if (parts.Length < 4)
                return false;

            // Detectar e remover marcador de versão (último campo `v=N`).
            // Apenas a versão atual (`v=1`) é aceita; versões futuras usarão ParseV2() etc.
            var lastToken = parts[^1].Trim();
            if (lastToken.StartsWith("v=", StringComparison.OrdinalIgnoreCase))
            {
                if (!lastToken.Equals(CurrentVersion, StringComparison.OrdinalIgnoreCase))
                    return false; // versão desconhecida; recusar para evitar leitura silenciosa
                parts = parts.Take(parts.Length - 1).ToArray();
            }

            var typeStr = parts[1].Trim().ToUpperInvariant();
            var exe     = parts[2].Trim().ToLowerInvariant();

            if (!int.TryParse(parts[3].Trim(), out var dscp))
                return false;

            if (typeStr == "DEFAULT")
            {
                var appId = parts.Length >= 5 ? parts[4].Trim() : string.Empty;
                descriptor = new OptiRouteRuleDescriptor
                {
                    RuleType       = OptiRouteRuleType.Default,
                    ExecutableName = exe,
                    Dscp           = dscp,
                    AppId          = appId
                };
                return true;
            }

            if (typeStr == "OVERRIDE" && parts.Length >= 5)
            {
                var ipStr = parts[4].Trim();
                if (!IPAddress.TryParse(ipStr, out var ip))
                    return false;

                var appId = parts.Length >= 6 ? parts[5].Trim() : string.Empty;

                descriptor = new OptiRouteRuleDescriptor
                {
                    RuleType       = OptiRouteRuleType.Override,
                    ExecutableName = exe,
                    Dscp           = dscp,
                    SourceIp       = ip,
                    AppId          = appId
                };
                return true;
            }
        }

        // 2. Compatibilidade com regras legadas da PoC (ex: OPTIRoute_CURL, OPTIRoute_WAN2)
        if (trimmed.StartsWith("OPTIRoute_", StringComparison.OrdinalIgnoreCase))
        {
            var suffix = trimmed["OPTIRoute_".Length..].ToLowerInvariant();
            descriptor = new OptiRouteRuleDescriptor
            {
                RuleType       = OptiRouteRuleType.Default,
                ExecutableName = suffix.EndsWith(".exe") ? suffix : $"{suffix}.exe",
                Dscp           = 0, // será lido do ToS na regra
                AppId          = string.Empty
            };
            return true;
        }

        return false;
    }
}
