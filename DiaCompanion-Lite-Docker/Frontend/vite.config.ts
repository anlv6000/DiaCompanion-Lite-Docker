import { defineConfig, type PluginOption } from "vite";
import react from "@vitejs/plugin-react";
import path from "path";

/* `vite build`               → bản đầy đủ  → dist/
   `vite build --mode lite`   → bản Thu thập dữ liệu → dist-lite/
   Ở mode lite, index.html được đổi điểm vào sang src/main.lite.tsx nên bundle
   chỉ chứa các trang Lite (không có triage, kê đơn, admin, blog ...). */
export default defineConfig(({ mode }) => {
  const lite = mode === "lite";
  const liteEntry: PluginOption = {
    name: "diacompanion-lite-entry",
    // order "pre": phải đổi điểm vào TRƯỚC khi Vite đọc <script type="module">
    // để gom bundle; đổi sau thì Vite vẫn build main.tsx (bản đầy đủ).
    transformIndexHtml: {
      order: "pre",
      handler(html) {
        return lite
          ? html
              .replace("/src/main.tsx", "/src/main.lite.tsx")
              .replace("Console lâm sàng", "Thu thập dữ liệu")
          : html;
      },
    },
  };
  return {
    plugins: [react(), liteEntry],
    resolve: { alias: { "@": path.resolve(__dirname, "./src") } },
    build: { outDir: lite ? "dist-lite" : "dist" },
    server: {
      port: 5173,
      // Proxy để gọi backend cùng origin khi dev (tránh CORS).
      proxy: { "/api": { target: "https://localhost:55403", changeOrigin: true } },
    },
  };
});
