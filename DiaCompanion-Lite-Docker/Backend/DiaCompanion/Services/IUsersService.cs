using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace DiaCompanion.Api.Services;

public interface IUsersService
{
    Task<ActionResult<PagedResult<StaffUserDto>>> List(
        string? q, string? role, bool? isActive,
       PageQuery page);
    Task<ActionResult<StaffUserDto>> Get(int id);
    Task<ActionResult<TempCredentialResponse>> Create(CreateStaffRequest req);
    Task<IActionResult> Update(int id, UpdateStaffRequest req);
    Task<IActionResult> SetActive(int id, bool value, ConcurrencyRequest req);
    Task<ActionResult<TempCredentialResponse>> ResetPassword(int id, ConcurrencyRequest req);
    Task<IActionResult> Doctors();

    Task<IReadOnlyList<LinkableUserDto>> GetLinkableUsersAsync(
    string? keyword,
    CancellationToken ct = default);
}
