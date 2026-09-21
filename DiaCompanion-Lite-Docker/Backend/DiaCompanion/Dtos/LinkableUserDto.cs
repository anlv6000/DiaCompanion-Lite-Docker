using DiaCompanion.Api.Common;



namespace DiaCompanion.Dtos
{
    public class LinkableUserDto
    {
        public int Id { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string? Email { get; init; }
        public string? Phone { get; init; }
        public IReadOnlyCollection<string> Roles { get; init; }
            = Array.Empty<string>();

    }
}
