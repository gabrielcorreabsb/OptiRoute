using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OptiRoute.Core.Exceptions;
using OptiRoute.Core.Interfaces;
using OptiRoute.Core.Models;

namespace OptiRoute.OPNsense.Client;

/// <summary>
/// Implementação de <see cref="IOpnsenseClient"/> para a API REST do OPNsense.
/// <para>
/// Usa a Automation/Filter API (plugin os-firewall).
/// Regras utilizam categoria "OptiRoute" e formato estruturado:
/// DEFAULT:  OPTIROUTE|DEFAULT|&lt;exe&gt;|&lt;dscp&gt;|&lt;appId&gt;
/// OVERRIDE: OPTIROUTE|OVERRIDE|&lt;exe&gt;|&lt;dscp&gt;|&lt;ip&gt;|&lt;appId&gt;
/// </para>
/// </summary>
public sealed class OpnsenseClient : IOpnsenseClient
{
    internal const string CategoryName = "OptiRoute";

    private readonly HttpClient                _http;
    private readonly ILogger<OpnsenseClient>   _logger;

    /// <summary>
    /// Cache em memória de categorias já confirmadas como existentes no OPNsense.
    /// Chave = nome (case-insensitive), valor = UUID. Evita refazer GET /api/firewall/category/search_item
    /// a cada addRule e fornece o UUID que <c>addRule</c> espera (ModelRelationField).
    /// </summary>
    private readonly Dictionary<string, string> _categoryUuidByName = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OpnsenseClient(HttpClient http, ILogger<OpnsenseClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Conectividade
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await GetVersionAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OPNsense] Connection test failed");
            return false;
        }
    }

    public async Task<string> GetVersionAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/core/firmware/info", ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        if (doc.RootElement.TryGetProperty("firmware", out var fw) &&
            fw.TryGetProperty("version", out var ver))
            return ver.GetString() ?? "unknown";

        if (doc.RootElement.TryGetProperty("product_version", out var pv))
            return pv.GetString() ?? "unknown";

        return "unknown";
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Gateways
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Gateway>> GetGatewaysAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/routes/gateway/status", ct);
        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<GatewayStatusResponse>(JsonOpts, ct)
            ?? throw new OpnsenseApiException("Empty response from gateway status endpoint");

        return payload.Items.Select(MapGateway).ToList();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Categorias de firewall (pré-condição para addRule)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<string?> EnsureCategoryExistsAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (_categoryUuidByName.TryGetValue(name, out var cachedUuid)) return cachedUuid;

        // 1. Listar categorias existentes
        var listResp = await _http.GetAsync("/api/firewall/category/search_item?add_empty=0", ct);
        listResp.EnsureSuccessStatusCode();

        var list = await listResp.Content.ReadFromJsonAsync<CategoryListResponse>(JsonOpts, ct)
                   ?? throw new OpnsenseApiException("Empty response from category/search_item");

        var existing = list.Rows?.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        string uuid;
        if (existing is not null)
        {
            _logger.LogInformation(
                "[OPNsense] Category '{Name}' already exists (uuid={Uuid}).",
                name, existing.Uuid);
            uuid = existing.Uuid!;
        }
        else
        {
            // 2. Criar
            var body = new { category = new { name } };
            var content = new StringContent(
                JsonSerializer.Serialize(body, JsonOpts),
                Encoding.UTF8, "application/json");

            var createResp = await _http.PostAsync("/api/firewall/category/add_item", content, ct);
            createResp.EnsureSuccessStatusCode();

            var result = await createResp.Content.ReadFromJsonAsync<AddCategoryResponse>(JsonOpts, ct)
                         ?? throw new OpnsenseApiException("Empty response from category/add_item");

            if (string.IsNullOrEmpty(result.Uuid))
                throw new OpnsenseApiException(
                    $"Failed to create category '{name}': result='{result.Result}', uuid='{result.Uuid}'");

            _logger.LogInformation(
                "[OPNsense] Category '{Name}' created (uuid={Uuid}).",
                name, result.Uuid);
            uuid = result.Uuid;
        }

        _categoryUuidByName[name] = uuid;
        return uuid;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Regras de firewall
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<string> EnsureRuleExistsAsync(OPNsenseRuleRequest request, CancellationToken ct = default)
    {
        // 0. Traduzir nome da categoria para UUID.
        //    OPNsense ModelRelationField (categories em Filter.xml) espera UUID na escrita —
        //    mesmo que a categoria exista com o nome, addRule rejeita "Related category not found"
        //    se receber o nome em vez do UUID. Mutamos request.Category in-place porque o caller
        //    (synchronizer) constrói um request novo a cada chamada.
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var categoryUuid = await EnsureCategoryExistsAsync(request.Category, ct);
            if (categoryUuid is not null)
                request.Category = categoryUuid;
        }

        // 1. Verificar se regra com essa descrição já existe
        var existing = await FindRuleByDescriptionAsync(request.Description, ct);

        if (existing is not null)
        {
            _logger.LogInformation("[OPNsense] Rule '{Desc}' already exists (uuid={Uuid}). Updating.",
                request.Description, existing.Uuid);

            await UpdateRuleAsync(existing.Uuid, request, ct);
            await ApplyRulesAsync(ct);
            return existing.Uuid;
        }

        // 2. Criar nova regra
        var body    = BuildRulePayload(request);
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var resp = await _http.PostAsync("/api/firewall/filter/addRule", content, ct);
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<AddRuleResponse>(JsonOpts, ct)
            ?? throw new OpnsenseApiException("Empty response from addRule endpoint");

        if (result.Result != "saved" || string.IsNullOrEmpty(result.Uuid))
        {
            var validationDetails = result.Validations is { Count: > 0 }
                ? " — validações: " + string.Join("; ", result.Validations.Select(kv => $"{kv.Key}='{kv.Value}'"))
                : " (sem detalhes de validação retornados pela API)";

            var payloadJson = JsonSerializer.Serialize(body, JsonOpts);
            _logger.LogError(
                "[OPNsense] addRule falhou: result='{Result}'{Details}. Payload enviado: {Payload}",
                result.Result, validationDetails, payloadJson);

            throw new OpnsenseApiException(
                $"addRule falhou: result='{result.Result}'{validationDetails}");
        }

        _logger.LogInformation("[OPNsense] Created rule '{Desc}' uuid={Uuid}",
            request.Description, result.Uuid);

        await ApplyRulesAsync(ct);
        return result.Uuid;
    }

    public async Task DeleteRuleAsync(string uuid, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"/api/firewall/filter/delRule/{uuid}", content: null, ct);
        resp.EnsureSuccessStatusCode();
        _logger.LogInformation("[OPNsense] Deleted rule uuid={Uuid}", uuid);
        await ApplyRulesAsync(ct);
    }

    public async Task ApplyRulesAsync(CancellationToken ct = default)
    {
        var resp = await _http.PostAsync("/api/firewall/filter/apply", content: null, ct);
        resp.EnsureSuccessStatusCode();
        _logger.LogDebug("[OPNsense] Rules applied.");
    }

    public async Task<IReadOnlyList<FirewallRuleInfo>> ListOptiRouteRulesAsync(CancellationToken ct = default)
    {
        var allRules = await ListAllRulesAsync(interfaceName: null, ct);

        return allRules
            .Where(r => r.IsOptiRouteRule)
            .ToList();
    }

    public async Task<IReadOnlyList<FirewallRuleInfo>> ListAllRulesAsync(string? interfaceName = null, CancellationToken ct = default)
    {
        // rowCount=-1 para buscar todas as regras
        var url = "/api/firewall/filter/searchRule?rowCount=500";
        var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<SearchRuleResponse>(JsonOpts, ct);

        var rows = payload?.Rows ?? [];

        // Delta 7: alerta de paginação silenciosa. Se total > rows.Count, a API cortou
        // regras sem indicar. Sem isso, o sincronizador acharia que está em sync quando
        // na verdade há regras não-carregadas. O usuário precisa aumentar rowCount ou
        // implementar paginação (delta futuro).
        if (payload is { Total: var total } && total > rows.Count)
        {
            _logger.LogWarning(
                "[OPNsense] searchRule paginou silenciosamente: retornou {Rows} regras mas o total é {Total}. " +
                "Regras podem ter sido omitidas — considere aumentar rowCount ou implementar paginação.",
                rows.Count, total);
        }

        return rows
            .Where(r => string.IsNullOrEmpty(interfaceName) ||
                        r.Interface?.Equals(interfaceName, StringComparison.OrdinalIgnoreCase) == true)
            .Select(r => new FirewallRuleInfo
            {
                Uuid           = r.Uuid           ?? string.Empty,
                Description    = r.Description    ?? string.Empty,
                Enabled        = r.Enabled == "1",
                Sequence       = int.TryParse(r.Sequence, out var seq) ? seq : 100,
                Interface      = r.Interface      ?? string.Empty,
                SourceNet      = r.SourceNet      ?? string.Empty,
                DestinationNet = r.DestinationNet ?? string.Empty,
                DestinationNot = r.DestinationNot ?? string.Empty,
                Gateway        = r.Gateway        ?? string.Empty,
                Category       = r.Categories     ?? string.Empty,
                Tos            = r.Tos            ?? string.Empty
            })
            .OrderBy(r => r.Sequence)
            .ToList();
    }

    public async Task UpdateRuleSequenceAsync(string uuid, int sequence, CancellationToken ct = default)
    {
        var body = new
        {
            rule = new
            {
                sequence = sequence.ToString()
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync($"/api/firewall/filter/setRule/{uuid}", content, ct);
        resp.EnsureSuccessStatusCode();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Auxiliares privados
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<FirewallRuleInfo?> FindRuleByDescriptionAsync(string description, CancellationToken ct)
    {
        var all = await ListOptiRouteRulesAsync(ct);
        return all.FirstOrDefault(r =>
            r.Description.Equals(description, StringComparison.OrdinalIgnoreCase));
    }

    private async Task UpdateRuleAsync(string uuid, OPNsenseRuleRequest request, CancellationToken ct)
    {
        var body    = BuildRulePayload(request);
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var resp = await _http.PostAsync($"/api/firewall/filter/setRule/{uuid}", content, ct);
        resp.EnsureSuccessStatusCode();
    }

    private static object BuildRulePayload(OPNsenseRuleRequest req)
    {
        var tosByte = req.DscpValue << 2; // ex: 33 * 4 = 132
        var tosHex  = $"0x{tosByte:X2}";  // ex: "0x84"

        return new
        {
            rule = new
            {
                enabled          = "1",
                sequence         = req.Sequence.ToString(),
                action           = "pass",
                quick            = "1",
                categories       = req.Category,
                @interface       = req.Interface,
                direction        = "in",
                ipprotocol       = "inet",
                protocol         = "any",
                source_net       = req.SourceIp,
                source_not       = "0",
                source_port      = "",
                // Destination: "! This firewall" para nunca desviar tráfego local do OPNsense (Unbound 53, GUI, etc.)
                destination_net  = req.DestinationNet,
                destination_not  = req.DestinationNot,
                destination_port = "",
                gateway          = req.Gateway,
                description      = req.Description,
                log              = "1",
                tos              = tosHex,
                dscp             = tosHex
            }
        };
    }

    private static Gateway MapGateway(GatewayStatusItem item)
    {
        var status = item.Status switch
        {
            "none"       => GatewayStatus.Online,
            "loss"       => GatewayStatus.Offline,
            "down"       => GatewayStatus.Offline,
            "force_down" => GatewayStatus.Offline,
            _            => GatewayStatus.Unknown
        };

        return new Gateway
        {
            Name        = item.Name      ?? string.Empty,
            Status      = status,
            Address     = item.Address,
            DelayMs     = item.Delay,
            PacketLoss  = item.Loss,
            Interface   = item.Interface
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // DTOs internos para deserialização
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class GatewayStatusResponse
    {
        [JsonPropertyName("items")]
        public List<GatewayStatusItem> Items { get; set; } = [];
    }

    private sealed class GatewayStatusItem
    {
        [JsonPropertyName("name")]      public string? Name      { get; set; }
        [JsonPropertyName("address")]   public string? Address   { get; set; }
        [JsonPropertyName("status")]    public string? Status    { get; set; }
        [JsonPropertyName("loss")]      public string? Loss      { get; set; }
        [JsonPropertyName("delay")]     public string? Delay     { get; set; }
        [JsonPropertyName("interface")] public string? Interface { get; set; }
    }

    private sealed class SearchRuleResponse
    {
        [JsonPropertyName("rows")]  public List<RuleRow>? Rows  { get; set; }
        [JsonPropertyName("total")] public int Total { get; set; }
    }

    private sealed class RuleRow
    {
        [JsonPropertyName("uuid")]             public string? Uuid           { get; set; }
        [JsonPropertyName("description")]      public string? Description    { get; set; }
        [JsonPropertyName("enabled")]          public string? Enabled        { get; set; }
        [JsonPropertyName("sequence")]         public string? Sequence       { get; set; }
        [JsonPropertyName("interface")]        public string? Interface      { get; set; }
        [JsonPropertyName("source_net")]       public string? SourceNet      { get; set; }
        [JsonPropertyName("destination_net")]  public string? DestinationNet { get; set; }
        [JsonPropertyName("destination_not")]  public string? DestinationNot { get; set; }
        [JsonPropertyName("gateway")]          public string? Gateway        { get; set; }
        [JsonPropertyName("categories")]       public string? Categories     { get; set; }
        [JsonPropertyName("tos")]              public string? Tos           { get; set; }
    }

    private sealed class AddRuleResponse
    {
        [JsonPropertyName("result")]      public string? Result { get; set; }
        [JsonPropertyName("uuid")]        public string? Uuid   { get; set; }

        /// <summary>
        /// OPNsense retorna detalhes de validação por campo quando <c>result == "failed"</c>.
        /// Ex.: { "rule.destination_net": "Invalid destination" }.
        /// Sem isso, ficamos no escuro sobre o motivo da falha.
        /// </summary>
        [JsonPropertyName("validations")]
        public Dictionary<string, string>? Validations { get; set; }
    }

    private sealed class CategoryListResponse
    {
        [JsonPropertyName("rows")] public List<CategoryRow>? Rows { get; set; }
    }

    private sealed class CategoryRow
    {
        [JsonPropertyName("uuid")] public string? Uuid { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
    }

    private sealed class AddCategoryResponse
    {
        [JsonPropertyName("result")] public string? Result { get; set; }
        [JsonPropertyName("uuid")]   public string? Uuid   { get; set; }
    }
}
