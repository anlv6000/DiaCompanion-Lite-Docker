import { Routes, Route, Navigate, useParams } from "react-router-dom";
import { useAuth } from "@/contexts/AuthContext";
import { ChangePasswordPage } from "@/pages/AuthPages";
import { FundusPage } from "@/pages/FundusPage";
import { LiteGate, LiteLoginPage, liteLanding } from "@/lite/LiteAuth";
import { LiteExportPage } from "@/lite/LiteExport";
import { LitePatientFormPage, LitePatientsPage } from "@/lite/LitePatients";
import { LiteCasePage } from "@/lite/LiteCase";

function CaseRoute() {
  const { id } = useParams();
  return <LiteCasePage id={Number(id)} />;
}
function EditRoute() {
  const { id } = useParams();
  return <LitePatientFormPage id={Number(id)} />;
}
function FundusRoute() {
  const { imageId } = useParams();
  return <FundusPage imageId={Number(imageId)} />;
}

/* Bảng route của bản Lite. Mọi đường dẫn khác của bản đầy đủ (/triage,
   /reception/..., /dashboard ...) đều rơi về trang chính qua route "*".
   Trang xem ảnh /fundus/:imageId dùng lại NGUYÊN trang của bản đầy đủ: chạy
   AI, xem lesion/vessel, xác nhận hoặc ghi đè phân độ. */
export function LiteRoutes() {
  const { user } = useAuth();
  return (
    <Routes>
      <Route path="/login" element={user ? <Navigate to={liteLanding(user)} replace /> : <LiteLoginPage />} />
      <Route
        path="/change-password"
        element={
          <LiteGate need="any">
            <ChangePasswordPage />
          </LiteGate>
        }
      />
      <Route path="/cases" element={<LiteGate><LitePatientsPage /></LiteGate>} />
      <Route path="/cases/new" element={<LiteGate><LitePatientFormPage /></LiteGate>} />
      <Route path="/cases/:id" element={<LiteGate><CaseRoute /></LiteGate>} />
      <Route path="/cases/:id/edit" element={<LiteGate><EditRoute /></LiteGate>} />
      <Route path="/fundus/:imageId" element={<LiteGate><FundusRoute /></LiteGate>} />
      {/* Kết xuất dữ liệu: vai trò Research (hoặc Admin). Bác sĩ đăng nhập cũng bị chặn ở backend. */}
      <Route path="/export" element={<LiteGate need="research"><LiteExportPage /></LiteGate>} />
      <Route path="*" element={<Navigate to={user ? liteLanding(user) : "/login"} replace />} />
    </Routes>
  );
}
