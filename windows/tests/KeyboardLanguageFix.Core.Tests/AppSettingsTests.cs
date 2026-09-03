using KeyboardLanguageFix.Core;
using Xunit;

namespace KeyboardLanguageFix.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void DefaultsRoundTripThroughJson()
    {
        var settings = new AppSettings();
        var restored = AppSettings.FromJson(settings.ToJson());

        Assert.Equal(settings.PrimaryLayout, restored.PrimaryLayout);
        Assert.Equal(settings.Mode, restored.Mode);
        Assert.Equal(settings.Hotkey.VirtualKey, restored.Hotkey.VirtualKey);
        Assert.True(restored.Hotkey.Control);
        Assert.True(restored.Hotkey.Shift);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{\"primaryLayout\": ")]
    [InlineData(null)]
    public void MalformedJsonFallsBackToDefaultsInsteadOfThrowing(string? json)
    {
        var settings = AppSettings.FromJson(json);
        Assert.Equal("ar", settings.PrimaryLayout);
    }

    [Fact]
    public void AnUnknownPrimaryLayoutIsRepaired()
    {
        var settings = AppSettings.FromJson("{\"primaryLayout\":\"klingon\"}");
        Assert.Equal("ar", settings.PrimaryLayout);
    }

    [Fact]
    public void UnknownEnabledLayoutsAreDropped()
    {
        var settings = AppSettings.FromJson(
            "{\"primaryLayout\":\"ru\",\"enabledLayouts\":[\"ru\",\"klingon\",\"he\"]}");
        Assert.Equal(new[] { "ru", "he" }, settings.EnabledLayouts);
    }

    [Fact]
    public void AnEmptyEnabledListFallsBackToThePrimaryLayout()
    {
        var settings = AppSettings.FromJson("{\"primaryLayout\":\"el\",\"enabledLayouts\":[]}");
        Assert.Equal(new[] { "el" }, settings.EnabledLayouts);
    }

    [Fact]
    public void AHotkeyWithNoModifierIsRejected()
    {
        // Registering a bare key would swallow it system-wide.
        var settings = AppSettings.FromJson(
            "{\"hotkey\":{\"virtualKey\":65,\"control\":false,\"shift\":false,\"alt\":false,\"windows\":false}}");
        Assert.True(settings.Hotkey.Control);
        Assert.Equal(0x20, settings.Hotkey.VirtualKey);
    }

    [Fact]
    public void ShiftAloneDoesNotCountAsAModifier()
    {
        var hotkey = new HotkeySetting { Control = false, Alt = false, Windows = false, Shift = true };
        Assert.False(hotkey.HasModifier);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(50, 100)]
    [InlineData(999999, 5000)]
    [InlineData(400, 400)]
    public void TheClipboardTimeoutIsClampedToSomethingUsable(int stored, int expected)
    {
        var settings = AppSettings.FromJson($"{{\"clipboardTimeoutMs\":{stored}}}");
        Assert.Equal(expected, settings.ClipboardTimeoutMs);
    }

    [Fact]
    public void TheRightClickEntryIsOnForSettingsFilesWrittenBeforeItExisted()
    {
        // Someone upgrading from 1.0.0 has a settings file with no such key.
        // Reading it must give them the entry, not silently withhold it.
        Assert.True(AppSettings.FromJson("{\"primaryLayout\":\"ar\"}").ShowInContextMenu);
        Assert.False(AppSettings.FromJson("{\"showInContextMenu\":false}").ShowInContextMenu);
    }

    [Fact]
    public void ConversionOptionsAlwaysRecogniseThePrimaryLayout()
    {
        var settings = new AppSettings { PrimaryLayout = "ru", EnabledLayouts = new List<string> { "ar" } };
        var options = settings.ToConversionOptions();

        Assert.Contains("ru", options.EnabledLayouts);
        Assert.Contains("ar", options.EnabledLayouts);
    }
}
