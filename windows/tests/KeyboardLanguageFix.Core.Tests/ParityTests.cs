using System.Text.Json;
using System.Text.Json.Serialization;
using KeyboardLanguageFix.Core;
using Xunit;

namespace KeyboardLanguageFix.Core.Tests;

/// <summary>
/// Replays parity-fixture.json — recorded from the browser extension's
/// JavaScript engine — against this C# engine. Any behavioural difference
/// between the two implementations shows up here.
/// </summary>
public class ParityTests
{
    private sealed record Case(
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("primaryLayout")] string PrimaryLayout,
        [property: JsonPropertyName("enabledLayouts")] string[] EnabledLayouts,
        [property: JsonPropertyName("mode")] string Mode,
        [property: JsonPropertyName("expected")] string Expected,
        [property: JsonPropertyName("changed")] bool Changed,
        [property: JsonPropertyName("direction")] string? Direction,
        [property: JsonPropertyName("layoutId")] string? LayoutId);

    private sealed record Fixture(
        [property: JsonPropertyName("cases")] Case[] Cases);

    private static Fixture Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "parity-fixture.json");
        Assert.True(File.Exists(path), $"fixture not found at {path}");
        var fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(path));
        Assert.NotNull(fixture);
        return fixture!;
    }

    private static ConversionMode ParseMode(string mode) => mode switch
    {
        "toLayout" => ConversionMode.ToLayout,
        "toLatin" => ConversionMode.ToLatin,
        _ => ConversionMode.Auto
    };

    [Fact]
    public void TheFixtureIsNotEmpty()
    {
        Assert.True(Load().Cases.Length > 500, "the fixture should cover every key of every layout");
    }

    [Fact]
    public void CSharpProducesTheSameTextAsJavaScript()
    {
        var failures = new List<string>();

        foreach (var testCase in Load().Cases)
        {
            var result = Converter.Convert(testCase.Input, new ConversionOptions
            {
                PrimaryLayout = testCase.PrimaryLayout,
                EnabledLayouts = testCase.EnabledLayouts,
                Mode = ParseMode(testCase.Mode)
            });

            if (!string.Equals(result.Text, testCase.Expected, StringComparison.Ordinal))
            {
                failures.Add(
                    $"[{testCase.PrimaryLayout}/{testCase.Mode}] {Escape(testCase.Input)}: " +
                    $"expected {Escape(testCase.Expected)}, got {Escape(result.Text)}");
            }
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count} case(s) diverged from the JavaScript engine:\n" +
            string.Join("\n", failures.Take(20)));
    }

    [Fact]
    public void CSharpAgreesAboutWhetherAnythingChanged()
    {
        var failures = new List<string>();

        foreach (var testCase in Load().Cases)
        {
            var result = Converter.Convert(testCase.Input, new ConversionOptions
            {
                PrimaryLayout = testCase.PrimaryLayout,
                EnabledLayouts = testCase.EnabledLayouts,
                Mode = ParseMode(testCase.Mode)
            });

            if (result.Changed != testCase.Changed)
            {
                failures.Add($"[{testCase.PrimaryLayout}/{testCase.Mode}] {Escape(testCase.Input)}: " +
                             $"expected changed={testCase.Changed}, got {result.Changed}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
    }

    [Fact]
    public void CSharpPicksTheSameDirectionAndLayout()
    {
        var failures = new List<string>();

        foreach (var testCase in Load().Cases)
        {
            if (!testCase.Changed) continue; // direction is not meaningful for a no-op

            var result = Converter.Convert(testCase.Input, new ConversionOptions
            {
                PrimaryLayout = testCase.PrimaryLayout,
                EnabledLayouts = testCase.EnabledLayouts,
                Mode = ParseMode(testCase.Mode)
            });

            var direction = result.Direction == ConversionDirection.ToLayout ? "toLayout" : "toLatin";
            if (direction != testCase.Direction || result.LayoutId != testCase.LayoutId)
            {
                failures.Add($"[{testCase.PrimaryLayout}/{testCase.Mode}] {Escape(testCase.Input)}: " +
                             $"expected {testCase.Direction}/{testCase.LayoutId}, " +
                             $"got {direction}/{result.LayoutId}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
    }

    /// <summary>Renders non-ASCII as \uXXXX so a failure message is readable in any console.</summary>
    private static string Escape(string value) =>
        "\"" + string.Concat(value.Select(ch =>
            ch is >= ' ' and <= '~' ? ch.ToString() : $"\\u{(int)ch:x4}")) + "\"";
}
