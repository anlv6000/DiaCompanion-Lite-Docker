import { useState } from "react";
import { useData } from "@/contexts/DataContext";
import { useAsync, useDebounce } from "@/lib/hooks";
import {
  Button,
  ConfirmDialog,
  DataTable,
  Field,
  Icon,
  LoadState,
  Modal,
  PageHeader,
  Pagination,
  Panel,
  StatusBadge,
  useSort,
} from "@/components/ui";
import { genders, label } from "@/lib/enums";
import { useToast } from "@/contexts/ToastContext";
import type { AdminPatientDto } from "@/types/api";

export function PatientAccountsPage() {
  const data = useData();
  const toast = useToast();

  const [q, setQ] = useState("");
  const dq = useDebounce(q, 300);

  const [active, setActive] = useState("");
  const [page, setPage] = useState(1);

  const [editor, setEditor] =
    useState<AdminPatientDto | null>(null);

  const [confirm, setConfirm] =
    useState<AdminPatientDto | null>(null);

  // ============================================================
  // DANH SÁCH BỆNH NHÂN
  // ============================================================

  // Khoá sắp xếp khớp EfRepository.Patients (nhánh danh sách tài khoản):
  // "code" | "created"; mặc định là FullName.
  const { sort, desc, onSort } = useSort();

  const list = useAsync(
    () =>
      data.patients.adminList({
        q: dq,

        // Backend đang nhận:
        // active | locked | no-account
        status:
  active === "true"
    ? "active"
    : active === "false"
      ? "locked"
      : "",

        page,
        pageSize: 25,
        sort,
        desc,
      }),
    [dq, active, page],
  );

  // ============================================================
  // KHÓA / MỞ TÀI KHOẢN PATIENT
  // ============================================================

  const toggle = async (patient: AdminPatientDto) => {
    if (
      !patient.hasAccount ||
      patient.isActive == null ||
      !patient.accountRowVersion
    ) {
      return;
    }

    await data.patients.adminSetActive(
      patient.id,
      !patient.isActive,
      patient.accountRowVersion,
    );

    toast.push(
      patient.isActive
        ? "Đã khóa tài khoản bệnh nhân."
        : "Đã mở tài khoản bệnh nhân.",
      "success",
    );

    setConfirm(null);

    // Phải reload để lấy AccountRowVersion mới.
    list.reload();
  };

  return (
    <>
      <PageHeader
        title="Tài khoản bệnh nhân"
        subtitle="Quản lý thông tin và trạng thái tài khoản bệnh nhân."
      />

      <Panel>
        {/* ======================================================
            FILTER
           ====================================================== */}

        <div className="toolbar">
          <Field
            labelText="Tìm kiếm"
            className="inline"
          >
            <input
              value={q}
              onChange={(e) => {
                setQ(e.target.value);
                setPage(1);
              }}
              placeholder="Họ tên, mã bệnh nhân, số điện thoại"
            />
          </Field>

          <Field
            labelText="Trạng thái"
            className="inline"
          >
            <select
              value={active}
              onChange={(e) => {
                setActive(e.target.value);
                setPage(1);
              }}
            >
              <option value="">
                Tất cả
              </option>

              <option value="true">
                Hoạt động
              </option>

              <option value="false">
                Đã khóa
              </option>

              
            </select>
          </Field>
        </div>

        {/* ======================================================
            TABLE
           ====================================================== */}

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
            headers={[
              { label: "Mã BN", sortKey: "code" },
              "Họ tên",
              "Giới tính",
              "Số điện thoại",
              "Địa chỉ",
              "Trạng thái",
              "Thao tác",
            ]}
          >
            {list.data?.items.map((patient) => (
              <tr key={patient.id}>
                <td className="mono">
                  {patient.code}
                </td>

                <td>
                  <b>{patient.fullName}</b>
                </td>

                <td>
                  {label(
                    genders,
                    patient.gender,
                  )}
                </td>

                <td className="mono">
                  {patient.phone || "—"}
                </td>

                <td>
                  {patient.address || "—"}
                </td>

                <td>
                  <PatientStatusBadge
                    patient={patient}
                  />
                </td>

                <td>
                  <div className="actions">
                    {/* SỬA */}

                    <Button
                      onClick={() =>
                        setEditor(patient)
                      }
                    >
                      <Icon name="edit" />
                      Sửa
                    </Button>

                    {/* KHÓA / MỞ */}

                    <Button
                      disabled={
                        !patient.hasAccount ||
                        patient.isActive == null ||
                        !patient.accountRowVersion
                      }
                      kind={
                        patient.isActive
                          ? "danger"
                          : "default"
                      }
                      onClick={() =>
                        setConfirm(patient)
                      }
                    >
                      {patient.isActive
                        ? "Khóa"
                        : "Mở"}
                    </Button>
                  </div>
                </td>
              </tr>
            ))}
          </DataTable>

          <Pagination
            page={
              list.data?.page || page
            }
            pageSize={
              list.data?.pageSize || 25
            }
            total={
              list.data?.totalItems || 0
            }
            totalPages={
              list.data?.totalPages
            }
            rangeLabel={
              list.data?.rangeLabel
            }
            onPage={setPage}
          />
        </LoadState>
      </Panel>

      {/* ========================================================
          MODAL SỬA
         ======================================================== */}

      {editor && (
        <PatientEditor
          patient={editor}
          onClose={() =>
            setEditor(null)
          }
          onSaved={() => {
            setEditor(null);
            list.reload();
          }}
        />
      )}

      {/* ========================================================
          XÁC NHẬN KHÓA / MỞ
         ======================================================== */}

      {confirm && (
        <ConfirmDialog
          title={
            confirm.isActive
              ? "Khóa tài khoản"
              : "Mở tài khoản"
          }
          message={`Xác nhận ${
            confirm.isActive
              ? "khóa"
              : "mở"
          } tài khoản Patient của ${confirm.fullName}?`}
          danger={
            confirm.isActive === true
          }
          onClose={() =>
            setConfirm(null)
          }
          onConfirm={() =>
            toggle(confirm)
          }
        />
      )}
    </>
  );
}

// ================================================================
// TRẠNG THÁI
// ================================================================

function PatientStatusBadge({
  patient,
}: {
  patient: AdminPatientDto;
}) {
  // Patient chưa liên kết User.
  if (!patient.hasAccount) {
    return (
      <StatusBadge text="Chưa có tài khoản" />
    );
  }

  // Có Patient UserRole và đang active.
  if (patient.isActive === true) {
    return (
      <StatusBadge
        text="Hoạt động"
        kind="ok"
      />
    );
  }

  // Patient UserRole bị khóa.
  if (patient.isActive === false) {
    return (
      <StatusBadge
        text="Đã khóa"
        kind="alert"
      />
    );
  }

  // Có User nhưng không tìm thấy Patient role.
  return (
    <StatusBadge
      text="Thiếu role Patient"
      kind="alert"
    />
  );
}

// ================================================================
// MODAL SỬA BỆNH NHÂN
// ================================================================

function PatientEditor({
  patient,
  onClose,
  onSaved,
}: {
  patient: AdminPatientDto;
  onClose: () => void;
  onSaved: () => void;
}) {
  const data = useData();
  const toast = useToast();

  const [fullName, setFullName] =
    useState(patient.fullName);

  const [gender, setGender] =
    useState(patient.gender);

  const [address, setAddress] =
    useState(patient.address || "");

  const [busy, setBusy] =
    useState(false);

  const [error, setError] =
    useState("");

  const save = async () => {
    // ==========================================================
    // VALIDATE
    // ==========================================================

    if (!fullName.trim()) {
      setError(
        "Vui lòng nhập họ tên bệnh nhân.",
      );
      return;
    }

    setBusy(true);
    setError("");

    try {
      await data.patients.adminUpdate(
        patient.id,
        {
          fullName:
            fullName.trim(),

          gender,

          address:
            address.trim() || null,

          // Concurrency của Patient.
          rowVersion:
            patient.patientRowVersion,
        },
      );

      toast.push(
        "Đã cập nhật thông tin bệnh nhân.",
        "success",
      );

      onSaved();
    } catch (e) {
      setError(
        (e as Error).message,
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      title="Cập nhật bệnh nhân"
      onClose={onClose}
      footer={
        <>
          <Button
            onClick={onClose}
          >
            Hủy
          </Button>

          <Button
            kind="primary"
            busy={busy}
            onClick={save}
          >
            Lưu thay đổi
          </Button>
        </>
      }
    >
      <div className="form-row">
        {/* HỌ TÊN */}

        <Field
          labelText="Họ tên"
          required
        >
          <input
            value={fullName}
            onChange={(e) =>
              setFullName(
                e.target.value,
              )
            }
          />
        </Field>

        {/* GIỚI TÍNH */}

        <Field
          labelText="Giới tính"
          required
        >
          <select
            value={gender}
            onChange={(e) =>
              setGender(
                Number(
                  e.target.value,
                ),
              )
            }
          >
            {genders.map(
              (genderName, index) => (
                <option
                  key={index}
                  value={index}
                >
                  {genderName}
                </option>
              ),
            )}
          </select>
        </Field>

        {/* ĐỊA CHỈ */}

        <Field labelText="Địa chỉ">
          <textarea
            value={address}
            onChange={(e) =>
              setAddress(
                e.target.value,
              )
            }
          />
        </Field>
      </div>

      {error && (
        <div className="state error">
          {error}
        </div>
      )}
    </Modal>
  );
}