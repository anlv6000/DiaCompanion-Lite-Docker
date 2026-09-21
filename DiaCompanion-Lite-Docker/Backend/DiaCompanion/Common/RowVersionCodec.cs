namespace DiaCompanion.Api.Common;

public static class RowVersionCodec
{
    public static byte[] Decode(string? encodedRowVersion)
    {
        if (string.IsNullOrWhiteSpace(encodedRowVersion))
            throw AppException.BadRequest(
                Msg.RequiredFields,
                "Thiếu RowVersion. Vui lòng tải lại dữ liệu trước khi lưu.");

        byte[] original;
        try
        {
            original = Convert.FromBase64String(encodedRowVersion);
        }
        catch (FormatException)
        {
            throw AppException.BadRequest(
                Msg.InvalidData,
                "RowVersion không hợp lệ. Vui lòng tải lại dữ liệu.");
        }

        if (original.Length != 8)
            throw AppException.BadRequest(
                Msg.InvalidData,
                "RowVersion không hợp lệ. Vui lòng tải lại dữ liệu.");

        return original;
    }
}
