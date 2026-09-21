using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class CloseVisitRequest
{
    private string _conclusion = "";

    [Required, MaxLength(2000)]
    public string Conclusion
    {
        get => _conclusion;
        set => _conclusion = InputText.TrimRequired(value);
    }

    public ReferralType Referral { get; set; }
        = ReferralType.None;

    /// <summary>
    /// Bỏ trống thì hệ thống suy từ mức DR đã xác nhận theo BR-19.
    /// </summary>
    public byte? RecheckMonths { get; set; }

    // KHÔNG trim RowVersion
    [Required]
    public string RowVersion { get; set; } = "";
}