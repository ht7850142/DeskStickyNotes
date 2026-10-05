using System.IO;
using System.Globalization;
using System.Text;
using DeskStickyNotes.Models;

namespace DeskStickyNotes.Services;

public enum TextFileImportError
{
    None,
    InvalidPath,
    FileNotFound,
    UnsupportedFileType,
    FileTooLarge,
    AccessDenied,
    ReadFailed,
    UnsupportedEncoding,
    BinaryContent
}

public sealed record TextFileImportResult(TextClipModel? Clip, TextFileImportError Error)
{
    public bool IsSuccess => Error == TextFileImportError.None && Clip is not null;

    public static TextFileImportResult Success(TextClipModel clip)
    {
        return new TextFileImportResult(clip, TextFileImportError.None);
    }

    public static TextFileImportResult Failure(TextFileImportError error)
    {
        return new TextFileImportResult(null, error);
    }
}

public sealed class TextFileImportService
{
    public const long MaximumFileSizeBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> SupportedTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".text", ".md", ".markdown", ".rst", ".log",
        ".csv", ".tsv",
        ".json", ".jsonc", ".xml", ".xaml", ".yaml", ".yml", ".toml",
        ".ini", ".cfg", ".conf", ".config", ".properties",
        ".cs", ".csx", ".vb", ".fs", ".fsx",
        ".c", ".h", ".cc", ".cpp", ".cxx", ".hh", ".hpp", ".hxx",
        ".java", ".kt", ".kts", ".go", ".rs", ".swift", ".dart",
        ".js", ".jsx", ".mjs", ".cjs", ".ts", ".tsx",
        ".html", ".htm", ".css", ".scss", ".sass", ".less", ".svg",
        ".py", ".pyw", ".rb", ".php", ".lua", ".r", ".sql",
        ".ps1", ".psm1", ".psd1", ".bat", ".cmd", ".sh", ".bash", ".zsh", ".fish"
    };

    private static readonly HashSet<string> SupportedExtensionlessFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dockerfile",
        "Makefile",
        "README",
        "LICENSE",
        "CHANGELOG",
        ".editorconfig",
        ".env",
        ".gitattributes",
        ".gitignore"
    };

    static TextFileImportService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static IReadOnlySet<string> SupportedExtensions => SupportedTextExtensions;

    public static IReadOnlySet<string> SupportedFileNames => SupportedExtensionlessFileNames;

    public static bool IsSupportedFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        try
        {
            var fileName = Path.GetFileName(filePath);
            var extension = Path.GetExtension(fileName);
            return SupportedTextExtensions.Contains(extension)
                || SupportedExtensionlessFileNames.Contains(fileName);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public TextFileImportResult Import(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return TextFileImportResult.Failure(TextFileImportError.InvalidPath);
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return TextFileImportResult.Failure(TextFileImportError.InvalidPath);
        }

        if (!File.Exists(fullPath))
        {
            return TextFileImportResult.Failure(TextFileImportError.FileNotFound);
        }

        if (!IsSupportedFile(fullPath))
        {
            return TextFileImportResult.Failure(TextFileImportError.UnsupportedFileType);
        }

        byte[] bytes;
        try
        {
            using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 4096,
                FileOptions.SequentialScan);

            if (stream.Length > MaximumFileSizeBytes)
            {
                return TextFileImportResult.Failure(TextFileImportError.FileTooLarge);
            }

            bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
        }
        catch (UnauthorizedAccessException)
        {
            return TextFileImportResult.Failure(TextFileImportError.AccessDenied);
        }
        catch (IOException)
        {
            return TextFileImportResult.Failure(TextFileImportError.ReadFailed);
        }
        catch (NotSupportedException)
        {
            return TextFileImportResult.Failure(TextFileImportError.ReadFailed);
        }

        var decodeResult = Decode(bytes);
        if (decodeResult.Error != TextFileImportError.None)
        {
            return TextFileImportResult.Failure(decodeResult.Error);
        }

        var sourceFileName = Path.GetFileName(fullPath);
        var extension = Path.GetExtension(sourceFileName).ToLowerInvariant();
        var clip = new TextClipModel
        {
            Id = Guid.NewGuid(),
            Title = sourceFileName,
            Content = decodeResult.Content,
            SourceFileName = sourceFileName,
            FileExtension = extension,
            OriginalByteLength = bytes.LongLength,
            ImportedAt = DateTimeOffset.UtcNow
        };

        return TextFileImportResult.Success(clip);
    }

    private static DecodeResult Decode(ReadOnlySpan<byte> bytes)
    {
        Encoding encoding;
        var preambleLength = 0;

        if (HasPrefix(bytes, 0x00, 0x00, 0xFE, 0xFF))
        {
            encoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: true);
            preambleLength = 4;
        }
        else if (HasPrefix(bytes, 0xFF, 0xFE, 0x00, 0x00))
        {
            encoding = new UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: true);
            preambleLength = 4;
        }
        else if (HasPrefix(bytes, 0xEF, 0xBB, 0xBF))
        {
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
            preambleLength = 3;
        }
        else if (HasPrefix(bytes, 0xFE, 0xFF))
        {
            encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true);
            preambleLength = 2;
        }
        else if (HasPrefix(bytes, 0xFF, 0xFE))
        {
            encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true);
            preambleLength = 2;
        }
        else
        {
            return DecodeWithoutPreamble(bytes);
        }

        string content;
        try
        {
            content = encoding.GetString(bytes[preambleLength..]);
        }
        catch (DecoderFallbackException)
        {
            return new DecodeResult("", TextFileImportError.UnsupportedEncoding);
        }

        return LooksLikeBinary(content)
            ? new DecodeResult("", TextFileImportError.BinaryContent)
            : new DecodeResult(content, TextFileImportError.None);
    }

    private static DecodeResult DecodeWithoutPreamble(ReadOnlySpan<byte> bytes)
    {
        if (LooksLikeBinaryBytes(bytes))
        {
            return new DecodeResult("", TextFileImportError.BinaryContent);
        }

        var encodings = new List<Encoding>
        {
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
        };

        AddEncoding(encodings, CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
        AddEncoding(encodings, 54936); // GB18030 covers common GBK/GB2312 text files.

        foreach (var candidate in encodings)
        {
            try
            {
                var content = candidate.GetString(bytes);
                if (!LooksLikeBinary(content))
                {
                    return new DecodeResult(content, TextFileImportError.None);
                }
            }
            catch (DecoderFallbackException)
            {
                // Try the next strict decoder.
            }
        }

        return new DecodeResult("", TextFileImportError.UnsupportedEncoding);
    }

    private static void AddEncoding(List<Encoding> encodings, int codePage)
    {
        if (encodings.Any(encoding => encoding.CodePage == codePage))
        {
            return;
        }

        try
        {
            encodings.Add(Encoding.GetEncoding(
                codePage,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback));
        }
        catch (ArgumentException)
        {
            // The optional code page is unavailable on this Windows installation.
        }
        catch (NotSupportedException)
        {
            // The optional code page is unavailable on this Windows installation.
        }
    }

    private static bool HasPrefix(ReadOnlySpan<byte> bytes, params byte[] prefix)
    {
        return bytes.StartsWith(prefix);
    }

    private static bool LooksLikeBinary(string content)
    {
        if (content.Contains('\0'))
        {
            return true;
        }

        var suspiciousControlCharacters = 0;
        foreach (var character in content)
        {
            if (char.IsControl(character)
                && character is not '\r' and not '\n' and not '\t' and not '\f' and not '\b')
            {
                suspiciousControlCharacters++;
            }
        }

        return suspiciousControlCharacters >= 4
            && suspiciousControlCharacters * 100L > Math.Max(1, content.Length) * 5L;
    }

    private static bool LooksLikeBinaryBytes(ReadOnlySpan<byte> bytes)
    {
        var suspiciousBytes = 0;
        foreach (var value in bytes)
        {
            if (value == 0)
            {
                return true;
            }

            if (value < 0x08 || value is > 0x0D and < 0x20)
            {
                suspiciousBytes++;
            }
        }

        return suspiciousBytes >= 4
            && suspiciousBytes * 100L > Math.Max(1, bytes.Length) * 5L;
    }

    private readonly record struct DecodeResult(string Content, TextFileImportError Error);
}
