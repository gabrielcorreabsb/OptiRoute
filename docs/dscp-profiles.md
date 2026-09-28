# Registro DSCP e Mapeamento de Identidade no OptiRoute

## 1. Princípio Fundamental: DSCP como Identidade do Aplicativo

No OptiRoute, o campo **DSCP (Differentiated Services Code Point)** é utilizado como **identificador unívoco e global do processo executável** em toda a rede local, e **não** como representação fixa de um gateway de saída.

```text
bf6.exe     ↔ DSCP 33 (global)
discord.exe ↔ DSCP 34 (global)
steam.exe   ↔ DSCP 35 (global)
```

### Por que esse modelo é superior?
1. **Multi-PC sem Colisão:** Se dois computadores na casa executarem `bf6.exe`, ambos marcarão seus pacotes com DSCP 33. O OPNsense pode aplicar a rota padrão global para ambos, ou aplicar um override individual para um deles baseado no IP de origem (`Source IP`).
2. **Independência de Rota:** Mudar a rota do jogo de `WAN1` para `WAN2` no OPNsense **não exige** recriar a política QoS no Windows ou alterar o DSCP da aplicação. A identidade do processo permanece estável.
3. **Escalabilidade:** O firewall passa a ter regras expressivas onde o DSCP identifica a aplicação e o `Source IP` diferencia os clientes da rede.

---

## 2. Pool de DSCPs Gerenciados (`ManagedPool`)

O DSCP é um campo de 6 bits (valores de `0` a `63`). O OptiRoute aloca dinamicamente os valores através da classe `DscpRegistry`, que gerencia um pool seguro e previne colisões com o tráfego padrão de rede.

### 2.1 Valores Reservados (Excluídos do Pool)
- **`DSCP 0` (Best Effort / CS0):** Tráfego comum de internet (sem política).
- **`DSCP 46` (Expedited Forwarding - EF):** Reservado para tráfego sensível à latência como VoIP/Telefonia.
- **Classes Padrão IETF (CS e AF):**
  - CS1 a CS7 (`8, 16, 24, 32, 40, 48, 56`)
  - AF1x a AF4x (`10, 12, 14, 18, 20, 22, 26, 28, 30, 34, 36, 38`)

### 2.2 Pool Padrão do OptiRoute
O OptiRoute prioriza valores não conflitantes:
$$\text{Pool Sugerido} = [33, 35, 37, 39, 41, 42, 43, 44, 45, 47, 49, 50, 51, \dots, 62]$$

---

## 3. Relação Matemática entre DSCP e ToS (Type of Service)

No cabeçalho IPv4, os 6 bits de maior ordem do byte ToS contêm o DSCP, enquanto os 2 bits inferiores são reservados para ECN (Explicit Congestion Notification):

$$\text{ToS Byte} = \text{DSCP} \ll 2 = \text{DSCP} \times 4$$

### Tabela de Referência Rápida

| Executável (Exemplo) | DSCP (Decimal) | DSCP (Binário) | ToS Byte (Hex) | Filtro Wireshark / Pcap |
|---|---|---|---|---|
| *Tráfego Padrão* | 0 | `000000` | `0x00` | `ip.dsfield.dscp == 0` |
| `bf6.exe` | 33 | `100001` | `0x84` | `ip.dsfield.dscp == 33` |
| `discord.exe` | 34 | `100010` | `0x88` | `ip.dsfield.dscp == 34` |
| `steam.exe` | 35 | `100011` | `0x8C` | `ip.dsfield.dscp == 35` |
| `valorant.exe` | 37 | `100101` | `0x94` | `ip.dsfield.dscp == 37` |

---

## 4. Resolução de Conflitos (`Conflict Resolution`)

Se um computador local tiver uma política legada no Windows configurando `bf6.exe` com DSCP 33, mas outro usuário na rede cadastrou `bf6.exe` no OPNsense como DSCP 35:

1. O `OptiRouteSynchronizer` detecta o estado `ApplicationSyncState.Conflict`.
2. A interface gráfica destaca o card com a cor vermelha e o aviso **`⚠ Divergência de DSCP detectada`**.
3. O usuário pode clicar em **`Corrigir para DSCP Global`**, que automaticamente reconfigura a política local do Windows para o DSCP oficial registrado no firewall (`35`), restabelecendo a harmonia em toda a rede.
