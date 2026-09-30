using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace OptiRoute.App.Services;

/// <summary>
/// Detecta o IP local da máquina que está na mesma rede do gateway OPNsense.
/// </summary>
public static class LocalNetworkDetector
{
    /// <summary>
    /// Caminho do snapshot do último IP local conhecido:
    /// <c>%APPDATA%\OptiRoute\last-known-ip.txt</c>. Usado no startup para detectar
    /// uma eventual mudança de IP (ex.: renovação de lease DHCP) entre execuções.
    /// </summary>
    private static readonly string SnapshotPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OptiRoute",
        "last-known-ip.txt");

    /// <summary>
    /// Detecta o endereço IPv4 local conectando virtualmente via socket UDP em direção ao gateway.
    /// Não transmite pacotes na rede; apenas consulta a tabela de rotas do Windows.
    /// </summary>
    public static IPAddress DetectLocalIp(string opnsenseHost = "https://10.0.0.1")
    {
        try
        {
            var uri = new Uri(opnsenseHost);
            var targetHost = uri.Host;

            // Se for IP direto
            if (!IPAddress.TryParse(targetHost, out var targetIp))
            {
                var addresses = Dns.GetHostAddresses(targetHost);
                targetIp = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                    ?? IPAddress.Parse("10.0.0.1");
            }

            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(targetIp, 65530);

            if (socket.LocalEndPoint is IPEndPoint endPoint)
                return endPoint.Address;
        }
        catch
        {
            // Fallback: busca pelo primeiro adaptador ativo não-loopback
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus != OperationalStatus.Up ||
                    iface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var ipProps = iface.GetIPProperties();
                var ipv4 = ipProps.UnicastAddresses
                    .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork &&
                                         !IPAddress.IsLoopback(u.Address));

                if (ipv4 is not null)
                    return ipv4.Address;
            }
        }

        return IPAddress.Parse("127.0.0.1");
    }

    /// <summary>
    /// Versão assíncrona de <see cref="DetectLocalIp"/>: executa a detecção (bloqueante,
    /// porém sem tráfego real na rede) em uma thread do pool para não travar a UI.
    /// </summary>
    /// <param name="opnsenseHost">Host/URL do OPNsense usado como destino da rota.</param>
    /// <param name="cancellationToken">Token opcional para cancelar o agendamento.</param>
    public static Task<IPAddress> DetectAsync(
        string opnsenseHost,
        CancellationToken cancellationToken = default)
        => Task.Run(() => DetectLocalIp(opnsenseHost), cancellationToken);

    /// <summary>
    /// Persiste o IP local atual (uma única linha) em
    /// <c>%APPDATA%\OptiRoute\last-known-ip.txt</c>, sobrescrevendo o conteúdo anterior.
    /// Best-effort: falhas de I/O são registradas como warning e nunca propagadas —
    /// o snapshot é apenas um auxiliar de diagnóstico no startup.
    /// </summary>
    public static async Task SaveSnapshotAsync(string ip, ILogger logger)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath)!);
            await File.WriteAllTextAsync(SnapshotPath, ip).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao gravar o snapshot do IP local em {Path}.", SnapshotPath);
        }
    }

    /// <summary>
    /// Lê o último IP local persistido por <see cref="SaveSnapshotAsync"/>. Retorna
    /// <c>null</c> quando o arquivo não existe, está vazio ou não pôde ser lido (best-effort).
    /// </summary>
    public static string? ReadSnapshot()
    {
        try
        {
            if (!File.Exists(SnapshotPath))
                return null;

            var value = File.ReadAllText(SnapshotPath).Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }
}
