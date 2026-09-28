# Referência de APIs e Contratos — OptiRoute

Como o OptiRoute opera como um **aplicativo desktop WPF autônomo** (sem servidor HTTP local, sem ASP.NET Core e sem portas abertas no host), esta referência documenta:
1. Os **endpoints REST consumidos no OPNsense**.
2. Os **contratos e interfaces dos serviços internos** (.NET 10).

---

## 1. Endpoints da API REST do OPNsense

A comunicação com o firewall é realizada pelo `OpnsenseClient` via HTTPS utilizando autenticação HTTP Basic (`ApiKey:ApiSecret`).

### 1.1 Gateways e Roteamento
- **`GET /api/routes/gateway/status`**
  - **Função:** Retorna o status operacional de todos os gateways e gateway groups cadastrados no OPNsense.
  - **Dados retornados:** `name`, `status` (`online`, `offline`), `loss` (perda de pacotes em %), `delay` (RTT em ms) e endereço IP de saída.

### 1.2 Categorias de Firewall (Plugin `os-firewall`)
Categorias são tags aplicadas às regras para organização/filtro. O OptiRoute usa a categoria `OptiRoute` em todas as suas regras.

> **⚠️ IMPORTANTE — payload de `addRule` exige UUID, não nome.** O campo `<categories>` em `Firewall/Filter.xml` é um `ModelRelationField` (validado por UUID na escrita). Mandar o nome (ex: `"OptiRoute"`) retorna `validations.rule.categories='Related category not found'` mesmo se a categoria existir. Por isso o OptiRoute resolve o UUID via `search_item` e o cacheia em memória antes de cada `addRule`.

- **`GET /api/firewall/category/search_item?add_empty=0`**
  - **Função:** Lista todas as categorias cadastradas no firewall.
  - **Retorno:** `{"rows":[{"uuid":"...","name":"..."}]}`.

- **`POST /api/firewall/category/add_item`**
  - **Função:** Cria uma categoria nova.
  - **Payload:** `{"category":{"name":"OptiRoute"}}`
  - **Retorno:** `{"result":"saved","uuid":"..."}`

### 1.3 Regras de Firewall (Plugin `os-firewall`)
- **`POST /api/firewall/filter/searchRule`**
  - **Função:** Consulta a lista de regras de automação ativas.
  - **Payload:** `{"searchPhrase": "OPTIROUTE"}`
  - **Retorno:** Lista de regras pertencentes à categoria `OptiRoute` com seus respectivos UUIDs, interfaces, descrições, gateways e ToS/DSCP.

- **`POST /api/firewall/filter/addRule`**
  - **Função:** Cria uma nova regra de firewall na interface LAN.
  - **Campos do Payload:**
    - `sequence`: Ordem de avaliação.
    - `description`: Descritor estruturado (`OPTIROUTE|DEFAULT|...` ou `OPTIROUTE|OVERRIDE|...`).
    - `categories`: **UUID da categoria** (não o nome — ver §1.2).
    - `interface`: `"lan"`.
    - `direction`: `"in"`.
    - `action`: `"pass"`.
    - `quick`: `"1"`.
    - `destination_net`: `"(self)"`.
    - `destination_not`: `"1"`.
    - `tos`: Valor em hexadecimal (ex: `"0x84"` para DSCP 33).
    - `gateway`: Nome da WAN destino (ex: `"WAN2"`).
    - `source_net`: `"lan"` (Default) ou endereço IP estático (Override).

- **`POST /api/firewall/filter/setRule/{uuid}`**
  - **Função:** Atualiza as propriedades de uma regra existente (ex: alteração de gateway ou de sequência).

- **`POST /api/firewall/filter/delRule/{uuid}`**
  - **Função:** Exclui uma regra pelo seu UUID.

- **`POST /api/firewall/filter/apply`**
  - **Função:** Aplica e recarrega as regras do filtro de pacotes (`pf`) em memória no OPNsense.

---

## 2. Contratos Internos (.NET 10)

### 2.1 `IOptiRouteSynchronizer`
Orquestrador central de estado:
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
Manipulação de políticas de rede no Windows:
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
Gerenciamento de overrides de gateway por IP de máquina:
```csharp
public interface IHostOverrideManager
{
    Task SetOverrideAsync(string executableName, string hostIp, string gateway, int dscp, Guid appId, CancellationToken ct = default);
    Task RemoveOverrideAsync(string executableName, string hostIp, CancellationToken ct = default);
}
```

### 2.4 `IRuleOrderManager`
Controle de ordenação relativa no firewall:
```csharp
public interface IRuleOrderManager
{
    Task EnsureRelativeOrderAsync(string interfaceName = "lan", CancellationToken ct = default);
}
```
