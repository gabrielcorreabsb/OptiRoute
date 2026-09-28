using System.Windows;
using System.Windows.Controls;

namespace OptiRoute.App.Themes.Patterns;

/// <summary>
/// Input field with inline error caption. Hosts any WPF input control (TextBox, PasswordBox,
/// ComboBox) and shows a red TextBlock below when <see cref="ErrorText"/> is non-empty.
/// </summary>
public partial class ValidationField : UserControl
{
    public static readonly DependencyProperty ErrorTextProperty =
        DependencyProperty.Register(
            nameof(ErrorText),
            typeof(string),
            typeof(ValidationField),
            new PropertyMetadata(string.Empty, OnErrorTextChanged));

    /// <summary>Error caption shown below the input. Empty = hidden.</summary>
    public string ErrorText
    {
        get => (string)GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    /// <summary>Consumer places their input control here (TextBox, PasswordBox, etc.).</summary>
    public ContentControl InputHost => InputSlot;

    public ValidationField()
    {
        InitializeComponent();
    }

    private static void OnErrorTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ValidationField vf)
        {
            vf.ErrorBlock.Text = (string)(e.NewValue ?? string.Empty);
            vf.ErrorBlock.Visibility = string.IsNullOrEmpty(vf.ErrorText)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }
}