using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class CreatePatientResponse
{
    public PatientDetailDto Patient { get; set; } = new();
    /// <summary>Chỉ trả về ĐÚNG MỘT LẦN để in phiếu. Không lưu dạng thô ở đâu cả.</summary>
    public TempCredentialResponse? Account { get; set; }
}
