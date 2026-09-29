using System.ComponentModel;
using OptiRoute.App.Properties;

namespace OptiRoute.App.Services;

/// <summary>
/// Singleton que expõe as strings localizadas como indexer, permitindo binding
/// reativo em XAML via <c>{loc:LString Key=Section.Subsection.Key}</c>.
/// WPF cacheia `{x:Static p:Strings.X}` no parse do XAML — mudar cultura não re-avalia.
/// O indexer binding + <see cref="OnCultureChanged"/> (que dispara
/// <c>PropertyChanged("Item[]")</c>) garante refresh automático de TODOS os
/// bindings de string quando <see cref="LocalizationManager.SetCulture"/> roda.
/// </summary>
public sealed class LocalizedStrings : INotifyPropertyChanged
{
    public static LocalizedStrings Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Indexer — recebe a chave (formato Designer-style com underscores: "Section_Subsection_Key")
    /// e retorna o valor localizado. Normaliza underscores → dots para o lookup no resx.
    /// Falha silenciosa: se a chave não existir, retorna <c>"[chave]"</c> para debug visual.
    /// </summary>
    public string this[string key] =>
        Strings.ResourceManager.GetString(key.Replace('_', '.'), Strings.Culture)
        ?? $"[{key}]";

    /// <summary>
    /// Notifica WPF que todos os indexers mudaram. Disparado por
    /// <see cref="LocalizationManager.SetCulture"/> quando o usuário troca idioma.
    /// </summary>
    public void OnCultureChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}