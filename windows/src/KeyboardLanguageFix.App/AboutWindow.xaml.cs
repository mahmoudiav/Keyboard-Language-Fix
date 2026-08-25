using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using KeyboardLanguageFix.Core;

namespace KeyboardLanguageFix.App;

/// <summary>
/// The About box. Its text is English only, on purpose: a person's name, an
/// address and a licence should read the same wherever they are quoted.
/// </summary>
public partial class AboutWindow : Window
{
    internal AboutWindow()
    {
        InitializeComponent();

        ProductNameText.Text = AboutInfo.ProductName;
        VersionText.Text = $"Version {AboutInfo.Version}";
        TaglineText.Text = AboutInfo.Tagline;
        CreditText.Text = AboutInfo.Credit;
        EmailText.Text = AboutInfo.Email;
        EmailLink.NavigateUri = new Uri($"mailto:{AboutInfo.Email}");
        PricingText.Text = AboutInfo.Pricing;
        LicenseText.Text = AboutInfo.License;

        try
        {
            LogoImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico"));
        }
        catch (IOException)
        {
            // The window is still perfectly readable without the logo.
        }
    }

    private void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
                                              or InvalidOperationException)
        {
            // No mail client configured. "Copy details" is the way out.
        }
        e.Handled = true;
    }

    /// <summary>Puts the details on the clipboard, for anyone reporting a problem.</summary>
    private void OnCopyDetails(object sender, RoutedEventArgs e)
    {
        var details = string.Join(Environment.NewLine,
            $"{AboutInfo.ProductName} {AboutInfo.Version}",
            AboutInfo.Credit,
            AboutInfo.Email,
            AboutInfo.Pricing,
            $"Windows {Environment.OSVersion.Version}");

        ClipboardHelper.TrySetText(details);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
