import { useState } from "react";
import { useData } from "@/contexts/DataContext";
import { useAsync } from "@/lib/hooks";
import {
  PageHeader,
  Panel,
  Button,
  DataTable,
  LoadState,
  Pagination,
  StatusBadge,
} from "@/components/ui";
import { fmtDate } from "@/lib/format";
import type { RecheckDto } from "@/types/api";

export function AppointmentsPage() {
  const data = useData();
  const [page, setPage] = useState(1);
  const [overdueOnly, setOverdueOnly] = useState(false);

  const list = useAsync(
    () =>
      data.recheck.due({
        page,
        pageSize: 50,
        overdueOnly: overdueOnly || undefined,
      }),
    [page, overdueOnly],
  );

  return (
    <>
      <PageHeader
        title="Nhắc tái tầm soát"
        subtitle="Danh sách bệnh nhân đến hạn hoặc quá hạn tái tầm soát."
        actions={
          <div className="pill">
            <button
              className={!overdueOnly ? "on" : ""}
              onClick={() => {
                setOverdueOnly(false);
                setPage(1);
              }}
            >
              Tất cả
            </button>
            <button
              className={overdueOnly ? "on" : ""}
              onClick={() => {
                setOverdueOnly(true);
                setPage(1);
              }}
            >
              Chỉ quá hạn
            </button>
          </div>
        }
      />
      <Panel>
        <LoadState
          loading={list.loading}
          error={list.error}
          empty={!list.data?.items.length}
          onRetry={list.reload}
        >
          <DataTable
            headers={[
              "Mã BN",
              "Bệnh nhân",
              "SĐT",
              "Lần khám cuối",
              "Ngày đến hạn",
              "Trạng thái",
              "DR xác nhận",
              "Tái khám",
            ]}
          >
            {list.data?.items.map((item: RecheckDto) => (
              <tr key={item.patientId}>
                <td>{item.patientCode}</td>
                <td>{item.patientName}</td>
                <td>{item.patientPhone || "—"}</td>
                <td className="mono">{fmtDate(item.lastVisitClosedAt)}</td>
                <td className="mono">{fmtDate(item.dueDate)}</td>
                <td>
                  <StatusBadge
                    text={item.statusLabel}
                    kind={item.isOverdue ? "alert" : "watch"}
                  />
                </td>
                <td>{item.lastConfirmedGradeLabel || "—"}</td>
                <td>{item.recheckMonths} tháng</td>
              </tr>
            ))}
          </DataTable>
          <Pagination
            page={list.data?.page || page}
            pageSize={list.data?.pageSize || 50}
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
