using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace DiaCompanion.Api.Services;

public interface IPrescriptionsService
{
    Task<ActionResult<PagedResult<PrescriptionDto>>> List(
        int? patientId,
        string? q,
        DateOnly? from,
        DateOnly? to,
        bool? voided,
        PageQuery page);
    Task<ActionResult<PrescriptionDto>> Get(int id);
    Task<ActionResult<PrescriptionDto>> Create(CreatePrescriptionRequest req);
    Task<ActionResult<PrescriptionDto>> Update(int id, UpdatePrescriptionRequest req);
    Task<IActionResult> Void(int id, VoidRequest req);
    Task<IActionResult> Adherence(
        int patientId,
        int days = 30,
        int? prescriptionId = null,
        DateOnly? from = null,
        DateOnly? to = null);
}
