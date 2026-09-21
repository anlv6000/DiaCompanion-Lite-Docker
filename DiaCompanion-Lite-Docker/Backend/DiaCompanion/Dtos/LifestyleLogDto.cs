using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class LifestyleLogDto
{
    public int Id { get; set; }
    public DateOnly LogLocalDate { get; set; }
    public string? MealNote { get; set; }
    public string? MealTags { get; set; }
    public short? ExerciseMinutes { get; set; }
    public string? ExerciseType { get; set; }

    public string RowVersion { get; set; } = "";
}
