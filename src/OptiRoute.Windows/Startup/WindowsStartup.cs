using Microsoft.Win32;

namespace OptiRoute.Windows.Startup;

/// <summary>
/// Gerencia a entrada de inicialização automática do OptiRoute no Windows via
/// a chave <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.
/// <para>
/// Toda a API é best-effort: falhas de permissão ou chave inexistente são
/// engolidas (o app não deve quebrar por causa disso). Por isso os métodos
/// sobrecarregados expõem <c>runKeyPath</c> para que os testes usem uma chave
/// isolada (ex.: <c>HKCU\Software\OptiRouteTests\Run</c>) sem tocar no Run real.
/// </para>
/// </summary>
public static class WindowsStartup
{
    /// <summary>Caminho relativo (a HKCU) da chave Run do usuário atual.</summary>
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Nome do valor que identifica a entrada do OptiRoute na chave Run.</summary>
    public const string AppName = "OptiRoute";

    /// <summary>
    /// Retorna <c>true</c> se <paramref name="appName"/> já existe na chave Run.
    /// </summary>
    public static bool IsRegistered(string appName) => IsRegistered(appName, RunKeyPath);

    /// <summary>
    /// Variante com chave customizada (usada pelos testes para isolar em HKCU\Software\OptiRouteTests\Run).
    /// </summary>
    public static bool IsRegistered(string appName, string runKeyPath)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            using var runKey  = baseKey.OpenSubKey(runKeyPath, writable: false);
            return runKey?.GetValue(appName) is not null;
        }
        catch
        {
            // Chave ausente / permissão insuficiente: tratado como "não registrado".
            return false;
        }
    }

    /// <summary>
    /// Escreve <c>appName = exePath</c> na chave Run (cria a subchave se necessário).
    /// </summary>
    public static void Register(string appName, string exePath)
        => Register(appName, exePath, RunKeyPath);

    /// <summary>
    /// Variante com chave customizada (usada pelos testes para isolar em HKCU\Software\OptiRouteTests\Run).
    /// </summary>
    public static void Register(string appName, string exePath, string runKeyPath)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            using var runKey  = baseKey.CreateSubKey(runKeyPath, writable: true);
            runKey?.SetValue(appName, exePath, RegistryValueKind.String);
        }
        catch
        {
            // Best-effort: falha de permissão não deve derrubar o app.
        }
    }

    /// <summary>
    /// Remove o valor <paramref name="appName"/> da chave Run (no-op se ausente).
    /// </summary>
    public static void Unregister(string appName) => Unregister(appName, RunKeyPath);

    /// <summary>
    /// Variante com chave customizada (usada pelos testes para isolar em HKCU\Software\OptiRouteTests\Run).
    /// </summary>
    public static void Unregister(string appName, string runKeyPath)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            using var runKey  = baseKey.OpenSubKey(runKeyPath, writable: true);
            runKey?.DeleteValue(appName, throwOnMissingValue: false);
        }
        catch
        {
            // Best-effort: chave ausente / permissão insuficiente.
        }
    }
}
