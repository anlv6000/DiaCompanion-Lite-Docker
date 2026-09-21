using Microsoft.EntityFrameworkCore;
using DiaCompanion.Api.Entities;

namespace DiaCompanion.Api.Repositories;

public sealed partial class EfRepository
{
    public async Task<IReadOnlyList<FundusImage>> GetFundusImagesAsync(int? patientId, int? visitId, CancellationToken ct = default)
    {
        var query = _db.FundusImages.AsNoTracking().AsQueryable().Where(f => (f.VisitId != null ));
        if (patientId is int pid) query = query.Where(f => f.PatientId == pid);
        if (visitId is int vid) query = query.Where(f => f.VisitId == vid);
        return await query.OrderBy(f => f.Eye).ThenByDescending(f => f.CreatedAt).ToListAsync(ct);
    }

    public Task<Visit?> GetVisitForPatientAsync(int visitId, int patientId, bool tracking = false, CancellationToken ct = default)
    {
        IQueryable<Visit> q = _db.Visits.Where(v => v.Id == visitId && v.MedicalRecord.PatientId == patientId);
        if (!tracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(ct);
    }

    public Task<Patient?> GetPatientAsync(int patientId, bool tracking = false, CancellationToken ct = default)
    {
        IQueryable<Patient> q = _db.Patients.Where(p => p.Id == patientId);
        if (!tracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(ct);
    }

    public Task<FundusImage?> GetFundusImageAsync(int id, bool tracking = false, CancellationToken ct = default)
    {
        IQueryable<FundusImage> q = _db.FundusImages.Where(f => f.Id == id);
        if (!tracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(ct);
    }

    public Task<FundusImage?> GetFundusImageWithVisitForUpdateAsync(int id, CancellationToken ct = default) =>
        _db.FundusImages.Include(x => x.Visit).FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> HasDiagnosisForImageAsync(int imageId, CancellationToken ct = default) =>
        _db.AiDiagnoses.AsNoTracking().AnyAsync(x => x.FundusImageId == imageId, ct);
}
