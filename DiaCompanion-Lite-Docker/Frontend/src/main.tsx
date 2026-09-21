import React from "react";
import ReactDOM from "react-dom/client";
import { App } from "@/app/App";
import "@/styles/app.css";

const container = document.getElementById("root");
if (!container) throw new Error("Không tìm thấy phần tử #root");

ReactDOM.createRoot(container).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
