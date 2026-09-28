# Windows QoS — Como funciona no OptiRoute

## 1. Policy-based QoS no Windows

O Windows possui um subsistema nativo de Qualidade de Serviço (QoS) gerenciado via módulo PowerShell `NetQoS`. Ele permite interceptar o tráfego gerado por qualquer aplicação na camada do driver de rede e marcar os cabeçalhos IP com um valor de **DSCP (Differentiated Services Code Point)** antes do pacote ser transmitido pela placa de rede.

O OptiRoute utiliza exclusivamente a combinação:
- **`AppPathNameMatchCondition`:** Nome do executável do processo (ex: `bf6.exe`, `discord.exe`).
- **`DSCPAction`:** Valor numérico do DSCP atribuído ao processo (ex: `33`).

---

## 2. Cmdlets PowerShell Utilizados

### 2.1 Criar ou Atualizar Política
```powershell
New-NetQosPolicy `
    -Name "OptiRoute-bf6" `
    -AppPathNameMatchCondition "bf6.exe" `
    -DSCPAction 33 `
    -NetworkProfile All
```

| Parâmetro | Valor Exemplo | Descrição |
|---|---|---|
| `-Name` | `OptiRoute-bf6` | Identificador único com prefixo protegido `OptiRoute-` |
| `-AppPathNameMatchCondition` | `bf6.exe` | Nome do binário executável (sem o caminho completo) |
| `-DSCPAction` | `33` | Código DSCP (0–63) |
| `-NetworkProfile` | `All` | Aplica-se a todos os perfis de rede (Domain, Private, Public) |

### 2.2 Consultar e Inspecionar Políticas Locais
O OptiRoute extrai tanto o DSCP quanto os metadados de propriedade da política (`Owner` e `PolicyStore`):
```powershell
Get-NetQosPolicy -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like 'OptiRoute-*' } |
    Select-Object Name, AppPathNameMatchCondition, AppPathName, DSCPAction, DSCPValue, Owner, PolicyStore |
    ConvertTo-Json -Compress
```

### 2.3 Remover Política com Segurança
```powershell
Remove-NetQosPolicy -Name "OptiRoute-bf6" -Confirm:$false -ErrorAction SilentlyContinue
```
Se a política foi criada em um repositório específico (ex: ActiveStore ou GPO de máquina), o OptiRoute respeita o parâmetro `-PolicyStore` correspondente.

---

## 3. Segurança e Políticas Órfãs (`LocalOnly`)

O OptiRoute segue três princípios rígidos de segurança para não interferir nas configurações do usuário:

1. **Filtro Estrito por Prefixo:** O OptiRoute apenas inspeciona, altera ou remove políticas que comecem estritamente com o prefixo `OptiRoute-`. Políticas de sistema (como QoS para áudio do Teams ou políticas de domínio) são completamente ignoradas.
2. **Sem Exclusão Silenciosa de Políticas Órfãs:** Se uma política `OptiRoute-*` existir no Windows mas não estiver cadastrada no OPNsense (por exemplo, após testes manuais ou se outro usuário apagou a regra global no firewall), o OptiRoute **nunca a remove automaticamente**. Em vez disso, ele classifica o aplicativo como `⚠ Somente neste Windows` (`LocalOnly`) e oferece opções explícitas ao usuário:
   - `Remover do Windows`: Exclui a política local via PowerShell.
   - `+ Registrar no OPNsense`: Promove a aplicação para o firewall com detecção de colisão de DSCP.
3. **Elevação UAC Automática:** A criação e remoção de políticas de rede no Windows exige privilégios de Administrador. O executável `OptiRoute.App.exe` contém um manifesto de aplicação (`app.manifest`) configurado com `requireAdministrator`, solicitando a elevação do usuário imediatamente ao abrir o aplicativo.

---

## 4. Persistência e Execução Técnica

- **Persistência no Registro:** As políticas criadas pelo OptiRoute sem `-PolicyStore ActiveStore` são salvas de forma permanente no registro do Windows em:
  ```text
  HKLM\SOFTWARE\Policies\Microsoft\Windows\QoS
  ```
  Isso significa que elas permanecem ativas mesmo se o computador for reiniciado ou se o aplicativo OptiRoute for fechado.
- **Execução Confiável sem Escapamento:** O `WindowsQosManager` escreve os comandos PowerShell em um script `.ps1` temporário criptograficamente aleatório e o executa via `powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "<temp>.ps1"`, evitando falhas de escape de aspas comuns na passagem direta de parâmetros via linha de comando.
