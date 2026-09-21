using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Common;

public static class ConcurrencyExtensions
{
    public static string ToRowVersion(this IHasRowVersion entity) =>
        Convert.ToBase64String(entity.RowVer);
}
