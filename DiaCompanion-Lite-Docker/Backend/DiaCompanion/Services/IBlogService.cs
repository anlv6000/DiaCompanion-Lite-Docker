using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using Microsoft.AspNetCore.Mvc;

namespace DiaCompanion.Api.Services;

public interface IBlogService
{
    Task<ActionResult<PagedResult<BlogPostDto>>> Published(
        string? q, BlogCategory? category, PageQuery page);
    Task<ActionResult<BlogPostDto>> Get(int id);
    Task<ActionResult<PagedResult<BlogPostDto>>> Manage(
        string? q, bool? published, BlogCategory? category, int? authorId, PageQuery page);
    Task<ActionResult<BlogPostDto>> Create(SaveBlogRequest req);
    Task<ActionResult<BlogPostDto>> Update(int id, SaveBlogRequest req);
    Task<IActionResult> Publish(int id, bool value, ConcurrencyRequest req);
    Task<IActionResult> Delete(int id, string rowVersion);
}
