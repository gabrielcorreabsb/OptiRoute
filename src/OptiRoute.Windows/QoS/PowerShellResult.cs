namespace OptiRoute.Windows.QoS;

/// <summary>Resultado de uma execução de script PowerShell.</summary>
internal sealed class PowerShellResult
{
    public int    ExitCode { get; }
    public string Output   { get; }
    public string Error    { get; }
    public bool   Success  => ExitCode == 0;

    public PowerShellResult(int exitCode, string output, string error)
    {
        ExitCode = exitCode;
        Output   = output;
        Error    = error;
    }
}
