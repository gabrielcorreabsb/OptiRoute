using System.Windows.Data;
using System.Windows.Markup;
using OptiRoute.App.Services;

namespace OptiRoute.App.Markup;

/// <summary>
/// MarkupExtension que cria um Binding ao indexer de <see cref="LocalizedStrings"/>.
/// Permite <c>{loc:LString Key=MainWindow.Title}</c> em XAML — equivalente vivo de
/// <c>{x:Static p:Strings.MainWindow_Title}</c> que NÃO re-avalia ao mudar cultura.
///
/// O binding path <c>[key]</c> referencia o indexer string do singleton; quando
/// <see cref="LocalizedStrings.OnCultureChanged"/> dispara, todos os bindings
/// indexer refrescam automaticamente.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public class LStringExtension : MarkupExtension
{
    /// <summary>Chave do resx (formato "Section.Subsection.Key" com pontos).</summary>
    public string Key { get; set; } = string.Empty;

    public LStringExtension() { }

    public LStringExtension(string key) { Key = key; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            return $"[{Key ?? "?"}]";

        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizedStrings.Instance,
            Mode = BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}