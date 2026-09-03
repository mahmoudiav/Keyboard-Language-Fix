using System.Text;

namespace KeyboardLanguageFix.Core;

/// <summary>How a text file was encoded, so it can be written back the same way.</summary>
public enum TextFileEncoding
{
    /// <summary>UTF-8 with no byte-order mark. What Windows writes by default today.</summary>
    Utf8,

    /// <summary>UTF-8 with a byte-order mark.</summary>
    Utf8WithMark,

    /// <summary>UTF-16, little endian ("Unicode" in the Windows Save dialog).</summary>
    Utf16LittleEndian,

    /// <summary>UTF-16, big endian.</summary>
    Utf16BigEndian,

    /// <summary>UTF-32, little endian.</summary>
    Utf32LittleEndian,

    /// <summary>UTF-32, big endian.</summary>
    Utf32BigEndian
}

/// <summary>Why a file could not be read as text.</summary>
public enum TextFileProblem
{
    /// <summary>It could.</summary>
    None,

    /// <summary>Larger than <see cref="TextFileCodec.MaxBytes"/>.</summary>
    TooLarge,

    /// <summary>Not text: it is binary, or it is in an encoding this cannot read.</summary>
    NotText
}

/// <summary>A file's contents, with enough about its encoding to write it back unchanged.</summary>
/// <param name="Text">The decoded text.</param>
/// <param name="Encoding">How it was encoded.</param>
public readonly record struct TextFileContent(string Text, TextFileEncoding Encoding);

/// <summary>
/// Reading and writing a text file safely enough to rewrite it in place.
/// </summary>
/// <remarks>
/// Deliberately narrow. It reads the Unicode encodings, which is what Notepad,
/// VS Code and every editor on a current Windows produce, and it refuses
/// everything else rather than guessing — a guess here would silently destroy
/// the file it was asked to fix. A legacy code-page file (Windows-1256 Arabic,
/// Windows-1252 Spanish) is rejected with that message, and re-saving it as
/// UTF-8 is the fix.
///
/// This lives in the shared library rather than beside the window that uses it
/// so that it can be tested without a filesystem or a Windows session.
/// </remarks>
public static class TextFileCodec
{
    /// <summary>The largest file this will open. A note typed in the wrong layout is small.</summary>
    public const int MaxBytes = 1024 * 1024;

    private static readonly byte[] Utf8Mark = { 0xEF, 0xBB, 0xBF };
    private static readonly byte[] Utf32LittleMark = { 0xFF, 0xFE, 0x00, 0x00 };
    private static readonly byte[] Utf32BigMark = { 0x00, 0x00, 0xFE, 0xFF };
    private static readonly byte[] Utf16LittleMark = { 0xFF, 0xFE };
    private static readonly byte[] Utf16BigMark = { 0xFE, 0xFF };

    /// <summary>
    /// Decodes <paramref name="bytes"/>, reporting why not rather than throwing.
    /// </summary>
    public static bool TryDecode(byte[] bytes, out TextFileContent content, out TextFileProblem problem)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        content = default;

        if (bytes.Length > MaxBytes)
        {
            problem = TextFileProblem.TooLarge;
            return false;
        }

        var encoding = DetectEncoding(bytes);
        string text;
        try
        {
            text = ToEncoding(encoding, writing: false)
                .GetString(bytes, MarkLength(encoding), bytes.Length - MarkLength(encoding));
        }
        catch (DecoderFallbackException)
        {
            // Not valid in the encoding its own byte-order mark claims, or not
            // UTF-8 when it has no mark at all. Either way, not ours to rewrite.
            problem = TextFileProblem.NotText;
            return false;
        }

        // A NUL in the decoded text is the giveaway for a binary file. Checking
        // after decoding rather than before keeps UTF-16, which is full of zero
        // bytes by design, from being mistaken for one.
        if (text.Contains('\0'))
        {
            problem = TextFileProblem.NotText;
            return false;
        }

        content = new TextFileContent(text, encoding);
        problem = TextFileProblem.None;
        return true;
    }

    /// <summary>Encodes <paramref name="text"/> the way the file it came from was encoded.</summary>
    public static byte[] Encode(string text, TextFileEncoding encoding)
    {
        ArgumentNullException.ThrowIfNull(text);

        var target = ToEncoding(encoding, writing: true);
        return target.GetPreamble().Concat(target.GetBytes(text)).ToArray();
    }

    /// <summary>The encoding a file's byte-order mark declares; UTF-8 when it has none.</summary>
    public static TextFileEncoding DetectEncoding(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        // UTF-32 LE has to be tested before UTF-16 LE: it starts with one.
        if (StartsWith(bytes, Utf32LittleMark)) return TextFileEncoding.Utf32LittleEndian;
        if (StartsWith(bytes, Utf32BigMark)) return TextFileEncoding.Utf32BigEndian;
        if (StartsWith(bytes, Utf8Mark)) return TextFileEncoding.Utf8WithMark;
        if (StartsWith(bytes, Utf16LittleMark)) return TextFileEncoding.Utf16LittleEndian;
        if (StartsWith(bytes, Utf16BigMark)) return TextFileEncoding.Utf16BigEndian;
        return TextFileEncoding.Utf8;
    }

    private static bool StartsWith(byte[] bytes, byte[] prefix) =>
        bytes.Length >= prefix.Length && bytes.AsSpan(0, prefix.Length).SequenceEqual(prefix);

    private static int MarkLength(TextFileEncoding encoding) => encoding switch
    {
        TextFileEncoding.Utf8WithMark => 3,
        TextFileEncoding.Utf16LittleEndian or TextFileEncoding.Utf16BigEndian => 2,
        TextFileEncoding.Utf32LittleEndian or TextFileEncoding.Utf32BigEndian => 4,
        _ => 0
    };

    /// <param name="encoding">The encoding to realise.</param>
    /// <param name="writing">
    /// Whether the encoding is for writing. Reading throws on invalid bytes, so
    /// a file that is not what it claims is refused instead of being decoded
    /// into replacement characters and written back over the original.
    /// </param>
    private static Encoding ToEncoding(TextFileEncoding encoding, bool writing) => encoding switch
    {
        TextFileEncoding.Utf8WithMark => new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: !writing),
        TextFileEncoding.Utf16LittleEndian => new UnicodeEncoding(
            bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: !writing),
        TextFileEncoding.Utf16BigEndian => new UnicodeEncoding(
            bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: !writing),
        TextFileEncoding.Utf32LittleEndian => new UTF32Encoding(
            bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: !writing),
        TextFileEncoding.Utf32BigEndian => new UTF32Encoding(
            bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: !writing),
        _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: !writing)
    };
}
