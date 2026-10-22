namespace DiaCompanion.Api.Services;

/// <summary>
/// 1.3.1: bỏ metadata khỏi ảnh trước khi đưa vào gói chấm cho máy khác, KHÔNG giải mã lại ảnh
/// (điểm ảnh giữ nguyên từng byte, người chấm thấy đúng ảnh bác sĩ đã nạp).
/// Máy chụp đáy mắt có thể ghi tên/mã bệnh nhân, ngày chụp vào EXIF/XMP/chú thích.
///  - JPEG: bỏ APP1 (EXIF, XMP), APP3–APP13, APP15 và COM. Giữ APP0 (JFIF), APP2 (hồ sơ màu ICC)
///    và APP14 (Adobe, cần để giải mã đúng màu). Dữ liệu ảnh sau SOS chép nguyên.
///  - PNG: bỏ các chunk tEXt, zTXt, iTXt, eXIf, tIME. Các chunk khác chép nguyên (kể cả CRC).
/// Tệp không đúng cấu trúc JPEG/PNG: ném InvalidDataException (người gọi quyết định, không âm thầm chép bản gốc).
/// </summary>
public static class ImageMetadataStripper
{
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly HashSet<string> PngDropChunks = new(StringComparer.Ordinal) { "tEXt", "zTXt", "iTXt", "eXIf", "tIME" };

    /// <summary>Trả bản sao đã bỏ metadata và phần mở rộng phù hợp (".jpg" hoặc ".png").</summary>
    public static (byte[] Bytes, string Extension) Strip(byte[] data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return (StripJpeg(data), ".jpg");
        if (data.Length >= 8 && data.AsSpan(0, 8).SequenceEqual(PngSignature))
            return (StripPng(data), ".png");
        throw new InvalidDataException("Không phải tệp JPEG hoặc PNG.");
    }

    public static byte[] StripJpeg(byte[] d)
    {
        using var o = new MemoryStream(d.Length);
        o.WriteByte(0xFF); o.WriteByte(0xD8);
        var i = 2;
        while (true)
        {
            if (i >= d.Length) throw new InvalidDataException("JPEG bị cắt cụt trước SOS.");
            if (d[i] != 0xFF) throw new InvalidDataException($"JPEG sai cấu trúc tại byte {i}.");
            // Byte 0xFF đệm trước marker.
            while (i < d.Length && d[i] == 0xFF) i++;
            if (i >= d.Length) throw new InvalidDataException("JPEG bị cắt cụt.");
            var marker = d[i++];

            // Marker không có độ dài.
            if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                o.WriteByte(0xFF); o.WriteByte(marker);
                continue;
            }
            if (marker == 0xD9) // EOI trước SOS: ảnh không có dữ liệu
            {
                o.WriteByte(0xFF); o.WriteByte(marker);
                return o.ToArray();
            }

            if (i + 2 > d.Length) throw new InvalidDataException("JPEG bị cắt cụt ở độ dài đoạn.");
            var len = (d[i] << 8) | d[i + 1];
            if (len < 2 || i + len > d.Length) throw new InvalidDataException("Độ dài đoạn JPEG không hợp lệ.");

            var drop = marker == 0xFE                                   // COM
                       || marker == 0xE1                                // APP1: EXIF, XMP
                       || (marker >= 0xE3 && marker <= 0xED)            // APP3–APP13
                       || marker == 0xEF;                               // APP15
            if (!drop)
            {
                o.WriteByte(0xFF); o.WriteByte(marker);
                o.Write(d, i, len);
            }
            i += len;

            if (marker == 0xDA) // SOS: phần còn lại là dữ liệu nén (kèm các marker RST/EOI), chép nguyên
            {
                o.Write(d, i, d.Length - i);
                return o.ToArray();
            }
        }
    }

    public static byte[] StripPng(byte[] d)
    {
        using var o = new MemoryStream(d.Length);
        o.Write(PngSignature);
        var i = 8;
        var sawEnd = false;
        while (i < d.Length)
        {
            if (i + 12 > d.Length) throw new InvalidDataException("PNG bị cắt cụt.");
            var len = (long)((uint)(d[i] << 24) | (uint)(d[i + 1] << 16) | (uint)(d[i + 2] << 8) | d[i + 3]);
            var total = 12 + len; // độ dài 4 + kiểu 4 + dữ liệu + CRC 4
            if (len > int.MaxValue || i + total > d.Length) throw new InvalidDataException("Độ dài chunk PNG không hợp lệ.");
            var type = System.Text.Encoding.ASCII.GetString(d, i + 4, 4);
            if (!PngDropChunks.Contains(type)) o.Write(d, i, (int)total);
            i += (int)total;
            if (type == "IEND") { sawEnd = true; break; }
        }
        if (!sawEnd) throw new InvalidDataException("PNG thiếu chunk IEND.");
        return o.ToArray();
    }
}
