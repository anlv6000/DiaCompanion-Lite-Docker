using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Common;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public async Task<BlogPage> GetBlogPageAsync(
        string? q,
        bool? published,
        BlogCategory? category,
        int? authorId,
        PageQuery page,
        bool publishedView,
        CancellationToken ct = default)
    {
        var query = _db.BlogPosts.AsNoTracking().AsQueryable();
        if (published is bool p) query = query.Where(b => b.IsPublished == p);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            query = query.Where(b =>
                EF.Functions.Like(b.Title, $"%{keyword}%")
                || (b.Summary != null && EF.Functions.Like(b.Summary, $"%{keyword}%"))
                || EF.Functions.Like(b.Body, $"%{keyword}%"));
        }
        if (category is BlogCategory c) query = query.Where(b => b.Category == c);
        if (authorId is int aid) query = query.Where(b => b.AuthorId == aid);

        var total = await query.CountAsync(ct);
        query = ApplyBlogSort(query, page, publishedView);
        var items = await query.Include(b => b.Author)
            .Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);
        return new BlogPage(items, total);
    }

    public Task<BlogPost?> GetBlogPostAsync(int id, bool tracking, CancellationToken ct = default)
    {
        IQueryable<BlogPost> query = _db.BlogPosts.Include(x => x.Author).Where(x => x.Id == id);
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(ct);
    }

    private static IQueryable<BlogPost> ApplyBlogSort(IQueryable<BlogPost> query, PageQuery page, bool publishedView) =>
        page.Sort?.Trim().ToLowerInvariant() switch
        {
            "title" => page.Desc
                ? query.OrderByDescending(b => b.Title).ThenByDescending(b => b.Id)
                : query.OrderBy(b => b.Title).ThenBy(b => b.Id),
            "author" => page.Desc
                ? query.OrderByDescending(b => b.Author!.FullName).ThenByDescending(b => b.Id)
                : query.OrderBy(b => b.Author!.FullName).ThenBy(b => b.Id),
            "category" => page.Desc
                ? query.OrderByDescending(b => b.Category).ThenByDescending(b => b.Id)
                : query.OrderBy(b => b.Category).ThenBy(b => b.Id),
            "published" => page.Desc
                ? query.OrderByDescending(b => b.PublishedAt).ThenByDescending(b => b.Id)
                : query.OrderBy(b => b.PublishedAt).ThenBy(b => b.Id),
            _ when publishedView => query.OrderByDescending(b => b.PublishedAt).ThenByDescending(b => b.Id),
            _ => query.OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt).ThenByDescending(b => b.Id)
        };
}
