using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class ProgressionPoint
{
    public DateTime Date { get; set; }
    public int? VisitId { get; set; }
    public byte? ConfirmedGrade { get; set; }
    public decimal? FractalDimension { get; set; }
    public decimal? HbA1c { get; set; }
}
