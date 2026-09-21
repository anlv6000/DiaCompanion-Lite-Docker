import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { grades, eyes, label } from "@/lib/enums";
import { normalizeText } from "@/lib/format";
export function Icon({ name }: { name: string }) {
  const paths: Record<string, any> = {
    menu: (
      <>
        <path d="M3 5h14M3 10h14M3 15h14" />
      </>
    ),
    users: (
      <>
        <circle cx="8" cy="7" r="3" />
        <path d="M2.5 17c.7-3 2.7-4.5 5.5-4.5s4.8 1.5 5.5 4.5M14 5.5a2.5 2.5 0 010 5M15 12.5c1.5.6 2.3 1.8 2.6 3.5" />
      </>
    ),
    calendar: (
      <>
        <rect x="3" y="4" width="14" height="13" rx="2" />
        <path d="M3 8h14M7 2v4M13 2v4" />
      </>
    ),
    chart: (
      <>
        <path d="M3 17V4M3 17h14M6 14l3-4 3 2 4-6" />
      </>
    ),
    settings: (
      <>
        <circle cx="10" cy="10" r="3" />
        <path d="M10 2v2M10 16v2M2 10h2M16 10h2M4.3 4.3l1.4 1.4M14.3 14.3l1.4 1.4M15.7 4.3l-1.4 1.4M5.7 14.3l-1.4 1.4" />
      </>
    ),
    logout: (
      <>
        <path d="M8 3H4v14h4M12 6l4 4-4 4M7 10h9" />
      </>
    ),
    eye: (
      <>
        <path d="M2 10s3-5 8-5 8 5 8 5-3 5-8 5-8-5-8-5z" />
        <circle cx="10" cy="10" r="2.5" />
      </>
    ),
    plus: (
      <>
        <path d="M10 3v14M3 10h14" />
      </>
    ),
    edit: (
      <>
        <path d="M4 16l1-4L14 3l3 3-9 9-4 1zM12 5l3 3" />
      </>
    ),
    trash: (
      <>
        <path d="M4 6h12M8 6V4h4v2M6 6l1 11h6l1-11" />
      </>
    ),
    download: (
      <>
        <path d="M10 3v10M6 9l4 4 4-4M4 17h12" />
      </>
    ),
    check: (
      <>
        <path d="M4 10l4 4 8-8" />
      </>
    ),
    warning: (
      <>
        <path d="M10 3l8 14H2zM10 8v4M10 15h.01" />
      </>
    ),
    bell: (
      <>
        <path d="M5 8a5 5 0 0110 0c0 5 2 5 2 6H3c0-1 2-1 2-6M8 17h4" />
      </>
    ),
    file: (
      <>
        <path d="M5 2h7l4 4v12H5zM12 2v5h4" />
      </>
    ),
    search: (
      <>
        <circle cx="9" cy="9" r="5" />
        <path d="M13 13l4 4" />
      </>
    ),
    lock: (
      <>
        <rect x="4" y="9" width="12" height="9" rx="2" />
        <path d="M7 9V6a3 3 0 016 0v3" />
      </>
    ),
    heart: (
      <>
        <path d="M10 17S3 13 3 7.5A3.5 3.5 0 0110 5a3.5 3.5 0 017 2.5C17 13 10 17 10 17z" />
      </>
    ),
  };
  return (
    <span className="icon" aria-hidden="true">
      <svg viewBox="0 0 20 20">{paths[name] || paths.file}</svg>
    </span>
  );
}
/**
 * Nút - FE_DESIGN_RULES muc 5.
 *
 * kind: "default" | "primary" | "danger" | "danger solid" | "ghost"
 *   - Mỗi panel/modal chỉ có ĐÚNG MỘT nút primary.
 *   - "danger solid" chỉ dùng cho nút xác nhận cuối trong modal huỷ dữ liệu.
 * size: "sm" (trong bảng) | "md" (mặc định) | "lg" (hành động chính, mobile)
 *
 * busy khoá nút để tránh bấm hai lần. Truyền busyText để đổi nhãn sang thể
 * tiếp diễn ("Đang lưu…") - nhãn tĩnh khi đang xử lý làm người dùng tưởng treo.
 */
export function Button({
  children,
  kind = "default",
  size = "md",
  busy = false,
  busyText,
  iconOnly = false,
  ...props
}: any) {
  const cls = [
    kind === "default" ? "" : kind,
    size === "md" ? "" : size,
    iconOnly ? "icon-only" : "",
  ]
    .filter(Boolean)
    .join(" ");
  return (
    <button className={cls} disabled={busy || props.disabled} {...props}>
      {busy ? <span className="mono">…</span> : null}
      {busy && busyText ? busyText : children}
    </button>
  );
}
export function PageHeader({
  title,
  subtitle,
  actions,
}: {
  title: string;
  subtitle?: string;
  actions?: any;
}) {
  return (
    <div className="title">
      <div>
        <h1>{normalizeText(title)}</h1>
        {subtitle && <p>{normalizeText(subtitle)}</p>}
      </div>
      <div className="page-actions">{actions}</div>
    </div>
  );
}
export function Panel({
  title,
  action,
  children,
  className = "",
}: {
  title?: any;
  action?: any;
  children?: any;
  className?: string;
}) {
  return (
    <section className={`panel ${className}`}>
      {title !== undefined && (
        <div className="panel-h">
          <span>{typeof title === "string" ? normalizeText(title) : title}</span>
          <span>{action}</span>
        </div>
      )}
      <div className="panel-b">{children}</div>
    </section>
  );
}
export function Field({
  labelText,
  required,
  help,
  error,
  children,
  className = "",
}: {
  labelText: string;
  required?: boolean;
  help?: string;
  error?: string;
  children?: any;
  className?: string;
}) {
  return (
    <div className={`field ${className}`}>
      <label className={required ? "required" : ""}>{labelText}</label>
      {children}
      {help && <div className="help">{help}</div>}
      {error && <div className="field-error">{error}</div>}
    </div>
  );
}
export function LoadingRows({ count = 6 }: { count?: number }) {
  return (
    <div>
      {Array.from({ length: count }, (_, i) => (
        <div className="skeleton" key={i} />
      ))}
    </div>
  );
}
export function LoadState({
  loading,
  error,
  empty,
  onRetry,
  children,
  emptyText = "Không có dữ liệu phù hợp.",
}: {
  loading: boolean;
  error: any;
  empty?: boolean;
  onRetry?: () => void;
  children?: any;
  emptyText?: string;
}) {
  if (loading) return <LoadingRows />;
  if (error)
    return (
      <div className="state error">
        <b>Không tải được dữ liệu.</b>
        <div>{error.message || String(error)}</div>
        {onRetry && <Button onClick={onRetry}>Thử lại</Button>}
      </div>
    );
  if (empty)
    return (
      <div className="empty">
        <b>Chưa có dữ liệu</b>
        {emptyText || "Khi có bản ghi mới, danh sách sẽ hiện ở đây."}
      </div>
    );
  return children;
}
/**
 * Modal - FE_DESIGN_RULES muc 7.
 *
 * size: "sm" 420px (xác nhận) | "md" 560px (form, mặc định) | "lg" (xem ảnh, báo cáo)
 * dismissible=false cho modal huỷ dữ liệu: không đóng bằng Esc, không đóng khi
 * bấm nền - để tránh mất thao tác đang nhập dở hoặc đóng nhầm.
 *
 * Cuộn nằm trong .modal-b, nên tiêu đề và cụm nút luôn nhìn thấy.
 */
export function Modal({
  title,
  children,
  onClose,
  footer,
  width,
  size = "md",
  dismissible = true,
}: {
  title: string;
  children?: any;
  onClose: () => void;
  footer?: any;
  width?: string;
  size?: "sm" | "md" | "lg";
  dismissible?: boolean;
}) {
  useEffect(() => {
    if (!dismissible) return;
    const h = (e: any) => {
      if (e.key === "Escape") onClose();
    };
    addEventListener("keydown", h);
    return () => removeEventListener("keydown", h);
  }, [dismissible]);
  return (
    <div
      className="modal-backdrop"
      onMouseDown={(e: any) => {
        if (dismissible && e.target === e.currentTarget) onClose();
      }}
    >
      <div
        className={`modal ${size === "md" ? "" : size}`.trim()}
        role="dialog"
        aria-modal="true"
        style={width ? { width } : undefined}
      >
        <div className="modal-h">
          <span>{normalizeText(title)}</span>
          <button
            className="close ghost"
            onClick={onClose}
            aria-label="Đóng"
            title="Đóng"
          >
            ×
          </button>
        </div>
        <div className="modal-b">
          {children}
          {footer && <div className="dialog-footer">{footer}</div>}
        </div>
      </div>
    </div>
  );
}
export function ConfirmDialog({
  title,
  message,
  confirmText = "Xác nhận",
  danger = false,
  onConfirm,
  onClose,
  requireReason = false,
  busy = false,
}: {
  title: string;
  message: any;
  confirmText?: string;
  danger?: boolean;
  onConfirm: (reason: string) => void;
  onClose: () => void;
  requireReason?: boolean;
  busy?: boolean;
}) {
  const [reason, setReason] = useState("");
  return (
    <Modal
      title={title}
      onClose={onClose}
      size="sm"
      dismissible={!danger}
      footer={
        <>
          <Button onClick={onClose} disabled={busy}>
            Hủy
          </Button>
          <Button
            kind={danger ? "danger solid" : "primary"}
            busy={busy}
            busyText="Đang xử lý…"
            disabled={requireReason && !reason.trim()}
            onClick={() => onConfirm(reason)}
          >
            {confirmText}
          </Button>
        </>
      }
    >
      <p>{message}</p>
      {requireReason && (
        <Field labelText="Lý do" required>
          <textarea
            value={reason}
            onChange={(e: any) => setReason(e.target.value)}
            placeholder="Nhập lý do để lưu vết kiểm toán"
          />
        </Field>
      )}
    </Modal>
  );
}
export function GradeBadge({ grade }: { grade?: number | null }) {
  return grade == null ? (
    <span className="faint">—</span>
  ) : (
    <span className={`badge g${grade}`}>{label(grades, grade)}</span>
  );
}
export function EyeBadge({ eye }: { eye?: number | null }) {
  return <span className="badge mono">{label(eyes, eye)}</span>;
}
export function StatusBadge({
  text,
  kind = "",
}: {
  text: string;
  kind?: string;
}) {
  return <span className={`badge ${kind}`}>{text}</span>;
}
export function Meter({
  value,
  kind = "",
}: {
  value?: number | null;
  kind?: string;
}) {
  const v = Math.max(0, Math.min(1, Number(value || 0)));
  return (
    <>
      <span className={`meter ${kind}`}>
        <i style={{ width: `${v * 100}%` }} />
      </span>
      <span className="mono">{Math.round(v * 100)}%</span>
    </>
  );
}
export function Pagination({
  page,
  pageSize,
  total,
  totalPages,
  rangeLabel,
  onPage,
}: {
  page: number;
  pageSize: number;
  total: number;
  totalPages?: number;
  rangeLabel?: string;
  onPage: (p: number) => void;
}) {
  const pages = Math.max(1, totalPages ?? Math.ceil(total / Math.max(1, pageSize)));
  const safePage = Math.min(Math.max(1, page), pages);
  const start = total ? (safePage - 1) * pageSize + 1 : 0;
  const end = Math.min(safePage * pageSize, total);
  return (
    <div className="pagination">
      <span className="faint mono">
        {rangeLabel || `${start}–${end} / ${total}`}
      </span>
      <div className="actions">
        <Button
          size="sm"
          disabled={safePage <= 1}
          onClick={() => onPage(safePage - 1)}
        >
          Trước
        </Button>
        <span className="badge mono">
          {safePage}/{pages}
        </span>
        <Button
          size="sm"
          disabled={safePage >= pages}
          onClick={() => onPage(safePage + 1)}
        >
          Sau
        </Button>
      </div>
    </div>
  );
}
export function Tabs({
  items,
  active,
  onChange,
}: {
  items: { key: string; label: string }[];
  active: string;
  onChange: (k: string) => void;
}) {
  return (
    <div className="tabs">
      {items.map((x) => (
        <div
          key={x.key}
          className={`tab ${active === x.key ? "on" : ""}`}
          onClick={() => onChange(x.key)}
        >
          {x.label}
        </div>
      ))}
    </div>
  );
}
/**
 * Trạng thái sắp xếp dùng chung cho các bảng có cột bấm được.
 *
 * Bấm lần đầu vào một cột: sắp tăng dần theo cột đó.
 * Bấm lại chính cột đó: đảo sang giảm dần.
 * Bấm sang cột khác: về lại tăng dần theo cột mới.
 *
 * Không có trạng thái "bỏ sắp xếp": backend luôn có thứ tự mặc định, nên để
 * người dùng rơi vào trạng thái không xác định sẽ gây bối rối hơn là giúp ích.
 */
export function useSort(initialKey = "", initialDesc = false) {
  const [sort, setSort] = useState(initialKey);
  const [desc, setDesc] = useState(initialDesc);

  const onSort = (key: string) => {
    if (key === sort) {
      setDesc((d) => !d);
    } else {
      setSort(key);
      setDesc(false);
    }
  };

  return { sort, desc, onSort };
}

export type SortableHeader = {
  label: string;
  /** Khoá gửi lên backend. Phải khớp nhánh switch trong repository tương ứng. */
  sortKey: string;
};

/**
 * Bảng dữ liệu.
 *
 * headers nhận hai dạng, trộn lẫn được trong cùng một bảng:
 *   - chuỗi                        -> cột thường, không bấm được
 *   - { label, sortKey }           -> cột sắp xếp được
 *
 * Cột sắp xếp được chỉ hoạt động khi trang truyền vào onSort. Nhờ vậy 15 bảng
 * hiện có không phải sửa dòng nào — chúng vẫn truyền mảng chuỗi như cũ.
 */
export function DataTable({
  headers,
  children,
  sort,
  desc,
  onSort,
}: {
  headers: (string | SortableHeader)[];
  children?: any;
  sort?: string;
  desc?: boolean;
  onSort?: (key: string) => void;
}) {
  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            {headers.map((h, i) => {
              const sortable =
                typeof h === "object" && h !== null && !!h.sortKey && !!onSort;

              if (!sortable) {
                return <th key={i}>{typeof h === "object" ? h.label : h}</th>;
              }

              const col = h as SortableHeader;
              const active = sort === col.sortKey;
              return (
                <th
                  key={i}
                  className={`sortable ${active ? "sorted" : ""}`}
                  onClick={() => onSort!(col.sortKey)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(e: any) => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      onSort!(col.sortKey);
                    }
                  }}
                  aria-sort={
                    active ? (desc ? "descending" : "ascending") : "none"
                  }
                  title={
                    active
                      ? desc
                        ? "Đang sắp giảm dần — bấm để sắp tăng dần"
                        : "Đang sắp tăng dần — bấm để sắp giảm dần"
                      : "Bấm để sắp xếp theo cột này"
                  }
                >
                  {col.label}
                  <span className="sort-arrow" aria-hidden="true">
                    {active ? (desc ? "\u25BC" : "\u25B2") : "\u21C5"}
                  </span>
                </th>
              );
            })}
          </tr>
        </thead>
        <tbody>{children}</tbody>
      </table>
    </div>
  );
}
export function ActionLink({ to, children }: { to: string; children?: any }) {
  return (
    <Link to={to} className="link">
      {children}
    </Link>
  );
}
