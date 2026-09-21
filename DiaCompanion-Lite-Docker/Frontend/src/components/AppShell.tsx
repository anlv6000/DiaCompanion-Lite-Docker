import { Fragment, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "@/contexts/AuthContext";
import { useData } from "@/contexts/DataContext";
import { useAsync } from "@/lib/hooks";
import { Icon, Modal, LoadState, Button } from "@/components/ui";
import { fmtDate, initials } from "@/lib/format";
import type { Role, NotificationDto } from "@/types/api";
import { hasAnyRole, rolesLabel } from "@/lib/roles";

/* Khung ứng dụng: điều hướng trái + thanh trên. AppShell là container cấp cao
  của layout nên được phép gọi useData (nạp thông báo). Các trang
   con render trong <main> qua <Outlet/> ở routes.

   nav lọc theo vai trò — chỉ hiện mục người dùng có quyền. Đây là web bệnh
   viện: menu chỉ gồm Bác sĩ / Lễ tân / Admin, không có gì của bệnh nhân. */
interface NavItem {
  to: string;
  label: string;
  icon: string;
  roles: Role[];
}
const NAV: [string, NavItem[]][] = [
  [
    "Lâm sàng",
    [
      {
        to: "/triage",
        label: "Triage",
        icon: "menu",
        roles: ["Doctor"],
      },
      {
        to: "/patients",
        label: "Bệnh nhân",
        icon: "users",
        roles: ["Doctor", "Receptionist"],
      },
      {
        to: "/my-visits",
        label: "Lượt khám của tôi",
        icon: "calendar",
        roles: ["Doctor"],
      },
      {
        to: "/recheck",
        label: "Tái tầm soát",
        icon: "calendar",
        roles: ["Doctor", "Receptionist"],
      },
      {
        to: "/progression",
        label: "Diễn tiến",
        icon: "chart",
        roles: ["Doctor"],
      },
      {
        to: "/symptoms",
        label: "Triệu chứng",
        icon: "heart",
        roles: ["Doctor"],
      },
    ],
  ],
  [
    "Tiếp đón",
    [
      {
        to: "/reception/patients/new",
        label: "Tạo hồ sơ",
        icon: "plus",
        roles: ["Receptionist"],
      },
      {
        to: "/reception/visits/new",
        label: "Tạo lượt khám",
        icon: "calendar",
        roles: ["Receptionist"],
      },
      {
        to: "/reception/visits",
        label: "Danh sách lượt khám",
        icon: "menu",
        roles: ["Receptionist"],
      },
    ],
  ],
  [
    "Báo cáo",
    [
      {
        to: "/conflicts",
        label: "Ca mâu thuẫn",
        icon: "warning",
        roles: ["Admin"],
      },
      {
        to: "/dashboard",
        label: "Thống kê",
        icon: "chart",
        roles: ["Doctor", "Admin"],
      },
      { to: "/blog", label: "Blog", icon: "file", roles: ["Doctor", "Admin"] },
      { to: "/feedback", label: "Phản hồi", icon: "heart", roles: ["Doctor", "Admin"] },
    ],
  ],
  [
    "Quản trị",
    [
      {
      to: "/users",
      label: "TK nhân viên",
      icon: "users",
      roles: ["Admin"],
    },

    {
      to: "/patient-accounts",
      label: "TK bệnh nhân",
      icon: "users",
      roles: ["Admin"],
    },   
      {
        to: "/reception/shifts",
        label: "Lịch ca trực",
        icon: "calendar",
        roles: ["Admin"],
      },
      { to: "/audit", label: "Nhật ký", icon: "lock", roles: ["Admin"] },
      { to: "/configs", label: "Cấu hình", icon: "settings", roles: ["Admin"] },
    ],
  ],
];


export function AppShell({ children }: { children?: React.ReactNode }) {
  const { user, logout } = useAuth();
  const data = useData();
  const { pathname } = useLocation();
  const [notices, setNotices] = useState(false);
  // Ngăn kéo điều hướng cho md/sm: dưới 1180px .side bị ẩn, nếu không có
  // ngăn kéo thì không còn đường nào để chuyển trang.
  const [navOpen, setNavOpen] = useState(false);

  const unread = useAsync(() => data.engagement.unread(), [user?.userId]);
  return (
    <div className={`app ${navOpen ? "nav-open" : ""}`.trim()}>
      {navOpen && (
        <div
          className="nav-backdrop"
          onClick={() => setNavOpen(false)}
          aria-hidden="true"
        />
      )}
      <aside className="side" onClick={() => setNavOpen(false)}>
        <div className="logo">DiaCompanion</div>
        <nav>
          {NAV.map(([group, items]) => {
            const visible = items.filter((x) => hasAnyRole(user, x.roles));
            if (!visible.length) return null;
            return (
              <Fragment key={group}>
                <div className="nav-group">{group}</div>
                {visible.map((x) => (
                  <Link
                    key={x.to}
                    to={x.to}
                    className={`navlink ${pathname.startsWith(x.to) ? "on" : ""}`}
                  >
                    <Icon name={x.icon} />
                    {x.label}
                  </Link>
                ))}
              </Fragment>
            );
          })}
        </nav>
        <div className="nav-spacer" />
        <div className="side-footer">
          {hasAnyRole(user, ["Doctor", "Receptionist"]) && (
            <Link to="/profile" className={`navlink ${pathname.startsWith("/profile") ? "on" : ""}`}>
              <Icon name="users" />
              Hồ sơ cá nhân
            </Link>
          )}
          <Link to="/change-password" className="navlink">
            <Icon name="lock" />
            Đổi mật khẩu
          </Link>
          <button className="navlink" onClick={logout}>
            <Icon name="logout" />
            Đăng xuất
          </button>
        </div>
      </aside>

      <main className="main">
        <header className="top">
          <button
            className="mobile-menu ghost icon-only"
            onClick={() => setNavOpen((v) => !v)}
            aria-label={navOpen ? "Đóng menu" : "Mở menu"}
            aria-expanded={navOpen}
            title="Menu"
          >
            <Icon name="menu" />
          </button>
          <small>Console lâm sàng — sàng lọc võng mạc ĐTĐ</small>
          <div className="top-actions">
            <button
              className="notification-button"
              title="Thông báo"
              onClick={() => setNotices(true)}
            >
              <Icon name="bell" />
              {(unread.data?.count || 0) > 0 && (
                <span className="notification-dot" />
              )}
            </button>
            {hasAnyRole(user, ["Doctor", "Receptionist"]) ? (
              <Link to="/profile" className="user-menu" title="Mở hồ sơ cá nhân">
                <span className="avatar">{initials(user?.fullName)}</span>
                <span>
                  {user?.fullName}
                  <small style={{ display: "block" }}>
                    {rolesLabel(user).toUpperCase()}
                  </small>
                </span>
              </Link>
            ) : (
              <div className="user-menu">
                <span className="avatar">{initials(user?.fullName)}</span>
                <span>
                  {user?.fullName}
                  <small style={{ display: "block" }}>
                    {rolesLabel(user).toUpperCase()}
                  </small>
                </span>
              </div>
            )}
          </div>
        </header>
        <div className="content">{children}</div>
      </main>

      {notices && (
        <NotificationsModal
          onClose={() => {
            setNotices(false);
            unread.reload();
          }}
        />
      )}
    </div>
  );
}

function NotificationsModal({ onClose }: { onClose: () => void }) {
  const data = useData();
  const list = useAsync(
    () => data.engagement.notifications({ page: 1, pageSize: 30 }),
    [],
  );
  const mark = async (id: number) => {
    await data.engagement.read(id);
    list.reload();
  };
  return (
    <Modal
      title="Thông báo"
      onClose={onClose}
      footer={
        <Button
          onClick={async () => {
            await data.engagement.readAll();
            list.reload();
          }}
        >
          Đánh dấu tất cả đã đọc
        </Button>
      }
    >
      <LoadState
        loading={list.loading}
        error={list.error}
        empty={!list.data?.items?.length}
        onRetry={list.reload}
      >
        <div className="notice-list">
          {list.data?.items.map((n: NotificationDto) => (
            <div
              key={n.id}
              className={`notice ${n.isRead ? "" : "unread"}`}
              onClick={() => !n.isRead && mark(n.id)}
            >
              <div className="split">
                <b>{n.title}</b>
                <span className="mono faint">{fmtDate(n.createdAt, true)}</span>
              </div>
              <div>{n.message}</div>
            </div>
          ))}
        </div>
      </LoadState>
    </Modal>
  );
}
