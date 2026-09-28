using System.Net;
using OptiRoute.Core.Models;
using Xunit;

namespace OptiRoute.Core.Tests.Models;

public class OptiRouteRuleDescriptorTests
{
    [Fact]
    public void FormatDescription_DefaultRule_ShouldProduceStandardFormat()
    {
        var desc = new OptiRouteRuleDescriptor
        {
            RuleType       = OptiRouteRuleType.Default,
            ExecutableName = "bf6.exe",
            Dscp           = 33,
            AppId          = "guid-1234"
        };

        var text = desc.FormatDescription();

        Assert.Equal("OPTIROUTE|DEFAULT|bf6.exe|33|guid-1234|v=1", text);
    }

    [Fact]
    public void FormatDescription_DefaultRule_WithoutAppId_StillIncludesVersionSuffix()
    {
        var desc = new OptiRouteRuleDescriptor
        {
            RuleType       = OptiRouteRuleType.Default,
            ExecutableName = "bf6.exe",
            Dscp           = 33,
            AppId          = string.Empty
        };

        var text = desc.FormatDescription();

        Assert.Equal("OPTIROUTE|DEFAULT|bf6.exe|33|v=1", text);
    }

    [Fact]
    public void FormatDescription_OverrideRule_ShouldIncludeIp()
    {
        var desc = new OptiRouteRuleDescriptor
        {
            RuleType       = OptiRouteRuleType.Override,
            ExecutableName = "bf6.exe",
            Dscp           = 33,
            SourceIp       = IPAddress.Parse("10.0.0.122"),
            AppId          = "guid-1234"
        };

        var text = desc.FormatDescription();

        Assert.Equal("OPTIROUTE|OVERRIDE|bf6.exe|33|10.0.0.122|guid-1234|v=1", text);
    }

    [Fact]
    public void TryParse_ValidDefaultWithAppId_ShouldParseCorrectly()
    {
        var success = OptiRouteRuleDescriptor.TryParse("OPTIROUTE|DEFAULT|bf6.exe|33|my-guid", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(OptiRouteRuleType.Default, result.RuleType);
        Assert.Equal("bf6.exe", result.ExecutableName);
        Assert.Equal(33, result.Dscp);
        Assert.Equal("my-guid", result.AppId);
    }

    [Fact]
    public void TryParse_ValidOverrideWithIpAndAppId_ShouldParseCorrectly()
    {
        var success = OptiRouteRuleDescriptor.TryParse("OPTIROUTE|OVERRIDE|bf6.exe|33|10.0.0.122|my-guid", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(OptiRouteRuleType.Override, result.RuleType);
        Assert.Equal("bf6.exe", result.ExecutableName);
        Assert.Equal(33, result.Dscp);
        Assert.Equal(IPAddress.Parse("10.0.0.122"), result.SourceIp);
        Assert.Equal("my-guid", result.AppId);
    }

    [Fact]
    public void TryParse_LegacyFormat_ShouldFallbackToDefault()
    {
        var success = OptiRouteRuleDescriptor.TryParse("OPTIRoute_CURL", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal("curl.exe", result.ExecutableName);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Versioned descriptor (v=1) — roundtrip e validação
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TryParse_VersionedDefaultWithAppId_StripsMarkerAndParses()
    {
        var success = OptiRouteRuleDescriptor.TryParse(
            "OPTIROUTE|DEFAULT|bf6.exe|33|guid-1234|v=1", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(OptiRouteRuleType.Default, result.RuleType);
        Assert.Equal("bf6.exe", result.ExecutableName);
        Assert.Equal(33, result.Dscp);
        Assert.Equal("guid-1234", result.AppId);
    }

    [Fact]
    public void TryParse_VersionedOverrideWithAppId_StripsMarkerAndParses()
    {
        var success = OptiRouteRuleDescriptor.TryParse(
            "OPTIROUTE|OVERRIDE|bf6.exe|33|10.0.0.122|guid-1234|v=1", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(OptiRouteRuleType.Override, result.RuleType);
        Assert.Equal("bf6.exe", result.ExecutableName);
        Assert.Equal(IPAddress.Parse("10.0.0.122"), result.SourceIp);
        Assert.Equal("guid-1234", result.AppId);
    }

    [Fact]
    public void TryParse_VersionedDefaultWithoutAppId_RoundtripsAsEmptyAppId()
    {
        var success = OptiRouteRuleDescriptor.TryParse(
            "OPTIROUTE|DEFAULT|bf6.exe|33|v=1", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result!.AppId);
    }

    [Fact]
    public void TryParse_UnknownVersion_IsRejectedSilently()
    {
        var success = OptiRouteRuleDescriptor.TryParse(
            "OPTIROUTE|DEFAULT|bf6.exe|33|guid-1234|v=2", out var result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void FormatDescription_RoundtripsThroughTryParse()
    {
        // Format/TryParse normalizam intencionalmente o ExecutableName (lowercase)
        // e o AppId (trim). O roundtrip preserva os valores normalizados, não os crus.
        var original = new OptiRouteRuleDescriptor
        {
            RuleType       = OptiRouteRuleType.Default,
            ExecutableName = "BF6.EXE",
            Dscp           = 33,
            AppId          = "  guid-ABCD-1234  "
        };

        var text = original.FormatDescription();

        Assert.True(OptiRouteRuleDescriptor.TryParse(text, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal("bf6.exe", parsed!.ExecutableName);
        Assert.Equal(original.Dscp, parsed.Dscp);
        Assert.Equal("guid-ABCD-1234", parsed.AppId); // trim aplicado, não "  guid-ABCD-1234  "
        Assert.Equal(original.RuleType, parsed.RuleType);
    }
}
