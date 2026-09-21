import { useState, useEffect, useRef } from "react";
import { useData } from "@/contexts/DataContext";
import { useAsync, useDebounce } from "@/lib/hooks";
import {
  PageHeader,
  Panel,
  Field,
  Button,
  DataTable,
  LoadState,
  Pagination,
  StatusBadge,
  Modal,
  ConfirmDialog,
  Icon,
} from "@/components/ui";
import { blogCategories, symptomSeverities, label } from "@/lib/enums";
import { fmtDate, downloadText, toCsv } from "@/lib/format";
import { useToast } from "@/contexts/ToastContext";
import type {
  BlogPostDto,
  SaveBlogRequest,
  SymptomReportDto,
} from "@/types/api";

/* ---------------- Blog ---------------- */
export function BlogPage() {
  const data = useData();
  const toast = useToast();
  const [published, setPublished] = useState("");
  // BE /api/blog/manage nhận q + category, trước đây FE chỉ gửi published.
  const [q, setQ] = useState("");
  const dq = useDebounce(q);
  const [category, setCategory] = useState("");
  const [page, setPage] = useState(1);
  const [editor, setEditor] = useState<BlogPostDto | "new" | null>(null);
  const [confirm, setConfirm] = useState<{
    post: BlogPostDto;
    action: "publish" | "delete";
  } | null>(null);

  const list = useAsync(
    () =>
      data.blog.manage({
        q: dq.trim() || undefined,
        category: category || undefined,
        published,
        page,
        pageSize: 25,
      }),
    [dq, category, published, page],
  );

  // Đổi bộ lọc thì phải về trang 1, nếu không sẽ hiện trang trống khi kết quả
  // mới ít hơn trang đang đứng.
  useEffect(() => {
    setPage(1);
  }, [dq, category]);

  const act = async () => {
    if (!confirm) return;
    if (confirm.action === "publish")
      await data.blog.publish(
        confirm.post.id,
        !confirm.post.isPublished,
        confirm.post.rowVersion,
      );
    else await data.blog.delete(confirm.post.id, confirm.post.rowVersion);
    toast.push(
      confirm.action === "publish"
        ? confirm.post.isPublished
          ? "Đã gỡ bài."
          : "Đã đăng bài."
        : "Đã xóa/ẩn bài.",
      "success",
    );
    setConfirm(null);
    list.reload();
  };

  return (
    <>
      <PageHeader
        title="Quản lý Blog"
        subtitle="Bác sĩ chỉ quản lý bài do chính mình tạo; Admin có thể quản lý toàn bộ. Bài mới luôn được lưu nháp trước."
        actions={
          <Button kind="primary" onClick={() => setEditor("new")}>
            <Icon name="plus" />
            Soạn bài
          </Button>
        }
      />
      <Panel>
        <div className="toolbar">
          <Field labelText="Tìm kiếm" className="inline">
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="Tiêu đề hoặc nội dung"
            />
          </Field>
          <Field labelText="Chủ đề" className="inline">
            <select
              value={category}
              onChange={(e) => setCategory(e.target.value)}
            >
              <option value="">Tất cả chủ đề</option>
              {blogCategories.map((c, i) =>
                i === 0 ? null : (
                  <option value={i} key={i}>
                    {c}
                  </option>
                ),
              )}
            </select>
          </Field>
          <Field labelText="Trạng thái" className="inline">
            <select
              value={published}
              onChange={(e) => {
                setPublished(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Tất cả</option>
              <option value="true">Đã đăng</option>
              <option value="false">Bản nháp</option>
            </select>
          </Field>
          {(q || category || published) && (
            <Button
              size="sm"
              onClick={() => {
                setQ("");
                setCategory("");
                setPublished("");
                setPage(1);
              }}
            >
              Xoá lọc
            </Button>
          )}
        </div>
        <LoadState
          loading={list.loading}
          error={list.error}
          empty={!list.data?.items.length}
          onRetry={list.reload}
        >
          <DataTable
            headers={[
              "Tiêu đề",
              "Tóm tắt",
              "Chủ đề",
              "Tác giả",
              "Trạng thái",
              "Ngày đăng/tạo",
              "Thao tác",
            ]}
          >
            {list.data?.items.map((p) => (
              <tr key={p.id}>
                <td>
                  <b>{p.title}</b>
                </td>
                <td className="wrap-text">{p.summary || "—"}</td>
                <td>{label(blogCategories, p.category)}</td>
                <td>{p.authorName}</td>
                <td>
                  <StatusBadge
                    text={p.isPublished ? "Đã đăng" : "Bản nháp"}
                    kind={p.isPublished ? "ok" : "watch"}
                  />
                </td>
                <td className="mono">
                  {fmtDate(p.publishedAt || p.createdAt, true)}
                </td>
                <td>
                  <div className="actions">
                    <Button onClick={() => setEditor(p)}>Sửa</Button>
                    <Button
                      onClick={() => setConfirm({ post: p, action: "publish" })}
                    >
                      {p.isPublished ? "Gỡ" : "Đăng"}
                    </Button>
                    <Button
                      kind="danger"
                      onClick={() => setConfirm({ post: p, action: "delete" })}
                    >
                      Xóa
                    </Button>
                  </div>
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
      {editor && (
        <BlogEditor
          value={editor}
          onClose={() => setEditor(null)}
          onSaved={() => {
            setEditor(null);
            list.reload();
          }}
        />
      )}
      {confirm && (
        <ConfirmDialog
          title={
            confirm.action === "delete"
              ? "Xóa bài viết"
              : confirm.post.isPublished
                ? "Gỡ bài viết"
                : "Đăng bài viết"
          }
          message={`${confirm.post.title}. ${confirm.action === "delete" && confirm.post.isPublished ? "Bài đã đăng sẽ được ẩn bằng soft delete." : ""}`}
          danger={confirm.action === "delete"}
          onClose={() => setConfirm(null)}
          onConfirm={act}
        />
      )}
    </>
  );
}

function BlogEditor({
  value,
  onClose,
  onSaved,
}: {
  value: BlogPostDto | "new";
  onClose: () => void;
  onSaved: () => void;
}) {
  const data = useData();
  const toast = useToast();
  const isNew = value === "new";
  const detail = useAsync(
    () => (!isNew ? data.blog.get(value.id) : Promise.resolve(null)),
    [isNew ? 0 : (value as BlogPostDto).id],
  );
  const [form, setForm] = useState<SaveBlogRequest>({
    title: isNew ? "" : value.title,
    summary: isNew ? "" : value.summary || "",
    body: isNew ? "" : value.body || "",
    category: isNew ? 1 : value.category,
  });
  const [busy, setBusy] = useState(false);
  const p = (k: keyof SaveBlogRequest, v: unknown) =>
    setForm((x) => ({ ...x, [k]: v }));

  // Nạp nội dung đầy đủ khi sửa.
  if (detail.data && form.body === "" && detail.data.body) {
    // đồng bộ một lần khi detail về
  }

  const save = async () => {
    if (!form.title.trim() || !form.body.trim()) {
      toast.push("Tiêu đề và nội dung là bắt buộc.", "error");
      return;
    }
    setBusy(true);
    try {
      if (isNew) await data.blog.create(form);
      else await data.blog.update(value.id, { ...form, rowVersion: value.rowVersion });
      toast.push("Đã lưu bài viết.", "success");
      onSaved();
    } catch (e) {
      toast.push((e as Error).message, "error");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      title={isNew ? "Soạn bài mới" : "Sửa bài viết"}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Hủy</Button>
          <Button kind="primary" busy={busy} onClick={save}>
            Lưu nháp
          </Button>
        </>
      }
    >
      <LoadState loading={detail.loading} error={detail.error} empty={false}>
        <BlogForm detail={detail.data} form={form} setForm={setForm} p={p} />
      </LoadState>
    </Modal>
  );
}

function BlogForm({ detail, form, setForm, p }: any) {
  // Đồng bộ form với nội dung tải về (chỉ chạy khi id đổi).
  useSyncBlog(detail, setForm);
  return (
    <>
      <div className="form-row">
        <Field labelText="Tiêu đề" required>
          <input
            value={form.title}
            onChange={(e) => p("title", e.target.value)}
          />
        </Field>
        <Field labelText="Chủ đề">
          <select
            value={form.category}
            onChange={(e) => p("category", Number(e.target.value))}
          >
            {blogCategories.map(
              (x, i) =>
                i > 0 && (
                  <option key={i} value={i}>
                    {x}
                  </option>
                ),
            )}
          </select>
        </Field>
      </div>
      <Field labelText="Tóm tắt">
        <textarea
          value={form.summary || ""}
          onChange={(e) => p("summary", e.target.value)}
        />
      </Field>
      <Field labelText="Nội dung" required>
        <textarea
          style={{ minHeight: 260 }}
          value={form.body}
          onChange={(e) => p("body", e.target.value)}
        />
      </Field>
    </>
  );
}

function useSyncBlog(detail: any, setForm: any) {
  const done = useRef<number | null>(null);
  useEffect(() => {
    if (detail && done.current !== detail.id) {
      done.current = detail.id;
      setForm({
        title: detail.title,
        summary: detail.summary || "",
        body: detail.body || "",
        category: detail.category,
      });
    }
  }, [detail, setForm]);
}

/* ---------------- Feedback ---------------- */
export function FeedbackPage() {
  const data = useData();
  const [rating, setRating] = useState("");
  // BE /api/engagement/feedback nhận q + from + to, trước đây FE chỉ gửi rating.
  const [q, setQ] = useState("");
  const dq = useDebounce(q);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);
  const list = useAsync(
    () =>
      data.engagement.feedback({
        rating,
        q: dq.trim() || undefined,
        from: from || undefined,
        to: to || undefined,
        page,
        pageSize: 50,
      }),
    [rating, dq, from, to, page],
  );

  useEffect(() => {
    setPage(1);
  }, [dq, from, to]);
  const summary = useAsync(() => data.engagement.feedbackSummary(), []);
  const csv = () =>
    downloadText(
      "feedback.csv",
      toCsv((list.data?.items || []) as any),
      "text/csv;charset=utf-8",
    );

  return (
    <>
      <PageHeader
        title="Phản hồi lượt khám"
        subtitle="Admin xem toàn hệ thống; bác sĩ chỉ thấy phản hồi của các lượt khám mình phụ trách."
        actions={
          <Button onClick={csv} disabled={!list.data?.items.length}>
            Xuất CSV
          </Button>
        }
      />
      <div className="stats">
        <div className="stat">
          <span>Điểm trung bình</span>
          <b className="mono">{summary.data?.average ?? "—"}</b>
        </div>
        <div className="stat">
          <span>Tổng phản hồi</span>
          <b className="mono">{summary.data?.total ?? "—"}</b>
        </div>
        {[1, 2, 3, 4, 5].map((i) => (
          <div className="stat" key={i}>
            <span>{i} sao</span>
            <b className="mono">
              {summary.data?.distribution?.[String(i)] ?? "—"}
            </b>
          </div>
        ))}
      </div>
      <Panel>
        <div className="toolbar">
          <Field labelText="Tìm kiếm" className="inline">
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="Tên bệnh nhân, mã hoặc nội dung nhận xét"
            />
          </Field>
          <Field labelText="Lọc theo điểm" className="inline">
            <select
              value={rating}
              onChange={(e) => {
                setRating(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Tất cả</option>
              {[1, 2, 3, 4, 5].map((i) => (
                <option value={i} key={i}>
                  {i} sao
                </option>
              ))}
            </select>
          </Field>
          <Field labelText="Từ ngày" className="inline">
            <input
              type="date"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
            />
          </Field>
          <Field labelText="Đến ngày" className="inline">
            <input
              type="date"
              value={to}
              onChange={(e) => setTo(e.target.value)}
            />
          </Field>
          {(q || rating || from || to) && (
            <Button
              size="sm"
              onClick={() => {
                setQ("");
                setRating("");
                setFrom("");
                setTo("");
                setPage(1);
              }}
            >
              Xoá lọc
            </Button>
          )}
        </div>
        <LoadState
          loading={list.loading}
          error={list.error}
          empty={!list.data?.items.length}
          onRetry={list.reload}
        >
          <DataTable
            headers={["Ngày", "Bệnh nhân", "Điểm", "Lượt khám", "Tags", "Nhận xét"]}
          >
            {list.data?.items.map((f) => (
              <tr key={f.id}>
                <td className="mono">{fmtDate(f.createdAt, true)}</td>
                <td>
                  <b>{f.patientName || "—"}</b>
                  <div className="faint mono">{f.patientCode || ""}</div>
                </td>
                <td className="mono">{f.rating}/5</td>
                <td className="mono">{f.visitId ? `#${f.visitId}` : "—"}</td>
                <td>{f.tags || "—"}</td>
                <td className="wrap-text">{f.comment || "—"}</td>
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

/* ---------------- Symptoms ---------------- */
export function SymptomsPage() {
  const data = useData();
  const toast = useToast();
  const [pending, setPending] = useState("true");
  const [page, setPage] = useState(1);
  const [reply, setReply] = useState<SymptomReportDto | null>(null);
  const list = useAsync(
    () =>
      data.engagement.symptoms({ pendingOnly: pending, page, pageSize: 50 }),
    [pending, page],
  );

  const save = async (text: string) => {
    if (!reply) return;
    await data.engagement.reply(reply.id, text, reply.rowVersion);
    toast.push("Đã gửi trả lời bác sĩ.", "success");
    setReply(null);
    list.reload();
  };

  return (
    <>
      <PageHeader
        title="Báo cáo triệu chứng"
        subtitle="Khuyến cáo tự động và trả lời của bác sĩ được lưu ở hai trường riêng."
      />
      <Panel>
        <Field labelText="Phạm vi" className="inline">
          <select
            value={pending}
            onChange={(e) => {
              setPending(e.target.value);
              setPage(1);
            }}
          >
            <option value="true">Chờ trả lời</option>
            <option value="false">Tất cả</option>
          </select>
        </Field>
        <LoadState
          loading={list.loading}
          error={list.error}
          empty={!list.data?.items.length}
          onRetry={list.reload}
        >
          <DataTable
            headers={[
              "Bệnh nhân",
              "Triệu chứng",
              "Mức độ",
              "Khởi phát",
              "Khuyến cáo tự động",
              "Trạng thái",
              "Ngày gửi",
              "Thao tác",
            ]}
          >
            {list.data?.items.map((s) => (
              <tr key={s.id}>
                <td>{s.patientName}</td>
                <td className="wrap-text">
                  <b>{s.symptoms}</b>
                  <div className="faint">{s.description || ""}</div>
                </td>
                <td>
                  <StatusBadge
                    text={label(symptomSeverities, s.severity)}
                    kind={
                      s.severity === 3
                        ? "alert"
                        : s.severity === 2
                          ? "watch"
                          : "ok"
                    }
                  />
                </td>
                <td>{s.onsetNote || "—"}</td>
                <td className="wrap-text">{s.autoAdvice}</td>
                <td>
                  <StatusBadge
                    text={s.state}
                    kind={s.doctorReply ? "ok" : "watch"}
                  />
                </td>
                <td className="mono">{fmtDate(s.createdAt, true)}</td>
                <td>
                  <Button
                    disabled={!!s.doctorReply}
                    onClick={() => setReply(s)}
                  >
                    {s.doctorReply ? "Đã trả lời" : "Trả lời"}
                  </Button>
                </td>
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
      {reply && (
        <ReplyModal item={reply} onClose={() => setReply(null)} onSave={save} />
      )}
    </>
  );
}

function ReplyModal({
  item,
  onClose,
  onSave,
}: {
  item: SymptomReportDto;
  onClose: () => void;
  onSave: (s: string) => void;
}) {
  const [text, setText] = useState("");
  return (
    <Modal
      title={`Trả lời ${item.patientName}`}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>Hủy</Button>
          <Button
            kind="primary"
            disabled={!text.trim()}
            onClick={() => onSave(text)}
          >
            Gửi trả lời
          </Button>
        </>
      }
    >
      <Panel title="Khuyến cáo tự động">
        <p>{item.autoAdvice}</p>
      </Panel>
      <Field labelText="Phản hồi của bác sĩ" required>
        <textarea
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Không dùng kênh này thay cho cấp cứu; ghi hướng dẫn rõ ràng cho bệnh nhân."
        />
      </Field>
    </Modal>
  );
}
