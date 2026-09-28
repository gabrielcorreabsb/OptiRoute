using System.Net;

namespace OptiRoute.Core.Models;

/// <summary>
/// Representa um computador/dispositivo na rede que utiliza o OptiRoute.
/// </summary>
public sealed class OptiRouteHost
{
    public string Name { get; set; } = string.Empty;
    public IPAddress Ip { get; set; } = null!;
}
