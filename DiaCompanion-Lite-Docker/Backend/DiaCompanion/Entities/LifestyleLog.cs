using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class LifestyleLog : ISoftDeletable, IHasRowVersion
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public DateOnly LogLocalDate { get; set; }
    [MaxLength(500)] public string? MealNote { get; set; }
    [MaxLength(300)] public string? MealTags { get; set; }
    public short? ExerciseMinutes { get; set; }
    [MaxLength(100)] public string? ExerciseType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public byte[] RowVer { get; set; } = Array.Empty<byte>();   
}
