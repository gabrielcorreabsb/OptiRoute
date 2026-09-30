using OptiRoute.Core.Models;

namespace OptiRoute.Core.Services;

/// <summary>
/// Gerenciador central de alocação de DSCP.
/// Mantém e valida mapeamento estritamente biunívoco (1:1) entre Executável e DSCP:
/// - Um executável NUNCA recebe dois DSCPs diferentes.
/// - Um DSCP NUNCA é compartilhado por dois executáveis diferentes.
/// </summary>
public sealed class DscpRegistry : Interfaces.IDscpRegistry
{
    /// <summary>
    /// Pool padrão de DSCPs dedicados ao OptiRoute.
    /// Exclui explicitamente:
    /// - 0 (Best Effort)
    /// - 8, 16, 24, 32, 40, 48, 56 (Class Selectors CS1-CS7)
    /// - 46 (Expedited Forwarding / Voz)
    /// </summary>
    public static readonly int[] DefaultPool = [
        33, 34, 35, 36, 37, 38, 39,
        41, 42, 43, 44, 45,
        49, 50, 51, 52, 53, 54, 55,
        57, 58, 59, 61, 62
    ];

    public static readonly HashSet<int> DefaultReserved = [
        0, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 40, 46, 48, 56
    ];

    private readonly List<int>      _managedPool;
    private readonly HashSet<int>   _reservedDscps;

    private readonly Dictionary<string, int> _exeToDscp = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, string> _dscpToExe = new();
    private readonly object                  _lock = new();

    public IReadOnlyList<int> ManagedPool => _managedPool.AsReadOnly();
    public IReadOnlySet<int>  ReservedDscps => _reservedDscps;

    public DscpRegistry(IEnumerable<int>? pool = null, IEnumerable<int>? reserved = null)
    {
        _reservedDscps = reserved is not null
            ? new HashSet<int>(reserved)
            : new HashSet<int>(DefaultReserved);

        _managedPool = pool is not null
            ? pool.Where(d => !_reservedDscps.Contains(d)).Distinct().OrderBy(d => d).ToList()
            : DefaultPool.Where(d => !_reservedDscps.Contains(d)).ToList();
    }

    /// <summary>
    /// Constrói o pool a partir do range inclusivo <c>[poolStart, poolEnd]</c>
    /// (ex.: 40–50), delegando ao construtor principal — que também remove os
    /// DSCPs reservados (CS1–CS7, EF, etc.) e ordena o resultado.
    /// <para>
    /// Sem valores default de propósito: adicionar defaults aqui tornaria
    /// <c>new DscpRegistry()</c> ambíguo com o construtor de <see cref="IEnumerable{Int32}"/>.
    /// </para>
    /// </summary>
    public DscpRegistry(int poolStart, int poolEnd)
        : this(Enumerable.Range(poolStart, poolEnd - poolStart + 1))
    {
    }

    public int? GetByExecutable(string executable)
    {
        var clean = CleanExecutable(executable);
        lock (_lock)
        {
            return _exeToDscp.TryGetValue(clean, out var dscp) ? dscp : null;
        }
    }

    public string? GetByDscp(int dscp)
    {
        lock (_lock)
        {
            return _dscpToExe.TryGetValue(dscp, out var exe) ? exe : null;
        }
    }

    public int AllocateNextAvailable(string executable)
    {
        var clean = CleanExecutable(executable);

        lock (_lock)
        {
            // 1. Se já está alocado, devolve o existente
            if (_exeToDscp.TryGetValue(clean, out var existingDscp))
                return existingDscp;

            // 2. Busca o primeiro DSCP do pool que não esteja em uso
            foreach (var candidate in _managedPool)
            {
                if (!_dscpToExe.ContainsKey(candidate))
                {
                    _exeToDscp[clean] = candidate;
                    _dscpToExe[candidate] = clean;
                    return candidate;
                }
            }

            throw new InvalidOperationException(
                $"No available DSCP in managed pool. All {_managedPool.Count} slots are allocated.");
        }
    }

    public void Register(string executable, int dscp)
    {
        var clean = CleanExecutable(executable);

        lock (_lock)
        {
            // Valida se o DSCP já está em uso por outro executável
            if (_dscpToExe.TryGetValue(dscp, out var currentExe))
            {
                if (!currentExe.Equals(clean, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"DSCP {dscp} is already allocated to '{currentExe}'. Cannot reassign to '{clean}'.");
                }
            }

            // Valida se o executável já possui outro DSCP
            if (_exeToDscp.TryGetValue(clean, out var currentDscp))
            {
                if (currentDscp != dscp)
                {
                    throw new InvalidOperationException(
                        $"Executable '{clean}' already has DSCP {currentDscp}. Cannot reassign to {dscp}.");
                }
            }

            _exeToDscp[clean] = dscp;
            _dscpToExe[dscp] = clean;
        }
    }

    public void ValidateUniqueness()
    {
        lock (_lock)
        {
            if (_exeToDscp.Count != _dscpToExe.Count)
            {
                throw new InvalidOperationException(
                    $"Integrity error in DscpRegistry: Executables ({_exeToDscp.Count}) does not match DSCPs ({_dscpToExe.Count}).");
            }
        }
    }

    public void SynchronizeFromRules(IEnumerable<OptiRouteRuleDescriptor> rules)
    {
        lock (_lock)
        {
            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule.ExecutableName) || rule.Dscp <= 0)
                    continue;

                var clean = CleanExecutable(rule.ExecutableName);

                // Se já existir conflito conhecido, registra aviso ou preserva o existente
                if (_exeToDscp.TryGetValue(clean, out var existingDscp))
                {
                    if (existingDscp != rule.Dscp)
                    {
                        // Conflito detectado: o mesmo executável apareceu com DSCPs diferentes nas regras
                        continue;
                    }
                }
                else if (_dscpToExe.TryGetValue(rule.Dscp, out var existingExe))
                {
                    if (!existingExe.Equals(clean, StringComparison.OrdinalIgnoreCase))
                    {
                        // Conflito detectado: o mesmo DSCP foi usado por executáveis diferentes
                        continue;
                    }
                }

                _exeToDscp[clean] = rule.Dscp;
                _dscpToExe[rule.Dscp] = clean;
            }
        }
    }

    public IReadOnlyDictionary<string, int> GetAllAllocations()
    {
        lock (_lock)
        {
            return new Dictionary<string, int>(_exeToDscp, StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _exeToDscp.Clear();
            _dscpToExe.Clear();
        }
    }

    private static string CleanExecutable(string exe)
    {
        if (string.IsNullOrWhiteSpace(exe))
            throw new ArgumentException("Executable name cannot be empty.", nameof(exe));

        // Normaliza separadores de path (\ e /) antes de extrair o file name.
        // Necessário porque Path.GetFileName só reconhece o separador da plataforma
        // atual — no Linux, "C:\Games\BF6.exe" viraria a string inteira, gerando
        // chaves duplicadas para o mesmo executável.
        var normalized = exe.Replace('/', '\\');
        return Path.GetFileName(normalized).Trim().ToLowerInvariant();
    }
}
