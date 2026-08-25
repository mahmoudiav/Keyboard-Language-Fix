using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using KeyboardLanguageFix.Core;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using FlowDirection = System.Windows.FlowDirection;

namespace KeyboardLanguageFix.App;

/// <summary>One row in the "layouts to recognise" list.</summary>
internal sealed class LayoutChoice : INotifyPropertyChanged
{
    private bool _isEnabled;

    internal LayoutChoice(Layout layout, bool isEnabled)
    {
        Id = layout.Id;
        Display = layout.ToString();
        _isEnabled = isEnabled;
    }

    /// <summary>The layout's id.</summary>
    public string Id { get; }

    /// <summary>Text shown next to the checkbox.</summary>
    public string Display { get; }

    /// <summary>Whether this layout is recognised when converting back to English.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>The settings window.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;
    private readonly ObservableCollection<LayoutChoice> _layoutChoices = new();
    private AppSettings _settings;
    private HotkeySetting _hotkey;
    private bool _recording;
    private bool _loaded;

    /// <summary>Raised after the user saves, carrying the new settings.</summary>
    internal event EventHandler<AppSettings>? SettingsSaved;

    internal SettingsWindow(AppSettings settings, SettingsStore store)
    {
        InitializeComponent();

        _store = store;
        _settings = settings;
        _hotkey = settings.Hotkey.Clone();

        EnabledLayoutsList.ItemsSource = _layoutChoices;
        LoadFromSettings();
        _loaded = true;
        UpdatePreview();
    }

    private void LoadFromSettings()
    {
        PrimaryLayoutBox.ItemsSource = Layouts.All.Select(layout => layout.ToString()).ToList();
        PrimaryLayoutBox.SelectedIndex = IndexOfLayout(_settings.PrimaryLayout);

        _layoutChoices.Clear();
        foreach (var layout in Layouts.All)
        {
            _layoutChoices.Add(new LayoutChoice(
                layout,
                _settings.EnabledLayouts.Contains(layout.Id, StringComparer.OrdinalIgnoreCase)));
        }

        ModeBox.SelectedIndex = _settings.Mode switch
        {
            ConversionMode.ToLayout => 1,
            ConversionMode.ToLatin => 2,
            _ => 0
        };

        ReplaceMethodBox.SelectedIndex = _settings.ReplaceMethod == ReplaceMethod.Type ? 1 : 0;
        RestoreClipboardBox.IsChecked = _settings.RestoreClipboard;
        NotificationsBox.IsChecked = _settings.ShowNotifications;
        TimeoutBox.Text = _settings.ClipboardTimeoutMs.ToString(CultureInfo.InvariantCulture);
        HotkeyText.Text = HotkeyLabel.Describe(_hotkey);

        StartupBox.IsChecked = PackageInfo.IsPackaged ? _settings.RunAtStartup : StartupManager.IsEnabled();
        StartupHint.Text = PackageInfo.IsPackaged
            ? "Installed from the Microsoft Store, so Windows owns this switch — it opens Startup Apps."
            : string.Empty;

        PreviewInput.Text = "hgsghl ugd;l";
    }

    private static int IndexOfLayout(string id)
    {
        for (var index = 0; index < Layouts.All.Count; index++)
        {
            if (string.Equals(Layouts.All[index].Id, id, StringComparison.OrdinalIgnoreCase)) return index;
        }
        return 0;
    }

    private string SelectedLayoutId =>
        Layouts.All[Math.Max(0, PrimaryLayoutBox.SelectedIndex)].Id;

    private ConversionMode SelectedMode => ModeBox.SelectedIndex switch
    {
        1 => ConversionMode.ToLayout,
        2 => ConversionMode.ToLatin,
        _ => ConversionMode.Auto
    };

    // ---- hotkey recording ----------------------------------------------

    private void OnRecord(object sender, RoutedEventArgs e)
    {
        if (_recording) { StopRecording(); return; }

        _recording = true;
        RecordButton.Content = "Press the keys…";
        HotkeyWarning.Visibility = Visibility.Collapsed;
        Keyboard.Focus(RecordButton);
    }

    private void StopRecording()
    {
        _recording = false;
        RecordButton.Content = "Record";
        HotkeyText.Text = HotkeyLabel.Describe(_hotkey);
    }

    /// <inheritdoc />
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!_recording)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;

        if (e.Key == Key.Escape) { StopRecording(); return; }

        // System keys arrive as Key.System with the real key in SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifier(key)) return; // wait for a real key

        var candidate = new HotkeySetting
        {
            VirtualKey = KeyInterop.VirtualKeyFromKey(key),
            Control = (Keyboard.Modifiers & ModifierKeys.Control) != 0,
            Shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0,
            Alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0,
            Windows = (Keyboard.Modifiers & ModifierKeys.Windows) != 0
        };

        if (!candidate.HasModifier)
        {
            HotkeyWarning.Text = "That shortcut needs Ctrl, Alt or the Windows key.";
            HotkeyWarning.Visibility = Visibility.Visible;
            return;
        }

        _hotkey = candidate;
        HotkeyWarning.Visibility = Visibility.Collapsed;
        StopRecording();
    }

    private static bool IsModifier(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or
        Key.LWin or Key.RWin or
        Key.System;

    // ---- live preview ---------------------------------------------------

    private void OnPrimaryLayoutChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => UpdatePreview();

    private void OnPreviewInputChanged(object sender, EventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (!_loaded) return;

        var layoutId = SelectedLayoutId;
        var enabled = _layoutChoices.Where(choice => choice.IsEnabled).Select(choice => choice.Id).ToList();
        // Mirror what Save does, so the preview never disagrees with reality.
        if (!enabled.Contains(layoutId, StringComparer.OrdinalIgnoreCase)) enabled.Add(layoutId);

        var result = Converter.Convert(PreviewInput.Text, new ConversionOptions
        {
            PrimaryLayout = layoutId,
            EnabledLayouts = enabled,
            Mode = SelectedMode,
            CustomMap = _settings.ToConversionOptions().CustomMap
        });

        PreviewOutput.Text = result.Text;

        var layout = Layouts.Find(result.LayoutId);
        PreviewOutput.FlowDirection =
            result.Direction == ConversionDirection.ToLayout && layout is { RightToLeft: true }
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;
    }

    // ---- startup --------------------------------------------------------

    private void OnStartupClicked(object sender, RoutedEventArgs e)
    {
        if (!PackageInfo.IsPackaged) return;

        // A packaged app cannot silently add itself to startup; Windows asks the
        // user. Send them to the page that owns the switch.
        StartupBox.IsChecked = _settings.RunAtStartup;
        StartupManager.OpenWindowsStartupSettings();
    }

    // ---- saving ---------------------------------------------------------

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TimeoutBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeout))
        {
            timeout = _settings.ClipboardTimeoutMs;
        }

        var enabled = _layoutChoices.Where(choice => choice.IsEnabled).Select(choice => choice.Id).ToList();

        var updated = new AppSettings
        {
            PrimaryLayout = SelectedLayoutId,
            EnabledLayouts = enabled,
            Mode = SelectedMode,
            Hotkey = _hotkey.Clone(),
            ReplaceMethod = ReplaceMethodBox.SelectedIndex == 1 ? ReplaceMethod.Type : ReplaceMethod.Paste,
            RestoreClipboard = RestoreClipboardBox.IsChecked == true,
            ShowNotifications = NotificationsBox.IsChecked == true,
            RunAtStartup = StartupBox.IsChecked == true,
            ClipboardTimeoutMs = timeout,
            CustomMap = _settings.CustomMap
        }.Normalised();

        if (!PackageInfo.IsPackaged)
        {
            StartupManager.TrySetEnabled(updated.RunAtStartup);
        }

        _settings = updated;
        _hotkey = updated.Hotkey.Clone();
        TimeoutBox.Text = updated.ClipboardTimeoutMs.ToString(CultureInfo.InvariantCulture);
        HotkeyText.Text = HotkeyLabel.Describe(_hotkey);

        StatusText.Text = _store.Save(updated) ? "Saved" : "Could not write the settings file";
        SettingsSaved?.Invoke(this, updated);
        UpdatePreview();
    }

    private void OnAbout(object sender, RoutedEventArgs e) =>
        ((App)System.Windows.Application.Current).ShowAbout();

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
