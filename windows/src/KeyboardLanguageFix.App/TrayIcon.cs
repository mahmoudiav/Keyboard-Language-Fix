using System.Drawing;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace KeyboardLanguageFix.App;

/// <summary>The notification-area icon and its menu. This is the app's only chrome.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _convertItem;
    private bool _disposed;

    /// <summary>Raised when the user asks for the settings window.</summary>
    internal event EventHandler? SettingsRequested;

    /// <summary>Raised when the user triggers a conversion from the menu.</summary>
    internal event EventHandler? ConvertRequested;

    /// <summary>Raised when the user asks to quit.</summary>
    internal event EventHandler? ExitRequested;

    internal TrayIcon()
    {
        _convertItem = new ToolStripMenuItem("Convert selected text");
        _convertItem.Click += (_, _) => ConvertRequested?.Invoke(this, EventArgs.Empty);

        var settingsItem = new ToolStripMenuItem("Settings…");
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_convertItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Text = "Keyboard Language Fix"
        };

        // Double-clicking the tray icon is the conventional way into settings.
        _icon.DoubleClick += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private static Icon LoadIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/app.ico");
        var stream = Application.GetResourceStream(uri);
        return stream is null ? SystemIcons.Application : new Icon(stream.Stream);
    }

    /// <summary>Shows the icon, with the current shortcut in its tooltip.</summary>
    internal void Show(string hotkeyDescription)
    {
        UpdateTooltip(hotkeyDescription);
        _icon.Visible = true;
    }

    /// <summary>Keeps the tooltip in step with the configured shortcut.</summary>
    internal void UpdateTooltip(string hotkeyDescription)
    {
        // NotifyIcon.Text is capped at 63 characters by the shell.
        var text = $"Keyboard Language Fix — {hotkeyDescription}";
        _icon.Text = text.Length <= 63 ? text : text[..63];

        _convertItem.Text = $"Convert selected text ({hotkeyDescription})";
    }

    /// <summary>Shows a short balloon message.</summary>
    internal void Notify(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        _icon.BalloonTipTitle = "Keyboard Language Fix";
        _icon.BalloonTipText = message;
        _icon.ShowBalloonTip(1500);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
