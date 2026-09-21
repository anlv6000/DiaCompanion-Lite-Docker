using System.Globalization;
using System.Text;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Services;

/// <summary>Trình tạo PDF văn bản tối giản, không phụ thuộc thư viện ngoài.</summary>
internal static class SimplePdfDocument
{
    private const int LinesPerPage = 48;

    public static byte[] Create(IEnumerable<string> sourceLines)
    {
        var lines = sourceLines.Select(ToPdfText).ToList();
        var pages = lines
            .Chunk(LinesPerPage)
            .Select(chunk => chunk.ToArray())
            .DefaultIfEmpty(Array.Empty<string>())
            .ToList();

        var pageCount = pages.Count;
        var fontObjectId = 3 + pageCount * 2;
        var objects = new Dictionary<int, byte[]>();

        objects[1] = Ascii("<< /Type /Catalog /Pages 2 0 R >>");
        var kids = string.Join(" ", Enumerable.Range(0, pageCount)
            .Select(i => $"{3 + i} 0 R"));
        objects[2] = Ascii($"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");

        for (var i = 0; i < pageCount; i++)
        {
            var pageObjectId = 3 + i;
            var contentObjectId = 3 + pageCount + i;
            objects[pageObjectId] = Ascii(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] " +
                $"/Resources << /Font << /F1 {fontObjectId} 0 R >> >> " +
                $"/Contents {contentObjectId} 0 R >>");

            var content = BuildContent(pages[i]);
            var contentBytes = Ascii(content);
            objects[contentObjectId] = Combine(
                Ascii($"<< /Length {contentBytes.Length} >>\nstream\n"),
                contentBytes,
                Ascii("\nendstream"));
        }

        objects[fontObjectId] = Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        using var output = new MemoryStream();
        Write(output, "%PDF-1.4\n%DiaCompanion\n");
        var offsets = new long[fontObjectId + 1];

        for (var id = 1; id <= fontObjectId; id++)
        {
            offsets[id] = output.Position;
            Write(output, $"{id} 0 obj\n");
            output.Write(objects[id]);
            Write(output, "\nendobj\n");
        }

        var xrefPosition = output.Position;
        Write(output, $"xref\n0 {fontObjectId + 1}\n");
        Write(output, "0000000000 65535 f \n");
        for (var id = 1; id <= fontObjectId; id++)
            Write(output, $"{offsets[id].ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n");

        Write(output,
            $"trailer\n<< /Size {fontObjectId + 1} /Root 1 0 R >>\n" +
            $"startxref\n{xrefPosition}\n%%EOF");
        return output.ToArray();
    }

    private static string BuildContent(IEnumerable<string> lines)
    {
        var sb = new StringBuilder("BT\n/F1 10 Tf\n50 790 Td\n14 TL\n");
        foreach (var line in lines)
            sb.Append('(').Append(Escape(line)).Append(") Tj\nT*\n");
        sb.Append("ET");
        return sb.ToString();
    }

    private static string ToPdfText(string value)
    {
        var noDiacritics = VietnameseText.RemoveDiacritics(value ?? string.Empty);
        return new string(noDiacritics.Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray());
    }

    private static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace("(", "\\(")
        .Replace(")", "\\)");

    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);

    private static byte[] Combine(params byte[][] parts)
    {
        var result = new byte[parts.Sum(p => p.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }
        return result;
    }

    private static void Write(Stream stream, string value)
    {
        var bytes = Ascii(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}
