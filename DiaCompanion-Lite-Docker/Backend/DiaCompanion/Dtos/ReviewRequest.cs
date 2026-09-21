using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;

public class ReviewRequest
{
    /// <summary>QT-9: bản RowVersion client đã thấy. Lệch nghĩa là có người vừa xử lý.</summary>
    [Required] 
    public string? RowVersion { get; set; }
}
