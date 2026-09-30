using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using OptiRoute.Core.Models;

namespace OptiRoute.App.Windows;

/// <summary>
/// Modal exibido após <c>ApplyPlanAsync</c> quando algumas ações de reconciliação falham.
/// Lista cada falha com executável, tipo da ação e motivo, e oferece ações auxiliares
/// (copiar detalhes, abrir pasta de logs, fechar).
///
/// Uso:
/// <code>
/// var result = await synchronizer.ApplyPlanAsync(plan);
/// if (!result.AllSucceeded)
/// {
///     var dlg = new ApplyFailuresDialog(result);
///     dlg.ShowDialog(owner);
/// }
/// </code>
/// </summary>
public partial class ApplyFailuresDialog : Window
{
    /// <summary>Falhas a exibir.</summary>
    public IReadOnlyList<ReconciliationActionFailure> Failures { get; }

    /// <summary>Quantidade de ações verificadas/aplicadas com sucesso.</summary>
    public int VerifiedCount { get; }

    /// <summary>Quantidade de ações que falharam.</summary>
    public int FailedCount => Failures.Count;

    /// <summary>True quando há ao menos uma falha a exibir.</summary>
    public bool HasFailures => Failures.Count > 0;

    /// <summary>True quando não há falhas a exibir (estado vazio defensivo).</summary>
    public bool HasNoFailures => Failures.Count == 0;

    /// <summary>Texto resumido: "X applied, Y failed".</summary>
    public string SummaryText { get; }

    /// <summary>
    /// Cria o dialog a partir do resultado completo de Apply/Verify.
    /// </summary>
    public ApplyFailuresDialog(ReconciliationResult result)
        : this(result.Failures, result.VerifiedCount)
    {
    }

    /// <summary>
    /// Cria o dialog a partir da lista de falhas e da contagem de ações bem-sucedidas.
    /// </summary>
    public ApplyFailuresDialog(IReadOnlyList<ReconciliationActionFailure> failures, int verifiedCount = 0)
    {
        InitializeComponent();

        Failures = failures;
        VerifiedCount = verifiedCount;

        var format = Properties.Strings.ResourceManager.GetString("Card.ApplyFailures.Subtitle", Properties.Strings.Culture)
                     ?? "OptiRoute applied {0} changes but {1} failed. Inspect the details below before retrying.";
        SummaryText = string.Format(CultureInfo.CurrentCulture, format, VerifiedCount, FailedCount);

        DataContext = this;
    }

    private void OnCopyDetailsClick(object sender, RoutedEventArgs e)
    {
        var lines = Failures
            .Select(f => $"{f.Action.Executable} | {f.Action.Type} | {f.Reason}")
            .ToList();

        var header = "Executable | Type | Reason";
        var text = lines.Count > 0
            ? string.Join(Environment.NewLine, lines.Prepend(header))
            : header;

        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not copy to clipboard: {ex.Message}",
                "Copy failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnOpenLogFolderClick(object sender, RoutedEventArgs e)
    {
        var logFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OptiRoute");

        try
        {
            Directory.CreateDirectory(logFolder);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{logFolder}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not open log folder: {ex.Message}",
                "Open folder failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

/// <summary>
/// Converte uma <see cref="ReconciliationActionFailure"/> em uma string de cabeçalho
/// localizada no formato "Executable — Type".
/// </summary>
[ValueConversion(typeof(ReconciliationActionFailure), typeof(string))]
public sealed class FailureHeaderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ReconciliationActionFailure failure)
            return string.Empty;

        var format = Properties.Strings.ResourceManager.GetString("Card.ApplyFailures.ItemHeader", culture)
                     ?? "{0} — {1}";
        return string.Format(culture, format, failure.Action.Executable, failure.Action.Type);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
