# DiaCompanion — Backend alignment for UC-33 to UC-61

This package updates the ASP.NET Core 8 backend to align with the use-case descriptions in Report 3 and the project's actual clinical workflow.

## Database upgrade order

1. Back up the `DiaCompanion` database.
2. Run `Database/20260801_AddOptimisticConcurrency.sql` if it has not been applied.
3. Run `Database/20260804_UC33_61_BusinessAlignment.sql`.
4. Rebuild and restart the API.

The second script is idempotent. It adds:

- `PrescriptionItems.IsActive`
- `MedicationLogs.ReminderSentAt`
- `SymptomReports.ResponsibleDoctorId`
- Supporting foreign keys and indexes
- A filtered unique index that permits only one active feedback per patient/visit
- Backfill of responsible Doctor for historical symptom reports using the latest related visit

The script stops instead of deleting data when it detects orphaned or duplicated historical feedback records.

## Use-case implementation matrix

| UC | Implemented backend behavior |
|---|---|
| UC-33 | Only the Doctor assigned through `Visit.DoctorId` can void a diagnosis review. The review is soft-voided, audited, protected by `rowVersion`, and the AI case can return to triage. |
| UC-34 | A completed, Doctor-confirmed examination can be returned as structured JSON or downloaded as a real PDF. A Patient can export only their own visit. |
| UC-35 | Admin can export Doctor-overridden human–AI disagreement cases as JSON or CSV, filtered by model version and date range, with summary indicators. |
| UC-36 | Prescription creation requires a non-voided In Progress visit, matching Patient, and the authenticated assigned Doctor. The authenticated Doctor is stored instead of trusting a client Doctor ID. |
| UC-37 | Prescription details include current and inactive lines, void metadata, adherence summary, and `rowVersion`; voided prescriptions remain readable as clinical history. |
| UC-38 | The assigned Doctor can replace the active item set. Existing IDs are validated, new items use `id = 0`, removed items become inactive, pending schedules are cancelled, processed history is retained, and new schedules are generated. The aggregate uses `rowVersion`. |
| UC-39 | Only the assigned Doctor can void a prescription. The operation is audited, pending doses are cancelled, processed dose history remains, and the response returns the new `rowVersion`. |
| UC-40 | Prescription history supports drug/note search, date and void-state filters, sort, server-side pagination, and per-prescription adherence information. |
| UC-41 | Patient can record glucose, HbA1c, or a complete systolic/diastolic blood-pressure pair with validation and local-date grouping. |
| UC-42 | Patient can update or soft-delete only their own measurements. Blood pressure is treated as a two-row aggregate and requires both version tokens. |
| UC-43 | Authorized actors can view glucose, HbA1c, and blood-pressure trends and latest/abnormal summaries without changing data. Patient access remains restricted to their own profile. |
| UC-44 | A hosted worker creates in-app medication reminders, marks old unconfirmed doses as Missed, and Patient confirms Taken/Skipped/Pending with the latest medication-log `rowVersion`. |
| UC-45 | Adherence returns Taken, Missed, Skipped, Pending, and rate, with optional prescription and date filtering. Cancelled schedules are excluded. |
| UC-46 | Patient creates separate diet/exercise journal entries after validation. |
| UC-47 | Patient updates or soft-deletes only their own lifestyle entries using `rowVersion`; data remains in the database. |
| UC-48 | A hosted worker creates in-app reminders 30, 7, 1, and 0 days before the latest re-screen due date and weekly when overdue. It does not implement self-scheduling/cancellation/rescheduling. |
| UC-49 | Every authenticated user sees only their own non-expired notifications and can mark one or all as read. |
| UC-50 | Patient receives immediate rule-based guidance. The report is linked to the latest assigned Doctor, only that Doctor can view/reply, and the Patient receives a reply notification. |
| UC-51 | Patient feedback validates ownership and completion of the related visit, prevents duplicate active feedback, and is audited. The duplicate legacy Visits endpoint was removed. |
| UC-52 | Admin feedback list supports rating, keyword, date, sort, pagination, Patient reference, and a rating summary. |
| UC-53 | Admin sees system statistics; Doctor sees only assigned-visit scope. Date range and model-version filters apply to defer, referral, override, triage, and grade statistics. |
| UC-54 | Authenticated roles can search/filter/page through published posts only. |
| UC-55 | Admin/Doctor management list supports keyword, state, category, author, sort, and pagination for drafts and published posts. |
| UC-56 | Admin/Doctor create drafts and edit posts with optimistic concurrency, audit, and a newly returned token. |
| UC-57 | Publish, unpublish, and delete are state-aware, audited, and concurrency-protected. Never-published drafts are hard-deleted; previously published posts are soft-deleted. |
| UC-58 | Admin changes thresholds using the latest config `rowVersion`; range validation, audit, and impact preview are retained. |
| UC-59 | Admin lists/registers/activates model versions. SHA-256 and evaluation metrics are validated, at least one evaluation metric is required, activation is transactional, only one model is active, and stale state returns HTTP 409 / MSG-43. |
| UC-60 | Admin can delete only a never-activated, currently inactive, unreferenced model using the current `rowVersion`. |
| UC-61 | Admin-only audit query supports action/entity/user/date filters and keyset pagination. The updated clinical write paths add audit records. |

## Important API changes

### Medication confirmation — UC-44

`PUT /api/monitoring/medications/{id}/status`

```json
{
  "status": 1,
  "rowVersion": "AAAAAAAAhOw="
}
```

Statuses: `0 Pending`, `1 Taken`, `2 Missed`, `3 Cancelled`, `4 Skipped`. Patient requests may set only Pending, Taken, or Skipped.

### Blood-pressure update/delete — UC-42

A blood-pressure response contains both tokens:

```json
{
  "id": 101,
  "rowVersion": "...",
  "pairMetricId": 102,
  "pairRowVersion": "...",
  "systolicValue": 125,
  "diastolicValue": 80
}
```

Update sends `rowVersion` and `pairRowVersion` in `CreateMetricRequest`. Delete sends both in `ConcurrencyRequest`.

### Prescription replacement — UC-38

`PUT /api/prescriptions/{id}` sends the prescription `rowVersion`. An existing line sends its real ID; a new line sends `id: 0`. Existing lines omitted from the request are deactivated and their pending schedules are cancelled.

### Examination PDF — UC-34

- Preview JSON: `GET /api/export/visit-report/{visitId}`
- PDF file: `GET /api/export/visit-report/{visitId}.pdf`

### Dashboard — UC-53

`GET /api/admin/dashboard?from=2026-08-01&to=2026-08-31&modelVersionId=1`

Doctor scope is derived from the JWT and cannot be supplied by the client.

## Runtime notes

- Medication and re-screen reminders are in-app `Notification` records. They are generated while the API process is running; no FCM/APNs transport was added.
- The built-in PDF writer has no external dependency. It uses Helvetica and converts Vietnamese diacritics to ASCII for maximum PDF-reader compatibility.
- All stale concurrency tokens are normalized to HTTP `409` with `MSG-43`.
