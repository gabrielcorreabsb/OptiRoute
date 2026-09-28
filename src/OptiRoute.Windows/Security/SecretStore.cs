using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OptiRoute.Windows.Security;

/// <summary>
/// Credenciais completas da API do OPNsense (Key + Secret).
/// Ambas são armazenadas criptografadas via DPAPI — nunca em texto simples.
/// </summary>
public sealed record OpnsenseCredentials(string ApiKey, string ApiSecret);

/// <summary>
/// Armazena e recupera credenciais usando Windows DPAPI (Data Protection API).
/// Os dados são criptografados para o contexto do usuário atual.
/// <para>
/// O API Secret NUNCA é armazenado em texto simples.
/// O arquivo fica em %APPDATA%\OptiRoute\credentials.bin.
/// </para>
/// </summary>
public static class SecretStore
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OptiRoute",
        "credentials.bin");

    // ──────────────────────────────────────────────────────────────────────────
    // Operações com credenciais completas (recomendado — importa arquivo OPNsense)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Salva as credenciais (Key + Secret) criptografadas via DPAPI.
    /// </summary>
    public static void SaveCredentials(OpnsenseCredentials credentials)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);

        var json      = JsonSerializer.Serialize(credentials);
        var data      = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(StorePath, encrypted);
    }

    /// <summary>
    /// Recupera as credenciais descriptografadas.
    /// Retorna <c>null</c> se nenhuma credencial foi armazenada ainda.
    /// </summary>
    public static OpnsenseCredentials? LoadCredentials()
    {
        if (!File.Exists(StorePath))
            return null;

        var encrypted = File.ReadAllBytes(StorePath);
        var data      = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        var json      = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<OpnsenseCredentials>(json);
    }

    /// <summary>Indica se as credenciais foram armazenadas.</summary>
    public static bool HasCredentials() => File.Exists(StorePath);

    /// <summary>Remove as credenciais armazenadas.</summary>
    public static void ClearCredentials()
    {
        if (File.Exists(StorePath))
            File.Delete(StorePath);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Compatibilidade — acesso direto ao secret (para quem só tem o secret)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Salva apenas o API Secret (mantém a Key se já existir).
    /// Prefira <see cref="SaveCredentials"/> quando tiver ambos.
    /// </summary>
    public static void SaveApiSecret(string secret)
    {
        var existing = LoadCredentials();
        SaveCredentials(new OpnsenseCredentials(
            ApiKey:    existing?.ApiKey ?? string.Empty,
            ApiSecret: secret));
    }

    /// <summary>Recupera apenas o API Secret.</summary>
    public static string? LoadApiSecret() => LoadCredentials()?.ApiSecret;

    /// <summary>Indica se algum secret está armazenado (compatibilidade).</summary>
    public static bool HasApiSecret() => HasCredentials();
}
