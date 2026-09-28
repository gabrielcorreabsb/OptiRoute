# OptiRoute

> Roteie aplicativos Windows por WANs específicas do OPNsense usando marcação DSCP.

## O que é

OptiRoute é um orquestrador de políticas de roteamento por aplicativo. Ele permite que você defina qual WAN do seu OPNsense cada aplicativo Windows utilizará — sem depender de listas de IPs remotos, aliases dinâmicos ou CDNs.

```
bf6.exe      → WAN Fernando NET  (DSCP 33)
discord.exe  → WAN Totus         (DSCP 32)
steam.exe    → Load Balance      (DSCP 34)
chrome.exe   → comportamento padrão
```

## Princípio técnico

```
Aplicativo Windows
      ↓
Policy-based QoS (Windows)
      ↓
Marca pacote: DSCP = 33
      ↓
OPNsense LAN
      ↓
Firewall Rule: DSCP 33 → Gateway WAN_FERNANDO
      ↓
Internet via Fernando NET
```

O Windows conhece qual executável origina cada pacote. O OptiRoute usa esse conhecimento para marcar o tráfego com um valor DSCP, e o OPNsense redireciona o pacote pelo gateway correto com base nessa marca.

## Ambiente suportado

| Componente | Versão |
|---|---|
| Windows | 11 |
| .NET | 10.0+ |
| OPNsense | 24.x / 25.x / 26.x |
| Plugin OPNsense | `os-firewall` (obrigatório) |

## Estrutura do repositório

```
OptiRoute/
├── src/
│   ├── OptiRoute.Poc/          # PoC: valida DSCP via linha de comando
│   ├── OptiRoute.Core/         # Entidades, interfaces e modelos
│   ├── OptiRoute.Windows/      # WindowsQosManager (PowerShell)
│   └── OptiRoute.OPNsense/     # OpnsenseClient (API REST)
├── tests/
│   ├── OptiRoute.Core.Tests/
│   └── OptiRoute.Windows.Tests/
└── docs/
    ├── architecture.md         # Arquitetura detalhada
    ├── dscp-profiles.md        # Como configurar perfis DSCP
    ├── opnsense-setup.md       # Setup do OPNsense passo a passo
    ├── windows-qos.md          # Como o QoS do Windows funciona
    └── api-reference.md        # Endpoints da API local
```

## Início rápido — PoC

```powershell
cd src\OptiRoute.Poc

# 1. Importar o arquivo .txt gerado pelo OPNsense (key + secret criptografados via DPAPI)
dotnet run -- secret import "C:\Users\...\apikey.txt"

# 2. Testar conectividade com o OPNsense
dotnet run -- opn test

# 3. Listar gateways detectados em tempo real
dotnet run -- opn gateways

# 4. Criar política DSCP para um aplicativo (Requer terminal como Administrador)
dotnet run -- qos add notepad.exe 33

# 5. Listar políticas criadas
dotnet run -- qos list

# 6. Remover política
dotnet run -- qos remove notepad.exe
```

Depois verifique no OPNsense via **Interfaces → Diagnostics → Packet Capture** que os pacotes chegam com DSCP=33.

## Fases de desenvolvimento

| Fase | Descrição | Status |
|---|---|---|
| 0 | OptiRoute.Poc — PoC de linha de comando | ✅ Implementado |
| 1 | Core + Windows + OPNsense (sem UI) | ✅ Implementado |
| 2 | API REST + SQLite + Windows Service | 🔄 Planejado |
| 3 | Interface Blazor + Wizard | 🔄 Planejado |
| 4 | Sync automático + Repair | 🔄 Planejado |
| 5 | Monitoramento de gateways | 🔄 Planejado |

## Segurança

- **API Secret nunca em texto simples** — armazenado via Windows DPAPI
- **API local bind apenas em `127.0.0.1`** — não exposto externamente
- **Regras identificáveis** — prefixo `OPTIRoute_` em todas as regras do OPNsense
- **OptiRoute nunca**: desativa o firewall, altera NAT global, limpa estados automaticamente ou modifica regras de terceiros

## Documentação

- [Arquitetura](architecture.md)
- [Perfis DSCP](dscp-profiles.md)
- [Setup do OPNsense](opnsense-setup.md)
- [Windows QoS](windows-qos.md)
- [API Reference](api-reference.md)
