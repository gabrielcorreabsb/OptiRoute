using System.Drawing;
using OptiRoute.App.Properties;
using Forms = System.Windows.Forms;

namespace OptiRoute.App.Views;

/// <summary>
/// Ícone na bandeja do sistema (tray) — <b>opcional e OFF por padrão</b>. Só é
/// instanciado quando o usuário habilita "Minimize to tray when closing" nas
/// configurações.
///
/// Expõe <see cref="ShowRequested"/> e <see cref="QuitRequested"/>; o
/// <c>MainViewModel</c> faz o wiring do restore da janela e do shutdown explícito.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private bool _disposed;

    /// <summary>Disparado no duplo-clique do ícone ou no item "Show" do menu.</summary>
    public event EventHandler? ShowRequested;

    /// <summary>Disparado no item "Quit" do menu — encerra o App de fato.</summary>
    public event EventHandler? QuitRequested;

    /// <summary>
    /// Disparado no item "Re-sync now" do menu. O <c>MainViewModel</c> responde
    /// chamando <c>SyncAsync</c> (ignorado se já houver sincronização em andamento).
    /// </summary>
    public event EventHandler? SyncRequested;

    /// <summary>
    /// Cria o ícone e o menu de contexto. O <paramref name="mainWindow"/> é usado
    /// apenas como referência/owner lógico (o wiring real dos eventos fica no VM).
    /// </summary>
    public TrayIcon(MainWindow mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

        var showItem = new Forms.ToolStripMenuItem(Strings.Tray_Menu_Show);
        showItem.Click += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);

        var syncItem = new Forms.ToolStripMenuItem(Strings.Tray_Menu_Resync);
        syncItem.Click += (_, _) => SyncRequested?.Invoke(this, EventArgs.Empty);
        syncItem.Font = new Font(syncItem.Font, FontStyle.Bold);

        var quitItem = new Forms.ToolStripMenuItem(Strings.Tray_Menu_Quit);
        quitItem.Click += (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty);

        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add(showItem);
        _menu.Items.Add(syncItem);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(quitItem);

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "OptiRoute",
            Visible = true,
            ContextMenuStrip = _menu
        };

        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Remove o ícone da bandeja e libera os recursos do WinForms.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        catch
        {
            // best-effort durante shutdown.
        }

        _menu.Dispose();
    }
}
