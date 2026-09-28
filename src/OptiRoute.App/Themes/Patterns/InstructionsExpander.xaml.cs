using System.Windows;
using System.Windows.Controls;

namespace OptiRoute.App.Themes.Patterns;

/// <summary>
/// Collapsible "How to obtain this data" panel. Set <see cref="HeaderText"/> for the title and
/// <see cref="Slot"/> for any UIElement (TextBlock, StackPanel, etc.) as the expandable content.
/// Slot accepts any UIElement via property element syntax in XAML.
/// </summary>
public partial class InstructionsExpander : UserControl
{
    public static readonly DependencyProperty HeaderTextProperty =
        DependencyProperty.Register(
            nameof(HeaderText),
            typeof(string),
            typeof(InstructionsExpander),
            new PropertyMetadata("How to obtain this data", OnHeaderTextChanged));

    /// <summary>Title shown in the Expander header (collapsed state).</summary>
    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public static readonly DependencyProperty SlotProperty =
        DependencyProperty.Register(
            nameof(Slot),
            typeof(object),
            typeof(InstructionsExpander),
            new PropertyMetadata(null, OnSlotChanged));

    /// <summary>
    /// Content shown when expanded. Accepts any UIElement (TextBlock, StackPanel, etc.).
    /// Use property element syntax in XAML: <c>&lt;patterns:InstructionsExpander.Slot&gt;...&lt;/patterns:InstructionsExpander.Slot&gt;</c>
    /// </summary>
    public object Slot
    {
        get => GetValue(SlotProperty);
        set => SetValue(SlotProperty, value);
    }

    public InstructionsExpander()
    {
        InitializeComponent();
    }

    private static void OnHeaderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InstructionsExpander ie)
            ie.RootExpander.Header = e.NewValue;
    }

    private static void OnSlotChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InstructionsExpander ie)
            ie.ContentHost.Content = e.NewValue;
    }
}