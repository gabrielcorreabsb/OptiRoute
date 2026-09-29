using System.Globalization;

namespace OptiRoute.App.Services;

/// <summary>
/// Helper central para troca de idioma em runtime. <c>Strings.Culture</c> é uma
/// get-only auto-derivada de <see cref="System.Threading.Thread.CurrentUICulture"/>
/// (ver Strings.Designer.cs linha 49-51: <c>public static CultureInfo Culture { get => CultureInfo.CurrentUICulture; }</c>),
/// portanto setar <c>Thread.CurrentThread.CurrentUICulture</c> é suficiente — toda chamada
/// subsequente de <c>Strings.ResourceManager.GetString(name)</c> usa a nova cultura.
/// Após a troca, dispara <see cref="LocalizedStrings.OnCultureChanged"/> para que WPF rebind
/// todos os indexer bindings do tipo <c>{loc:LString Key=X}</c>.
/// </summary>
public static class LocalizationManager
{
    /// <summary>
    /// Troca a cultura atual em runtime. Idempotente: se a cultura já é a
    /// solicitada, não dispara OnCultureChanged.
    /// </summary>
    public static void SetCulture(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return;

        var newCulture = CultureInfo.GetCultureInfo(code);

        // Idempotência
        if (Thread.CurrentThread.CurrentUICulture.Name == newCulture.Name)
            return;

        Thread.CurrentThread.CurrentUICulture = newCulture;
        LocalizedStrings.Instance.OnCultureChanged();
    }
}