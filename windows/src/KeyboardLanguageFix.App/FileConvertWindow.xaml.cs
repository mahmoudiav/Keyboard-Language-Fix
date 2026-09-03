using System.IO;
using System.Windows;
using KeyboardLanguageFix.Core;
using Clipboard = System.Windows.Clipboard;
using FlowDirection = System.Windows.FlowDirection;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;

namespace KeyboardLanguageFix.App;

/// <summary>
/// What the Windows right-click menu opens: one file, converted, shown before
/// and after, saved only if the user says so.
/// </summary>
/// <remarks>
/// A context menu that silently rewrote a document would be the wrong shape for
/// this. The conversion is a guess about what someone meant to type, so the
/// window shows the guess and lets it be edited before anything is written.
/// </remarks>
public partial class FileConvertWindow : Window
{
    private readonly string _path;
    private readonly AppSettings _settings;
    private TextFileContent _content;
    private bool _readable;
    private bool _loaded;

    internal FileConvertWindow(string path, AppSettings settings)
    {
        InitializeComponent();

        _path = path;
        _settings = settings;

        FileNameText.Text = SafeFileName(path);
        FilePathText.Text = path;

        LayoutBox.ItemsSource = Layouts.All.Select(layout => layout.ToString()).ToList();
        LayoutBox.SelectedIndex = IndexOfLayout(settings.PrimaryLayout);
        ModeBox.SelectedIndex = settings.Mode switch
        {
            ConversionMode.ToLayout => 1,
            ConversionMode.ToLatin => 2,
            _ => 0
        };

        Load();
        _loaded = true;
        UpdateConversion();
    }

    /// <summary>Reads the file, or explains why it will not be touched.</summary>
    private void Load()
    {
        byte[] bytes;
        try
        {
            var info = new FileInfo(_path);
            if (info.Length > TextFileCodec.MaxBytes)
            {
                // Checked before reading, so a huge file is never pulled into memory.
                Refuse($"This file is {info.Length / 1024 / 1024} MB. " +
                       $"Only files up to {TextFileCodec.MaxBytes / 1024 / 1024} MB are opened here.");
                return;
            }

            bytes = File.ReadAllBytes(_path);
        }
        catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or System.Security.SecurityException
                                              or ArgumentException
                                              or NotSupportedException)
        {
            // ArgumentException and NotSupportedException cover a path Explorer
            // should never hand us but that must not crash the app if it does.
            Refuse("This file could not be opened: " + exception.Message);
            return;
        }

        if (!TextFileCodec.TryDecode(bytes, out var content, out var problem))
        {
            Refuse(problem == TextFileProblem.TooLarge
                ? "This file is too large to open here."
                : "This is not a Unicode text file — it is either binary, or saved in an older " +
                  "Windows code page. Open it in Notepad and save it again as UTF-8, then try once more.");
            return;
        }

        _content = content;
        _readable = true;
        OriginalText.Text = content.Text;
    }

    private void Refuse(string reason)
    {
        _readable = false;
        ProblemText.Text = reason;
        ProblemText.Visibility = Visibility.Visible;
        SaveButton.IsEnabled = false;
        CopyButton.IsEnabled = false;
        ConvertedText.IsReadOnly = true;
        SaveHint.Visibility = Visibility.Collapsed;
    }

    private static string SafeFileName(string path)
    {
        try
        {
            return Path.GetFileName(path);
        }
        catch (ArgumentException)
        {
            return path;
        }
    }

    private static int IndexOfLayout(string id)
    {
        for (var index = 0; index < Layouts.All.Count; index++)
        {
            if (string.Equals(Layouts.All[index].Id, id, StringComparison.OrdinalIgnoreCase)) return index;
        }
        return 0;
    }

    private string SelectedLayoutId => Layouts.All[Math.Max(0, LayoutBox.SelectedIndex)].Id;

    private ConversionMode SelectedMode => ModeBox.SelectedIndex switch
    {
        1 => ConversionMode.ToLayout,
        2 => ConversionMode.ToLatin,
        _ => ConversionMode.Auto
    };

    private void OnChoiceChanged(object sender, SelectionChangedEventArgs e) => UpdateConversion();

    private void UpdateConversion()
    {
        if (!_loaded || !_readable) return;

        var layoutId = SelectedLayoutId;
        var enabled = _settings.EnabledLayouts.ToList();
        if (!enabled.Contains(layoutId, StringComparer.OrdinalIgnoreCase)) enabled.Add(layoutId);

        var result = Converter.Convert(_content.Text, new ConversionOptions
        {
            PrimaryLayout = layoutId,
            EnabledLayouts = enabled,
            Mode = SelectedMode,
            CustomMap = _settings.ToConversionOptions().CustomMap
        });

        ConvertedText.Text = result.Text;

        var layout = Layouts.Find(result.LayoutId);
        ConvertedText.FlowDirection =
            result.Direction == ConversionDirection.ToLayout && layout is { RightToLeft: true }
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

        StatusText.Text = result.Changed ? string.Empty : "Nothing to change";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!_readable) return;

        try
        {
            // Keep what the file says now, so a conversion the user did not want
            // is one rename away from being undone.
            File.Copy(_path, _path + ".bak", overwrite: true);

            // Write beside the target and swap, so an interrupted save cannot
            // leave the document truncated.
            var temporary = _path + ".klf.tmp";
            File.WriteAllBytes(temporary, TextFileCodec.Encode(ConvertedText.Text, _content.Encoding));
            File.Move(temporary, _path, overwrite: true);

            StatusText.Text = "Saved";
        }
        catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or System.Security.SecurityException)
        {
            ProblemText.Text = "This file could not be saved: " + exception.Message;
            ProblemText.Visibility = Visibility.Visible;
            StatusText.Text = string.Empty;
        }
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(ConvertedText.Text);
            StatusText.Text = "Copied";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Another program is holding the clipboard open; there is nothing
            // useful to do about it beyond saying so.
            StatusText.Text = "The clipboard was busy — try again";
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
