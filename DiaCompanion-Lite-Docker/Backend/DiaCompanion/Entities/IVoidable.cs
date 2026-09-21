namespace DiaCompanion.Api.Entities;

/// <summary>
/// NT-1: hồ sơ lâm sàng chỉ ghi thêm, không xoá vật lý. Thu hồi bằng void
/// kèm lý do và người thực hiện (BR-05). Ràng buộc CK_*_Void ở tầng CSDL
/// bảo đảm không void được mà thiếu lý do.
/// </summary>
public interface IVoidable
{
    bool IsVoided { get; set; }
    string? VoidReason { get; set; }
    int? VoidedBy { get; set; }
    DateTime? VoidedAt { get; set; }
}
