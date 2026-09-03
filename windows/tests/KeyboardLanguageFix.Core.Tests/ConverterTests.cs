using KeyboardLanguageFix.Core;
using Xunit;

namespace KeyboardLanguageFix.Core.Tests;

/// <summary>
/// These mirror test/converter.test.mjs in the browser extension, one for one.
/// If either side drifts, one of the two suites goes red.
/// </summary>
public class ConverterTests
{
    private static ConversionOptions Arabic(ConversionMode mode = ConversionMode.Auto) => new()
    {
        PrimaryLayout = "ar",
        EnabledLayouts = new[] { "ar" },
        Mode = mode
    };

    [Theory]
    [InlineData("hgsghl", "السلام")]              // السلام
    [InlineData("lvpfh", "مرحبا")]                     // مرحبا
    [InlineData(";dt phg;", "كيف حالك")]     // كيف حالك
    public void EnglishKeystrokesBecomeTheArabicTheUserMeant(string input, string expected)
    {
        Assert.Equal(expected, Converter.Convert(input, Arabic()).Text);
    }

    [Theory]
    [InlineData("اثممخ", "hello")]
    [InlineData("صخقمي", "world")]
    public void ArabicKeystrokesBecomeTheEnglishTheUserMeant(string input, string expected)
    {
        Assert.Equal(expected, Converter.Convert(input, Arabic()).Text);
    }

    [Fact]
    public void RoundTripIsStableForTheLetterRows()
    {
        const string source = "the quick brown fox jumps over the lazy dog";
        var arabic = Converter.Convert(source, Arabic(ConversionMode.ToLayout)).Text;

        Assert.NotEqual(source, arabic);
        Assert.Equal(source, Converter.Convert(arabic, Arabic(ConversionMode.ToLatin)).Text);
    }

    [Theory]
    [InlineData("b", "لا")]   // لا
    [InlineData("G", "لأ")]   // لأ
    [InlineData("B", "لآ")]   // لآ
    [InlineData("T", "لإ")]   // لإ
    public void MultiCharacterKeysSurviveARoundTrip(string key, string ligature)
    {
        Assert.Equal(ligature, Converter.Convert(key, Arabic(ConversionMode.ToLayout)).Text);
        Assert.Equal(key, Converter.Convert(ligature, Arabic(ConversionMode.ToLatin)).Text);
    }

    [Theory]
    // Word capitalises the first letter of a sentence, so "lpl,]" arrives as
    // "Lpl,]" — and Shift+L on the Arabic layout is "/", not a letter.
    [InlineData("lpl,]")]
    [InlineData("Lpl,]")]
    [InlineData("LPL,]")]   // ...and Caps Lock does the same thing
    public void ACapitalTheUserNeverTypedDoesNotBecomePunctuation(string input)
    {
        Assert.Equal("محمود", Converter.Convert(input, Arabic()).Text);
    }

    [Theory]
    [InlineData("Hpl]")]
    [InlineData("HPL]")]
    public void ACapitalThatYieldsARealLetterIsLeftAlone(string input)
    {
        // Shift+H is how you type the alef with hamza; that capital is deliberate.
        Assert.Equal("أحمد", Converter.Convert(input, Arabic()).Text);
    }

    [Theory]
    // These are typed with Shift at the end of a word, never with a word glued
    // to their right, which is what separates them from an accidental capital.
    [InlineData("K", "،")]
    [InlineData("P", "؛")]
    [InlineData("L", "/")]
    [InlineData("hgslhxK rvdfh", "السماء، قريبا")]
    public void PunctuationTypedWithShiftStillWorks(string input, string expected)
    {
        Assert.Equal(expected, Converter.Convert(input, Arabic(ConversionMode.ToLayout)).Text);
    }

    [Theory]
    [InlineData("ru", "CASE", "СФЫУ")]
    [InlineData("ru", "Ghbdtn", "Привет")]
    [InlineData("el", "CASE", "ΨΑΣΕ")]
    public void LayoutsWhoseShiftLayerIsTheirOwnUpperCaseAreUntouched(
        string layoutId, string input, string expected)
    {
        var options = new ConversionOptions
        {
            PrimaryLayout = layoutId,
            EnabledLayouts = new[] { layoutId },
            Mode = ConversionMode.ToLayout
        };
        Assert.Equal(expected, Converter.Convert(input, options).Text);
    }

    [Fact]
    public void AutoDirectionFollowsTheDominantScript()
    {
        Assert.Equal(ConversionDirection.ToLayout, Converter.Convert("hgsghl", Arabic()).Direction);
        Assert.Equal(ConversionDirection.ToLatin,
            Converter.Convert("اثممخ", Arabic()).Direction);
    }

    [Fact]
    public void DigitsAndUnmappedCharactersPassThrough()
    {
        var output = Converter.Convert("abc 123 @", Arabic(ConversionMode.ToLayout)).Text;
        Assert.Contains(" 123 ", output, StringComparison.Ordinal);
        Assert.EndsWith("@", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyAndWhitespaceInputReportNoChange(string? input)
    {
        Assert.False(Converter.Convert(input, Arabic()).Changed);
    }

    [Fact]
    public void RussianHandlesTheClassicGhbdtn()
    {
        var options = new ConversionOptions { PrimaryLayout = "ru", EnabledLayouts = new[] { "ru" } };
        Assert.Equal("привет", Converter.Convert("ghbdtn", options).Text);
        Assert.Equal("ghbdtn", Converter.Convert("привет", options).Text);
        Assert.Equal("Привет", Converter.Convert("Ghbdtn", options).Text);
    }

    [Fact]
    public void HebrewFoldsUpperCaseOntoTheBaseLayer()
    {
        var options = new ConversionOptions { PrimaryLayout = "he", EnabledLayouts = new[] { "he" } };
        Assert.Equal(
            Converter.Convert("shalom", options).Text,
            Converter.Convert("SHALOM", options).Text);
    }

    [Fact]
    public void GreekKeepsItsOwnCaseDistinction()
    {
        var options = new ConversionOptions { PrimaryLayout = "el", EnabledLayouts = new[] { "el" } };
        Assert.Equal("καλα", Converter.Convert("kala", options).Text);
        Assert.Equal("Καλα", Converter.Convert("Kala", options).Text);
    }

    [Fact]
    public void PersianMapsItsOwnLettersNotTheArabicOnes()
    {
        var options = new ConversionOptions { PrimaryLayout = "fa", EnabledLayouts = new[] { "fa" } };
        Assert.Equal("ی", Converter.Convert("d", options).Text);   // Persian yeh
        Assert.Equal("ک", Converter.Convert(";", options).Text);   // Persian keheh
        Assert.Equal("چ", Converter.Convert("]", options).Text);   // che
    }

    [Theory]
    [InlineData("مرحبا", "ar")]
    [InlineData("привет", "ru")]
    [InlineData("שלום", "he")]
    public void DetectLayoutPicksTheScriptActuallyPresent(string text, string expected)
    {
        Assert.Equal(expected, Converter.DetectLayout(text, new[] { "ar", "ru", "he" })?.Id);
    }

    [Fact]
    public void DetectLayoutReturnsNullForLatinText()
    {
        Assert.Null(Converter.DetectLayout("hello", new[] { "ar", "ru", "he" }));
    }

    [Fact]
    public void CustomMappingsOverrideTheBuiltInTableBothWays()
    {
        var options = new ConversionOptions
        {
            PrimaryLayout = "ar",
            EnabledLayouts = new[] { "ar" },
            CustomMap = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["ar"] = new Dictionary<string, string> { ["q"] = "ﻻ" }
            }
        };

        options.Mode = ConversionMode.ToLayout;
        Assert.Equal("ﻻ", Converter.Convert("q", options).Text);

        options.Mode = ConversionMode.ToLatin;
        Assert.Equal("q", Converter.Convert("ﻻ", options).Text);
    }

    [Fact]
    public void ForcedModesIgnoreTheDetectedScript()
    {
        Assert.Equal("hello", Converter.Convert("hello", Arabic(ConversionMode.ToLatin)).Text);
        Assert.Equal("مرحبا",
            Converter.Convert("مرحبا", Arabic(ConversionMode.ToLayout)).Text);
    }

    private static ConversionOptions Spanish(ConversionMode mode = ConversionMode.Auto) => new()
    {
        PrimaryLayout = "es",
        EnabledLayouts = new[] { "es" },
        Mode = mode
    };

    [Theory]
    [InlineData("Espa;a", "España")]
    [InlineData("Ma;ana", "Mañana")]
    [InlineData("a;o", "año")]
    public void SpanishReachesTheLetterAUsKeyboardHasNot(string input, string expected)
    {
        Assert.Equal(expected, Converter.Convert(input, Spanish()).Text);
    }

    [Theory]
    // Two keystrokes, one letter: the dead accent key, then the vowel.
    [InlineData("est'a", "está")]
    [InlineData("'Angel", "Ángel")]
    [InlineData("ping\"uino", "pingüino")]
    [InlineData("'ANGEL", "ÁNGEL")]
    [InlineData("'", "´")]
    public void SpanishDeadKeysComposeWithTheVowelThatFollows(string input, string expected)
    {
        Assert.Equal(expected, Converter.Convert(input, Spanish(ConversionMode.ToLayout)).Text);
    }

    [Fact]
    public void SpanishLeavesTheLettersAlone()
    {
        // a-z sits on the same keys in both layouts, so there is nothing to fix.
        Assert.False(Converter.Convert("hello world", Spanish(ConversionMode.ToLayout)).Changed);
        Assert.Equal("HOLA", Converter.Convert("HOLA", Spanish(ConversionMode.ToLayout)).Text);
    }

    [Fact]
    public void SpanishDirectionTurnsOnWhatAUsKeyboardCannotType()
    {
        // Both alphabets are Latin, so counting letters decides nothing. One
        // "ñ" is proof the text has already been through the Spanish layout.
        Assert.Equal(ConversionDirection.ToLayout, Converter.Convert("Espa;a", Spanish()).Direction);
        Assert.Equal(ConversionDirection.ToLatin, Converter.Convert("España", Spanish()).Direction);
        Assert.Equal("Espa;a", Converter.Convert("España", Spanish()).Text);

        // The everyday complaint of anyone writing code on a Spanish keyboard.
        Assert.Equal("console.log(x);", Converter.Convert("console.log)x=ñ", Spanish()).Text);
    }

    [Fact]
    public void SpanishStaysOutOfTheWayOfTheOtherLayouts()
    {
        Assert.Equal("es", Converter.DetectLayout("mañana", new[] { "ar", "es", "ru" })?.Id);
        Assert.Equal("ar", Converter.DetectLayout("مرحبا", new[] { "ar", "es" })?.Id);
        Assert.Null(Converter.DetectLayout("hello world", new[] { "ar", "es" }));
    }

    [Fact]
    public void EveryLayoutMapsEachOfItsOwnCharactersBackToAKey()
    {
        foreach (var layout in Layouts.All)
        {
            var options = new ConversionOptions
            {
                PrimaryLayout = layout.Id,
                EnabledLayouts = new[] { layout.Id },
                Mode = ConversionMode.ToLatin
            };

            foreach (var (key, value) in layout.BaseLayer)
            {
                Assert.Equal(key, Converter.Convert(value, options).Text);
            }
        }
    }
}
