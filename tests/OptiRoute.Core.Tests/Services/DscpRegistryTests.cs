using OptiRoute.Core.Models;
using OptiRoute.Core.Services;
using Xunit;

namespace OptiRoute.Core.Tests.Services;

public class DscpRegistryTests
{
    [Fact]
    public void ManagedPool_ShouldNotContainReservedDscps()
    {
        var registry = new DscpRegistry();

        Assert.DoesNotContain(0, registry.ManagedPool);
        Assert.DoesNotContain(46, registry.ManagedPool);
        Assert.DoesNotContain(32, registry.ManagedPool);
    }

    [Fact]
    public void AllocateNextAvailable_FirstCall_ShouldAllocateFirstInPool()
    {
        var registry = new DscpRegistry();

        var dscp = registry.AllocateNextAvailable("bf6.exe");

        Assert.Equal(registry.ManagedPool[0], dscp);
        Assert.Equal(dscp, registry.GetByExecutable("bf6.exe"));
        Assert.Equal("bf6.exe", registry.GetByDscp(dscp));
    }

    [Fact]
    public void AllocateNextAvailable_SameExecutable_ShouldReturnSameDscp()
    {
        var registry = new DscpRegistry();

        var dscp1 = registry.AllocateNextAvailable("bf6.exe");
        var dscp2 = registry.AllocateNextAvailable("bf6.exe");
        var dscp3 = registry.AllocateNextAvailable("C:\\Games\\BF6.exe");

        Assert.Equal(dscp1, dscp2);
        Assert.Equal(dscp1, dscp3);
    }

    [Fact]
    public void AllocateNextAvailable_DifferentExecutables_ShouldAllocateDifferentDscps()
    {
        var registry = new DscpRegistry();

        var dscpBf6 = registry.AllocateNextAvailable("bf6.exe");
        var dscpDiscord = registry.AllocateNextAvailable("discord.exe");
        var dscpSteam = registry.AllocateNextAvailable("steam.exe");

        Assert.NotEqual(dscpBf6, dscpDiscord);
        Assert.NotEqual(dscpDiscord, dscpSteam);
        Assert.NotEqual(dscpBf6, dscpSteam);
    }

    [Fact]
    public void Register_ConflictDscp_ShouldThrowInvalidOperationException()
    {
        var registry = new DscpRegistry();
        registry.Register("bf6.exe", 33);

        // Tentativa de associar cod.exe ao mesmo DSCP 33 deve falhar
        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.Register("cod.exe", 33));

        Assert.Contains("already allocated", ex.Message);
    }

    [Fact]
    public void Register_ConflictExecutable_ShouldThrowInvalidOperationException()
    {
        var registry = new DscpRegistry();
        registry.Register("bf6.exe", 33);

        // Tentativa de associar bf6.exe a outro DSCP deve falhar
        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.Register("bf6.exe", 34));

        Assert.Contains("already has DSCP", ex.Message);
    }

    [Fact]
    public void SynchronizeFromRules_ShouldReconstructMapping()
    {
        var registry = new DscpRegistry();
        var rules = new[]
        {
            new OptiRouteRuleDescriptor { ExecutableName = "bf6.exe", Dscp = 33, RuleType = OptiRouteRuleType.Default },
            new OptiRouteRuleDescriptor { ExecutableName = "bf6.exe", Dscp = 33, RuleType = OptiRouteRuleType.Override },
            new OptiRouteRuleDescriptor { ExecutableName = "discord.exe", Dscp = 34, RuleType = OptiRouteRuleType.Default }
        };

        registry.SynchronizeFromRules(rules);

        Assert.Equal(33, registry.GetByExecutable("bf6.exe"));
        Assert.Equal(34, registry.GetByExecutable("discord.exe"));
        Assert.Equal("bf6.exe", registry.GetByDscp(33));
        Assert.Equal("discord.exe", registry.GetByDscp(34));
    }

    [Fact]
    public void CustomPool_AllocatesWithinRange()
    {
        var registry = new DscpRegistry(40, 50);

        var dscp = registry.AllocateNextAvailable("game.exe");

        Assert.InRange(dscp, 40, 50);
        Assert.Equal(dscp, registry.GetByExecutable("game.exe"));
    }

    [Fact]
    public void CustomPool_ExcludesReservedValues()
    {
        var registry = new DscpRegistry(40, 50);

        // No range [40,50] são reservados: 40 (CS5), 46 (EF) e 48 (CS6).
        // Há 8 slots livres (41,42,43,44,45,47,49,50) — aloca todos.
        var allocated = new List<int>();
        for (var i = 0; i < 8; i++)
            allocated.Add(registry.AllocateNextAvailable($"app{i}.exe"));

        Assert.All(allocated, d => Assert.InRange(d, 40, 50));
        Assert.DoesNotContain(40, allocated);
        Assert.DoesNotContain(46, allocated);
        Assert.DoesNotContain(48, allocated);
        Assert.DoesNotContain(registry.ReservedDscps, d => allocated.Contains(d));
    }
}
