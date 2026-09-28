using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OptiRoute.App.Services;

/// <summary>
/// Detecta o IP local da máquina que está na mesma rede do gateway OPNsense.
/// </summary>
public static class LocalNetworkDetector
{
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
}
