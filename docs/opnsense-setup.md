# Setup e Configuração do OPNsense para o OptiRoute

## 1. Pré-requisitos no OPNsense

1. **Plugin `os-firewall` instalado:**
   - Acesse: **System → Firmware → Plugins**
   - Busque por `os-firewall` e clique em **+** (Install).
   - Este plugin habilita o menu **Firewall → Automation → Filter** e a API REST `/api/firewall/filter/*`.

2. **Geração de Chaves de API:**
   - Acesse: **System → Access → Users**
   - Selecione o usuário desejado (ex: `admin` ou um usuário com permissões completas de firewall).
   - Role até a seção **API keys** e clique em **+**.
   - O navegador fará o download de um arquivo de texto (ex: `apikey.txt`) contendo:
     ```text
     key=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
     secret=yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy
     ```
   - Guarde este arquivo para importação no OptiRoute.

---

## 2. Estrutura das Regras no OPNsense

Todas as regras geradas pelo OptiRoute são criadas dentro da categoria **`OptiRoute`** na interface LAN.

### 2.1 Padrão de Nomenclatura e Descrição Estruturada
As regras utilizam o campo `Description` para persistir os metadados de identificação entre múltiplos PCs:

| Tipo | Formato da Descrição | Origem (Source) | Destino (Destination) | Gateway |
|---|---|---|---|---|
| **DEFAULT** | `OPTIROUTE|DEFAULT|<exe>|<dscp>|<appId>` | `lan net` (ou `any`) | `! (self)` | WAN padrão da rede |
| **OVERRIDE** | `OPTIROUTE|OVERRIDE|<exe>|<dscp>|<sourceIp>|<appId>` | `<IP do PC>` | `! (self)` | WAN customizada |

### 2.2 Proteção Contra Sequestro de DNS e Serviços Locais (`! This firewall`)
Um requisito indispensável configurado automaticamente pelo OptiRoute é:
- `destination_net`: `"(self)"`
- `destination_not`: `"1"`

**Por que isso é crítico?**
Se o destino fosse `any`, requisições DNS feitas pelo jogo para o resolvedor local do OPNsense (Unbound DNS na porta 53 do IP `10.0.0.1`) teriam sua rota forçada para a interface WAN externa, impedindo a resolução de nomes na máquina local. A negação `! (self)` garante que todo tráfego destinado aos serviços internos do firewall permaneça intacto.

### 2.3 Mapeamento ToS e DSCP
O OPNsense manipula o campo DSCP através do byte ToS (Type of Service) em notação hexadecimal.
A relação matemática é:
$$\text{ToS} = \text{DSCP} \ll 2 = \text{DSCP} \times 4$$

Exemplos:
- DSCP `33` $\rightarrow$ ToS `0x84` ($33 \times 4 = 132$)
- DSCP `34` $\rightarrow$ ToS `0x88` ($34 \times 4 = 136$)
- DSCP `35` $\rightarrow$ ToS `0x8C` ($35 \times 4 = 140$)

O cliente `OpnsenseClient` do OptiRoute formata e envia os campos `dscp` e `tos` automaticamente.

---

## 3. Ordenação das Regras na Interface LAN

O OPNsense avalia regras de cima para baixo (*first match wins*). Para garantir que as exceções locais (Overrides) funcionem e que as regras gerais do usuário continuem ativas, o `RuleOrderManager` organiza a lista na seguinte sequência:

```text
1. Regras de sistema e regras customizadas no topo da LAN
2. Regras OptiRoute OVERRIDE (específicas por IP de cada PC)
3. Regras OptiRoute DEFAULT (gerais para toda a rede LAN)
4. Regra Âncora do usuário (ex: LAN LOADBALANCE / Saída Geral)
5. Demais regras e bloqueios padrão
```

O OptiRoute **nunca** move ou deleta regras que não pertençam à categoria `OptiRoute`.

---

## 4. Como Diagnosticar e Validar o Tráfego

### 4.1 Validação via Packet Capture no OPNsense
Para confirmar que os pacotes do jogo estão chegando marcados com DSCP e saindo pela WAN correta:

1. Acesse: **Interfaces → Diagnostics → Packet Capture**
2. Selecione a interface **LAN**.
3. Ative o modo promíscuo.
4. Clique em **Start**, gere tráfego na aplicação (ex: abra o jogo ou envie um `curl` de teste).
5. Clique em **Stop** e baixe o arquivo `.pcap`.
6. Abra no **Wireshark** e utilize o filtro:
   ```text
   ip.dsfield.dscp == 33
   ```
   Você verá o campo *Differentiated Services Field* com o valor exato `DSCP 33 (0x21)` e ToS `0x84`.

7. Em seguida, capture na interface WAN de saída esperada (ex: `WAN2`) para confirmar que o tráfego daquele IP/porta está fluindo por aquele link.

### 4.2 Validação via SSH (`tcpdump`)
Conecte-se ao OPNsense via terminal SSH:
```bash
# Capturar pacotes com DSCP 33 na interface LAN (ex: igb1)
tcpdump -ni igb1 "ip and (ip[1] & 0xfc) >> 2 == 33" -vv
```
