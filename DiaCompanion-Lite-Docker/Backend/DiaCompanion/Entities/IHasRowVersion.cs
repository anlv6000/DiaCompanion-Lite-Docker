namespace DiaCompanion.Api.Entities;

/// <summary>
/// Marker for SQL Server rowversion-backed optimistic concurrency.
/// The raw token never comes from the client directly; API DTOs expose it as Base64.
/// </summary>
public interface IHasRowVersion
{
    byte[] RowVer { get; set; }
}
