using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using OptiRoute.App.Services;

namespace OptiRoute.App.Windows;

/// <summary>
/// Modal "Add Application": substitui o antigo file picker inline.
/// Permite escolher o executável via Browse…, selecionar um processo em execução
/// no momento (com contagem de instâncias e memória) ou digitar o caminho.
/// Exibe também Display name e o Gateway padrão. Enter confirma, Escape cancela.
///
/// Uso:
/// <code>
/// var dlg = new AddApplicationDialog(gatewayNames);
/// if (dlg.ShowDialog(owner) == true) { /* dlg.Executable, dlg.DisplayName, dlg.Gateway */ }
/// </code>
/// </summary>
public partial class AddApplicationDialog : Window
{
    /// <summary>Caminho completo do executável escolhido (vazio quando cancelado).</summary>
    public string Executable { get; private set; } = string.Empty;

    /// <summary>Nome de exibição amigável (pode ser vazio — o Core deriva do binário).</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Gateway padrão escolhido (vazio quando não há gateways disponíveis).</summary>
    public string Gateway { get; private set; } = string.Empty;

    /// <summary>
    /// Refresher opcional injetado pelo MainViewModel: re-sincroniza com o OPNsense e
    /// devolve a lista de gateways utilizáveis. Quando null, o botão Refresh do Gateway
    /// é desabilitado (não há fonte de dados para atualizar).
    /// </summary>
    private readonly Func<Task<IReadOnlyList<string>>>? _gatewayRefresher;

    public AddApplicationDialog(
        IEnumerable<string> availableGateways,
        Func<Task<IReadOnlyList<string>>>? gatewayRefresher = null)
    {
        InitializeComponent();

        _gatewayRefresher = gatewayRefresher;

        PopulateGateways(availableGateways);

        if (_gatewayRefresher is null)
            GatewayRefreshButton.IsEnabled = false;

        LoadRunningProcesses();
    }

    /// <summary>
    /// Repopula o GatewayBox preservando a seleção atual quando o nome ainda existe na
    /// nova lista; caso contrário seleciona o primeiro item (ou limpa a seleção).
    /// </summary>
    private void PopulateGateways(IEnumerable<string> gateways)
    {
        var previous = GatewayBox.SelectedItem as string;

        GatewayBox.Items.Clear();
        foreach (var gateway in gateways ?? [])
            GatewayBox.Items.Add(gateway);

        if (previous is not null && GatewayBox.Items.Contains(previous))
            GatewayBox.SelectedItem = previous;
        else if (GatewayBox.Items.Count > 0)
            GatewayBox.SelectedIndex = 0;
    }

    // ── Executable / validation ─────────────────────────────────────────────

    private void OnExecutableChanged(object sender, TextChangedEventArgs e)
    {
        var hasValue = !string.IsNullOrWhiteSpace(ExecutableBox.Text);
        SaveButton.IsEnabled = hasValue;
        if (hasValue)
            RequiredErrorText.Visibility = Visibility.Collapsed;
    }

    // ── Browse (OpenFileDialog + persistência do último diretório) ──────────

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var config = AppConfigManager.Load();
        var initialDir = string.IsNullOrWhiteSpace(config.LastUsedFolder)
            ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            : config.LastUsedFolder;

        var dialog = new OpenFileDialog
        {
            Title = TryGetString("AddApplication.Title", "Add Application"),
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = initialDir
        };

        if (dialog.ShowDialog(this) != true)
            return;

        ExecutableBox.Text = dialog.FileName;

        if (string.IsNullOrWhiteSpace(DisplayNameBox.Text))
            DisplayNameBox.Text = TryGetProductName(dialog.FileName)
                                  ?? Path.GetFileNameWithoutExtension(dialog.FileName);

        // Persiste o diretório escolhido para a próxima abertura do picker (default inicial).
        var folder = Path.GetDirectoryName(dialog.FileName);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            config.LastUsedFolder = folder;
            AppConfigManager.Save(config);
        }
    }

    // ── Currently running ────────────────────────────────────────────────────

    private void OnRefreshClick(object sender, RoutedEventArgs e) => LoadRunningProcesses();

    private void LoadRunningProcesses()
    {
        IReadOnlyList<RunningProcessInfo> processes;
        try
        {
            processes = RunningProcessScanner.Scan();
        }
        catch
        {
            // O scanner é best-effort; nunca deve impedir o dialog de abrir.
            processes = [];
        }

        RunningAppsBox.ItemsSource = processes;

        var hasAny = processes.Count > 0;
        RunningAppsBox.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;
        NoRunningAppsText.Visibility = hasAny ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnRunningAppSelected(object sender, SelectionChangedEventArgs e)
    {
        if (RunningAppsBox.SelectedItem is not RunningProcessInfo info)
            return;

        ExecutableBox.Text = info.FullPath;

        if (string.IsNullOrWhiteSpace(DisplayNameBox.Text))
            DisplayNameBox.Text = TryGetProductName(info.FullPath)
                                  ?? Path.GetFileNameWithoutExtension(info.FullPath);
    }

    // ── Gateway refresh ──────────────────────────────────────────────────────

    /// <summary>
    /// Re-sincroniza os gateways via <see cref="_gatewayRefresher"/>. O dialog NÃO fecha
    /// e, em caso de falha (ex.: OPNsense offline), mantém a lista atual — nunca esvazia.
    /// O botão fica desabilitado enquanto o refresh está em andamento.
    /// </summary>
    private async void OnGatewayRefreshClick(object sender, RoutedEventArgs e)
    {
        if (_gatewayRefresher is null)
            return;

        GatewayRefreshButton.IsEnabled = false;
        try
        {
            var gateways = await _gatewayRefresher();

            // Só substitui quando o refresh devolve algo; falha mantém a lista anterior.
            if (gateways is { Count: > 0 })
                PopulateGateways(gateways);
        }
        catch
        {
            // Best-effort: qualquer exceção mantém a lista já exibida.
        }
        finally
        {
            GatewayRefreshButton.IsEnabled = true;
        }
    }

    // ── Save / Cancel / keys ─────────────────────────────────────────────────

    private void OnSaveClick(object sender, RoutedEventArgs e) => TrySave();

    private void OnCancelClick(object sender, RoutedEventArgs e) => TryClose(false);

    private void TrySave()
    {
        if (string.IsNullOrWhiteSpace(ExecutableBox.Text))
        {
            RequiredErrorText.Visibility = Visibility.Visible;
            ExecutableBox.Focus();
            return;
        }

        Executable   = ExecutableBox.Text.Trim();
        DisplayName  = DisplayNameBox.Text.Trim();
        Gateway      = GatewayBox.SelectedItem as string ?? string.Empty;
        TryClose(true);
    }

    /// <summary>
    /// Enter confirma (quando válido), Escape cancela. Sem <see cref="Window.DialogResult"/>
    /// quando a janela não foi aberta via ShowDialog (defensivo: evita InvalidOperationException).
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                TrySave();
                e.Handled = true;
                break;

            case Key.Escape:
                TryClose(false);
                e.Handled = true;
                break;
        }
    }

    private void TryClose(bool result)
    {
        try
        {
            DialogResult = result;
        }
        catch (InvalidOperationException)
        {
            // Não aberto como modal — apenas fecha.
            Close();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Fallback localizada melhor-esforço para o título nativo do picker.</summary>
    private static string TryGetString(string key, string fallback)
    {
        var value = Properties.Strings.ResourceManager.GetString(key, Properties.Strings.Culture);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    /// <summary>
    /// Tenta ler <c>ProductName</c> via <see cref="FileVersionInfo"/>. Qualquer falha
    /// (arquivo sem versão, acesso negado, path inválido) retorna null.
    /// </summary>
    private static string? TryGetProductName(string path)
    {
        try
        {
            var name = FileVersionInfo.GetVersionInfo(path).ProductName;
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }
        catch
        {
            return null;
        }
    }
}
