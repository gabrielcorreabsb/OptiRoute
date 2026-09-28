using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OptiRoute.Core.Interfaces;
using OptiRoute.Core.Models;
using OptiRoute.Core.Services;
using Xunit;

namespace OptiRoute.Core.Tests.Services;

public class RuleOrderManagerTests
{
    private readonly Mock<IOpnsenseClient> _clientMock = new();
    private readonly RuleOrderManager _orderManager;

    public RuleOrderManagerTests()
    {
        _orderManager = new RuleOrderManager(
            _clientMock.Object,
            NullLogger<RuleOrderManager>.Instance);
    }

    [Fact]
    public async Task EnsureRelativeOrderAsync_WhenAlreadyOrdered_ShouldNotUpdateRules()
    {
        var rules = new List<FirewallRuleInfo>
        {
            new() { Uuid = "u-override", Description = "OPTIROUTE|OVERRIDE|bf6.exe|33|10.0.0.122|id", Sequence = 10, Category = "OptiRoute" },
            new() { Uuid = "u-default",  Description = "OPTIROUTE|DEFAULT|bf6.exe|33|id", Sequence = 20, Category = "OptiRoute" },
            new() { Uuid = "u-anchor",   Description = "LAN LOADBALANCE", Sequence = 100, Gateway = "LB_IPV4" }
        };

        _clientMock.Setup(c => c.ListAllRulesAsync("lan", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        var result = await _orderManager.EnsureRelativeOrderAsync("lan");

        Assert.True(result);
        _clientMock.Verify(c => c.UpdateRuleSequenceAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureRelativeOrderAsync_WhenOptiRouteIsAfterAnchor_ShouldMoveOptiRouteBeforeAnchor()
    {
        var rules = new List<FirewallRuleInfo>
        {
            new() { Uuid = "u-anchor",   Description = "LAN LOADBALANCE", Sequence = 50, Gateway = "LB_IPV4" },
            new() { Uuid = "u-default",  Description = "OPTIROUTE|DEFAULT|bf6.exe|33|id", Sequence = 100, Category = "OptiRoute" }
        };

        _clientMock.Setup(c => c.ListAllRulesAsync("lan", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        var result = await _orderManager.EnsureRelativeOrderAsync("lan");

        Assert.True(result);

        // Deve mover u-default para uma sequência menor que 50 (a âncora)
        _clientMock.Verify(c => c.UpdateRuleSequenceAsync("u-default", It.Is<int>(seq => seq < 50), It.IsAny<CancellationToken>()), Times.Once);

        // NUNCA deve alterar a regra do usuário (u-anchor)
        _clientMock.Verify(c => c.UpdateRuleSequenceAsync("u-anchor", It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

        // Deve aplicar as alterações
        _clientMock.Verify(c => c.ApplyRulesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureRelativeOrderAsync_OverridesMustPrecedeDefaults()
    {
        var rules = new List<FirewallRuleInfo>
        {
            // Default antes do Override (ordem incorreta dentro do OptiRoute)
            new() { Uuid = "u-default",  Description = "OPTIROUTE|DEFAULT|bf6.exe|33|id", Sequence = 10, Category = "OptiRoute" },
            new() { Uuid = "u-override", Description = "OPTIROUTE|OVERRIDE|bf6.exe|33|10.0.0.122|id", Sequence = 20, Category = "OptiRoute" },
            new() { Uuid = "u-anchor",   Description = "LAN LOADBALANCE", Sequence = 100, Gateway = "LB_IPV4" }
        };

        _clientMock.Setup(c => c.ListAllRulesAsync("lan", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        int updatedOverrideSeq = -1;
        int updatedDefaultSeq = -1;

        _clientMock.Setup(c => c.UpdateRuleSequenceAsync("u-override", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, CancellationToken>((_, seq, _) => updatedOverrideSeq = seq)
            .Returns(Task.CompletedTask);

        _clientMock.Setup(c => c.UpdateRuleSequenceAsync("u-default", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, CancellationToken>((_, seq, _) => updatedDefaultSeq = seq)
            .Returns(Task.CompletedTask);

        var result = await _orderManager.EnsureRelativeOrderAsync("lan");

        Assert.True(result);
        Assert.True(updatedOverrideSeq < updatedDefaultSeq, "Override sequence must be strictly less than Default sequence");
    }
}
