using Microsoft.Win32;
using OptiRoute.Windows.Startup;
using Xunit;

namespace OptiRoute.Windows.Tests.Startup;

/// <summary>
/// Testes de <see cref="WindowsStartup"/>.
/// <para>
/// IMPORTANTE: os testes usam uma chave ISOLADA
/// (<c>HKCU\Software\OptiRouteTests\Run</c>) em vez do Run real
/// (<c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>), portanto não
/// poluem a inicialização do sistema. A árvore de teste é removida no Dispose.
/// </para>
/// </summary>
public sealed class WindowsStartupTests : IDisposable
{
    /// <summary>Chave de teste isolada — nunca o Run real.</summary>
    private const string TestKeyPath = @"Software\OptiRouteTests\Run";

    /// <summary>Árvore raiz removida no Dispose.</summary>
    private const string TestRootPath = @"Software\OptiRouteTests";

    // Nome único por teste para evitar colisões caso rodem em paralelo.
    private readonly string _appName = "OptiRouteTests_" + Guid.NewGuid().ToString("N");

    private const string ExePath = @"C:\Test\OptiRoute.exe";

    [Fact]
    public void Register_AddsValueToRunKey()
    {
        Assert.False(WindowsStartup.IsRegistered(_appName, TestKeyPath));

        WindowsStartup.Register(_appName, ExePath, TestKeyPath);

        Assert.True(WindowsStartup.IsRegistered(_appName, TestKeyPath));
        Assert.Equal(ExePath, ReadValue(_appName));
    }

    [Fact]
    public void Unregister_RemovesValue()
    {
        WindowsStartup.Register(_appName, ExePath, TestKeyPath);
        Assert.True(WindowsStartup.IsRegistered(_appName, TestKeyPath));

        WindowsStartup.Unregister(_appName, TestKeyPath);

        Assert.False(WindowsStartup.IsRegistered(_appName, TestKeyPath));
        Assert.Null(ReadValue(_appName));
    }

    [Fact]
    public void IsRegistered_ReturnsTrueAfterRegister_FalseAfterUnregister()
    {
        Assert.False(WindowsStartup.IsRegistered(_appName, TestKeyPath));

        WindowsStartup.Register(_appName, ExePath, TestKeyPath);
        Assert.True(WindowsStartup.IsRegistered(_appName, TestKeyPath));

        WindowsStartup.Unregister(_appName, TestKeyPath);
        Assert.False(WindowsStartup.IsRegistered(_appName, TestKeyPath));
    }

    private static object? ReadValue(string appName)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
        using var runKey  = baseKey.OpenSubKey(TestKeyPath, writable: false);
        return runKey?.GetValue(appName);
    }

    public void Dispose()
    {
        // Remove a árvore de teste inteira (não toca no Run real).
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            baseKey.DeleteSubKeyTree(TestRootPath, throwOnMissingSubKey: false);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
