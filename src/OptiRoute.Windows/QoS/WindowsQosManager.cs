using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using OptiRoute.Core.Exceptions;
using OptiRoute.Core.Interfaces;
using OptiRoute.Core.Models;

namespace OptiRoute.Windows.QoS;

/// <summary>
/// Implementação de <see cref="IWindowsQosManager"/> usando cmdlets PowerShell.
/// <para>
/// Todas as políticas criadas recebem o prefixo "OptiRoute-" para identificação.
/// </para>
/// <para>
/// Requer que o processo esteja rodando com privilégios de Administrador.
/// </para>
/// </summary>
public sealed class WindowsQosManager : IWindowsQosManager
{
    internal const string PolicyPrefix        = "OptiRoute-";
    internal const string ScriptFilePrefix    = "optiroute_";
    internal const string ScriptFileExtension = ".ps1";

    /// <summary>
    /// Idade mínima (UTC) para um arquivo .ps1 em %TEMP% ser considerado "órfão".
    /// Arquivos mais novos são deixados em paz para não interromper execuções concorrentes.
    /// </summary>
    internal static readonly TimeSpan OrphanedScriptThreshold = TimeSpan.FromMinutes(10);

    private readonly ILogger<WindowsQosManager> _logger;

    public WindowsQosManager(ILogger<WindowsQosManager> logger)
    {
        _logger = logger;

        // Cleanup de scripts .ps1 órfãos (crash/kill anteriores). Best-effort —
        // qualquer exceção é logada e silenciosamente engolida: não pode impedir a inicialização.
        try
        {
            var removed = CleanupOrphanedScripts(logger: logger);
            if (removed > 0)
                _logger.LogInformation("[WindowsQoS] Startup cleanup removed {Count} orphaned script(s)", removed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[WindowsQoS] Orphaned script cleanup failed at startup");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cleanup de scripts órfãos (Delta 6)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Remove scripts PowerShell temporários deixados por execuções anteriores
    /// (crash, kill, etc.) que sejam mais antigos que <see cref="OrphanedScriptThreshold"/>.
    /// Todos os parâmetros são opcionais para permitir testes determinísticos.
    /// </summary>
    /// <param name="tempDirectory">Diretório a varrer (default: <see cref="Path.GetTempPath"/>).</param>
    /// <param name="patternOverride">Pattern glob (default: "optiroute_*.ps1").</param>
    /// <param name="nowUtc">Timestamp de referência (default: <see cref="DateTime.UtcNow"/>).</param>
    /// <param name="logger">Logger opcional para reportar contadores.</param>
    /// <returns>Número de arquivos removidos.</returns>
    internal static int CleanupOrphanedScripts(
        string? tempDirectory   = null,
        string? patternOverride  = null,
        DateTime? nowUtc         = null,
        ILogger? logger          = null)
    {
        var dir     = tempDirectory ?? Path.GetTempPath();
        var pattern = patternOverride ?? $"{ScriptFilePrefix}*{ScriptFileExtension}";
        var cutoff  = (nowUtc ?? DateTime.UtcNow) - OrphanedScriptThreshold;

        if (!Directory.Exists(dir))
            return 0;

        int removed = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(dir, pattern))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                    {
                        File.Delete(file);
                        removed++;
                    }
                }
                catch (Exception ex)
                {
                    // Arquivo em uso, permissão negada, etc. — ignora e segue
                    logger?.LogDebug("[WindowsQoS] Could not delete orphaned script {File}: {Error}",
                        file, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            // Falha de enumeração do diretório — também não bloqueia o app
            logger?.LogDebug(ex, "[WindowsQoS] Directory enumeration failed during orphan cleanup");
        }

        if (removed > 0)
            logger?.LogInformation("[WindowsQoS] Cleaned {Count} orphaned script(s) from {Dir}", removed, dir);

        return removed;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Operações públicas
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<bool> PolicyExistsAsync(string executableName, CancellationToken ct = default)
    {
        var policyName = BuildPolicyName(executableName);
        var script = string.Join(Environment.NewLine,
            $"$p = Get-NetQosPolicy -Name \"{policyName}\" -ErrorAction SilentlyContinue",
            "if ($p) { 'true' } else { 'false' }");

        var result = await RunScriptAsync(script, ct);
        return result.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task CreatePolicyAsync(string executableName, int dscp, CancellationToken ct = default)
    {
        ValidateDscp(dscp);
        var policyName = BuildPolicyName(executableName);

        _logger.LogInformation("[WindowsQoS] Creating policy {Name} DSCP={Dscp}", policyName, dscp);

        // Idempotência: remove se já existir para garantir DSCP correto
        await DeletePolicyAsync(executableName, ct);

        var script = string.Join(Environment.NewLine,
            $"New-NetQosPolicy `",
            $"    -Name \"{policyName}\" `",
            $"    -PolicyStore ActiveStore `",
            $"    -AppPathNameMatchCondition \"{executableName}\" `",
            $"    -DSCPAction {dscp} `",
            $"    -NetworkProfile All `",
            $"    -ErrorAction Stop | Out-Null",
            "Write-Host 'OK'");

        var result = await RunScriptAsync(script, ct);
        if (result.ExitCode != 0 || !result.Output.Contains("OK"))
            throw new QosPolicyException(
                $"Failed to create QoS policy '{policyName}': {result.Error}");

        _logger.LogInformation("[WindowsQoS] Created {Name} DSCP={Dscp} App={App}",
            policyName, dscp, executableName);
    }

    /// <inheritdoc />
    public async Task DeletePolicyAsync(string executableName, CancellationToken ct = default)
    {
        var policyName = BuildPolicyName(executableName);

        // 1) Tentar ActiveStore (caminho normal)
        var (success, lastOutput) = await TryDeletePolicyFromStoreAsync(policyName, "ActiveStore", ct);
        if (success)
        {
            _logger.LogInformation("[WindowsQoS] Deleted {Name} (ActiveStore)", policyName);
            return;
        }

        // 2) Fallback: tentar GPO store local — cobre policies Owner="Group Policy (Machine)"
        _logger.LogInformation("[WindowsQoS] ActiveStore delete failed for {Name}, trying GPO:$env:COMPUTERNAME", policyName);
        var (gpoSuccess, _) = await TryDeletePolicyFromStoreAsync(policyName, "GPO:$env:COMPUTERNAME", ct);
        if (gpoSuccess)
        {
            _logger.LogInformation("[WindowsQoS] Deleted {Name} (GPO store)", policyName);
            return;
        }

        var owner = ExtractOwner(lastOutput) ?? "Unknown";
        throw new QosPolicyException(
            $"QoS policy '{policyName}' could not be removed from ActiveStore or GPO store. " +
            $"Owner='{owner}'. Edit/delete via Local Group Policy Editor (gpedit.msc) → " +
            $"Computer Configuration → Windows Settings → Policy-based QoS, OR run in elevated PowerShell: " +
            $"Remove-NetQosPolicy -Name \"{policyName}\" -PolicyStore \"GPO:$env:COMPUTERNAME\" -Confirm:$false");
    }

    /// <summary>
    /// Helper compartilhado por ambos os overloads de Delete. Tenta remover a policy do
    /// store especificado, com verify-after-remove. Retorna (true, output) em caso de
    /// sucesso (incluindo 'NotFound' idempotente) e (false, output) em falha.
    /// </summary>
    private async Task<(bool Success, string Output)> TryDeletePolicyFromStoreAsync(
        string policyName, string store, CancellationToken ct)
    {
        var script = string.Join(Environment.NewLine,
            $"$p = Get-NetQosPolicy -Name \"{policyName}\" -PolicyStore {store} -ErrorAction SilentlyContinue",
            "if (-not $p) { Write-Host 'NotFound'; exit 0 }",
            "try {",
            $"    Remove-NetQosPolicy -Name \"{policyName}\" -PolicyStore {store} -Confirm:$false -ErrorAction Stop",
            "    Start-Sleep -Milliseconds 200",
            $"    $check = Get-NetQosPolicy -Name \"{policyName}\" -PolicyStore {store} -ErrorAction SilentlyContinue",
            "    if ($check) { Write-Host \"RemoveFailed:policy still exists (Owner=$($check.Owner))\"; exit 1 }",
            "    Write-Host 'Removed'",
            "} catch {",
            "    Write-Host \"RemoveFailed:$($_.Exception.Message)\"",
            "    exit 1",
            "}");

        var result = await RunScriptAsync(script, ct);
        var output = result.Output ?? string.Empty;
        return (output.Contains("Removed", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("NotFound", StringComparison.OrdinalIgnoreCase), output);
    }

    private static string? ExtractOwner(string output)
    {
        const string marker = "Owner=";
        var idx = output.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var rest = output.Substring(idx + marker.Length);
        var endIdx = rest.IndexOfAny(new[] { ')', '\r', '\n' });
        return endIdx < 0 ? rest.Trim() : rest.Substring(0, endIdx).Trim();
    }

    /// <inheritdoc />
    public async Task DeletePolicyAsync(LocalQosPolicy policy, CancellationToken ct = default)
    {
        if (!policy.IsOptiRouteManaged)
        {
            _logger.LogWarning("[WindowsQoS] Refusing to delete policy '{Name}' as it is not managed by OptiRoute.", policy.Name);
            return;
        }

        // Mesmo padrão de verify-after-remove do overload string:
        // Remove-NetQosPolicy falha silenciosamente para Owner="Group Policy (Machine)".
        var storeArg = !string.IsNullOrWhiteSpace(policy.PolicyStore)
            ? $"-PolicyStore \"{policy.PolicyStore}\""
            : "-PolicyStore ActiveStore";

        var script = string.Join(Environment.NewLine,
            $"$before = Get-NetQosPolicy -Name \"{policy.Name}\" {storeArg} -ErrorAction SilentlyContinue",
            "if (-not $before) { Write-Host 'NotFound'; exit 0 }",
            "try {",
            $"    Remove-NetQosPolicy -Name \"{policy.Name}\" {storeArg} -Confirm:$false -ErrorAction SilentlyContinue",
            "    Start-Sleep -Milliseconds 200",
            $"    $check = Get-NetQosPolicy -Name \"{policy.Name}\" {storeArg} -ErrorAction SilentlyContinue",
            "    if ($check) { Write-Host \"RemoveFailed:policy still exists (Owner=$($check.Owner))\"; exit 1 }",
            "    Write-Host 'Removed'",
            "} catch {",
            "    Write-Host \"RemoveFailed:$($_.Exception.Message)\"",
            "    exit 1",
            "}");

        var result = await RunScriptAsync(script, ct);

        if (result.Output.Contains("RemoveFailed", StringComparison.OrdinalIgnoreCase))
        {
            var marker = "Owner=";
            var idx = result.Output.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            var owner = idx >= 0 ? result.Output.Substring(idx + marker.Length).Trim() : "Unknown";
            _logger.LogError(
                "[WindowsQoS] Failed to delete '{Name}' from store '{Store}' — Owner='{Owner}' is not removable via PowerShell. " +
                "Edit/delete via Local Group Policy Editor (gpedit.msc) → " +
                "Computer Configuration → Windows Settings → Policy-based QoS.",
                policy.Name, policy.PolicyStore ?? "ActiveStore", owner);
            throw new QosPolicyException(
                $"QoS policy '{policy.Name}' is owned by '{owner}' and cannot be removed via PowerShell. " +
                $"Use gpedit.msc → Computer Configuration → Windows Settings → Policy-based QoS.");
        }
        else if (result.Output.Contains("Removed"))
            _logger.LogInformation("[WindowsQoS] Deleted policy '{Name}' from store '{Store}'", policy.Name, policy.PolicyStore ?? "ActiveStore");
        // 'NotFound' → nothing to delete, idempotent success (no log noise)
    }

    /// <inheritdoc />
    public async Task UpdatePolicyAsync(string executableName, int dscp, CancellationToken ct = default)
    {
        // New-NetQosPolicy não tem Set-*, então recriamos
        await CreatePolicyAsync(executableName, dscp, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QosPolicy>> ListOptiRoutePoliciesAsync(CancellationToken ct = default)
    {
        var script = string.Join(Environment.NewLine,
            "Get-NetQosPolicy -PolicyStore ActiveStore |",
            $"    Where-Object {{ $_.Name -like '{PolicyPrefix}*' }} |",
            "    Select-Object Name, AppPathNameMatchCondition, DSCPAction, @{Name='NetworkProfile';Expression={$_.NetworkProfile.ToString()}} |",
            "    ConvertTo-Json -Compress");

        var result = await RunScriptAsync(script, ct);

        if (result.ExitCode != 0)
            throw new QosPolicyException($"Failed to list QoS policies: {result.Error}");

        var json = result.Output.Trim();
        if (string.IsNullOrWhiteSpace(json) || json == "null")
            return Array.Empty<QosPolicy>();

        return ParsePoliciesJson(json);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocalQosPolicy>> ListLocalPoliciesAsync(CancellationToken ct = default)
    {
        var script = string.Join(Environment.NewLine,
            "Get-NetQosPolicy -PolicyStore ActiveStore -ErrorAction SilentlyContinue |",
            $"    Where-Object {{ $_.Name -like '{PolicyPrefix}*' }} |",
            "    Select-Object Name, AppPathNameMatchCondition, AppPathName, DSCPAction, DSCPValue, Owner, PolicyStore |",
            "    ConvertTo-Json -Compress");

        var result = await RunScriptAsync(script, ct);

        if (result.ExitCode != 0)
        {
            _logger.LogWarning("[WindowsQoS] Failed to list local policies: {Error}", result.Error);
            return Array.Empty<LocalQosPolicy>();
        }

        var json = result.Output.Trim();
        if (string.IsNullOrWhiteSpace(json) || json == "null")
            return Array.Empty<LocalQosPolicy>();

        return ParseLocalPoliciesJson(json);
    }

    /// <inheritdoc />
    public async Task SyncAllAsync(
        IEnumerable<ApplicationRule> rules,
        IEnumerable<RoutingProfile>  profiles,
        CancellationToken ct = default)
    {
        var profileMap      = profiles.ToDictionary(p => p.Id);
        var existingPolicies = await ListOptiRoutePoliciesAsync(ct);
        var existingNames   = existingPolicies.Select(p => p.AppPathName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var activeRules  = rules.Where(r => r.Enabled).ToList();
        var expectedApps = activeRules
            .Select(r => r.ExecutableName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Criar políticas faltantes
        foreach (var rule in activeRules)
        {
            if (!profileMap.TryGetValue(rule.RoutingProfileId, out var profile))
            {
                _logger.LogWarning("[WindowsQoS] Profile {Id} not found for rule {App}",
                    rule.RoutingProfileId, rule.ExecutableName);
                continue;
            }

            if (!existingNames.Contains(rule.ExecutableName))
            {
                _logger.LogInformation("[WindowsQoS] Sync: creating missing policy for {App}", rule.ExecutableName);
                await CreatePolicyAsync(rule.ExecutableName, profile.DscpValue, ct);
            }
        }

        // Remover políticas obsoletas
        foreach (var policy in existingPolicies)
        {
            if (!expectedApps.Contains(policy.AppPathName))
            {
                _logger.LogInformation("[WindowsQoS] Sync: removing obsolete policy {Name}", policy.Name);
                await DeletePolicyAsync(policy.AppPathName, ct);
            }
        }

        _logger.LogInformation("[WindowsQoS] Sync complete. Active={Active}", activeRules.Count);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Auxiliares internos
    // ──────────────────────────────────────────────────────────────────────────

    internal static string BuildPolicyName(string executableName)
        => $"{PolicyPrefix}{Path.GetFileNameWithoutExtension(executableName)}";

    private static void ValidateDscp(int dscp)
    {
        if (dscp is < 0 or > 63)
            throw new ArgumentOutOfRangeException(nameof(dscp),
                $"DSCP value must be between 0 and 63 (got {dscp}).");
    }

    private async Task<PowerShellResult> RunScriptAsync(string script, CancellationToken ct)
    {
        // Escrever script em arquivo temporário evita problemas de escape no -Command
        var tempFile = Path.Combine(Path.GetTempPath(), $"optiroute_{Guid.NewGuid():N}.ps1");

        try
        {
            await File.WriteAllTextAsync(tempFile, script, Encoding.UTF8, ct);

            var psi = new ProcessStartInfo
            {
                FileName               = "powershell.exe",
                Arguments              = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{tempFile}\"",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding  = Encoding.UTF8
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            var outputTask = proc.StandardOutput.ReadToEndAsync(ct);
            var errorTask  = proc.StandardError.ReadToEndAsync(ct);

            await proc.WaitForExitAsync(ct);

            var output = await outputTask;
            var error  = await errorTask;

            if (!string.IsNullOrWhiteSpace(error))
                _logger.LogDebug("[WindowsQoS] PowerShell stderr: {Error}", error.Trim());

            return new PowerShellResult(proc.ExitCode, output, error);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* best-effort */ }
        }
    }

    private static IReadOnlyList<QosPolicy> ParsePoliciesJson(string json)
    {
        // ConvertTo-Json retorna objeto único se só há 1 item, array se houver mais
        if (!json.StartsWith('['))
            json = $"[{json}]";

        var items = System.Text.Json.JsonSerializer.Deserialize<PsQosPolicyDto[]>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? Array.Empty<PsQosPolicyDto>();

        return items.Select(dto =>
        {
            string profile = dto.NetworkProfile.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => dto.NetworkProfile.GetString() ?? string.Empty,
                System.Text.Json.JsonValueKind.Number => dto.NetworkProfile.GetInt32() switch
                {
                    0 => "All",
                    1 => "Domain",
                    2 => "Private",
                    3 => "Public",
                    _ => dto.NetworkProfile.ToString()
                },
                _ => dto.NetworkProfile.ToString()
            };

            return new QosPolicy
            {
                Name           = dto.Name ?? string.Empty,
                AppPathName    = dto.AppPathNameMatchCondition ?? string.Empty,
                DscpAction     = dto.DSCPAction,
                NetworkProfile = profile
            };
        }).ToList();
    }

    private static IReadOnlyList<LocalQosPolicy> ParseLocalPoliciesJson(string json)
    {
        if (!json.StartsWith('['))
            json = $"[{json}]";

        var items = System.Text.Json.JsonSerializer.Deserialize<PsLocalQosDto[]>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? Array.Empty<PsLocalQosDto>();

        return items.Select(dto =>
        {
            var policyName = dto.Name ?? string.Empty;
            var exe = dto.AppPathNameMatchCondition ?? dto.AppPathName;
            if (string.IsNullOrWhiteSpace(exe) && policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                exe = $"{policyName[PolicyPrefix.Length..]}.exe";
            }

            var dscp = dto.DSCPAction > 0 ? dto.DSCPAction : dto.DSCPValue;

            return new LocalQosPolicy
            {
                Name           = policyName,
                ExecutableName = Path.GetFileName(exe ?? string.Empty).ToLowerInvariant(),
                Dscp           = dscp,
                Owner          = dto.Owner,
                PolicyStore    = dto.PolicyStore
            };
        }).ToList();
    }

    // DTOs para deserialização do JSON gerado pelo PowerShell
    private sealed class PsQosPolicyDto
    {
        public string? Name                             { get; set; }
        public string? AppPathNameMatchCondition        { get; set; }
        public int     DSCPAction                       { get; set; }
        public System.Text.Json.JsonElement NetworkProfile { get; set; }
    }

    private sealed class PsLocalQosDto
    {
        public string? Name                      { get; set; }
        public string? AppPathNameMatchCondition { get; set; }
        public string? AppPathName               { get; set; }
        public int     DSCPAction                { get; set; }
        public int     DSCPValue                 { get; set; }
        public string? Owner                     { get; set; }
        public string? PolicyStore               { get; set; }
    }
}
