using DiaCompanion.Api.Common;


namespace DiaCompanion.Api.Dtos;

public class ModelVersionDto
{
    public int Id { get; set; }
    public byte ModelType { get; set; }
    public string ModelTypeLabel { get; set; } = "";
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public decimal? Qwk { get; set; }
    public decimal? Dice { get; set; }
    public decimal? IoU { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; }
    public bool WasActivated { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public int DiagnosisCount { get; set; }
    public string RowVersion { get; set; } = "";
}
