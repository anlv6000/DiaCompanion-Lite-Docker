using DiaCompanion.Api.Common;
using DiaCompanion.Api.Dtos;
using DiaCompanion.Api.Entities;
using DiaCompanion.Api.Repositories;
using DiaCompanion.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace DiaCompanion.Api.Services;

/// <summary>UC-12..17 — nghiệp vụ hồ sơ bệnh nhân. Không truy cập EF/DbContext trực tiếp.</summary>
public class PatientsService : BaseService, IPatientsService
{
    private readonly IRepository _repository;
    private readonly ICurrentUser _me;
    private readonly IAuditService _audit;
    private readonly IPasswordHasher _hasher;
    private readonly IVoidService _void;
    private readonly IClinicClock _clock;
    private readonly IOtpService _otp;

    public PatientsService(
        IRepository repository,
        ICurrentUser me,
        IAuditService audit,
        IPasswordHasher hasher,
        IVoidService voidSvc,
        IClinicClock clock,
        IOtpService otp)
    {
        _repository = repository;
        _me = me;
        _audit = audit;
        _hasher = hasher;
        _void = voidSvc;
        _clock = clock;
        _otp = otp;
    }

    public async Task<ActionResult<PagedResult<PatientListItemDto>>> Search(
        [FromQuery] string? q,
        [FromQuery] byte? diabetesType,
        [FromQuery] byte? grade,
        [FromQuery] PageQuery page)
    {
        string? normalized = null;
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            if (q.Length < 2)
                throw AppException.BadRequest(Msg.RequiredFields, "Vui lòng nhập tối thiểu 2 ký tự để tìm kiếm.");
            normalized = VietnameseText.RemoveDiacritics(q);
        }

        var result = await _repository.SearchPatientsAsync(normalized, q, diabetesType, grade, page);
        var today = _clock.LocalToday;
        var items = result.Items.Select(r => new PatientListItemDto
        {
            Id = r.Id,
            Code = r.Code,
            FullName = r.FullName,
            Age = today.Year - r.DateOfBirth.Year,
            Gender = r.Gender,
            Phone = r.Phone,
            DiabetesType = r.DiabetesType,
            DiabetesDurationYears = r.DiabetesDurationYears,
            LatestDrGrade = r.LatestDrGrade,
            LatestVisitDate = _clock.ToLocal(r.LatestVisitDate),
            HasAccount = r.HasAccount
        }).ToList();

        return Ok(new PagedResult<PatientListItemDto>
        {
            Items = items,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalItems = result.Total
        });
    }

    public async Task<ActionResult<PatientDetailDto>> Get(int id)
    {
        var patient = await _repository.GetPatientAsync(id)
            ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy hồ sơ bệnh nhân.");
        return Ok(await ToDetailAsync(patient));
    }

    public async Task<ActionResult<PatientDetailDto>> GetMine()
    {
        var patientId = RequireMyPatientId(_me);
        var patient = await _repository.GetPatientAsync(patientId)
            ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy hồ sơ bệnh nhân.");
        return Ok(await ToDetailAsync(patient));
    }

    public async Task<ActionResult> Create(CreatePatientRequest req)
    {
        var phone = NormalizePhone(req.Phone);

        // 1. Validate toàn bộ request trước khi bắt đầu transaction.
        await ValidateCreatePatientAsync(req, phone);

        CreatePatientResponse? response = null;

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            // 2. Tạo entity Patient.
            var patient = await BuildPatientAsync(req, phone);

            // 3. Nếu cần tài khoản:
            //    - link User có sẵn
            //    - hoặc tạo User Patient mới.
            TempCredentialResponse? credential = null;

            if (req.CreateAccount)
            {
                credential = await PreparePatientAccountAsync(
                    req,
                    patient,
                    phone);
            }

            // 4. Lưu Patient.
            _repository.Add(patient);
            await _repository.CommitAsync();

            // 5. Audit.
            await _audit.LogAsync(
                AuditAction.PatientCreate,
                nameof(Patient),
                patient.Id,
                null,
                new
                {
                    patient.Code,
                    patient.FullName,
                    patient.Phone,
                    hasAccount = req.CreateAccount
                });

            await _repository.CommitAsync();

            // 6. Response.
            response = new CreatePatientResponse
            {
                Patient = await ToDetailAsync(patient),
                Account = credential
            };
        });

        return CreatedAtAction(
            nameof(Get),
            new { id = response!.Patient.Id },
            response);
    }


    private async Task ValidateCreatePatientAsync(
    CreatePatientRequest req,
    string phone)
    {
        // ------------------------------------------------------------
        // Phone của hồ sơ Patient phải duy nhất.
        // ------------------------------------------------------------
        if (await _repository.PatientPhoneExistsAsync(phone))
        {
            throw AppException.Conflict(
                Msg.PhoneTaken,
                "Số điện thoại này đã được dùng cho một hồ sơ khác. " +
                "Mỗi bệnh nhân cần một số riêng vì đây là định danh đăng nhập.");
        }
        if (!req.CreateAccount &&
            req.ExistingUserId.HasValue)
        {
            throw AppException.BadRequest(
                Msg.InvalidData,
                "Không được chọn tài khoản có sẵn khi không yêu cầu tạo/liên kết tài khoản.");
        }
        // ------------------------------------------------------------
        // Nếu đang tạo User mới thì Phone cũng chưa được thuộc User khác.
        //
        // ExistingUserId != null thì không check ở đây,
        // vì User được chọn có thể chính là chủ sở hữu Phone đó.
        // ------------------------------------------------------------
        if (req.CreateAccount
            && req.ExistingUserId is null
            && await _repository.UserPhoneExistsAsync(phone))
        {
            throw AppException.Conflict(
                Msg.PhoneTaken,
                "Số điện thoại này đã được dùng cho tài khoản khác.");
        }

        // ------------------------------------------------------------
        // Validate HbA1c.
        // ------------------------------------------------------------
        if (req.BaselineHbA1c is decimal hba1c
            && (hba1c < 3 || hba1c > 20))
        {
            throw AppException.BadRequest(
                Msg.RequiredFields,
                "HbA1c ban đầu phải nằm trong khoảng từ 3% đến 20%.");
        }

        //dateOfbirth
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (req.DateOfBirth > today)
        {

            throw AppException.BadRequest(
                Msg.RequiredFields,
                "Ngày sinh không được lớn hơn ngày hiện tại.");
        }

        // ------------------------------------------------------------
        // Nếu cần cấp/link account thì Role Patient phải tồn tại
        // và đang active ở bảng Roles.
        // ------------------------------------------------------------
        if (req.CreateAccount)
        {
            var roleNames =
                await _repository.GetActiveRoleNamesByNamesAsync(
                    new[] { Roles.Patient });

            if (!roleNames.Contains(
                    Roles.Patient,
                    StringComparer.OrdinalIgnoreCase))
            {
                throw AppException.Conflict(
                    Msg.RequiredFields,
                    "Vai trò Patient chưa tồn tại hoặc đang bị khóa trong cơ sở dữ liệu.");
            }
        }
    }


    private async Task<Patient> BuildPatientAsync(
    CreatePatientRequest req,
    string phone)
    {
        return new Patient
        {
            Code = await NextCodeAsync(),

            FullName = req.FullName.Trim(),

            Gender = req.Gender,

            DateOfBirth = req.DateOfBirth,

            Phone = phone,

            Address = req.Address,

            DiabetesType = req.DiabetesType,

            DiabetesDurationYears =
                req.DiabetesDurationYears,

            BaselineHbA1c =
                req.BaselineHbA1c,

            Note = req.Note,

            CreatedBy = _me.RequireId()
        };
    }


    private async Task<TempCredentialResponse?> PreparePatientAccountAsync(
    CreatePatientRequest req,
    Patient patient,
    string phone)
    {
        // ------------------------------------------------------------
        // ExistingUserId có giá trị:
        // dùng User đã tồn tại.
        //
        // Đây chính là trường hợp:
        // Doctor đã có account -> thêm Patient role.
        // ------------------------------------------------------------
        if (req.ExistingUserId is int existingUserId)
        {
            await LinkExistingUserToPatientAsync(
                existingUserId,
                patient,
                phone);

            // User cũ đã có password rồi,
            // nên KHÔNG tạo mật khẩu tạm.
            return null;
        }

        // ------------------------------------------------------------
        // Không có ExistingUserId:
        // tạo User Patient mới.
        // ------------------------------------------------------------
        return await CreateNewPatientUserAsync(
            patient,
            phone);
    }
    private async Task LinkExistingUserToPatientAsync(
    int existingUserId,
    Patient patient,
    string phone)
    {
        // ------------------------------------------------------------
        // 1. User phải tồn tại.
        // ------------------------------------------------------------
        var linkedUser =
            await _repository.GetUserByIdAsync(existingUserId);

        if (linkedUser is null)
        {
            throw AppException.NotFound(
                Msg.RequiredFields,
                "Tài khoản được chọn không tồn tại.");
        }

        var isLinkable =
    await _repository.IsUserLinkableToPatientAsync(
        existingUserId,
        _me.RequireId());

        if (!isLinkable)
        {
            throw AppException.Conflict(
                Msg.InvalidData,
                "Tài khoản này không đủ điều kiện để liên kết với hồ sơ bệnh nhân.");
        }
        // ------------------------------------------------------------
        // 2. Một User chỉ được liên kết với một Patient active.
        // ------------------------------------------------------------
        var alreadyHasPatient =
            await _repository.UserAlreadyLinkedToActivePatientAsync(
                existingUserId);

        if (alreadyHasPatient)
        {
            throw AppException.Conflict(
                Msg.RequiredFields,
                "Tài khoản này đã được liên kết với một hồ sơ bệnh nhân.");
        }


        // ------------------------------------------------------------
        // 3. Đồng bộ Phone.
        //
        // User chưa có Phone:
        // -> lấy Phone của hồ sơ Patient.
        //
        // User đã có Phone:
        // -> bắt buộc phải giống Phone Patient.
        // ------------------------------------------------------------
        if (string.IsNullOrWhiteSpace(linkedUser.Phone))
        {
            var phoneTaken =
                await _repository.UserPhoneExistsExceptUserAsync(
                    phone,
                    linkedUser.Id);

            if (phoneTaken)
            {
                throw AppException.Conflict(
                    Msg.PhoneTaken,
                    "Số điện thoại đã được sử dụng bởi tài khoản khác.");
            }

            linkedUser.Phone = phone;
        }
        else if (!string.Equals(
                     linkedUser.Phone,
                     phone,
                     StringComparison.Ordinal))
        {
            throw AppException.Conflict(
                Msg.PhoneTaken,
                "Số điện thoại hồ sơ bệnh nhân không khớp với tài khoản được chọn.");
        }


        // ------------------------------------------------------------
        // 4. Gán thêm Patient role.
        //
        // QUAN TRỌNG:
        // EnsureUserRoleActiveAsync chỉ thêm/reactivate Patient.
        // Không được xóa Doctor.
        // ------------------------------------------------------------
        var patientRoleReady =
            await _repository.EnsureUserRoleActiveAsync(
                linkedUser,
                Roles.Patient,
                _me.RequireId());

        if (!patientRoleReady)
        {
            throw AppException.Conflict(
                Msg.RequiredFields,
                "Vai trò Patient chưa tồn tại hoặc đang bị khóa.");
        }


        // ------------------------------------------------------------
        // 5. Liên kết Patient với chính User đó.
        // ------------------------------------------------------------
        patient.UserId = linkedUser.Id;
    }

    private async Task<TempCredentialResponse> CreateNewPatientUserAsync(
    Patient patient,
    string phone)
    {
        // ------------------------------------------------------------
        // Check lại trong transaction để tránh race condition
        // giữa lúc validate và lúc insert.
        // ------------------------------------------------------------
        if (await _repository.UserPhoneExistsAsync(phone))
        {
            throw AppException.Conflict(
                Msg.PhoneTaken,
                "Số điện thoại này đã được dùng cho tài khoản khác.");
        }


        // ------------------------------------------------------------
        // Sinh mật khẩu tạm.
        // ------------------------------------------------------------
        var tempPassword =
            _hasher.GenerateTempPassword();


        // ------------------------------------------------------------
        // Tạo User mới.
        // ------------------------------------------------------------
        var user = new User
        {
            Phone = phone,

            PasswordHash =
                _hasher.Hash(tempPassword),

            FullName =
                patient.FullName,

            MustChangePassword = true
        };

        _repository.Add(user);

        // Cần save để SQL sinh User.Id.
        await _repository.CommitAsync();


        // ------------------------------------------------------------
        // Gán Patient role.
        // ------------------------------------------------------------
        var patientRoleReady =
            await _repository.EnsureUserRoleActiveAsync(
                user,
                Roles.Patient,
                _me.RequireId());

        if (!patientRoleReady)
        {
            throw AppException.Conflict(
                Msg.RequiredFields,
                "Vai trò Patient chưa tồn tại hoặc đang bị khóa.");
        }


        // ------------------------------------------------------------
        // Liên kết Patient -> User.
        // ------------------------------------------------------------
        patient.UserId = user.Id;


        // ------------------------------------------------------------
        // Chỉ User mới tạo mới có mật khẩu tạm.
        // ------------------------------------------------------------
        return new TempCredentialResponse
        {
            LoginId = phone,

            TempPassword = tempPassword,

            Note =
                "Mật khẩu tạm chỉ hiển thị một lần. " +
                "In cho bệnh nhân; hệ thống sẽ bắt đổi mật khẩu " +
                "ở lần đăng nhập đầu tiên."
        };
    }
    public async Task<ActionResult<PatientDetailDto>> Update(int id, UpdatePatientRequest req)
    {
        var patient = await _repository.GetPatientAsync(id, tracking: true)
            ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy hồ sơ bệnh nhân.");
        _repository.ApplyOriginalRowVersion(patient, req.RowVersion);

        var phone = NormalizePhone(req.Phone);
        if (phone != patient.Phone && await _repository.PatientPhoneExistsAsync(phone, id))
            throw AppException.Conflict(Msg.PhoneTaken, "Số điện thoại này đã được dùng cho một hồ sơ khác.");
        if (phone != patient.Phone && await _repository.UserPhoneExistsAsync(phone, patient.UserId))
            throw AppException.Conflict(Msg.PhoneTaken, "Số điện thoại này đã được dùng cho tài khoản khác.");
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (req.DateOfBirth > today)
        {
            throw AppException.BadRequest(
                Msg.RequiredFields,
                "Ngày sinh không được lớn hơn ngày hiện tại.");
        }
        var before = new { patient.FullName, patient.Phone, patient.Address, patient.DiabetesType, patient.BaselineHbA1c };
        patient.FullName = req.FullName.Trim();
        patient.Gender = req.Gender;
        patient.DateOfBirth = req.DateOfBirth;
        patient.Phone = phone;
        patient.Address = req.Address;
        patient.DiabetesType = req.DiabetesType;
        patient.DiabetesDurationYears = req.DiabetesDurationYears;
        patient.BaselineHbA1c = req.BaselineHbA1c;
        patient.Note = req.Note;
        patient.UpdatedAt = _clock.UtcNow;

        if (patient.UserId is int userId)
        {
            var user = await _repository.GetUserForUpdateAsync(userId);
            if (user is not null)
            {
                user.Phone = phone;
                user.FullName = patient.FullName;
                user.UpdatedAt = _clock.UtcNow;
            }
        }

        await _audit.LogAsync(
            AuditAction.PatientUpdate,
            nameof(Patient),
            patient.Id,
            before,
            new { patient.FullName, patient.Phone, patient.Address, patient.DiabetesType, patient.BaselineHbA1c });
        await _repository.CommitAsync();

        return Ok(await ToDetailAsync(patient));
    }

    public async Task<IActionResult> UpdateMine(UpdateMyProfileRequest req)
    {
        var patientId = RequireMyPatientId(_me);
        var patient = await _repository.GetPatientAsync(patientId, tracking: true)
            ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy hồ sơ bệnh nhân.");
        _repository.ApplyOriginalRowVersion(patient, req.RowVersion);

        var fullName = req.FullName.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            throw AppException.BadRequest(Msg.RequiredFields, "Họ tên không được để trống.");
        if (req.DateOfBirth > DateOnly.FromDateTime(_clock.LocalNow.Date))
            throw AppException.BadRequest(Msg.RequiredFields, "Ngày sinh không được nằm trong tương lai.");

        var before = new { patient.FullName, patient.Gender, patient.DateOfBirth, patient.Address };
        patient.FullName = fullName;
        patient.FullNameSearch = VietnameseText.RemoveDiacritics(fullName);
        patient.Gender = req.Gender;
        patient.DateOfBirth = req.DateOfBirth;
        patient.Address = string.IsNullOrWhiteSpace(req.Address) ? null : req.Address.Trim();
        patient.UpdatedAt = _clock.UtcNow;

        if (patient.UserId is int userId)
        {
            var user = await _repository.GetUserForUpdateAsync(userId);
            if (user is not null)
            {
                user.FullName = fullName;
                user.UpdatedAt = _clock.UtcNow;
            }
        }

        await _audit.LogAsync(
            AuditAction.PatientUpdate,
            nameof(Patient),
            patient.Id,
            before,
            new { patient.FullName, patient.Gender, patient.DateOfBirth, patient.Address });
        await _repository.CommitAsync();

        return Ok(new { message = "Cập nhật thông tin cá nhân thành công.", rowVersion = patient.ToRowVersion() });
    }

    public async Task<IActionResult> RequestPhoneChangeOtp(RequestPhoneChangeOtpRequest req, IWebHostEnvironment env)
    {
        var patientId = RequireMyPatientId(_me);
        var patient = await _repository.GetPatientAsync(patientId)
            ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy hồ sơ bệnh nhân.");
        var newPhone = NormalizePhone(req.NewPhone);

        if (newPhone == patient.Phone)
            throw AppException.BadRequest(Msg.RequiredFields, "Số điện thoại mới phải khác số đang sử dụng.");

        await EnsurePhoneAvailableAsync(newPhone, patientId, patient.UserId);
        var code = await _otp.IssueAsync(newPhone, OtpPurpose.ChangePhone, patient.UserId);

        await _audit.LogAsync(
            AuditAction.OtpIssued,
            nameof(Patient),
            patient.Id,
            detail: $"Cấp OTP đổi số điện thoại sang {MaskPhone(newPhone)}");
        await _repository.CommitAsync();

        return Ok(new
        {
            message = "Mã xác minh đã được gửi tới số điện thoại mới.",
            devCode = env.IsDevelopment() ? code : null,
            note = env.IsDevelopment() ? "Mã chỉ được trả trực tiếp trong môi trường Development." : null
        });
    }

    public async Task<IActionResult> ConfirmPhoneChange(ConfirmPhoneChangeRequest req)
    {
        return await _repository.ExecuteInTransactionAsync<IActionResult>(async () =>
        {
            var patientId = RequireMyPatientId(_me);
            var patient = await _repository.GetPatientAsync(patientId, tracking: true)
                ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy hồ sơ bệnh nhân.");
            _repository.ApplyOriginalRowVersion(patient, req.RowVersion);

            var newPhone = NormalizePhone(req.NewPhone);
            if (newPhone == patient.Phone)
                throw AppException.BadRequest(Msg.RequiredFields, "Số điện thoại mới phải khác số đang sử dụng.");

            await EnsurePhoneAvailableAsync(newPhone, patientId, patient.UserId);
            if (!await _otp.VerifyAsync(newPhone, req.Code.Trim(), OtpPurpose.ChangePhone))
                throw AppException.BadRequest(Msg.OtpInvalid, "Mã xác minh không đúng hoặc đã hết hạn.");

            var oldPhone = patient.Phone;
            patient.Phone = newPhone;
            patient.UpdatedAt = _clock.UtcNow;

            if (patient.UserId is int userId)
            {
                var user = await _repository.GetUserForUpdateAsync(userId)
                    ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy tài khoản liên kết với bệnh nhân.");
                user.Phone = newPhone;
                user.UpdatedAt = _clock.UtcNow;
            }

            await _audit.LogAsync(
                AuditAction.PatientPhoneChange,
                nameof(Patient),
                patient.Id,
                new { Phone = MaskPhone(oldPhone) },
                new { Phone = MaskPhone(newPhone) });
            await _repository.CommitAsync();

            return Ok(new
            {
                message = "Đổi số điện thoại thành công. Từ lần đăng nhập sau, hãy dùng số mới.",
                phone = newPhone,
                rowVersion = patient.ToRowVersion()
            });
        });
    }

    public async Task<ActionResult<TempCredentialResponse>> ReissueCredentials(int id)
    {
        var patient = await _repository.GetPatientAsync(id, tracking: true)
            ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy hồ sơ bệnh nhân.");
        var temp = _hasher.GenerateTempPassword();

        await _repository.ExecuteInTransactionAsync(async () =>
        {
            User user;
            if (patient.UserId is int userId)
            {
                user = await _repository.GetUserForUpdateAsync(userId)
                    ?? throw AppException.NotFound(Msg.PatientNotFound, "Không tìm thấy tài khoản liên kết với bệnh nhân.");
                user.PasswordHash = _hasher.Hash(temp);
                user.MustChangePassword = true;
                user.Phone = patient.Phone;
                user.FullName = patient.FullName;
                user.UpdatedAt = _clock.UtcNow;
            }
            else
            {
                if (await _repository.UserPhoneExistsAsync(patient.Phone))
                    throw AppException.Conflict(Msg.PhoneTaken, "Số điện thoại này đã được dùng cho tài khoản khác.");

                user = new User
                {
                    Phone = patient.Phone,
                    PasswordHash = _hasher.Hash(temp),
                    FullName = patient.FullName,
                    MustChangePassword = true
                };
                _repository.Add(user);
                await _repository.CommitAsync();
                patient.UserId = user.Id;
            }

            if (!await _repository.EnsureUserRoleActiveAsync(user, Roles.Patient, _me.RequireId()))
                throw AppException.Conflict(Msg.RequiredFields, "Vai trò Patient chưa tồn tại hoặc đang bị khóa trong cơ sở dữ liệu.");

            await _audit.LogAsync(AuditAction.PasswordReset, nameof(Patient), patient.Id,
                detail: "Cấp lại mật khẩu tạm tại quầy");
            await _repository.CommitAsync();
        });

        return Ok(new TempCredentialResponse
        {
            LoginId = patient.Phone,
            TempPassword = temp,
            Note = "Mật khẩu tạm chỉ hiển thị một lần. Bệnh nhân phải đổi ở lần đăng nhập đầu.",
            RowVersion = patient.ToRowVersion()
        });
    }

    public async Task<IActionResult> Void(int id, VoidRequest req)
    {
        await _void.VoidPatientAsync(id, req.Reason, req.RowVersion);
        return Ok(new { message = "Đã thu hồi hồ sơ bệnh nhân." });
    }

    private async Task EnsurePhoneAvailableAsync(string phone, int patientId, int? userId)
    {
        if (await _repository.PatientPhoneExistsAsync(phone, patientId))
            throw AppException.Conflict(Msg.PhoneTaken, "Số điện thoại này đã được dùng cho một hồ sơ bệnh nhân khác.");
        if (await _repository.UserPhoneExistsAsync(phone, userId))
            throw AppException.Conflict(Msg.PhoneTaken, "Số điện thoại này đã được dùng cho tài khoản khác.");
    }

    private async Task<string> NextCodeAsync()
    {
        var prefix = $"BN{_clock.LocalToday.Year}";
        var last = await _repository.GetLastPatientCodeAsync(prefix);
        var sequence = last is null ? 1 : int.Parse(last[prefix.Length..]) + 1;
        return $"{prefix}{sequence:D4}";
    }

    private async Task<PatientDetailDto> ToDetailAsync(Patient patient)
    {
        var stats = await _repository.GetPatientDetailStatsAsync(patient.Id);
        var today = _clock.LocalToday;
        return new PatientDetailDto
        {
            Id = patient.Id,
            Code = patient.Code,
            FullName = patient.FullName,
            Age = today.Year - patient.DateOfBirth.Year,
            Gender = patient.Gender,
            Phone = patient.Phone,
            Address = patient.Address,
            DateOfBirth = patient.DateOfBirth,
            DiabetesType = patient.DiabetesType,
            DiabetesDurationYears = patient.DiabetesDurationYears,
            BaselineHbA1c = patient.BaselineHbA1c,
            Note = patient.Note,
            CreatedAt = _clock.ToLocal(patient.CreatedAt)!.Value,
            HasAccount = patient.UserId != null,
            LatestDrGrade = stats.LatestDrGrade,
            DoctorInCharge = stats.DoctorInCharge,
            VisitCount = stats.VisitCount,
            RowVersion = patient.ToRowVersion()
        };
    }

    private static string NormalizePhone(string value)
    {
        var phone = value.Trim();

        if (phone.Length < 10 ||
            phone.Length > 11 ||
            phone.Any(c => !char.IsDigit(c)))
        {
            throw AppException.BadRequest(
                Msg.RequiredFields,
                "Số điện thoại phải gồm 10 đến 11 chữ số.");
        }
        return phone;
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length <= 4) return new string('*', phone.Length);
        return new string('*', phone.Length - 4) + phone[^4..];
    }


    public async Task<ActionResult<PagedResult<AdminPatientDto>>> AdminList(
    string? q,
    string? status,
    PageQuery page)
    {
        var result =
            await _repository.GetAdminPatientPageAsync(
                q,
                status,
                page);

        var items = result.Items
            .Select(x => new AdminPatientDto
            {
                Id = x.Patient.Id,

                UserId = x.User?.Id,

                Code = x.Patient.Code,

                FullName = x.Patient.FullName,

                Gender = (byte)x.Patient.Gender,

                Phone = x.Patient.Phone,

                Address = x.Patient.Address,

                HasAccount = x.User is not null,

                // Đây mới là trạng thái tài khoản Patient.
                IsActive = x.PatientRoleIsActive,

                PatientRowVersion =
                    x.Patient.ToRowVersion(),

                AccountRowVersion =
                    x.User?.ToRowVersion()
            })
            .ToList();

        return Ok(new PagedResult<AdminPatientDto>
        {
            Items = items,

            Page = page.Page,

            PageSize = page.PageSize,

            TotalItems = result.Total
        });
    }
    public async Task<IActionResult> AdminUpdate(
        int id,
        AdminUpdatePatientRequest req)
    {
        // ============================================================
        // LOAD
        // ============================================================

        var target =
            await _repository.GetPatientAdminTargetAsync(
                id,
                tracking: true)
            ?? throw AppException.NotFound(
                Msg.PatientNotFound,
                "Không tìm thấy hồ sơ bệnh nhân.");

        var patient = target.Patient;


        // ============================================================
        // VALIDATE
        // ============================================================

        var fullName = req.FullName?.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw AppException.BadRequest(
                Msg.RequiredFields,
                "Họ tên bệnh nhân không được để trống.");
        }

        // Nếu Gender hiện tại của bạn dùng 0,1,2.
        if (req.Gender > 2)
        {
            throw AppException.BadRequest(
                Msg.InvalidData,
                "Giới tính không hợp lệ.");
        }


        // ============================================================
        // CONCURRENCY
        // ============================================================

        _repository.ApplyOriginalRowVersion(
            patient,
            req.RowVersion);


        // ============================================================
        // UPDATE PATIENT
        // ============================================================

        var before = new
        {
            patient.FullName,
            patient.Gender,
            patient.Address
        };

        patient.FullName = fullName;

        patient.Gender =
            req.Gender; // nếu entity Gender là enum

        patient.Address =
            string.IsNullOrWhiteSpace(req.Address)
                ? null
                : req.Address.Trim();

        patient.UpdatedAt = _clock.UtcNow;


        // ============================================================
        // ĐỒNG BỘ TÊN USER
        // ============================================================

        if (target.User is not null)
        {
            /*
             * Patient và User là cùng một người.
             * Vì vậy khi sửa tên Patient thì đồng bộ User.FullName.
             *
             * Doctor + Patient cũng vẫn chỉ có một User.
             */
            target.User.FullName = patient.FullName;
            target.User.UpdatedAt = _clock.UtcNow;
        }


        // ============================================================
        // AUDIT + SAVE
        // ============================================================

        await _audit.LogAsync(
            AuditAction.PatientUpdate,
            nameof(Patient),
            patient.Id,
            before,
            new
            {
                patient.FullName,
                patient.Gender,
                patient.Address
            });

        await _repository.CommitAsync();


        return Ok(new
        {
            message = "Đã cập nhật thông tin bệnh nhân.",

            rowVersion =
                patient.ToRowVersion()
        });
    }
    public async Task<IActionResult> SetPatientAccountActive(
    int id,
    bool value,
    ConcurrencyRequest req)
    {
        // ============================================================
        // LOAD
        // ============================================================

        var target =
            await _repository.GetPatientAdminTargetAsync(
                id,
                tracking: true)
            ?? throw AppException.NotFound(
                Msg.PatientNotFound,
                "Không tìm thấy hồ sơ bệnh nhân.");


        if (target.User is null)
        {
            throw AppException.BadRequest(
                Msg.InvalidData,
                "Bệnh nhân chưa có tài khoản đăng nhập.");
        }


        if (target.PatientRole is null)
        {
            throw AppException.BadRequest(
                Msg.InvalidData,
                "Tài khoản chưa được gán role Patient.");
        }


        var user = target.User;
        var patientRole = target.PatientRole;


        // ============================================================
        // CONCURRENCY
        // ============================================================

        _repository.ApplyOriginalRowVersion(
            user,
            req.RowVersion);


        // ============================================================
        // CHỈ ĐỔI PATIENT ROLE
        // ============================================================

        var oldValue =
            patientRole.IsActive;

        patientRole.IsActive =
            value;


        /*
         * Không đụng Doctor / Receptionist / Admin.
         *
         * Touch User để User.RowVersion thay đổi phục vụ
         * optimistic concurrency, KHÔNG sửa Users.IsActive.
         */
        user.UpdatedAt = _clock.UtcNow;


        // ============================================================
        // AUDIT
        // ============================================================

        await _audit.LogAsync(
            AuditAction.UserLock,
            nameof(User),
            user.Id,
            new
            {
                role = Roles.Patient,
                isActive = oldValue
            },
            new
            {
                role = Roles.Patient,
                isActive = value
            });

        await _repository.CommitAsync();


        return Ok(new
        {
            message = value
                ? "Đã mở khóa tài khoản bệnh nhân."
                : "Đã khóa tài khoản bệnh nhân.",

            rowVersion =
                user.ToRowVersion()
        });
    }
}
