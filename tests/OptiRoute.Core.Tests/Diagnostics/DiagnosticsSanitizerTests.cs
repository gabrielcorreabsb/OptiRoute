using System.Text;
using OptiRoute.Core.Diagnostics;

namespace OptiRoute.Core.Tests.Diagnostics;

public class DiagnosticsSanitizerTests
{
    private const string ApiKey    = "abcd1234efgh5678ijkl9012mnop3456";  // 32 chars
    private const string ApiSecret = "secret_AAAA_BBBB_CCCC_DDDD_EEEE_FFFF";

    // Marcador real emitido por DiagnosticsSanitizer (mantido idêntico ao
    // comportamento anterior de DiagnosticsExporter).
    private const string RedactedMarker = "***REDACTED***";

    // Camada 0: Basic token (base64("key:secret"))
    [Fact]
    public void Sanitize_RedactsBasicToken()
    {
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ApiKey}:{ApiSecret}"));
        var input = $"Authorization: Basic {basic}\nother line";
        var output = DiagnosticsSanitizer.Sanitize(input, ApiKey, ApiSecret, "192.168.1.1");
        Assert.DoesNotContain(basic, output);
        Assert.Contains(RedactedMarker, output);
    }

    // Camada 1: ApiKey / ApiSecret literais onde quer que apareçam
    [Fact]
    public void Sanitize_RedactsLiteralApiKeyAndSecret()
    {
        var input = $"api_key={ApiKey}\napi_secret={ApiSecret}";
        var output = DiagnosticsSanitizer.Sanitize(input, ApiKey, ApiSecret);
        Assert.DoesNotContain(ApiKey, output);
        Assert.DoesNotContain(ApiSecret, output);
        Assert.Contains(RedactedMarker, output);
    }

    // Camada 2a: header Authorization genérico
    [Fact]
    public void Sanitize_RedactsAuthorizationHeader()
    {
        var input = "Authorization: Bearer abc123.def456.ghi789";
        var output = DiagnosticsSanitizer.Sanitize(input);
        Assert.DoesNotContain("abc123.def456.ghi789", output);
        Assert.Contains($"Authorization: {RedactedMarker}", output);
    }

    // Camada 2b: Bearer explícito
    [Fact]
    public void Sanitize_RedactsBearerToken()
    {
        var input = "Header: Bearer eyJhbGciOiJIUzI1NiJ9.payload.sig";
        var output = DiagnosticsSanitizer.Sanitize(input);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiJ9.payload.sig", output);
    }

    // Camada 2c: URL com userinfo (scheme://user:pass@host)
    [Fact]
    public void Sanitize_RedactsUserInfoInUrl()
    {
        var input = "Connecting to https://admin:p4ssw0rd@opnsense.local/api/core/firmware";
        var output = DiagnosticsSanitizer.Sanitize(input);
        Assert.DoesNotContain("admin", output);
        Assert.DoesNotContain("p4ssw0rd", output);
        Assert.Contains($"{RedactedMarker}@", output);
    }

    // Camada 3: pares chave=valor sensíveis (api_key, apikey, secret, password, pwd, token)
    [Fact]
    public void Sanitize_RedactsKeyValueSecrets()
    {
        var input = "password=hunter2&token=eyJxxx&api_key=ABC123DEF456";
        var output = DiagnosticsSanitizer.Sanitize(input);
        Assert.DoesNotContain("hunter2", output);
        Assert.DoesNotContain("eyJxxx", output);
        Assert.DoesNotContain("ABC123DEF456", output);
    }

    // Camada 4: host OPNsense mascarado
    [Fact]
    public void Sanitize_MasksOpnsenseHost()
    {
        var input = "POST https://192.168.1.1/api/firewall/filter/addRule";
        var output = DiagnosticsSanitizer.Sanitize(input, host: "192.168.1.1");
        Assert.DoesNotContain("192.168.1.1", output);
        Assert.Contains("[OPNSENSE-HOST]", output);
    }

    // Múltiplas camadas combinadas: input com vários padrões deve redigir todos
    [Fact]
    public void Sanitize_HandlesMultiplePatternsAtOnce()
    {
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ApiKey}:{ApiSecret}"));
        var input = $@"
            GET https://192.168.1.1/api/core/firmware
            Authorization: Basic {basic}
            api_key={ApiKey}
            password=foo
        ";
        var output = DiagnosticsSanitizer.Sanitize(input, ApiKey, ApiSecret, "192.168.1.1");
        Assert.DoesNotContain(basic, output);
        Assert.DoesNotContain(ApiKey, output);
        Assert.DoesNotContain("192.168.1.1", output);
        Assert.DoesNotContain("foo", output);
        Assert.Contains(RedactedMarker, output);
        Assert.Contains("[OPNSENSE-HOST]", output);
    }

    // Edge: input sem nenhum segredo deve passar inalterado
    [Fact]
    public void Sanitize_LeavesCleanInputUnchanged()
    {
        var input = "OptiRoute v0.1.0 — sync 5/5 ok. QoS applied. Rule created.";
        var output = DiagnosticsSanitizer.Sanitize(input, ApiKey, ApiSecret, "192.168.1.1");
        Assert.Equal(input, output);
    }

    // Edge: null / empty
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_HandlesNullAndEmpty(string? input)
    {
        var output = DiagnosticsSanitizer.Sanitize(input!, ApiKey, ApiSecret);
        Assert.Equal(input ?? string.Empty, output);
    }
}
