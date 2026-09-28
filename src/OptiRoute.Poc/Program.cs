using Microsoft.Extensions.Logging;
using OptiRoute.OPNsense.Client;
using OptiRoute.Windows.QoS;
using OptiRoute.Windows.Security;
using Spectre.Console;

// ─────────────────────────────────────────────────────────────────────────────
//  OptiRoute.Poc — Proof of Concept via linha de comando
//
//  Uso:
//    optiroute-poc qos add <exe> <dscp>    → Cria política QoS
//    optiroute-poc qos remove <exe>        → Remove política QoS
//    optiroute-poc qos list                → Lista políticas OptiRoute
//    optiroute-poc opn test                → Testa conexão com OPNsense
//    optiroute-poc opn gateways            → Lista gateways
//    optiroute-poc opn rule add            → Cria regra DSCP no OPNsense
//    optiroute-poc secret set              → Salva API Secret (DPAPI)
//
//  Requer: Administrador (para cmdlets QoS)
// ─────────────────────────────────────────────────────────────────────────────

if (args.Length < 2)
{
    PrintHelp();
    return 1;
}

using var loggerFactory = LoggerFactory.Create(b => b
    .AddConsole()
    .SetMinimumLevel(LogLevel.Debug));

var command = args[0].ToLowerInvariant();
var sub     = args[1].ToLowerInvariant();

switch (command)
{
    case "qos":
        return await HandleQosAsync(sub, args[2..], loggerFactory);

    case "opn":
        return await HandleOpnAsync(sub, args[2..], loggerFactory);

    case "secret":
        return HandleSecret(sub, args[2..]);

    default:
        AnsiConsole.MarkupLine($"[red]Unknown command:[/] {command}");
        PrintHelp();
        return 1;
}

// ─────────────────────────────────────────────────────────────────────────────
// QoS handlers
// ─────────────────────────────────────────────────────────────────────────────

static async Task<int> HandleQosAsync(string sub, string[] args, ILoggerFactory loggerFactory)
{
    var manager = new WindowsQosManager(loggerFactory.CreateLogger<WindowsQosManager>());

    switch (sub)
    {
        case "add":
        {
            if (args.Length < 2)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] qos add <exe> <dscp>");
                return 1;
            }

            var exe  = args[0];
            var dscp = int.Parse(args[1]);

            AnsiConsole.MarkupLine($"[yellow]Creating QoS policy:[/] {exe} → DSCP {dscp}");
            await manager.CreatePolicyAsync(exe, dscp);
            AnsiConsole.MarkupLine($"[green]✓[/] Policy [bold]OptiRoute-{Path.GetFileNameWithoutExtension(exe)}[/] created (DSCP={dscp})");
            return 0;
        }

        case "remove":
        {
            if (args.Length < 1)
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] qos remove <exe>");
                return 1;
            }

            await manager.DeletePolicyAsync(args[0]);
            AnsiConsole.MarkupLine($"[green]✓[/] Policy removed (if existed)");
            return 0;
        }

        case "list":
        {
            var policies = await manager.ListOptiRoutePoliciesAsync();

            if (!policies.Any())
            {
                AnsiConsole.MarkupLine("[grey]No OptiRoute QoS policies found.[/]");
                return 0;
            }

            var table = new Table()
                .AddColumn("Policy Name")
                .AddColumn("Executable")
                .AddColumn("DSCP")
                .AddColumn("Profile");

            foreach (var p in policies)
                table.AddRow(p.Name, p.AppPathName, p.DscpAction.ToString(), p.NetworkProfile);

            AnsiConsole.Write(table);
            return 0;
        }

        default:
            AnsiConsole.MarkupLine($"[red]Unknown qos subcommand:[/] {sub}");
            return 1;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// OPNsense handlers
// ─────────────────────────────────────────────────────────────────────────────

static async Task<int> HandleOpnAsync(string sub, string[] args, ILoggerFactory loggerFactory)
{
    var settings = LoadSettings(out var secret);
    if (settings is null) return 1;

    using var http   = OpnsenseHttpClientFactory.Create(settings, secret!);
    var client = new OpnsenseClient(http, loggerFactory.CreateLogger<OpnsenseClient>());

    switch (sub)
    {
        case "test":
        {
            AnsiConsole.MarkupLine($"[yellow]Testing connection to:[/] {settings.Host}");
            var ok = await client.TestConnectionAsync();
            if (ok)
            {
                var version = await client.GetVersionAsync();
                AnsiConsole.MarkupLine($"[green]✓ Connected[/] — OPNsense {version}");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Connection failed[/]");
            }
            return ok ? 0 : 1;
        }

        case "gateways":
        {
            var gateways = await client.GetGatewaysAsync();

            var table = new Table()
                .AddColumn("Name")
                .AddColumn("Status")
                .AddColumn("Delay")
                .AddColumn("Loss")
                .AddColumn("Interface");

            foreach (var gw in gateways)
            {
                var statusMarkup = gw.Status switch
                {
                    OptiRoute.Core.Models.GatewayStatus.Online  => "[green]Online[/]",
                    OptiRoute.Core.Models.GatewayStatus.Offline => "[red]Offline[/]",
                    _                                            => "[grey]Unknown[/]"
                };
                table.AddRow(gw.Name, statusMarkup, gw.DelayMs ?? "-", gw.PacketLoss ?? "-", gw.Interface ?? "-");
            }

            AnsiConsole.Write(table);
            return 0;
        }

        case "rule":
        {
            if (args.Length < 1 || args[0] != "add")
            {
                AnsiConsole.MarkupLine("[red]Usage:[/] opn rule add");
                return 1;
            }

            var description = AnsiConsole.Ask<string>("Rule description (ex: OPTIRoute_WAN2):");
            var dscp        = AnsiConsole.Ask<int>("DSCP value:");
            var gateway     = AnsiConsole.Ask<string>("Gateway name (ex: WAN_FERNANDO):");
            var sourceIp    = AnsiConsole.Ask<string>($"PC IP address (default: {settings.PcIpAddress}):", settings.PcIpAddress);

            var request = new OptiRoute.Core.Models.OPNsenseRuleRequest
            {
                Description = description,
                DscpValue   = dscp,
                Gateway     = gateway,
                Interface   = settings.LanInterface,
                SourceIp    = sourceIp
            };

            AnsiConsole.MarkupLine("[yellow]Creating rule...[/]");
            var uuid = await client.EnsureRuleExistsAsync(request);
            AnsiConsole.MarkupLine($"[green]✓ Rule created/updated[/] uuid={uuid}");
            return 0;
        }

        case "rules":
        {
            var rules = await client.ListOptiRouteRulesAsync();

            if (!rules.Any())
            {
                AnsiConsole.MarkupLine("[grey]No OPTIRoute_ rules found.[/]");
                return 0;
            }

            var table = new Table()
                .AddColumn("UUID")
                .AddColumn("Description")
                .AddColumn("Enabled");

            foreach (var r in rules)
                table.AddRow(r.Uuid, r.Description, r.Enabled ? "[green]Yes[/]" : "[red]No[/]");

            AnsiConsole.Write(table);
            return 0;
        }

        default:
            AnsiConsole.MarkupLine($"[red]Unknown opn subcommand:[/] {sub}");
            return 1;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Secret handler
// ─────────────────────────────────────────────────────────────────────────────

static int HandleSecret(string sub, string[] args)
{
    switch (sub)
    {
        // Importar o arquivo .txt gerado pelo OPNsense (recomendado)
        // Uso: secret import "C:\Users\...\apikeys.txt"
        case "import":
        {
            string filePath;
            if (args.Length > 0)
                filePath = string.Join(" ", args).Trim('"');
            else
                filePath = AnsiConsole.Ask<string>("Path to OPNsense key file (.txt):").Trim('"');

            if (!File.Exists(filePath))
            {
                AnsiConsole.MarkupLine($"[red]File not found:[/] {filePath}");
                return 1;
            }

            try
            {
                var creds = OPNsenseKeyFileParser.Parse(filePath);
                SecretStore.SaveCredentials(creds);

                var keyPreview = creds.ApiKey.Length > 12
                    ? $"{creds.ApiKey[..8]}...{creds.ApiKey[^4..]}"
                    : creds.ApiKey;

                AnsiConsole.MarkupLine("[green]✓ Credentials imported and saved (encrypted via DPAPI)[/]");
                AnsiConsole.MarkupLine($"  API Key:    [dim]{keyPreview}[/]");
                AnsiConsole.MarkupLine($"  API Secret: [dim](protected)[/]");
                return 0;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to parse key file:[/] {ex.Message}");
                AnsiConsole.MarkupLine("[grey]Expected file format:\n  key=xxxx\n  secret=yyyy[/]");
                return 1;
            }
        }

        // Entrada manual de key + secret (alternativa ao import)
        case "set":
        {
            var key = AnsiConsole.Ask<string>("Enter OPNsense API Key:");
            var secret = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter OPNsense API Secret:")
                    .Secret());

            SecretStore.SaveCredentials(new OpnsenseCredentials(key, secret));
            AnsiConsole.MarkupLine("[green]✓ Credentials saved (encrypted via DPAPI)[/]");
            return 0;
        }

        case "clear":
        {
            SecretStore.ClearCredentials();
            AnsiConsole.MarkupLine("[yellow]Credentials cleared.[/]");
            return 0;
        }

        case "has":
        {
            var has = SecretStore.HasCredentials();
            if (has)
            {
                var creds = SecretStore.LoadCredentials()!;
                var keyPreview = creds.ApiKey.Length > 8 ? $"{creds.ApiKey[..8]}..." : creds.ApiKey;
                AnsiConsole.MarkupLine($"[green]✓ Credentials stored[/] — Key: [dim]{keyPreview}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]No credentials stored.[/]");
            }
            return 0;
        }

        default:
            AnsiConsole.MarkupLine($"[red]Unknown secret subcommand:[/] {sub}");
            return 1;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

static OpnsenseSettings? LoadSettings(out string? secret)
{
    secret = null;

    // Carregar credenciais salvas (importadas do arquivo OPNsense ou via 'secret set')
    var stored = SecretStore.LoadCredentials();

    if (stored is null)
    {
        AnsiConsole.MarkupLine("[yellow]No credentials stored.[/]");
        AnsiConsole.MarkupLine("Run: [bold]dotnet run -- secret import \"C:\\path\\to\\apikeys.txt\"[/]");
        return null;
    }

    secret = stored.ApiSecret;

    // Host: variável de ambiente tem prioridade, senão pede interativamente
    var host = Environment.GetEnvironmentVariable("OPTIROUTE_HOST")
        ?? AnsiConsole.Ask<string>("OPNsense host (ex: https://10.0.0.1):");

    return new OpnsenseSettings
    {
        Host         = host,
        ApiKey       = stored.ApiKey,   // vem das credenciais salvas — não precisa digitar
        VerifyTls    = false,
        LanInterface = Environment.GetEnvironmentVariable("OPTIROUTE_LAN") ?? "lan",
        PcIpAddress  = Environment.GetEnvironmentVariable("OPTIROUTE_PCIP") ?? "any"
    };
}

static void PrintHelp()
{
    var panel = new Panel("""
        [bold]OptiRoute PoC[/] — Proof of Concept

        [yellow]Credenciais (fazer primeiro):[/]
          secret import <arquivo>   Importa key+secret do arquivo gerado pelo OPNsense
          secret set                Digitar key+secret manualmente
          secret has                Verificar se credenciais estão salvas
          secret clear              Remover credenciais

        [yellow]QoS (requer Administrador):[/]
          qos add <exe> <dscp>      Cria política (ex: qos add bf6.exe 33)
          qos remove <exe>          Remove política
          qos list                  Lista políticas OptiRoute

        [yellow]OPNsense:[/]
          opn test                  Testa conexão
          opn gateways              Lista gateways e status
          opn rule add              Cria regra DSCP → Gateway
          opn rules                 Lista regras OPTIRoute_*

        [yellow]Variáveis de ambiente (opcionais):[/]
          OPTIROUTE_HOST            URL do OPNsense (ex: https://10.0.0.1)
          OPTIROUTE_LAN             Interface LAN (default: lan)
          OPTIROUTE_PCIP            IP do PC (default: any)
        """)
        .Header("Usage")
        .BorderColor(Color.Blue);

    AnsiConsole.Write(panel);
}
