using OptiRoute.Core.Models;

namespace OptiRoute.Core.Interfaces;

/// <summary>
/// Contrato do gerenciador central de alocação e unicidade de DSCPs.
/// Garante mapeamento biunívoco (1:1) global entre ExecutableName e DSCP.
/// </summary>
public interface IDscpRegistry
{
    /// <summary>Lista de DSCPs permitidos para alocação automática.</summary>
    IReadOnlyList<int> ManagedPool { get; }

    /// <summary>Lista de DSCPs reservados que nunca devem ser alocados (ex: 0, 46/EF).</summary>
    IReadOnlySet<int> ReservedDscps { get; }

    /// <summary>Recupera o DSCP alocado para um executável, ou null se não cadastrado.</summary>
    int? GetByExecutable(string executable);

    /// <summary>Recupera o executável associado a um DSCP, ou null se livre.</summary>
    string? GetByDscp(int dscp);

    /// <summary>
    /// Aloca o próximo DSCP disponível do pool para um novo executável.
    /// Se o executável já tiver um DSCP registrado, retorna o DSCP existente.
    /// </summary>
    int AllocateNextAvailable(string executable);

    /// <summary>
    /// Registra manualmente uma associação Executable ↔ DSCP verificando colisões.
    /// </summary>
    void Register(string executable, int dscp);

    /// <summary>
    /// Valida que não existem colisões (nem Executable com múltiplos DSCPs, nem DSCP com múltiplos Executables).
    /// </summary>
    void ValidateUniqueness();

    /// <summary>
    /// Sincroniza o registro a partir de descritores de regras lidos do OPNsense.
    /// </summary>
    void SynchronizeFromRules(IEnumerable<OptiRouteRuleDescriptor> rules);

    /// <summary>Retorna um snapshot de todas as alocações ativas (Executable → DSCP).</summary>
    IReadOnlyDictionary<string, int> GetAllAllocations();

    /// <summary>Limpa as alocações da memória.</summary>
    void Clear();
}
