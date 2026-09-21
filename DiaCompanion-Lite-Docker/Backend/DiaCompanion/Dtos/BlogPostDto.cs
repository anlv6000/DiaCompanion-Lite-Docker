using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;

namespace DiaCompanion.Api.Dtos;



public class BlogPostDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Summary { get; set; }
    public string? Body { get; set; }
    public byte Category { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string AuthorName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string RowVersion { get; set; } = "";
}
