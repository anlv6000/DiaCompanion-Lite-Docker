using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

/* =========================== PATIENTS (UC-12..17) ======================== */

public class PatientListItemDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string FullName { get; set; } = "";
    public int Age { get; set; }
    public byte Gender { get; set; }
    public string Phone { get; set; } = "";
    public byte DiabetesType { get; set; }
    public short? DiabetesDurationYears { get; set; }
    /// <summary>Mức DR đã xác nhận gần nhất — lấy MẮT NẶNG HƠN (BR-21).</summary>
    public byte? LatestDrGrade { get; set; }
    public DateTime? LatestVisitDate { get; set; }
    public bool HasAccount { get; set; }
}
