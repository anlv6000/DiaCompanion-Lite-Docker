import { BrowserRouter } from "react-router-dom";
import { AuthProvider } from "@/contexts/AuthContext";
import { DataProvider } from "@/contexts/DataContext";
import { ToastProvider } from "@/contexts/ToastContext";
import { AppRoutes } from "@/routes";

/* Thứ tự provider:
   BrowserRouter — để AuthProvider dùng được useNavigate (phải nằm trong router).
   AuthProvider  — phiên đăng nhập.
   DataProvider  — cửa duy nhất tới backend (mọi page lấy dữ liệu ở đây).
   ToastProvider — thông báo nhanh.                                             */
export function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <DataProvider>
          <ToastProvider>
            <AppRoutes />
          </ToastProvider>
        </DataProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}
