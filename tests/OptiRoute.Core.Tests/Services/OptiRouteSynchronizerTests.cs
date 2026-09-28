using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OptiRoute.Core.Interfaces;
using OptiRoute.Core.Models;
using OptiRoute.Core.Services;
using Xunit;

namespace OptiRoute.Core.Tests.Services;

public class OptiRouteSynchronizerTests
{
    private readonly Mock<IOpnsenseClient>   _clientMock   = new();
    private readonly Mock<IWindowsQosManager> _qosMock      = new();
    private readonly Mock<IDscpRegistry>     _registryMock = new();
    private readonly Mock<IRuleOrderManager> _orderMock    = new();
    private readonly OptiRouteSynchronizer   _synchronizer;

    public OptiRouteSynchronizerTests()
    {
        _synchronizer = new OptiRouteSynchronizer(
            _clientMock.Object,
            _qosMock.Object,
            _registryMock.Object,
            _orderMock.Object,
            NullLogger<OptiRouteSynchronizer>.Instance);
    }

    [Fact]
    public async Task SyncAsync_MandatoryMultiPcScenario_ShouldCalculateCorrectEffectiveRoutes()
    {
        var rules = new List<FirewallRuleInfo>
        {
            new()
            {
                Uuid = "u-default",
                Description = "OPTIROUTE|DEFAULT|bf6.exe|33|guid-1",
                Gateway = "WAN2",
                Category = "OptiRoute",
                Sequence = 10
            },
            new()
            {
                Uuid = "u-override",
                Description = "OPTIROUTE|OVERRIDE|bf6.exe|33|10.0.0.122|guid-1",
                Gateway = "WAN1",
                Category = "OptiRoute",
                Sequence = 5
            }
        };

        _clientMock.Setup(c => c.ListOptiRouteRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        _qosMock.Setup(q => q.ListLocalPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LocalQosPolicy>
            {
                new() { Name = "OptiRoute-bf6", ExecutableName = "bf6.exe", Dscp = 33, Owner = "Group Policy (Machine)" }
            });

        // Teste PC 1: 10.0.0.121 (sem override -> usa Global Default WAN2)
        var resultPc1 = await _synchronizer.SyncAsync(IPAddress.Parse("10.0.0.121"));

        Assert.Single(resultPc1.Routes);
        var routePc1 = resultPc1.Routes[0];
        Assert.Equal("bf6.exe", routePc1.Executable);
        Assert.Equal(33, routePc1.Dscp);
        Assert.Equal("WAN2", routePc1.DefaultGateway);
        Assert.Equal("WAN2", routePc1.EffectiveGateway);
        Assert.False(routePc1.HasOverride);
        Assert.Equal(ApplicationSyncState.Synchronized, routePc1.SyncState);
        Assert.Equal("Global Default", routePc1.Reason);
        Assert.True(routePc1.IsQosActive);

        // Teste PC 2: 10.0.0.122 (com override -> usa WAN1)
        var resultPc2 = await _synchronizer.SyncAsync(IPAddress.Parse("10.0.0.122"));

        Assert.Single(resultPc2.Routes);
        var routePc2 = resultPc2.Routes[0];
        Assert.Equal("bf6.exe", routePc2.Executable);
        Assert.Equal(33, routePc2.Dscp);
        Assert.Equal("WAN2", routePc2.DefaultGateway);
        Assert.Equal("WAN1", routePc2.EffectiveGateway);
        Assert.True(routePc2.HasOverride);
        Assert.Equal(ApplicationSyncState.Synchronized, routePc2.SyncState);
        Assert.Equal("Local Override", routePc2.Reason);
        Assert.True(routePc2.IsQosActive);
    }

    [Fact]
    public async Task SyncAsync_WhenLocalOrphanPolicyExists_ShouldClassifyAsLocalOnlyAndNeverAutoDelete()
    {
        // OPNsense vazio (sem regras)
        _clientMock.Setup(c => c.ListOptiRouteRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FirewallRuleInfo>());

        // Windows tem OptiRoute-bf6
        _qosMock.Setup(q => q.ListLocalPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LocalQosPolicy>
            {
                new() { Name = "OptiRoute-bf6", ExecutableName = "bf6.exe", Dscp = 33, Owner = "Group Policy (Machine)" }
            });

        var result = await _synchronizer.SyncAsync(IPAddress.Parse("10.0.0.121"));

        Assert.Single(result.Routes);
        var route = result.Routes[0];
        Assert.Equal("bf6.exe", route.Executable);
        Assert.Equal(ApplicationSyncState.LocalOnly, route.SyncState);
        Assert.Equal("Default LAN Routing", route.EffectiveGateway);
        Assert.Equal("Default LAN Routing", route.Reason);

        // Garante que NUNCA excluiu automaticamente
        _qosMock.Verify(q => q.DeletePolicyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _qosMock.Verify(q => q.DeletePolicyAsync(It.IsAny<LocalQosPolicy>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncAsync_WhenGlobalExistsWithoutLocal_ShouldClassifyAsGlobalOnly()
    {
        var rules = new List<FirewallRuleInfo>
        {
            new()
            {
                Uuid = "u-default",
                Description = "OPTIROUTE|DEFAULT|cod.exe|35|guid-2",
                Gateway = "WAN1",
                Category = "OptiRoute"
            }
        };

        _clientMock.Setup(c => c.ListOptiRouteRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        _qosMock.Setup(q => q.ListLocalPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LocalQosPolicy>());

        var result = await _synchronizer.SyncAsync(IPAddress.Parse("10.0.0.121"));

        Assert.Single(result.Routes);
        var route = result.Routes[0];
        Assert.Equal("cod.exe", route.Executable);
        Assert.Equal(ApplicationSyncState.GlobalOnly, route.SyncState);
        Assert.False(route.IsQosActive);
    }

    [Fact]
    public async Task SyncAsync_WhenDscpDiffers_ShouldClassifyAsConflict()
    {
        var rules = new List<FirewallRuleInfo>
        {
            new()
            {
                Uuid = "u-default",
                Description = "OPTIROUTE|DEFAULT|bf6.exe|33|guid-1",
                Gateway = "WAN2",
                Category = "OptiRoute"
            }
        };

        _clientMock.Setup(c => c.ListOptiRouteRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        // Windows configurado com DSCP 40 (divergente de 33)
        _qosMock.Setup(q => q.ListLocalPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LocalQosPolicy>
            {
                new() { Name = "OptiRoute-bf6", ExecutableName = "bf6.exe", Dscp = 40 }
            });

        var result = await _synchronizer.SyncAsync(IPAddress.Parse("10.0.0.121"));

        Assert.Single(result.Routes);
        var route = result.Routes[0];
        Assert.Equal(ApplicationSyncState.Conflict, route.SyncState);
        Assert.Equal(33, route.GlobalDscp);
        Assert.Equal(40, route.LocalDscp);
    }

    [Fact]
    public async Task PromoteLocalToGlobalAsync_WhenCollision_ShouldAllocateNewDscpAndUpdateLocalQos()
    {
        _qosMock.Setup(q => q.ListLocalPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LocalQosPolicy>
            {
                new() { Name = "OptiRoute-bf6", ExecutableName = "bf6.exe", Dscp = 33 }
            });

        // DSCP 33 já alocado para outro app ("cod.exe")
        _registryMock.Setup(r => r.GetByDscp(33)).Returns("cod.exe");
        _registryMock.Setup(r => r.AllocateNextAvailable("bf6.exe")).Returns(34);
        _clientMock.Setup(c => c.EnsureRuleExistsAsync(It.IsAny<OPNsenseRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("uuid-promoted");

        var uuid = await _synchronizer.PromoteLocalToGlobalAsync("bf6.exe", "WAN2");

        Assert.Equal("uuid-promoted", uuid);

        // Deve ter atualizado o QoS local para o novo DSCP 34 antes de publicar
        _qosMock.Verify(q => q.UpdatePolicyAsync("bf6.exe", 34, It.IsAny<CancellationToken>()), Times.Once);

        // Deve criar regra no OPNsense com DSCP 34
        _clientMock.Verify(c => c.EnsureRuleExistsAsync(It.Is<OPNsenseRuleRequest>(r =>
            r.DscpValue == 34 && r.Gateway == "WAN2"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Delta 4 — Multi-PC dedup em RegisterOrUpdateApplicationAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterOrUpdateApplicationAsync_WhenExistingRuleWithDifferentAppId_AdoptsExistingAppId()
    {
        // Cenário multi-PC: PC1 já criou a regra com appId "existing-guid"
        var existingRules = new List<FirewallRuleInfo>
        {
            new()
            {
                Uuid        = "u-existing",
                Description = "OPTIROUTE|DEFAULT|bf6.exe|33|existing-guid|v=1",
                Gateway     = "WAN1",
                Category    = "OptiRoute"
            }
        };
        _clientMock.Setup(c => c.ListOptiRouteRulesAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(existingRules);
        _registryMock.Setup(r => r.AllocateNextAvailable("bf6.exe")).Returns(33);
        _clientMock.Setup(c => c.EnsureRuleExistsAsync(It.IsAny<OPNsenseRuleRequest>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync("u-existing");

        // PC2 tenta registrar com GUID novo
        var identity = new ApplicationIdentity { ExecutableName = "bf6.exe", DisplayName = "Battlefield 6", AppId = "new-guid" };
        var uuid = await _synchronizer.RegisterOrUpdateApplicationAsync(identity, "WAN1");

        Assert.Equal("u-existing", uuid);

        // A descrição usada deve conter o appId ADOTADO, não o novo
        _clientMock.Verify(c => c.EnsureRuleExistsAsync(It.Is<OPNsenseRuleRequest>(r =>
            r.Description.Contains("existing-guid") &&
            !r.Description.Contains("new-guid")
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterOrUpdateApplicationAsync_WhenNoExistingRule_UsesIncomingAppId()
    {
        _clientMock.Setup(c => c.ListOptiRouteRulesAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<FirewallRuleInfo>());
        _registryMock.Setup(r => r.AllocateNextAvailable("fresh.exe")).Returns(40);
        _clientMock.Setup(c => c.EnsureRuleExistsAsync(It.IsAny<OPNsenseRuleRequest>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync("u-new");

        var identity = new ApplicationIdentity { ExecutableName = "fresh.exe", DisplayName = "Fresh", AppId = "fresh-guid" };
        var uuid = await _synchronizer.RegisterOrUpdateApplicationAsync(identity, "WAN2");

        Assert.Equal("u-new", uuid);

        _clientMock.Verify(c => c.EnsureRuleExistsAsync(It.Is<OPNsenseRuleRequest>(r =>
            r.Description.Contains("fresh-guid")
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterOrUpdateApplicationAsync_WhenExistingRuleHasEmptyAppId_UsesIncomingAppId()
    {
        // Cenário: regra legada de PoC sem AppId — devemos criar uma nova regra
        // com AppId do incoming (o post-dedup safety net pode remover a legada depois).
        var existingRules = new List<FirewallRuleInfo>
        {
            new()
            {
                Uuid        = "u-legacy",
                Description = "OPTIROUTE|DEFAULT|bf6.exe|33||v=1", // AppId vazio
                Gateway     = "WAN1",
                Category    = "OptiRoute"
            }
        };
        _clientMock.Setup(c => c.ListOptiRouteRulesAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(existingRules);
        _registryMock.Setup(r => r.AllocateNextAvailable("bf6.exe")).Returns(33);
        _clientMock.Setup(c => c.EnsureRuleExistsAsync(It.IsAny<OPNsenseRuleRequest>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync("u-new");

        var identity = new ApplicationIdentity { ExecutableName = "bf6.exe", DisplayName = "Battlefield 6", AppId = "incoming-guid" };
        await _synchronizer.RegisterOrUpdateApplicationAsync(identity, "WAN1");

        _clientMock.Verify(c => c.EnsureRuleExistsAsync(It.Is<OPNsenseRuleRequest>(r =>
            r.Description.Contains("incoming-guid")
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterOrUpdateApplicationAsync_WhenExistingRuleHasSameAppId_NoChange()
    {
        // Mesmo AppId — fluxo normal, sem adoção (mas também sem barulho)
        var existingRules = new List<FirewallRuleInfo>
        {
            new()
            {
                Uuid        = "u-mine",
                Description = "OPTIROUTE|DEFAULT|bf6.exe|33|my-guid|v=1",
                Gateway     = "WAN1",
                Category    = "OptiRoute"
            }
        };
        _clientMock.Setup(c => c.ListOptiRouteRulesAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(existingRules);
        _registryMock.Setup(r => r.AllocateNextAvailable("bf6.exe")).Returns(33);
        _clientMock.Setup(c => c.EnsureRuleExistsAsync(It.IsAny<OPNsenseRuleRequest>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync("u-mine");

        var identity = new ApplicationIdentity { ExecutableName = "bf6.exe", DisplayName = "Battlefield 6", AppId = "my-guid" };
        var uuid = await _synchronizer.RegisterOrUpdateApplicationAsync(identity, "WAN1");

        Assert.Equal("u-mine", uuid);
        _clientMock.Verify(c => c.EnsureRuleExistsAsync(It.IsAny<OPNsenseRuleRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
