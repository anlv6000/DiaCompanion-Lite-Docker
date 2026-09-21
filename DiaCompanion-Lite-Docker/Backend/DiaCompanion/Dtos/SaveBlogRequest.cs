using System.ComponentModel.DataAnnotations;
using DiaCompanion.Api.Common;
using DiaCompanion.Common;

namespace DiaCompanion.Api.Dtos;

public class SaveBlogRequest
{
    private string _title = "";
    private string? _summary;
    private string _body = "";

    [Required, MaxLength(300)]
    public string Title
    {
        get => _title;
        set => _title = InputText.TrimRequired(value);
    }

    [MaxLength(500)]
    public string? Summary
    {
        get => _summary;
        set => _summary = InputText.TrimOptional(value);
    }

    [Required]
    public string Body
    {
        get => _body;
        set => _body = InputText.TrimRequired(value);
    }

    public BlogCategory Category { get; set; }
        = BlogCategory.Knowledge;

    /// <summary>
    /// Required for update; ignored when creating a new post.
    /// </summary>
    // KHÔNG trim RowVersion
    public string? RowVersion { get; set; }
}