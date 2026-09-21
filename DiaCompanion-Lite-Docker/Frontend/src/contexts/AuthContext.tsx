import {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  type ReactNode,
} from "react";
import { useNavigate } from "react-router-dom";
import { authApi } from "@/api/services";
import { tokenStore } from "@/api/client";
import { STORAGE_KEYS } from "@/config";
import { resolveLandingRoute } from "@/lib/permissions";
import { hasAnyRole } from "@/lib/roles";
import type { LoginResponse, Role, ChangePasswordRequest } from "@/types/api";

/* Phiên đăng nhập. Console bệnh viện: chỉ nhân viên, đăng nhập bằng email.
   Token + user giữ trong sessionStorage (mất khi đóng hẳn trình duyệt). */

interface AuthValue {
  user: LoginResponse | null;
  isAuthenticated: boolean;
  checking: boolean;
  login: (loginId: string, password: string) => Promise<LoginResponse>;
  logout: () => Promise<void>;
  refresh: () => Promise<void>;
  changePassword: (body: ChangePasswordRequest) => Promise<void>;
  clearMustChange: () => void;
  hasRole: (...roles: Role[]) => boolean;
}

const AuthContext = createContext<AuthValue | null>(null);

function readUser(): LoginResponse | null {
  try {
    return JSON.parse(sessionStorage.getItem(STORAGE_KEYS.user) || "null");
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children?: ReactNode }) {
  const [user, setUser] = useState<LoginResponse | null>(readUser);
  const [checking, setChecking] = useState(false);
  const navigate = useNavigate();

  const persist = useCallback((u: LoginResponse | null) => {
    setUser(u);
    if (u) sessionStorage.setItem(STORAGE_KEYS.user, JSON.stringify(u));
    else sessionStorage.removeItem(STORAGE_KEYS.user);
  }, []);

  const login = useCallback(
  async (loginId: string, password: string) => {
    const value = loginId.trim();

    const isEmail = value.includes("@");

    const res = await authApi.login({
      email: isEmail ? value : undefined,
      phone: isEmail ? undefined : value,
      password,
    });

    tokenStore.set(res.token || null);
    persist(res);

    navigate(
      res.mustChangePassword
        ? "/change-password"
        : resolveLandingRoute(
            res,
            res.defaultRoute,
          ),
      { replace: true },
    );

    return res;
  },
  [navigate, persist],
);

  const logout = useCallback(async () => {
    try {
      await authApi.logout();
    } catch {
      /* vẫn xoá phiên cục bộ */
    }
    tokenStore.set(null);
    persist(null);
    navigate("/login", { replace: true });
  }, [navigate, persist]);

  const refresh = useCallback(async () => {
    if (!tokenStore.get()) return;
    setChecking(true);
    try {
      const me = await authApi.me();
      persist({ ...me, token: tokenStore.get() || user?.token || "" });
    } catch {
      tokenStore.set(null);
      persist(null);
    } finally {
      setChecking(false);
    }
  }, [persist, user?.token]);

  const changePassword = useCallback(
    async (body: ChangePasswordRequest) => {
      const response = await authApi.change(body);
      tokenStore.set(response.token || null);
      persist(response);
    },
    [persist],
  );

  const clearMustChange = useCallback(() => {
    if (user) persist({ ...user, mustChangePassword: false });
  }, [persist, user]);

  // Token hết hạn giữa chừng → về login.
  useEffect(() => {
    const h = () => {
      tokenStore.set(null);
      persist(null);
      navigate("/login?expired=1", { replace: true });
    };
    window.addEventListener("dc:unauthorized", h);
    return () => window.removeEventListener("dc:unauthorized", h);
  }, [navigate, persist]);

  // Có token nhưng chưa có user (mở tab mới) → xác minh lại.
  useEffect(() => {
    if (tokenStore.get() && !user) void refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: !!user,
        checking,
        login,
        logout,
        refresh,
        changePassword,
        clearMustChange,
        hasRole: (...roles: Role[]) => !!user && hasAnyRole(user, roles),
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth phải nằm trong <AuthProvider>");
  return ctx;
}
