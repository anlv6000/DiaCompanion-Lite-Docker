using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Entities;

public class BlogPost : ISoftDeletable, IHasRowVersion
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public User? Author { get; set; }
    [Required, MaxLength(300)] public string Title { get; set; } = "";
    [MaxLength(500)] public string? Summary { get; set; }
    [Required] public string Body { get; set; } = "";
    public BlogCategory Category { get; set; } = BlogCategory.Knowledge;
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public byte[] RowVer { get; set; } = Array.Empty<byte>();
}
