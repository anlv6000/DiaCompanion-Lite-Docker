using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed record BlogPage(IReadOnlyList<BlogPost> Items, int Total);

public partial interface IRepository
{
    Task<BlogPage> GetBlogPageAsync(
        string? q,
        bool? published,
        BlogCategory? category,
        int? authorId,
        PageQuery page,
        bool publishedView,
        CancellationToken ct = default);
    Task<BlogPost?> GetBlogPostAsync(int id, bool tracking, CancellationToken ct = default);
}
