using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OptiRoute.Core.Interfaces;
using OptiRoute.Core.Models;
using OptiRoute.Core.Services;
using Xunit;

namespace OptiRoute.Core.Tests.Services;

public class HostOverrideManagerTests
{
    private readonly Mock<IOpnsenseClient>   _clientMock   = new();
    private readonly Mock<IDscpRegistry>     _registryMock = new();
    private readonly Mock<IRuleOrderManager> _orderMock    = new();
    private readonly HostOverrideManager     _overrideManager;

    public HostOverrideManagerTests()
    {
        _overrideManager = new HostOverrideManager(
            _clientMock.Object,
            _registryMock.Object,
            _orderMock.Object,
            NullLogger<HostOverrideManager>.Instance);
    }

    [Fact]
    public async Task SetOverrideAsync_ShouldCreateRuleWithDestinationNotLocalFirewall()
    {
        var identity = ApplicationIdentity.Create("bf6.exe", "Battlefield 6", "guid-123");
        var ip = IPAddress.Parse("10.0.0.122");

        _registryMock.Setup(r => r.AllocateNextAvailable("bf6.exe")).Returns(33);
        _clientMock.Setup(c => c.EnsureRuleExistsAsync(It.IsAny<OPNsenseRuleRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("uuid-override");

        var uuid = await _overrideManager.SetOverrideAsync(identity, ip, "WAN1");

        Assert.Equal("uuid-override", uuid);

        _clientMock.Verify(c => c.EnsureRuleExistsAsync(It.Is<OPNsenseRuleRequest>(r =>
            r.Gateway == "WAN1" &&
            r.SourceIp == "10.0.0.122" &&
            r.DscpValue == 33 &&
            r.DestinationNet == "(self)" &&
            r.DestinationNot == "1" &&
            r.Category == "OptiRoute" &&
            r.Description == "OPTIROUTE|OVERRIDE|bf6.exe|33|10.0.0.122|guid-123|v=1"
        ), It.IsAny<CancellationToken>()), Times.Once);

        _orderMock.Verify(o => o.EnsureRelativeOrderAsync("lan", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveOverrideAsync_WhenExists_ShouldDeleteRule()
    {
        var ip = IPAddress.Parse("10.0.0.122");
        var rules = new List<FirewallRuleInfo>
        {
            new()
            {
                Uuid = "uuid-to-delete",
                Description = "OPTIROUTE|OVERRIDE|bf6.exe|33|10.0.0.122|guid-123|v=1",
                Gateway = "WAN1",
                Category = "OptiRoute"
            }
        };

        _clientMock.Setup(c => c.ListOptiRouteRulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        var removed = await _overrideManager.RemoveOverrideAsync("bf6.exe", ip);

        Assert.True(removed);
        _clientMock.Verify(c => c.DeleteRuleAsync("uuid-to-delete", It.IsAny<CancellationToken>()), Times.Once);
    }
}
