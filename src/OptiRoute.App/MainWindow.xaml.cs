using System.Windows;
using OptiRoute.App.ViewModels;

namespace OptiRoute.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += async (_, _) =>
        {
            if (_viewModel is not null)
                await _viewModel.InitializeAsync();
        };

        // Hook do botão "Settings" do header: MainViewModel constrói o VM,
        // MainWindow exibe a janela. Mantém o VM livre de tipos UI.
        if (_viewModel is not null)
        {
            _viewModel.OpenSettingsRequested += (_, vm) =>
            {
                var win = new Windows.SettingsWindow(vm) { Owner = this };
                win.ShowDialog();
            };
        }
    }
}