import { BrowserRouter } from "react-router-dom";
import { AuthProvider } from "@/contexts/AuthContext";
import { DataProvider } from "@/contexts/DataContext";
import { ToastProvider } from "@/contexts/ToastContext";
import { LiteRoutes } from "@/lite/LiteRoutes";

/* Cùng thứ tự provider với bản đầy đủ (app/App.tsx) để dùng lại nguyên
   AuthContext / DataContext / ToastContext, chỉ khác bảng route. */
export function LiteApp() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <DataProvider>
          <ToastProvider>
            <LiteRoutes />
          </ToastProvider>
        </DataProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}
