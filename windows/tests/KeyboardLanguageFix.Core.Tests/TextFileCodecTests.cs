using System.Text;
using KeyboardLanguageFix.Core;
using Xunit;

namespace KeyboardLanguageFix.Core.Tests;

/// <summary>
/// The right-click entry rewrites a file in place, so these are about not
/// destroying one: a file is read back exactly as it was written, and anything
/// that cannot be read faithfully is refused rather than guessed at.
/// </summary>
public class TextFileCodecTests
{
    [Theory]
    [InlineData(TextFileEncoding.Utf8)]
    [InlineData(TextFileEncoding.Utf8WithMark)]
    [InlineData(TextFileEncoding.Utf16LittleEndian)]
    [InlineData(TextFileEncoding.Utf16BigEndian)]
    [InlineData(TextFileEncoding.Utf32LittleEndian)]
    [InlineData(TextFileEncoding.Utf32BigEndian)]
    public void EveryEncodingSurvivesARoundTrip(TextFileEncoding encoding)
    {
        const string text = "السلام عليكم\r\nEspaña, mañana\nПривет\tκαλά";

        var bytes = TextFileCodec.Encode(text, encoding);

        Assert.True(TextFileCodec.TryDecode(bytes, out var content, out var problem));
        Assert.Equal(TextFileProblem.None, problem);
        Assert.Equal(text, content.Text);
        Assert.Equal(encoding, content.Encoding);

        // Saving what was read has to produce the same file, byte for byte.
        Assert.Equal(bytes, TextFileCodec.Encode(content.Text, content.Encoding));
    }

    [Fact]
    public void AByteOrderMarkIsKeptAndAMissingOneIsNotInvented()
    {
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF },
            TextFileCodec.Encode("a", TextFileEncoding.Utf8WithMark)[..3]);
        Assert.Equal(new byte[] { (byte)'a' }, TextFileCodec.Encode("a", TextFileEncoding.Utf8));
    }

    [Fact]
    public void PlainUtf8IsAssumedWhenThereIsNoMark()
    {
        var bytes = Encoding.UTF8.GetBytes("mañana");

        Assert.True(TextFileCodec.TryDecode(bytes, out var content, out _));
        Assert.Equal("mañana", content.Text);
        Assert.Equal(TextFileEncoding.Utf8, content.Encoding);
    }

    [Fact]
    public void ALegacyCodePageFileIsRefusedRatherThanMangled()
    {
        // "mañana" as Windows-1252: the 0xF1 is not valid UTF-8. Decoding it
        // anyway would put a replacement character in the file we then save.
        var bytes = new byte[] { (byte)'m', (byte)'a', 0xF1, (byte)'a', (byte)'n', (byte)'a' };

        Assert.False(TextFileCodec.TryDecode(bytes, out _, out var problem));
        Assert.Equal(TextFileProblem.NotText, problem);
    }

    [Fact]
    public void ABinaryFileIsRefused()
    {
        var bytes = new byte[] { (byte)'M', (byte)'Z', 0x00, 0x00, 0x01, (byte)'a' };

        Assert.False(TextFileCodec.TryDecode(bytes, out _, out var problem));
        Assert.Equal(TextFileProblem.NotText, problem);
    }

    [Fact]
    public void Utf16IsNotMistakenForBinaryDespiteItsZeroBytes()
    {
        var bytes = TextFileCodec.Encode("hi", TextFileEncoding.Utf16LittleEndian);

        Assert.Contains((byte)0, bytes);
        Assert.True(TextFileCodec.TryDecode(bytes, out var content, out _));
        Assert.Equal("hi", content.Text);
    }

    [Fact]
    public void AFileLargerThanTheLimitIsRefusedBeforeAnythingElse()
    {
        var bytes = new byte[TextFileCodec.MaxBytes + 1];

        Assert.False(TextFileCodec.TryDecode(bytes, out _, out var problem));
        Assert.Equal(TextFileProblem.TooLarge, problem);
    }

    [Fact]
    public void AnEmptyFileIsReadAsEmptyText()
    {
        Assert.True(TextFileCodec.TryDecode(Array.Empty<byte>(), out var content, out _));
        Assert.Equal(string.Empty, content.Text);
        Assert.Equal(TextFileEncoding.Utf8, content.Encoding);
    }

    [Fact]
    public void Utf32IsRecognisedBeforeUtf16WhoseMarkItBeginsWith()
    {
        // FF FE 00 00 is a UTF-32 LE mark, and also a UTF-16 LE mark followed by
        // a NUL. Reading it the second way would call every UTF-32 file binary.
        Assert.Equal(TextFileEncoding.Utf32LittleEndian,
            TextFileCodec.DetectEncoding(new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x61, 0, 0, 0 }));
    }

    [Fact]
    public void ConvertingAFileGoesThroughTheSameEngineAsTheShortcut()
    {
        var bytes = TextFileCodec.Encode("Espa;a", TextFileEncoding.Utf8);
        Assert.True(TextFileCodec.TryDecode(bytes, out var content, out _));

        var result = Converter.Convert(content.Text, new ConversionOptions
        {
            PrimaryLayout = "es",
            EnabledLayouts = new[] { "es" }
        });

        Assert.Equal("España", result.Text);
        Assert.Equal("España", Encoding.UTF8.GetString(
            TextFileCodec.Encode(result.Text, content.Encoding)));
    }
}
