namespace OptiRoute.App.ViewModels;

/// <summary>
/// Item de seleção no ComboBox de idioma da header.
/// Exibe DisplayName no ComboBox, usa Code como valor persistido no config.json.
/// </summary>
public sealed record CultureOption(string Code, string DisplayName);
