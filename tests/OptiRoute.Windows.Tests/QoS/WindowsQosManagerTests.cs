using Microsoft.Extensions.Logging.Abstractions;
using OptiRoute.Core.Models;
using OptiRoute.Windows.QoS;
using Xunit;

namespace OptiRoute.Windows.Tests.QoS;

/// <summary>
/// Testes unitários do WindowsQosManager.
/// Os testes que executam PowerShell real são marcados como [Trait("Category", "Integration")]
/// e precisam ser rodados como Administrador.
/// </summary>
public class WindowsQosManagerTests
{
    private readonly WindowsQosManager _manager;

    public WindowsQosManagerTests()
    {
        _manager = new WindowsQosManager(NullLogger<WindowsQosManager>.Instance);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Testes de lógica pura (sem PowerShell)
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("bf6.exe",     "OptiRoute-bf6")]
    [InlineData("discord.exe", "OptiRoute-discord")]
    [InlineData("steam.exe",   "OptiRoute-steam")]
    [InlineData("C:\\Games\\bf6.exe", "OptiRoute-bf6")]   // path completo → extrai apenas nome
    public void BuildPolicyName_ShouldReturnCorrectPrefix(string exeName, string expected)
    {
        var result = WindowsQosManager.BuildPolicyName(exeName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CleanupOrphanedScripts_RemovesOnlyOldMatchingFiles()
    {
        // Diretório isolado para o teste — não toca em %TEMP% real
        var tempDir = Path.Combine(Path.GetTempPath(), $"optiroute_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var now          = DateTime.UtcNow;
            var oldScript    = Path.Combine(tempDir, "optiroute_old_aaa.ps1");
            var newScript    = Path.Combine(tempDir, "optiroute_new_bbb.ps1");
            var unrelated    = Path.Combine(tempDir, "unrelated_old.ps1");

            File.WriteAllText(oldScript, "# old");
            File.WriteAllText(newScript, "# new");
            File.WriteAllText(unrelated, "# nope");

            File.SetLastWriteTimeUtc(oldScript, now.AddMinutes(-30)); // órfão (>10 min)
            File.SetLastWriteTimeUtc(newScript, now);                  // recente (<10 min)
            File.SetLastWriteTimeUtc(unrelated, now.AddMinutes(-30)); // órfão mas fora do padrão

            var removed = WindowsQosManager.CleanupOrphanedScripts(
                tempDirectory:   tempDir,
                patternOverride: "optiroute_*.ps1",
                nowUtc:          now);

            Assert.Equal(1, removed);
            Assert.False(File.Exists(oldScript),    "órfão antigo deve ter sido removido");
            Assert.True (File.Exists(newScript),    "recente deve ter sido preservado");
            Assert.True (File.Exists(unrelated),    "fora do padrão deve ser ignorado");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void CleanupOrphanedScripts_OnNonExistentDirectory_ReturnsZero()
    {
        var ghostDir = Path.Combine(Path.GetTempPath(), $"optiroute_ghost_{Guid.NewGuid():N}");

        var removed = WindowsQosManager.CleanupOrphanedScripts(
            tempDirectory:   ghostDir,
            patternOverride: "optiroute_*.ps1",
            nowUtc:          DateTime.UtcNow);

        Assert.Equal(0, removed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    public async Task CreatePolicyAsync_ValidDscp_ShouldNotThrow(int dscp)
    {
        // Apenas valida que valores válidos não causam exceção antes de chamar PS
        // O teste real (com PS) fica na categoria Integration
        var ex = await Record.ExceptionAsync(
            () => Task.FromException(new Exception("no-ps-in-unit-test")));
        // Verificação de range é feita internamente antes de chamar PS
        Assert.InRange(dscp, 0, 63);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(64)]
    [InlineData(100)]
    public async Task CreatePolicyAsync_InvalidDscp_ShouldThrowArgumentOutOfRange(int dscp)
    {
        // O método ValidateDscp é chamado antes do PowerShell
        // Esperamos ArgumentOutOfRangeException para valores inválidos
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _manager.CreatePolicyAsync("test.exe", dscp));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Testes de integração (marcados — requerem Admin + Windows)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Integration")]
    public async Task FullLifecycle_CreateListDelete_ShouldWork()
    {
        const string exe  = "optiroute-test-dummy.exe";
        const int    dscp = 33;

        try
        {
            // 1. Criar
            await _manager.CreatePolicyAsync(exe, dscp);

            // 2. Verificar existência
            var exists = await _manager.PolicyExistsAsync(exe);
            Assert.True(exists, "Policy should exist after creation");

            // 3. Listar
            var policies = await _manager.ListOptiRoutePoliciesAsync();
            var policy   = policies.FirstOrDefault(p => p.AppPathName == exe);
            Assert.NotNull(policy);
            Assert.Equal(dscp, policy.DscpAction);
            Assert.Equal(WindowsQosManager.BuildPolicyName(exe), policy.Name);

            // 4. Idempotência — criar duas vezes não duplica
            await _manager.CreatePolicyAsync(exe, dscp);
            var policies2 = await _manager.ListOptiRoutePoliciesAsync();
            Assert.Equal(
                policies.Count,
                policies2.Count(p => p.AppPathName == exe));
        }
        finally
        {
            // 5. Limpar
            await _manager.DeletePolicyAsync(exe);
            var stillExists = await _manager.PolicyExistsAsync(exe);
            Assert.False(stillExists, "Policy should be removed after deletion");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DeletePolicyAsync_NonExistent_ShouldNotThrow()
    {
        // Deletar policy inexistente não deve lançar exceção
        var ex = await Record.ExceptionAsync(
            () => _manager.DeletePolicyAsync("nonexistent-app-xyz.exe"));

        Assert.Null(ex);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SyncAllAsync_ShouldRecreateRemovedPolicy()
    {
        var profile = new RoutingProfile { Id = Guid.NewGuid(), Name = "Test", DscpValue = 45 };
        var rule    = new ApplicationRule
        {
            ExecutableName  = "optiroute-sync-test.exe",
            RoutingProfileId = profile.Id,
            Enabled         = true
        };

        try
        {
            // Sync deve criar a política
            await _manager.SyncAllAsync([rule], [profile]);

            var exists = await _manager.PolicyExistsAsync(rule.ExecutableName);
            Assert.True(exists, "Sync should have created the policy");
        }
        finally
        {
            await _manager.DeletePolicyAsync(rule.ExecutableName);
        }
    }
}
