using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class CreateLifestyleRequest
{
    private string? _mealNote;
    private string? _mealTags;
    private string? _exerciseType;

    public DateOnly? LogLocalDate { get; set; }

    [MaxLength(500)]
    public string? MealNote
    {
        get => _mealNote;
        set => _mealNote = InputText.TrimOptional(value);
    }

    [MaxLength(300)]
    public string? MealTags
    {
        get => _mealTags;
        set => _mealTags = InputText.TrimOptional(value);
    }

    public short? ExerciseMinutes { get; set; }

    [MaxLength(100)]
    public string? ExerciseType
    {
        get => _exerciseType;
        set => _exerciseType = InputText.TrimOptional(value);
    }

    // KHÔNG trim RowVersion
    public string? RowVersion { get; set; }
}