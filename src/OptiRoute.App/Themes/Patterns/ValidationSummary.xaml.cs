using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace OptiRoute.App.Themes.Patterns;

/// <summary>
/// Summary box shown at the top of a settings page when <see cref="HasErrors"/> is true.
/// Lists all error messages from <see cref="ValidationErrors"/> as bullet items.
/// </summary>
public partial class ValidationSummary : UserControl
{
    public static readonly DependencyProperty HasErrorsProperty =
        DependencyProperty.Register(
            nameof(HasErrors),
            typeof(bool),
            typeof(ValidationSummary),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ValidationErrorsProperty =
        DependencyProperty.Register(
            nameof(ValidationErrors),
            typeof(IEnumerable),
            typeof(ValidationSummary),
            new PropertyMetadata(null));

    public bool HasErrors
    {
        get => (bool)GetValue(HasErrorsProperty);
        set => SetValue(HasErrorsProperty, value);
    }

    public IEnumerable ValidationErrors
    {
        get => (IEnumerable)GetValue(ValidationErrorsProperty);
        set => SetValue(ValidationErrorsProperty, value);
    }

    public ValidationSummary()
    {
        InitializeComponent();
    }
}