using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class RegisterModelRequest
{
    private string _name = "";
    private string _filePath = "";
    private string _sha256 = "";
    private string? _note;

    [EnumDataType(typeof(ModelType))]
    public ModelType ModelType { get; set; }

    [Required, MaxLength(100)]
    public string Name
    {
        get => _name;
        set => _name = InputText.TrimRequired(value);
    }

    [Required, MaxLength(400)]
    public string FilePath
    {
        get => _filePath;
        set => _filePath = InputText.TrimRequired(value);
    }

    [Required, MinLength(64), MaxLength(64)]
    public string Sha256
    {
        get => _sha256;
        set => _sha256 = InputText.TrimRequired(value);
    }

    public decimal? Qwk { get; set; }

    public decimal? Dice { get; set; }

    public decimal? IoU { get; set; }

    [MaxLength(500)]
    public string? Note
    {
        get => _note;
        set => _note = InputText.TrimOptional(value);
    }
}