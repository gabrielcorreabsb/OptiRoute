using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using OptiRoute.App.ViewModels;

namespace OptiRoute.App.Views;

/// <summary>
/// UserControl reutilizável que contém toda a UI de Settings (4 abas + Welcome + footer).
/// Usado inline na MainWindow (first-run / ⚙) — o app é single-window.
///
/// Dependency Properties:
///   IsFirstRun  — quando true, mostra Welcome panel; quando false, mostra TabControl.
/// Routed Events:
///   Cancelled — dispara quando user clica Cancel.
/// </summary>
public partial class SettingsPanel : UserControl
{
    public static readonly DependencyProperty IsFirstRunProperty =
        DependencyProperty.Register(
            nameof(IsFirstRun),
            typeof(bool),
            typeof(SettingsPanel),
            new PropertyMetadata(false, OnIsFirstRunChanged));

    public bool IsFirstRun
    {
        get => (bool)GetValue(IsFirstRunProperty);
        set => SetValue(IsFirstRunProperty, value);
    }

    /// <summary>Routed event raised when user clicks Cancel.</summary>
    public static readonly RoutedEvent CancelledEvent = EventManager.RegisterRoutedEvent(
        nameof(Cancelled), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SettingsPanel));

    public event RoutedEventHandler Cancelled
    {
        add => AddHandler(CancelledEvent, value);
        remove => RemoveHandler(CancelledEvent, value);
    }

    /// <summary>Routed event raised when the underlying SettingsViewModel saves successfully.</summary>
    public static readonly RoutedEvent SavedEvent = EventManager.RegisterRoutedEvent(
        nameof(Saved), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SettingsPanel));

    public event RoutedEventHandler Saved
    {
        add => AddHandler(SavedEvent, value);
        remove => RemoveHandler(SavedEvent, value);
    }

    /// <summary>ViewModel — exposto para code-behind handlers (e.g., SecretBox, ImportFile).</summary>
    public SettingsViewModel? ViewModel { get; private set; }

    private SettingsViewModel? _subscribedViewModel;

    public SettingsPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Mantém <see cref="ViewModel"/> sincronizado com o DataContext e reencaminha o
    /// evento <c>Saved</c> do VM como um RoutedEvent que borbulha até o MainWindow.
    /// Desinscreve do VM antigo para evitar leaks ao trocar/limpar o DataContext.
    /// </summary>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel is not null)
            _subscribedViewModel.Saved -= OnViewModelSaved;

        ViewModel = DataContext as SettingsViewModel;

        if (ViewModel is not null)
            ViewModel.Saved += OnViewModelSaved;

        _subscribedViewModel = ViewModel;
    }

    private void OnViewModelSaved(object? sender, EventArgs e)
        => RaiseEvent(new RoutedEventArgs(SavedEvent, this));

    private static void OnIsFirstRunChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsPanel panel)
        {
            // Trigger IsNotFirstRun re-evaluation by setting it as a separate DP... or use a converter.
            // Workaround: bind TabControl/SaveButton to IsNotFirstRun directly via RelativeSource AncestorType.
            // For now, force re-fetch via panel binding path.
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(CancelledEvent, this));
    }

    private void OnWizardNextClick(object sender, RoutedEventArgs e)
    {
        // O SettingsViewModel é a fonte de verdade da alternância Welcome ↔ abas/Save.
        // Antes, setar apenas o DP IsFirstRun escondia o Welcome, mas deixava o
        // TabControl e o botão Save ocultos (VM.IsNotFirstRun continuava false) → tela em branco.
        // O DP é bound a SettingsVm.IsFirstRun na MainWindow, então acompanha a mudança.
        if (ViewModel is not null)
            ViewModel.IsFirstRun = false;
        else
            IsFirstRun = false;
    }

    private void SecretBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb && ViewModel is not null)
            ViewModel.OnSecretBoxChanged(pb.Password);
    }

    private void OnToggleSecretClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var owner = Window.GetWindow(this)!;
        if (string.IsNullOrEmpty(ViewModel.ApiSecret))
            MessageBox.Show(owner, "(empty)", "API secret", MessageBoxButton.OK, MessageBoxImage.Information);
        else
            MessageBox.Show(owner, ViewModel.ApiSecret, "API secret", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnImportKeyFileClick(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select OPNsense API key file",
            Filter = "OPNsense key files (*.txt)|*.txt|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) == true)
            ViewModel?.ImportKeyFile(dlg.FileName);
    }

    private void OnHowToCreateClick(object sender, RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}