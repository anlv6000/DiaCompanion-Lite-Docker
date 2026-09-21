import React from "react";
import ReactDOM from "react-dom/client";
import { LiteApp } from "@/lite/LiteApp";
import "@/styles/app.css";

/* Điểm vào của bản "Thu thập dữ liệu" (Lite).
   Build: `npm run build:lite` → dist-lite/. Bản đầy đủ vẫn dùng main.tsx. */
const container = document.getElementById("root");
if (!container) throw new Error("Không tìm thấy phần tử #root");

ReactDOM.createRoot(container).render(
  <React.StrictMode>
    <LiteApp />
  </React.StrictMode>,
);
