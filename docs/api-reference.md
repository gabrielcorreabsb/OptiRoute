# API and Contracts Reference — OptiRoute

> **Notice (v0.1.x).** The .NET contract snippets in §2 lag behind the current
> implementation. `IOptiRouteSynchronizer.SyncAsync` now takes an `IPAddress`
> and an `IProgress<SyncProgress>?` and returns `Task<OptiRouteSyncResult>`
> (not `SyncResult`). The full pipeline exposes `BuildPlanAsync`,
> `ApplyPlanAsync`, `VerifyAsync`, and `VerifyRoutesAsync`; this file documents
> the pre-pipeline shape. A regenerated reference is planned for the v0.2
> release alongside the multi-PC smoke validation. Until then, treat the code
> as the source of truth — see `src/OptiRoute.Core/Interfaces/`.

OptiRoute is a self-contained WPF desktop application. It does not run a local HTTP server, does not use ASP.NET Core, and does not open any ports on the host. This reference therefore documents two things:

1. The REST endpoints consumed on OPNsense.
2. The internal .NET 10 contracts and interfaces (note the lag above).

## 1. OPNsense REST API endpoints

Communication with the firewall goes through `OpnsenseClient` over HTTPS using HTTP Basic authentication (`ApiKey:ApiSecret`).

### 1.1 Gateways and routing

`GET /api/routes/gateway/status`

Returns the operational status of every gateway and gateway group on OPNsense. The response carries `name`, `status` (`online` or `offline`), `loss` (packet loss percentage), `delay` (round-trip time in ms), and the egress IP address.

### 1.2 Firewall categories (os-firewall plugin)

Categories are tags applied to rules for filtering and grouping. OptiRoute uses the `OptiRoute` category on every rule it manages.

The `<categories>` field in `Firewall/Filter.xml` is a `ModelRelationField` validated by UUID on write, not by display name. Sending the category name (for example `"OptiRoute"`) returns `validations.rule.categories='Related category not found'` even when the category exists. OptiRoute therefore resolves the UUID through `search_item` and caches it in memory before each `addRule` call.

`GET /api/firewall/category/search_item?add_empty=0`

Lists all registered categories. The response is `{"rows":[{"uuid":"...","name":"..."}]}`.

`POST /api/firewall/category/add_item`

Creates a new category. Payload: `{"category":{"name":"OptiRoute"}}`. Response: `{"result":"saved","uuid":"..."}`.

### 1.3 Firewall rules (os-firewall plugin)

`POST /api/firewall/filter/searchRule`

Queries the active automation rules. Payload: `{"searchPhrase": "OPTIROUTE"}`. Response: the list of rules in the `OptiRoute` category with their UUIDs, interfaces, descriptions, gateways, and ToS/DSCP values.

`POST /api/firewall/filter/addRule`

Creates a new firewall rule on the LAN interface.

Payload fields:

- `sequence`: evaluation order
- `description`: structured descriptor (`OPTIROUTE|DEFAULT|...` or `OPTIROUTE|OVERRIDE|...`), with the `|v=1` suffix
- `categories`: UUID of the category, never the name
- `interface`: `"lan"`
- `direction`: `"in"`
- `action`: `"pass"`
- `quick`: `"1"`
- `destination_net`: `"(self)"`
- `destination_not`: `"1"`
- `tos`: hexadecimal value (for example `"0x84"` for DSCP 33)
- `gateway`: destination WAN name (for example `"WAN2"`)
- `source_net`: `"lan"` for Default, or a static IP for Override

`POST /api/firewall/filter/setRule/{uuid}`

Updates the properties of an existing rule, for example to change the gateway or the sequence.

`POST /api/firewall/filter/delRule/{uuid}`

Deletes a rule by UUID.

`POST /api/firewall/filter/apply`

Applies and reloads the packet filter rules in memory on OPNsense.

## 2. Internal .NET 10 contracts

### 2.1 `IOptiRouteSynchronizer`

The central state orchestrator.

```csharp
public interface IOptiRouteSynchronizer
{
    Task<SyncResult> SyncAsync(string localHostIp, CancellationToken ct = default);
    Task RegisterOrUpdateApplicationAsync(ApplicationIdentity identity, string defaultGateway, int? explicitDscp = null, CancellationToken ct = default);
    Task DeleteGlobalApplicationAsync(string executableName, CancellationToken ct = default);
    Task RemoveLocalApplicationAsync(string executableName, CancellationToken ct = default);
    Task PromoteLocalToGlobalAsync(string executableName, string targetGateway, CancellationToken ct = default);
    Task ActivateGlobalOnLocalAsync(string executableName, CancellationToken ct = default);
    Task RepairConflictAsync(string executableName, CancellationToken ct = default);
}
```

### 2.2 `IWindowsQosManager`

Manipulates Windows network policies through PowerShell.

```csharp
public interface IWindowsQosManager
{
    Task CreatePolicyAsync(string executableName, int dscp, CancellationToken ct = default);
    Task DeletePolicyAsync(LocalQosPolicy policy, CancellationToken ct = default);
    Task<IReadOnlyList<LocalQosPolicy>> ListLocalPoliciesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<QosPolicy>> ListOptiRoutePoliciesAsync(CancellationToken ct = default);
}
```

### 2.3 `IHostOverrideManager`

Manages gateway overrides keyed by machine IP.

```csharp
public interface IHostOverrideManager
{
    Task SetOverrideAsync(string executableName, string hostIp, string gateway, int dscp, Guid appId, CancellationToken ct = default);
    Task RemoveOverrideAsync(string executableName, string hostIp, CancellationToken ct = default);
}
```

### 2.4 `IRuleOrderManager`

Controls relative rule ordering on the firewall.

```csharp
public interface IRuleOrderManager
{
    Task EnsureRelativeOrderAsync(string interfaceName = "lan", CancellationToken ct = default);
}
```