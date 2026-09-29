using System.Diagnostics;
using System.IO;

namespace OptiRoute.App.Services;

/// <summary>
/// Snapshot agregado de um executável em execução no momento (agrupado por path).
/// </summary>
/// <param name="Name">Nome do arquivo do executável (ex.: <c>bf6.exe</c>).</param>
/// <param name="FullPath">Caminho completo no disco, usado para preencher o campo Executable.</param>
/// <param name="InstanceCount">Quantidade de processos com o mesmo path em execução.</param>
/// <param name="TotalMemoryBytes">Soma do working set (bytes) de todas as instâncias.</param>
public record RunningProcessInfo(string Name, string FullPath, int InstanceCount, long TotalMemoryBytes)
{
    /// <summary>
    /// Texto de exibição pronto para UI: "bf6.exe ×2 · 1.2 GB".
    /// Nunca lança — usa apenas os campos já calculados no scan.
    /// </summary>
    public string Summary
    {
        get
        {
            var instances = InstanceCount > 1 ? $" ×{InstanceCount}" : string.Empty;
            var memory = TotalMemoryBytes > 0 ? $" · {FormatBytes(TotalMemoryBytes)}" : string.Empty;
            return $"{Name}{instances}{memory}";
        }
    }

    private static string FormatBytes(long bytes)
    {
        const double kb = 1024.0;
        const double mb = kb * 1024.0;
        const double gb = mb * 1024.0;

        if (bytes >= gb) return $"{bytes / gb:0.#} GB";
        if (bytes >= mb) return $"{bytes / mb:0.#} MB";
        if (bytes >= kb) return $"{bytes / kb:0.#} KB";
        return $"{bytes} B";
    }
}

/// <summary>
/// Enumera os processos em execução e agrupa por executável.
/// Best-effort e à prova de travamento: <see cref="Process.MainModule"/> pode lançar
/// (access denied / processo de sistema / bitness diferente) e é sempre cercado por try/catch.
/// </summary>
public static class RunningProcessScanner
{
    /// <summary>
    /// Processos de sistema/UI que nunca são candidatos a gerenciamento de QoS.
    /// Comparação case-insensitive.
    /// </summary>
    private static readonly HashSet<string> _blocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "svchost", "csrss", "lsass", "services", "wininit", "winlogon",
        "dwm", "explorer", "taskmgr", "dllhost", "conhost", "smss", "Idle"
    };

    /// <summary>
    /// Executa o scan síncrono (rápido, ~dezenas de ms) e retorna os executáveis
    /// agrupados por path, ordenados por nome. Nunca lança.
    /// </summary>
    public static IReadOnlyList<RunningProcessInfo> Scan()
    {
        var groups = new Dictionary<string, RunningProcessInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var path = proc.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(path)) continue;

                var name = Path.GetFileName(path);
                if (_blocked.Contains(name)) continue;

                long mem = 0;
                try { mem = proc.WorkingSet64; } catch { /* access denied — mantém 0 */ }
                try { proc.Dispose(); } catch { /* swallow */ }

                if (groups.TryGetValue(path, out var existing))
                {
                    groups[path] = existing with
                    {
                        InstanceCount = existing.InstanceCount + 1,
                        TotalMemoryBytes = existing.TotalMemoryBytes + mem
                    };
                }
                else
                {
                    groups[path] = new RunningProcessInfo(name, path, 1, mem);
                }
            }
            catch
            {
                // Processo de sistema, acesso negado ou MainModule indisponível — ignora.
                // O scanner NUNCA pode travar a UI por causa de um processo hostil/zumbi.
            }
        }

        return groups.Values
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
