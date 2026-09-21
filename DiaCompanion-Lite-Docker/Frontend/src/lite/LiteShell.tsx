import { useState, type ReactNode } from "react";
import { Link, useLocation } from "react-router-dom";
import { useAuth } from "@/contexts/AuthContext";
import { Icon } from "@/components/ui";
import { initials } from "@/lib/format";
import { hasRole } from "@/lib/roles";

/* Khung ứng dụng rút gọn: dùng lại đúng class CSS của AppShell nhưng chỉ có
   hai mục điều hướng, không chuông thông báo, không hồ sơ cá nhân. */
const CLINICAL_NAV = [
  { to: "/cases/new", label: "Thêm bệnh nhân", icon: "plus" },
  { to: "/cases", label: "Bệnh nhân & ca thu thập", icon: "users" },
];
const RESEARCH_NAV = [{ to: "/export", label: "Kết xuất dữ liệu", icon: "download" }];

export function LiteShell({ children }: { children?: ReactNode }) {
  const { user, logout } = useAuth();
  const { pathname } = useLocation();
  const [navOpen, setNavOpen] = useState(false);
  const clinical = hasRole(user, "Doctor") && hasRole(user, "Receptionist");
  const research = hasRole(user, "Research") || hasRole(user, "Admin");
  // Bác sĩ chỉ thấy khu lâm sàng; Research chỉ thấy kết xuất.
  const NAV = [...(clinical ? CLINICAL_NAV : []), ...(research && !clinical ? RESEARCH_NAV : [])];

  // "/cases" chỉ sáng khi không phải "/cases/new".
  const isOn = (to: string) =>
    to === "/cases"
      ? pathname === "/cases" || (pathname.startsWith("/cases/") && pathname !== "/cases/new")
      : pathname === to;

  return (
    <div className={`app ${navOpen ? "nav-open" : ""}`.trim()}>
      {navOpen && (
        <div className="nav-backdrop" onClick={() => setNavOpen(false)} aria-hidden="true" />
      )}
      <aside className="side" onClick={() => setNavOpen(false)}>
        <div className="logo">DiaCompanion</div>
        <nav>
          <div className="nav-group">Thu thập dữ liệu</div>
          {NAV.map((x) => (
            <Link key={x.to} to={x.to} className={`navlink ${isOn(x.to) ? "on" : ""}`}>
              <Icon name={x.icon} />
              {x.label}
            </Link>
          ))}
        </nav>
        <div className="nav-spacer" />
        <div className="side-footer">
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
          <small>Thu thập dữ liệu — sàng lọc võng mạc đái tháo đường</small>
          <div className="top-actions">
            <div className="user-menu">
              <span className="avatar">{initials(user?.fullName)}</span>
              <span>{user?.fullName}</span>
            </div>
          </div>
        </header>
        <div className="content">{children}</div>
      </main>
    </div>
  );
}
