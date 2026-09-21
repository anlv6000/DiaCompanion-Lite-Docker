import { useState, useEffect, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useData } from "@/contexts/DataContext";
import { useAsync, useDebounce } from "@/lib/hooks";
import {
  ActionLink,
  Button,
  DataTable,
  Field,
  GradeBadge,
  Icon,
  LoadState,
  Modal,
  PageHeader,
  Pagination,
  Panel,
  StatusBadge,
  useSort,
} from "@/components/ui";
import { genders, diabetesTypes, grades, label } from "@/lib/enums";
import { clinicToday, fmtDate } from "@/lib/format";
import { useToast } from "@/contexts/ToastContext";
import { useAuth } from "@/contexts/AuthContext";
import { can } from "@/lib/permissions";
import { hasRole } from "@/lib/roles";
import type {
  CreatePatientRequest,
  LinkablePatientUserDto,
  TempCredentialResponse,
} from "@/types/api";

export function PatientsPage() {
  const data = useData();
  const { user } = useAuth();
  const isDoctor = hasRole(user, "Doctor");
  const isReceptionist = hasRole(user, "Receptionist") && !isDoctor;
  const [q, setQ] = useState("");
  const dq = useDebounce(q);
  const [type, setType] = useState("");
  const [grade, setGrade] = useState("");
  const [page, setPage] = useState(1);
  // Khoá sắp xếp phải khớp nhánh switch trong EfRepository.Patients:
  // "name" | "code"; mặc định (chuỗi rỗng) là CreatedAt giảm dần.
  const { sort, desc, onSort } = useSort();

  const list = useAsync(
    () =>
      data.patients.list({
        q: dq.trim().length >= 2 ? dq : undefined,
        diabetesType: isReceptionist ? undefined : type,
        grade: isReceptionist ? undefined : grade,
        page,
        pageSize: 25,
        sort,
        desc,
      }),
    [dq, type, grade, page, isReceptionist, sort, desc],
  );

  // Đổi cách sắp xếp thì phải về trang 1: thứ tự mới làm bản ghi ở trang đang
  // đứng không còn liên quan gì tới vị trí cũ.
  useEffect(() => {
    setPage(1);
  }, [sort, desc]);

  // Nếu sau khi lọc/xóa dữ liệu mà trang hiện tại vượt quá tổng số trang,
  // tự đưa UI về trang hợp lệ cuối cùng.
  useEffect(() => {
    if (list.data && list.data.totalPages > 0 && page > list.data.totalPages) {
      setPage(list.data.totalPages);
    }
  }, [list.data?.totalPages, page]);

  return (
    <>
      <PageHeader
        title="Bệnh nhân"
        subtitle={
          isReceptionist
            ? "Chỉ hiển thị thông tin cơ bản để hỗ trợ tiếp đón và tạo lượt khám."
            : "Tìm kiếm không phân biệt dấu theo họ tên, mã hoặc số điện thoại."
        }
        actions={
          /* Tạo hồ sơ bệnh nhân là nghiệp vụ LỄ TÂN. Staff không thấy nút. */
          can.createPatient(user) ? (
            <ActionLink to="/reception/patients/new">
              <Button kind="primary">
                <Icon name="plus" />
                Tạo bệnh nhân
              </Button>
            </ActionLink>
          ) : undefined
        }
      />
      <Panel>
        <div className="toolbar">
          <Field labelText="Tìm kiếm" className="inline">
            <input
              value={q}
              onChange={(e) => {
                setQ(e.target.value);
                setPage(1);
              }}
              placeholder="Tối thiểu 2 ký tự"
            />
          </Field>
          {!isReceptionist && (
            <>
              <Field labelText="Loại tiểu đường" className="inline">
                <select
                  value={type}
                  onChange={(e) => {
                    setType(e.target.value);
                    setPage(1);
                  }}
                >
                  <option value="">Tất cả</option>
                  {diabetesTypes.map(
                    (x, i) =>
                      i > 0 && (
                        <option key={i} value={i}>
                          {x}
                        </option>
                      ),
                  )}
                </select>
              </Field>
              <Field labelText="Mức DR xác nhận" className="inline">
                <select
                  value={grade}
                  onChange={(e) => {
                    setGrade(e.target.value);
                    setPage(1);
                  }}
                >
                  <option value="">Tất cả</option>
                  {grades.map((x, i) => (
                    <option key={i} value={i}>
                      {x}
                    </option>
                  ))}
                </select>
              </Field>
            </>
          )}
          <Button
            onClick={() => {
              setQ("");
              setType("");
              setGrade("");
              setPage(1);
            }}
          >
            Xóa bộ lọc
          </Button>
        </div>
        <LoadState
          loading={list.loading}
          error={list.error}
          empty={!list.data?.items.length}
          onRetry={list.reload}
        >
          <DataTable
            sort={sort}
            desc={desc}
            onSort={onSort}
            headers={
              isReceptionist
                ? [
                    { label: "Mã", sortKey: "code" },
                    { label: "Họ tên", sortKey: "name" },
                    "Tuổi",
                    "Giới tính",
                    "Số điện thoại",
                    "Lần khám gần nhất",
                    "Hồ sơ",
                  ]
                : [
                    { label: "Mã", sortKey: "code" },
                    { label: "Họ tên", sortKey: "name" },
                    "Tuổi",
                    "Giới tính",
                    "Số điện thoại",
                    "ĐTĐ",
                    "DR gần nhất",
                    "Lần khám gần nhất",
                    "Tài khoản",
                    "Hồ sơ",
                  ]
            }
          >
            {list.data?.items.map((p) => (
              <tr key={p.id}>
                <td className="mono">{p.code}</td>
                <td>
                  <b>{p.fullName}</b>
                </td>
                <td className="mono">{p.age}</td>
                <td>{label(genders, p.gender)}</td>
                <td className="mono">{p.phone}</td>
                {!isReceptionist && <td>{label(diabetesTypes, p.diabetesType)}</td>}
                {!isReceptionist && (
                  <td>
                    <GradeBadge grade={p.latestDrGrade} />
                  </td>
                )}
                <td className="mono">{fmtDate(p.latestVisitDate)}</td>
                {!isReceptionist && (
                  <td>
                    <StatusBadge
                      text={p.hasAccount ? "Đã cấp" : "Chưa cấp"}
                      kind={p.hasAccount ? "ok" : "watch"}
                    />
                  </td>
                )}
                <td>
                  <ActionLink to={`/patients/${p.id}`}>Mở hồ sơ →</ActionLink>
                </td>
              </tr>
            ))}
          </DataTable>
          <Pagination
            page={list.data?.page || page}
            pageSize={list.data?.pageSize || 25}
            total={list.data?.totalItems || 0}
            totalPages={list.data?.totalPages}
            rangeLabel={list.data?.rangeLabel}
            onPage={setPage}
          />
        </LoadState>
      </Panel>
    </>
  );
}

const EMPTY: CreatePatientRequest = {
  fullName: "",
  gender: 0,
  dateOfBirth: "",
  phone: "",
  address: "",
  diabetesType: 2,
  diabetesDurationYears: null,
  baselineHbA1c: null,
  note: "",
  createAccount: true,
  existingUserId: null,
};

export function PatientFormPage({ id }: { id?: number }) {
  const edit = !!id;
  const data = useData();
  const navigate = useNavigate();
  const toast = useToast();
  const { user } = useAuth();
  const isReceptionist = hasRole(user, "Receptionist");
  const isDoctor = hasRole(user, "Doctor");
  const detail = useAsync(
    () => (id ? data.patients.get(id) : Promise.resolve(null)),
    [id],
  );
  const [form, setForm] = useState<CreatePatientRequest>(EMPTY);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [cred, setCred] = useState<TempCredentialResponse | null>(null);
  const [accountMode, setAccountMode] = useState<"new" | "existing">("new");
  const [selectedExistingUser, setSelectedExistingUser] = useState<LinkablePatientUserDto | null>(null);
  const [userSearch, setUserSearch] = useState("");
  const debouncedUserSearch = useDebounce(userSearch, 250);
  const linkableUsers = useAsync(
    () =>
      !edit && form.createAccount && accountMode === "existing"
        ? data.users.linkablePatients(debouncedUserSearch.trim())
        : Promise.resolve([] as LinkablePatientUserDto[]),
    [edit, form.createAccount, accountMode, debouncedUserSearch],
  );

  useEffect(() => {
    if (detail.data) {
      const d = detail.data;
      setForm({
        fullName: d.fullName,
        gender: d.gender,
        dateOfBirth: d.dateOfBirth,
        address: d.address || "",
        phone: d.phone,
        diabetesType: d.diabetesType,
        diabetesDurationYears: d.diabetesDurationYears ?? null,
        baselineHbA1c: d.baselineHbA1c ?? null,
        note: d.note || "",
        createAccount: d.hasAccount,
        existingUserId: null,
      });
    }
  }, [detail.data]);

  const patch = (k: keyof CreatePatientRequest, v: unknown) =>
    setForm((x) => ({ ...x, [k]: v }));

  async function save(e: FormEvent) {
    e.preventDefault();
    if (!form.fullName.trim() || !form.phone.trim() || !form.dateOfBirth) {
      setError("Vui lòng nhập họ tên, ngày sinh và số điện thoại.");
      return;
    }
    setBusy(true);
    setError("");
    try {
      if (id) {
        const { createAccount, existingUserId, ...body } = form;
        await data.patients.update(id, {
          ...body,
          rowVersion: detail.data!.rowVersion,
        });
        toast.push("Đã cập nhật hồ sơ.", "success");
        navigate(`/patients/${id}`);
      } else {
        if (form.createAccount && accountMode === "existing" && !form.existingUserId) {
          setError("Vui lòng chọn một tài khoản có sẵn để liên kết với hồ sơ bệnh nhân.");
          return;
        }

        const payload: CreatePatientRequest = {
          ...form,
          existingUserId:
            form.createAccount && accountMode === "existing"
              ? form.existingUserId
              : null,
        };
        const r = await data.patients.create(payload);
        if (r.account) setCred(r.account);
        toast.push("Đã tạo hồ sơ bệnh nhân.", "success");
        if (!r.account) navigate(`/patients/${r.patient.id}`);
      }
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  }

  if (edit && detail.loading)
    return (
      <LoadState loading error={null}>
        {null}
      </LoadState>
    );

  return (
    <>
      <PageHeader
        title={edit ? "Cập nhật hồ sơ bệnh nhân" : "Tạo hồ sơ bệnh nhân"}
        subtitle={
          edit && isReceptionist
            ? "Lễ tân chỉ cập nhật thông tin hành chính và liên hệ."
            : edit && isDoctor
              ? "Bác sĩ chỉ cập nhật thông tin lâm sàng của bệnh nhân."
              : "Tạo hồ sơ và cấp tài khoản bệnh nhân tại quầy tiếp đón."
        }
      />
      <Panel>
        <form onSubmit={save}>
          <div className="form-row three">
            {(!edit || isReceptionist) && (
              <>
                <Field labelText="Họ tên" required>
                  <input
                    value={form.fullName}
                    onChange={(e) => patch("fullName", e.target.value)}
                    disabled={!edit && accountMode === "existing" && selectedExistingUser != null}
                    title={
                      !edit && accountMode === "existing" && selectedExistingUser
                        ? "Họ tên được lấy từ tài khoản đã chọn và không thể chỉnh sửa."
                        : undefined
                    }
                  />
                </Field>
                <Field labelText="Giới tính" required>
                  <select
                    value={form.gender}
                    onChange={(e) => patch("gender", Number(e.target.value))}
                  >
                    {genders.map((x, i) => (
                      <option value={i} key={i}>
                        {x}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field labelText="Ngày sinh" required>
                  <input
                    type="date"
                    max={clinicToday()}
                    value={form.dateOfBirth}
                    onChange={(e) => patch("dateOfBirth", e.target.value)}
                  />
                </Field>
                <Field
                  labelText="Số điện thoại"
                  required
                  help="Đây là định danh đăng nhập của bệnh nhân."
                >
                  <input
                    className="mono"
                    value={form.phone}
                    disabled={!edit && accountMode === "existing" && !!selectedExistingUser?.phone}
                    title={
                      !edit && accountMode === "existing" && selectedExistingUser?.phone
                        ? "Số điện thoại được lấy từ tài khoản đã chọn và không thể chỉnh sửa."
                        : undefined
                    }
                    onChange={(e) => patch("phone", e.target.value)}
                  />
                </Field>
                <Field labelText="Địa chỉ">
                  <input
                    value={form.address || ""}
                    onChange={(e) => patch("address", e.target.value)}
                  />
                </Field>
              </>
            )}

            {(!edit || isDoctor) && (
              <>
                <Field labelText="Loại tiểu đường">
                  <select
                    value={form.diabetesType}
                    onChange={(e) => patch("diabetesType", Number(e.target.value))}
                  >
                    {diabetesTypes.map((x, i) => (
                      <option value={i} key={i}>
                        {x}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field labelText="Thời gian mắc (năm)">
                  <input
                    type="number"
                    min="0"
                    max="100"
                    value={form.diabetesDurationYears ?? ""}
                    onChange={(e) =>
                      patch(
                        "diabetesDurationYears",
                        e.target.value === "" ? null : Number(e.target.value),
                      )
                    }
                  />
                </Field>
                <Field labelText="HbA1c nền (%)">
                  <input
                    type="number"
                    step="0.1"
                    min="3"
                    max="20"
                    value={form.baselineHbA1c ?? ""}
                    onChange={(e) =>
                      patch(
                        "baselineHbA1c",
                        e.target.value === "" ? null : Number(e.target.value),
                      )
                    }
                  />
                </Field>
                <Field labelText="Ghi chú lâm sàng">
                  <textarea
                    value={form.note || ""}
                    onChange={(e) => patch("note", e.target.value)}
                  />
                </Field>
              </>
            )}
          </div>
          {!edit && (
            <>
              <label className="checkbox">
                <input
                  type="checkbox"
                  checked={form.createAccount}
                  onChange={(e) => {
                    const checked = e.target.checked;
                    patch("createAccount", checked);
                    if (!checked) {
                      setAccountMode("new");
                      setSelectedExistingUser(null);
                      patch("existingUserId", null);
                    }
                  }}
                />
                Cấp / liên kết tài khoản bệnh nhân khi tạo hồ sơ
              </label>

              {form.createAccount && (
                <Panel title="Tài khoản đăng nhập">
                  <div className="form-row two">
                    <label className="checkbox">
                      <input
                        type="radio"
                        name="patient-account-mode"
                        checked={accountMode === "new"}
                        onChange={() => {
                          setAccountMode("new");
                          setSelectedExistingUser(null);
                          patch("existingUserId", null);
                        }}
                      />
                      Tạo tài khoản Patient mới
                    </label>
                    <label className="checkbox">
                      <input
                        type="radio"
                        name="patient-account-mode"
                        checked={accountMode === "existing"}
                        onChange={() => setAccountMode("existing")}
                      />
                      Liên kết tài khoản có sẵn (ví dụ bác sĩ)
                    </label>
                  </div>

                  {accountMode === "new" ? (
                    <div className="help">
                      Hệ thống sẽ tạo User mới, gán role Patient và trả mật khẩu tạm.
                    </div>
                  ) : (
                    <>
                      <Field
                        labelText="Tìm tài khoản chưa có hồ sơ bệnh nhân"
                        help="Có thể tìm theo họ tên, email hoặc số điện thoại. Danh sách chỉ nên trả về User đang hoạt động và chưa liên kết với Patient."
                      >
                        <input
                          value={userSearch}
                          onChange={(e) => setUserSearch(e.target.value)}
                          placeholder="Nhập tên, email hoặc số điện thoại"
                        />
                      </Field>

                      <LoadState
                        loading={linkableUsers.loading}
                        error={linkableUsers.error}
                        empty={!linkableUsers.data?.length}
                        onRetry={linkableUsers.reload}
                        emptyText="Không có tài khoản phù hợp để liên kết."
                      >
                        <DataTable
                          headers={[
                            "User ID",
                            "Họ tên",
                            "Email",
                            "Số điện thoại",
                            "Role hiện tại",
                            "Chọn",
                          ]}
                        >
                          {linkableUsers.data?.map((u) => (
                            <tr key={u.id}>
                              <td className="mono">{u.id}</td>
                              <td>
                                <b>{u.fullName}</b>
                              </td>
                              <td>{u.email || "—"}</td>
                              <td className="mono">{u.phone || "—"}</td>
                              <td>{u.roles.join(", ") || "—"}</td>
                              <td>
                                <Button
                                  type="button"
                                  kind={form.existingUserId === u.id ? "primary" : "default"}
                                  onClick={() => {
                                    setSelectedExistingUser(u);
                                    patch("existingUserId", u.id);
                                    patch("fullName", u.fullName);
                                    patch("phone", u.phone || "");
                                  }}
                                >
                                  {form.existingUserId === u.id ? "Đã chọn" : "Chọn"}
                                </Button>
                              </td>
                            </tr>
                          ))}
                        </DataTable>
                      </LoadState>

                      {form.existingUserId && (
                        <div className="state ok">
                          Đang liên kết hồ sơ với User Name <b>{form.fullName}</b>.
                          User này sẽ được giữ nguyên các role hiện có và được gán thêm role Patient.
                        </div>
                      )}
                    </>
                  )}
                </Panel>
              )}
            </>
          )}
          {error && <div className="state error">{error}</div>}
          <div className="dialog-footer">
            <Button
              type="button"
              onClick={() => navigate(id ? `/patients/${id}` : "/patients")}
            >
              Hủy
            </Button>
            <Button kind="primary" type="submit" busy={busy}>
              {edit ? "Lưu thay đổi" : "Tạo hồ sơ"}
            </Button>
          </div>
        </form>
      </Panel>
      {cred && (
        <CredentialAfterCreate
          cred={cred}
          onDone={() => navigate("/patients")}
        />
      )}
    </>
  );
}

function CredentialAfterCreate({
  cred,
  onDone,
}: {
  cred: TempCredentialResponse;
  onDone: () => void;
}) {
  return (
    <Modal
      title="Tài khoản bệnh nhân đã được cấp"
      onClose={onDone}
      footer={
        <Button kind="primary" onClick={onDone}>
          Đã in / lưu thông tin
        </Button>
      }
    >
      <div className="credential">
        <p>Thông tin này chỉ hiển thị một lần.</p>
        <div>
          Đăng nhập: <code>{cred.loginId}</code>
        </div>
        <div>
          Mật khẩu tạm: <code>{cred.tempPassword}</code>
        </div>
        <p>{cred.note}</p>
      </div>
    </Modal>
  );
}
