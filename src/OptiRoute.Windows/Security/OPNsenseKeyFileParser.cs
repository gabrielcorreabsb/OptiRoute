using OptiRoute.Windows.Security;

namespace OptiRoute.Windows.Security;

/// <summary>
/// Faz o parse do arquivo de credenciais gerado pelo OPNsense.
/// <para>
/// O OPNsense gera um arquivo .txt ao criar API Keys com o formato:
/// <code>
/// key=xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
/// secret=yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy
/// </code>
/// </para>
/// </summary>
public static class OPNsenseKeyFileParser
{
    /// <summary>
    /// Lê e interpreta um arquivo de API Key gerado pelo OPNsense.
    /// </summary>
    /// <param name="filePath">Caminho completo para o arquivo .txt gerado pelo OPNsense.</param>
    /// <returns>Credenciais extraídas do arquivo.</returns>
    /// <exception cref="InvalidOperationException">Se o arquivo não contiver key e secret.</exception>
    public static OpnsenseCredentials Parse(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Key file not found: {filePath}");

        var lines = File.ReadAllLines(filePath);
        return ParseLines(lines, filePath);
    }

    /// <summary>
    /// Lê e interpreta o conteúdo de um arquivo de API Key do OPNsense.
    /// </summary>
    public static OpnsenseCredentials ParseContent(string content)
    {
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return ParseLines(lines, source: "content");
    }

    private static OpnsenseCredentials ParseLines(string[] lines, string source)
    {
        string? key    = null;
        string? secret = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            // Formato padrão OPNsense: "key=xxx" ou "key = xxx" ou "key: xxx"
            var separatorIndex = line.IndexOfAny(['=', ':']);
            if (separatorIndex > 0)
            {
                var prefix = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();

                if (prefix.Equals("key", StringComparison.OrdinalIgnoreCase))
                    key = value;
                else if (prefix.Equals("secret", StringComparison.OrdinalIgnoreCase))
                    secret = value;
            }
        }

        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                $"Could not find 'key=' in {source}. Expected format:\n  key=xxx\n  secret=yyy");

        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException(
                $"Could not find 'secret=' in {source}. Expected format:\n  key=xxx\n  secret=yyy");

        return new OpnsenseCredentials(key, secret);
    }
}
