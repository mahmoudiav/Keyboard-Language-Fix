using System.IO;
using System.Windows;
using KeyboardLanguageFix.App.Interop;
using KeyboardLanguageFix.Core;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace KeyboardLanguageFix.App;

/// <summary>
/// The application shell: a tray icon, one global hotkey, and a settings window.
/// There is deliberately no main window — the app lives in the notification area.
/// </summary>
public partial class App : Application
{
    /// <summary>Guards against a second copy grabbing the same hotkey.</summary>
    private const string SingleInstanceMutex = @"Local\KeyboardLanguageFix.SingleInstance";

    private Mutex? _instanceMutex;
    private SettingsStore _store = null!;
    private AppSettings _settings = null!;
    private HotkeyListener _hotkey = null!;
    private TextSwapper _swapper = null!;
    private TrayIcon _tray = null!;
    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private bool _busy;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // The Windows right-click entry starts a separate, short-lived copy for
        // one file. It gets no tray icon and no hotkey, and it is handled before
        // the single-instance check on purpose: it is not competing with a
        // running copy for the shortcut, so it must not be turned away by it.
        var fileToConvert = FileArgument(e.Args);
        if (fileToConvert is not null)
        {
            ShowFileWindow(fileToConvert);
            return;
        }

        _instanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutex, out var isFirstInstance);
        if (!isFirstInstance)
        {
            // A second copy would fight the first one for the hotkey.
            MessageBox.Show(
                "Keyboard Language Fix is already running. Look for it in the notification area.",
                "Keyboard Language Fix",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _store = new SettingsStore();
        _settings = _store.Load();
        _swapper = new TextSwapper(() => _settings);

        _hotkey = new HotkeyListener();
        _hotkey.Pressed += async (_, _) => await ConvertSelectionAsync().ConfigureAwait(true);

        _tray = new TrayIcon();
        _tray.SettingsRequested += (_, _) => ShowSettings();
        _tray.ConvertRequested += async (_, _) => await ConvertSelectionAsync().ConfigureAwait(true);
        _tray.AboutRequested += (_, _) => ShowAbout();
        _tray.ExitRequested += (_, _) => Shutdown();

        ApplyHotkey(announceFailure: true);
        SyncContextMenu();
        _tray.Show(DescribeHotkey());

        // First run has nothing configured yet, so show the window once.
        if (!File.Exists(_store.FilePath))
        {
            _store.Save(_settings);
            ShowSettings();
        }
    }

    /// <summary>The file Explorer asked us to convert, or null for a normal start.</summary>
    private static string? FileArgument(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], ShellMenu.ConvertFileSwitch, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }
        return null;
    }

    /// <summary>Opens one file in its own window and quits when it is closed.</summary>
    private void ShowFileWindow(string path)
    {
        var window = new FileConvertWindow(path, new SettingsStore().Load());
        // ShutdownMode is OnExplicitShutdown, which is what keeps the windowless
        // tray app alive; this run has nothing to stay alive for.
        window.Closed += (_, _) => Shutdown();
        window.Show();
    }

    /// <summary>
    /// Brings the right-click menu entry into line with the setting.
    /// </summary>
    /// <remarks>
    /// Done at every start rather than only when the setting changes, because
    /// the entry records the path of the exe: after an update that moved it, the
    /// stale entry has to be rewritten or Explorer would launch nothing.
    /// </remarks>
    private void SyncContextMenu()
    {
        if (!ShellMenu.IsSupported) return;

        if (_settings.ShowInContextMenu)
        {
            if (!ShellMenu.IsRegistered()) ShellMenu.TrySetEnabled(true);
        }
        else if (ShellMenu.IsPresent())
        {
            ShellMenu.TrySetEnabled(false);
        }
    }

    /// <summary>Registers the configured hotkey and reports any conflict.</summary>
    private void ApplyHotkey(bool announceFailure)
    {
        var error = _hotkey.Register(_settings.Hotkey);
        if (error is null || !announceFailure) return;

        MessageBox.Show(
            $"{error}\n\nPick a different shortcut in Settings.",
            "Keyboard Language Fix",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    /// <summary>Runs one conversion, ignoring presses that arrive while one is in flight.</summary>
    private async Task ConvertSelectionAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var outcome = await _swapper.SwapAsync().ConfigureAwait(true);
            if (_settings.ShowNotifications) _tray.Notify(Describe(outcome));
        }
        finally
        {
            _busy = false;
        }
    }

    private static string Describe(SwapOutcome outcome) => outcome.Status switch
    {
        SwapStatus.Converted => "Converted",
        SwapStatus.NothingSelected => "Nothing was selected",
        SwapStatus.NothingToChange => "Already in the right language",
        SwapStatus.ClipboardUnavailable => "The clipboard was busy — try again",
        _ => string.Empty
    };

    private string DescribeHotkey() => HotkeyLabel.Describe(_settings.Hotkey);

    /// <summary>Opens the settings window, or brings the open one to the front.</summary>
    private void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, _store);
        _settingsWindow.SettingsSaved += (_, saved) =>
        {
            _settings = saved;
            ApplyHotkey(announceFailure: true);
            _tray.UpdateTooltip(DescribeHotkey());
            Converter.Invalidate();
        };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    /// <summary>Opens the About box, or brings the open one to the front.</summary>
    internal void ShowAbout()
    {
        if (_aboutWindow is not null)
        {
            _aboutWindow.Activate();
            return;
        }

        _aboutWindow = new AboutWindow();
        _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        _aboutWindow.Show();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _hotkey?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
