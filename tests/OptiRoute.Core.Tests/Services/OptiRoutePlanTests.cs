using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OptiRoute.Core.Interfaces;
using OptiRoute.Core.Models;
using OptiRoute.Core.Services;
using Xunit;

namespace OptiRoute.Core.Tests.Services;

/// <summary>
/// Cobre o pipeline BuildPlan → ApplyPlan → Verify introduzido no Delta 2.
/// </summary>
public class OptiRoutePlanTests
{
    private readonly Mock<IOpnsenseClient>    _clientMock   = new();
    private readonly Mock<IWindowsQosManager> _qosMock      = new();
    private readonly Mock<IDscpRegistry>      _registryMock = new();
    private readonly Mock<IRuleOrderManager>  _orderMock    = new();
    private readonly OptiRouteSynchronizer    _synchronizer;
    private readonly IPAddress                _localIp      = IPAddress.Parse("10.0.0.100");

    public OptiRoutePlanTests()
    {
        _synchronizer = new OptiRouteSynchronizer(
            _clientMock.Object,
            _qosMock.Object,
            _registryMock.Object,
            _orderMock.Object,
            NullLogger<OptiRouteSynchronizer>.Instance);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static EffectiveApplicationRoute MakeRoute(
        string exe,
        ApplicationSyncState state,
        int? localDscp = null,
        int? globalDscp = null,
        string gateway = "WAN1")
        => new()
        {
            Identity        = new ApplicationIdentity { ExecutableName = exe, AppId = $"guid-{exe}" },
            SyncState       = state,
            Dscp            = globalDscp ?? localDscp ?? 0,
            LocalDscp       = localDscp,
            GlobalDscp      = globalDscp,
            DefaultGateway  = gateway,
            EffectiveGateway = gateway
        };

    private static OptiRouteSyncResult StateOf(params EffectiveApplicationRoute[] routes)
        => new()
        {
            LocalHostIp = IPAddress.Parse("10.0.0.100"),
            Routes      = routes,
            Gateways    = new List<Gateway> { new() { Name = "WAN1" } }
        };

    // ──────────────────────────────────────────────────────────────────────────
    // BuildPlanAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildPlanAsync_SynchronizedRoute_EmitsNoAction()
    {
        var state = StateOf(MakeRoute("bf6.exe", ApplicationSyncState.Synchronized, 33, 33));

        var plan = await _synchronizer.BuildPlanAsync(state);

        Assert.Empty(plan.Actions);
        Assert.False(plan.HasActions);
        Assert.Equal(0, plan.ActionCount);
    }

    [Fact]
    public async Task BuildPlanAsync_ConflictRoute_EmitsUpdateQosPolicyWithGlobalDscp()
    {
        var state = StateOf(MakeRoute("bf6.exe", ApplicationSyncState.Conflict, 26, 33));

        var plan = await _synchronizer.BuildPlanAsync(state);

        Assert.Single(plan.Actions);
        var action = plan.Actions[0];
        Assert.Equal(ReconciliationActionType.UpdateQosPolicy, action.Type);
        Assert.Equal("bf6.exe", action.Executable);
        Assert.Equal(33, action.Dscp);
        Assert.Equal("WAN1", action.TargetGateway);
        Assert.Equal("OptiRoute-bf6.exe", action.LocalQosPolicyName);
    }

    [Fact]
    public async Task BuildPlanAsync_GlobalOnlyRoute_EmitsCreateQosPolicy()
    {
        var state = StateOf(MakeRoute("cod.exe", ApplicationSyncState.GlobalOnly, null, 35));

        var plan = await _synchronizer.BuildPlanAsync(state);

        Assert.Single(plan.Actions);
        var action = plan.Actions[0];
        Assert.Equal(ReconciliationActionType.CreateQosPolicy, action.Type);
        Assert.Equal("cod.exe", action.Executable);
        Assert.Equal(35, action.Dscp);
    }

    [Fact]
    public async Task BuildPlanAsync_LocalOnlyRoute_EmitsDeleteLocalQosPolicy()
    {
        // D1: per-card fica com promoção. Plano conservador oferta Delete.
        var state = StateOf(MakeRoute("orphan.exe", ApplicationSyncState.LocalOnly, 40, null));

        var plan = await _synchronizer.BuildPlanAsync(state);

        Assert.Single(plan.Actions);
        var action = plan.Actions[0];
        Assert.Equal(ReconciliationActionType.DeleteLocalQosPolicy, action.Type);
        Assert.Equal("orphan.exe", action.Executable);
        Assert.Equal(40, action.Dscp);
        Assert.Equal(string.Empty, action.TargetGateway);
    }

    [Fact]
    public async Task BuildPlanAsync_MixedStates_EmitsOnlyNonSynchronizedActions()
    {
        var state = StateOf(
            MakeRoute("sync.exe",   ApplicationSyncState.Synchronized, 33, 33),
            MakeRoute("conflict.exe", ApplicationSyncState.Conflict,    26, 33),
            MakeRoute("global.exe",  ApplicationSyncState.GlobalOnly,   null, 35),
            MakeRoute("orphan.exe",  ApplicationSyncState.LocalOnly,    40, null));

        var plan = await _synchronizer.BuildPlanAsync(state);

        Assert.Equal(3, plan.Actions.Count);
        Assert.Contains(plan.Actions, a => a.Type == ReconciliationActionType.UpdateQosPolicy);
        Assert.Contains(plan.Actions, a => a.Type == ReconciliationActionType.CreateQosPolicy);
        Assert.Contains(plan.Actions, a => a.Type == ReconciliationActionType.DeleteLocalQosPolicy);
    }

    [Fact]
    public async Task BuildPlanAsync_CalledTwiceWithSameState_IsIdempotent()
    {
        var state = StateOf(
            MakeRoute("conflict.exe", ApplicationSyncState.Conflict, 26, 33),
            MakeRoute("global.exe",   ApplicationSyncState.GlobalOnly, null, 35));

        var plan1 = await _synchronizer.BuildPlanAsync(state);
        var plan2 = await _synchronizer.BuildPlanAsync(state);

        Assert.Equal(plan1.Actions.Count, plan2.Actions.Count);
        for (int i = 0; i < plan1.Actions.Count; i++)
        {
            Assert.Equal(plan1.Actions[i].Type, plan2.Actions[i].Type);
            Assert.Equal(plan1.Actions[i].Executable, plan2.Actions[i].Executable);
            Assert.Equal(plan1.Actions[i].Dscp, plan2.Actions[i].Dscp);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ApplyPlanAsync
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyPlanAsync_UpdateQosPolicy_CallsQosManagerWithCorrectArgs()
    {
        var plan = new ReconciliationPlan
        {
            Actions = new[]
            {
                new ReconciliationAction(
                    Type:               ReconciliationActionType.UpdateQosPolicy,
                    Executable:         "bf6.exe",
                    AppId:              "guid-1",
                    SourceIp:           null,
                    Dscp:               33,
                    TargetGateway:      "WAN1",
                    RuleUuid:           null,
                    LocalQosPolicyName: "OptiRoute-bf6.exe",
                    Description:        "test")
            }
        };

        var result = await _synchronizer.ApplyPlanAsync(plan);

        Assert.True(result.AllSucceeded);
        Assert.Empty(result.Failures);
        Assert.Equal(1, result.VerifiedCount);
        _qosMock.Verify(q => q.UpdatePolicyAsync("bf6.exe", 33, It.IsAny<CancellationToken>()), Times.Once);
        // QoS-only: ordem relativa NÃO deve ser checada
        _orderMock.Verify(o => o.EnsureRelativeOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyPlanAsync_CreateQosPolicy_CallsQosManagerCreate()
    {
        var plan = new ReconciliationPlan
        {
            Actions = new[]
            {
                new ReconciliationAction(
                    Type:               ReconciliationActionType.CreateQosPolicy,
                    Executable:         "cod.exe",
                    AppId:              "guid-2",
                    SourceIp:           null,
                    Dscp:               35,
                    TargetGateway:      "WAN1",
                    RuleUuid:           null,
                    LocalQosPolicyName: "OptiRoute-cod.exe",
                    Description:        "test")
            }
        };

        var result = await _synchronizer.ApplyPlanAsync(plan);

        Assert.True(result.AllSucceeded);
        _qosMock.Verify(q => q.CreatePolicyAsync("cod.exe", 35, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyPlanAsync_DeleteLocalQosPolicy_CallsQosManagerDelete()
    {
        var plan = new ReconciliationPlan
        {
            Actions = new[]
            {
                new ReconciliationAction(
                    Type:               ReconciliationActionType.DeleteLocalQosPolicy,
                    Executable:         "orphan.exe",
                    AppId:              "guid-3",
                    SourceIp:           null,
                    Dscp:               0,
                    TargetGateway:      string.Empty,
                    RuleUuid:           null,
                    LocalQosPolicyName: "OptiRoute-orphan.exe",
                    Description:        "test")
            }
        };

        var result = await _synchronizer.ApplyPlanAsync(plan);

        Assert.True(result.AllSucceeded);
        _qosMock.Verify(q => q.DeletePolicyAsync("orphan.exe", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyPlanAsync_FirewallAction_ReturnsFailureWithNotImplementedReason()
    {
        var plan = new ReconciliationPlan
        {
            Actions = new[]
            {
                new ReconciliationAction(
                    Type:               ReconciliationActionType.CreateFirewallRule,
                    Executable:         "x.exe",
                    AppId:              "guid-4",
                    SourceIp:           null,
                    Dscp:               0,
                    TargetGateway:      "WAN1",
                    RuleUuid:           null,
                    LocalQosPolicyName: null,
                    Description:        "test")
            }
        };

        var result = await _synchronizer.ApplyPlanAsync(plan);

        Assert.False(result.AllSucceeded);
        Assert.Single(result.Failures);
        Assert.Contains("ainda não", result.Failures[0].Reason, StringComparison.OrdinalIgnoreCase);
        _clientMock.VerifyNoOtherCalls(); // firewall actions ainda não tocam OPNsense
    }

    [Fact]
    public async Task ApplyPlanAsync_QosThrowsAction_ReturnsFailureWithExceptionMessage()
    {
        _qosMock.Setup(q => q.UpdatePolicyAsync("broken.exe", 33, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("access denied"));

        var plan = new ReconciliationPlan
        {
            Actions = new[]
            {
                new ReconciliationAction(
                    Type:               ReconciliationActionType.UpdateQosPolicy,
                    Executable:         "broken.exe",
                    AppId:              "guid-5",
                    SourceIp:           null,
                    Dscp:               33,
                    TargetGateway:      "WAN1",
                    RuleUuid:           null,
                    LocalQosPolicyName: "OptiRoute-broken.exe",
                    Description:        "test")
            }
        };

        var result = await _synchronizer.ApplyPlanAsync(plan);

        Assert.False(result.AllSucceeded);
        Assert.Single(result.Failures);
        Assert.Equal("access denied", result.Failures[0].Reason);
        Assert.Equal(ReconciliationActionType.UpdateQosPolicy, result.Failures[0].Action.Type);
    }

    [Fact]
    public async Task ApplyPlanAsync_MixedSuccessAndFailure_ContinuesAndReturnsBoth()
    {
        _qosMock.Setup(q => q.UpdatePolicyAsync("good.exe", 33, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        _qosMock.Setup(q => q.UpdatePolicyAsync("bad.exe", 35, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

        var plan = new ReconciliationPlan
        {
            Actions = new[]
            {
                new ReconciliationAction(
                    Type: ReconciliationActionType.UpdateQosPolicy, Executable: "good.exe",
                    AppId: "g1", SourceIp: null, Dscp: 33, TargetGateway: "WAN1",
                    RuleUuid: null, LocalQosPolicyName: "OptiRoute-good.exe", Description: "ok"),
                new ReconciliationAction(
                    Type: ReconciliationActionType.UpdateQosPolicy, Executable: "bad.exe",
                    AppId: "g2", SourceIp: null, Dscp: 35, TargetGateway: "WAN1",
                    RuleUuid: null, LocalQosPolicyName: "OptiRoute-bad.exe", Description: "fail"),
                new ReconciliationAction(
                    Type: ReconciliationActionType.CreateQosPolicy, Executable: "third.exe",
                    AppId: "g3", SourceIp: null, Dscp: 40, TargetGateway: "WAN1",
                    RuleUuid: null, LocalQosPolicyName: "OptiRoute-third.exe", Description: "ok")
            }
        };

        var result = await _synchronizer.ApplyPlanAsync(plan);

        Assert.False(result.AllSucceeded);
        Assert.Equal(2, result.VerifiedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Single(result.Failures);
        Assert.Equal("bad.exe", result.Failures[0].Action.Executable);
    }

    [Fact]
    public async Task ApplyPlanAsync_HasFirewallAction_ChecksRuleOrderEvenIfFailed()
    {
        var plan = new ReconciliationPlan
        {
            Actions = new[]
            {
                new ReconciliationAction(
                    Type: ReconciliationActionType.CreateFirewallRule,
                    Executable: "x.exe", AppId: "g", SourceIp: null,
                    Dscp: 0, TargetGateway: "WAN1", RuleUuid: null,
                    LocalQosPolicyName: null, Description: "firewall")
            }
        };

        await _synchronizer.ApplyPlanAsync(plan);

        _orderMock.Verify(o => o.EnsureRelativeOrderAsync("lan", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApplyPlanAsync_EmptyPlan_ReturnsEmptyResult()
    {
        var result = await _synchronizer.ApplyPlanAsync(ReconciliationPlan.Empty);

        Assert.True(result.AllSucceeded);
        Assert.Equal(0, result.VerifiedCount);
        Assert.Equal(0, result.FailedCount);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // VerifyAsync — depende de SyncAsync (precisa mockar IOpnsenseClient + IWindowsQosManager)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_AllActionsMatchState_AllSucceeded()
    {
        // Plano: corrigir DSCP de 26 para 33 em bf6.exe
        var plan = new ReconciliationPlan
        {
            Actions = new[]
            {
                new ReconciliationAction(
                    Type: ReconciliationActionType.UpdateQosPolicy, Executable: "bf6.exe",
                    AppId: "g1", SourceIp: null, Dscp: 33, TargetGateway: "WAN1",
                    RuleUuid: null, LocalQosPolicyName: "OptiRoute-bf6.exe", Description: "fix")
            }
        };

        _clientMock.Setup(c => c.ListOptiRouteRulesAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<FirewallRuleInfo>
                   {
                       new()
                       {
                           Uuid = "u",
                           Description = "OPTIROUTE|DEFAULT|bf6.exe|33|guid-bf6.exe",
                           Gateway = "WAN1",
                           Category = "OptiRoute",
                           Sequence = 10
                       }
                   });
        _registryMock.Setup(r => r.GetByExecutable(It.IsAny<string>())).Returns((int?)null);
        _qosMock.Setup(q => q.ListLocalPoliciesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<LocalQosPolicy>
                {
                    new()
                    {
                        Name           = "OptiRoute-bf6.exe",
                        ExecutableName = "bf6.exe",
                        Dscp           = 33
                    }
                });

        var result = await _synchronizer.VerifyAsync(plan, _localIp);

        Assert.True(result.AllSucceeded);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public async Task VerifyAsync_LocalDscpStillDiverges_AddsFailureWithReason()
    {
        var plan = new ReconciliationPlan
        {
            Actions = new[]
            {
                new ReconciliationAction(
                    Type: ReconciliationActionType.UpdateQosPolicy, Executable: "bf6.exe",
                    AppId: "g1", SourceIp: null, Dscp: 33, TargetGateway: "WAN1",
                    RuleUuid: null, LocalQosPolicyName: "OptiRoute-bf6.exe", Description: "fix")
            }
        };

        // Estado pós-apply: DSCP local AINDA está 26 (não corrigiu).
        // BuildState real lê QoS local via _qosMock.ListLocalPoliciesAsync().
        _qosMock.Setup(q => q.ListLocalPoliciesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<LocalQosPolicy>
                {
                    new()
                    {
                        Name           = "OptiRoute-bf6.exe",
                        ExecutableName = "bf6.exe",
                        Dscp           = 26
                    }
                });

        var result = await _synchronizer.VerifyAsync(plan, _localIp);

        Assert.False(result.AllSucceeded);
        Assert.Single(result.Failures);
        Assert.Contains("26", result.Failures[0].Reason);
    }

    [Fact]
    public async Task VerifyAsync_LocalOnlyWasDeleted_VerifiesSuccess()
    {
        var plan = new ReconciliationPlan
        {
            Actions = new[]
            {
                new ReconciliationAction(
                    Type: ReconciliationActionType.DeleteLocalQosPolicy, Executable: "orphan.exe",
                    AppId: "g1", SourceIp: null, Dscp: 0, TargetGateway: string.Empty,
                    RuleUuid: null, LocalQosPolicyName: "OptiRoute-orphan.exe", Description: "del")
            }
        };

        // Pós-apply: rota não existe mais (sem regra OPNsense e sem QoS local)
        _clientMock.Setup(c => c.ListOptiRouteRulesAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<FirewallRuleInfo>());
        _qosMock.Setup(q => q.ListLocalPoliciesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<LocalQosPolicy>());

        var result = await _synchronizer.VerifyAsync(plan, _localIp);

        Assert.True(result.AllSucceeded);
    }

    [Fact]
    public async Task VerifyAsync_EmptyPlan_AllSucceededNoFailures()
    {
        _clientMock.Setup(c => c.ListOptiRouteRulesAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<FirewallRuleInfo>());
        _qosMock.Setup(q => q.ListLocalPoliciesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<LocalQosPolicy>());

        var result = await _synchronizer.VerifyAsync(ReconciliationPlan.Empty, _localIp);

        Assert.True(result.AllSucceeded);
        Assert.Equal(0, result.VerifiedCount);
        Assert.Equal(0, result.FailedCount);
    }
}
