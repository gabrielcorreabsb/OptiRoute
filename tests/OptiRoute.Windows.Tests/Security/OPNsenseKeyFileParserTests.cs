using OptiRoute.Windows.Security;
using Xunit;

namespace OptiRoute.Windows.Tests.Security;

public class OPNsenseKeyFileParserTests
{
    [Fact]
    public void ParseContent_StandardFormat_ShouldExtractKeyAndSecret()
    {
        var content = """
            key=abcdef1234567890abcdef1234567890
            secret=secret_xyz9876543210987654321
            """;

        var creds = OPNsenseKeyFileParser.ParseContent(content);

        Assert.Equal("abcdef1234567890abcdef1234567890", creds.ApiKey);
        Assert.Equal("secret_xyz9876543210987654321", creds.ApiSecret);
    }

    [Fact]
    public void ParseContent_WithWhitespaceAndComments_ShouldIgnoreAndExtract()
    {
        var content = """
            # OPNsense API Key generated on 2026-09-27
            
               key = abcdef1234567890   
               secret = secret_xyz987654321   
            """;

        var creds = OPNsenseKeyFileParser.ParseContent(content);

        Assert.Equal("abcdef1234567890", creds.ApiKey);
        Assert.Equal("secret_xyz987654321", creds.ApiSecret);
    }

    [Fact]
    public void ParseContent_MissingSecret_ShouldThrowInvalidOperationException()
    {
        var content = "key=abcdef1234567890";

        Assert.Throws<InvalidOperationException>(() => OPNsenseKeyFileParser.ParseContent(content));
    }

    [Fact]
    public void ParseContent_MissingKey_ShouldThrowInvalidOperationException()
    {
        var content = "secret=secret_xyz987654321";

        Assert.Throws<InvalidOperationException>(() => OPNsenseKeyFileParser.ParseContent(content));
    }
}
