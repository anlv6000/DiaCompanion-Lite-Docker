using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace DiaCompanion.Api.Services;

public interface IPatientsService
{
    Task<ActionResult<PagedResult<PatientListItemDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] byte? diabetesType,
        [FromQuery] byte? grade,
        [FromQuery] PageQuery page);
    Task<ActionResult<PatientDetailDto>> Get(int id);
    Task<ActionResult<PatientDetailDto>> GetMine();
    Task<ActionResult> Create(CreatePatientRequest req);
    Task<ActionResult<PatientDetailDto>> Update(int id, UpdatePatientRequest req);
    Task<IActionResult> UpdateMine(UpdateMyProfileRequest req);
    Task<IActionResult> RequestPhoneChangeOtp(RequestPhoneChangeOtpRequest req, IWebHostEnvironment env);
    Task<IActionResult> ConfirmPhoneChange(ConfirmPhoneChangeRequest req);
    Task<ActionResult<TempCredentialResponse>> ReissueCredentials(int id);
    Task<IActionResult> Void(int id, VoidRequest req);

    Task<ActionResult<PagedResult<AdminPatientDto>>> AdminList(
    string? q,
    string? status,
    PageQuery page);

    Task<IActionResult> AdminUpdate(
        int id,
        AdminUpdatePatientRequest req);

    Task<IActionResult> SetPatientAccountActive(
        int id,
        bool value,
        ConcurrencyRequest req);
}
