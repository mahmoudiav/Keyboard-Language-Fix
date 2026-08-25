using KeyboardLanguageFix.Core;
using Xunit;

namespace KeyboardLanguageFix.Core.Tests;

/// <summary>
/// The About box, the installer and the executable's file properties all quote
/// these values, so they are worth pinning down.
/// </summary>
public class AboutInfoTests
{
    [Fact]
    public void TheCreditNamesTheAuthor()
    {
        Assert.Equal("Mahmoud SATALEH", AboutInfo.Author);
        Assert.Equal("Idea and implementation: Mahmoud SATALEH", AboutInfo.Credit);
    }

    [Fact]
    public void TheContactAddressIsCorrect()
    {
        Assert.Equal("mahmoudiav@icloud.com", AboutInfo.Email);
    }

    [Fact]
    public void TheAppIsDescribedAsFree()
    {
        Assert.Contains("Free", AboutInfo.Pricing, StringComparison.Ordinal);
    }

    [Fact]
    public void TheVersionIsThreeNumbersTakenFromTheAssembly()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", AboutInfo.Version);
        Assert.Equal(
            typeof(AboutInfo).Assembly.GetName().Version!.Major,
            int.Parse(AboutInfo.Version.Split('.')[0]));
    }

    [Fact]
    public void EveryFieldIsFilledIn()
    {
        foreach (var value in new[]
                 {
                     AboutInfo.ProductName, AboutInfo.Tagline, AboutInfo.Author,
                     AboutInfo.Credit, AboutInfo.Email, AboutInfo.Pricing,
                     AboutInfo.License, AboutInfo.Version, AboutInfo.Copyright
                 })
        {
            Assert.False(string.IsNullOrWhiteSpace(value));
        }
    }

    [Fact]
    public void TheTextIsEnglishOnly()
    {
        // Asked for explicitly: a name, an address and a licence should read the
        // same in every language the interface is shown in.
        foreach (var value in new[]
                 {
                     AboutInfo.ProductName, AboutInfo.Tagline, AboutInfo.Credit,
                     AboutInfo.Email, AboutInfo.Pricing, AboutInfo.License
                 })
        {
            Assert.DoesNotMatch(@"[؀-ۿ]", value);
        }
    }
}
