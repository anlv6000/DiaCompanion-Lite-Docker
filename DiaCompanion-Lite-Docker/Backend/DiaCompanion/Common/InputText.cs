namespace DiaCompanion.Common
{
    public class InputText
    {
        /// <summary>
        /// Dùng cho string bắt buộc.
        /// Null -> ""
        /// "   abc   " -> "abc"
        /// </summary>
        public static string TrimRequired(string? value)
            => value?.Trim() ?? "";

        /// <summary>
        /// Dùng cho string optional.
        /// null / "" / "   " -> null
        /// "   abc   " -> "abc"
        /// </summary>
        public static string? TrimOptional(string? value)
            => string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
    }
}
