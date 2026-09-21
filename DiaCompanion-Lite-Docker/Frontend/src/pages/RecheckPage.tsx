import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useData } from "@/contexts/DataContext";
import { useAsync } from "@/lib/hooks";
import {
  PageHeader,
  Panel,
  Field,
  Button,
  DataTable,
  LoadState,
  Pagination,
  GradeBadge,
  StatusBadge,
} from "@/components/ui";
import { referralTypes, label } from "@/lib/enums";
import { fmtDate } from "@/lib/format";
import type { RecheckDto } from "@/types/api";

/**
 * Danh sách tái tầm soát — thay cho trang "Lịch khám" (appointments) đã bỏ.
 *
 * Không còn khái niệm đặt/hủy/khung giờ. Danh sách này TÍNH từ lượt khám hoàn
 * tất gần nhất của mỗi bệnh nhân (ClosedAt + RecheckMonths). Bệnh nhân được coi
 * là "đến hạn" khi tới ngày dự kiến mà chưa có lượt khám mới hơn. Họ đến khám
 * trực tiếp trong giờ làm việc; nhân viên dùng danh sách này để nhắc/gọi.
 */
export function RecheckPage() {
  const data = useData();
  const navigate = useNavigate();

  const [overdueOnly, setOverdueOnly] = useState(false);
  const [withinDays, setWithinDays] = useState(30);
  const [page, setPage] = useState(1);

  const list = useAsync(
    () =>
      data.recheck.due({
        overdueOnly,
        withinDays,
        page,
        pageSize: 25,
      }),
    [overdueOnly, withinDays, page],
  );

  // Badge tổng số quá hạn (đọc riêng để hiện kể cả khi đang lọc).
  const overdue = useAsync(() => data.recheck.overdueCount(), []);

  return (
    <>
      <PageHeader
        title="Tái tầm soát"
        subtitle="Bệnh nhân đến hạn tái khám, tính từ lần khám hoàn tất gần nhất. Bệnh nhân đến khám trực tiếp, không đặt lịch."
        actions={
          overdue.data ? (
            <StatusBadge
              text={`${overdue.data.overdue} quá hạn`}
              kind={overdue.data.overdue > 0 ? "alert" : "ok"}
            />
          ) : undefined
        }
      />

      <Panel>
        <div className="toolbar">
          <Field labelText="Phạm vi" className="inline">
            <select
              value={overdueOnly ? "overdue" : "all"}
              onChange={(e) => {
                setOverdueOnly(e.target.value === "overdue");
                setPage(1);
              }}
            >
              <option value="all">Sắp đến hạn + quá hạn</option>
              <option value="overdue">Chỉ quá hạn</option>
            </select>
          </Field>

          <Field labelText="Trong vòng (ngày)" className="inline">
            <select
              value={String(withinDays)}
              onChange={(e) => {
                setWithinDays(Number(e.target.value));
                setPage(1);
              }}
              disabled={overdueOnly}
            >
              <option value="7">7 ngày</option>
              <option value="14">14 ngày</option>
              <option value="30">30 ngày</option>
              <option value="60">60 ngày</option>
              <option value="90">90 ngày</option>
            </select>
          </Field>
        </div>

        <LoadState
          loading={list.loading}
          error={list.error}
          empty={!list.data?.items.length}
          emptyText="Không có bệnh nhân nào đến hạn trong phạm vi này."
        >
          {list.data && (
            <>
              <DataTable
                headers={[
                  "Mã",
                  "Họ tên",
                  "Điện thoại",
                  "Mức DR gần nhất",
                  "Khám gần nhất",
                  "Ngày đến hạn",
                  "Trạng thái",
                  "",
                ]}
              >
                {list.data.items.map((r: RecheckDto) => (
                  <tr key={r.patientId}>
                    <td>{r.patientCode}</td>
                    <td>{r.patientName}</td>
                    <td>{r.patientPhone || "—"}</td>
                    <td>
                      <GradeBadge grade={r.lastConfirmedGrade} />
                    </td>
                    <td>{fmtDate(r.lastVisitClosedAt)}</td>
                    <td>{fmtDate(r.dueDate)}</td>
                    <td>
                      <StatusBadge
                        text={r.statusLabel}
                        kind={r.isOverdue ? "alert" : "watch"}
                      />
                    </td>
                    <td>
                      <Button onClick={() => navigate(`/patients/${r.patientId}`)}>
                        Hồ sơ
                      </Button>
                    </td>
                  </tr>
                ))}
              </DataTable>
              <Pagination
                page={list.data.page || page}
                total={list.data.totalItems}
                pageSize={list.data.pageSize || 25}
                totalPages={list.data.totalPages}
                rangeLabel={list.data.rangeLabel}
                onPage={setPage}
              />
            </>
          )}
        </LoadState>
      </Panel>
    </>
  );
}
