// Electron shell that serves the built `dist/` over a local HTTP server
// (http://localhost:9001) instead of file:// — cleaner routing, correct MIME,
// no file:// path quirks, easier to optimize/cache.
const { app, BrowserWindow } = require("electron");
const http = require("http");
const fs = require("fs");
const path = require("path");

const DIST = path.join(__dirname, "..", process.env.DIACOMPANION_DIST || "dist");
const PREFERRED_PORT = 9001;

const MIME = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".webp": "image/webp",
  ".ico": "image/x-icon",
  ".woff2": "font/woff2",
  ".woff": "font/woff",
  ".ttf": "font/ttf",
  ".map": "application/json",
};

function createServer() {
  return http.createServer((req, res) => {
    try {
      const urlPath = decodeURIComponent((req.url || "/").split("?")[0]);
      let filePath = path.join(DIST, urlPath === "/" ? "index.html" : urlPath);
      const rel = path.relative(DIST, filePath);
      // block path traversal outside dist
      if (rel.startsWith("..") || path.isAbsolute(rel)) {
        res.statusCode = 403;
        return res.end("Forbidden");
      }
      // SPA fallback: unknown paths -> index.html
      if (!fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
        filePath = path.join(DIST, "index.html");
      }
      const ext = path.extname(filePath).toLowerCase();
      res.setHeader("Content-Type", MIME[ext] || "application/octet-stream");
      res.setHeader("Cache-Control", "no-cache");
      fs.createReadStream(filePath).pipe(res);
    } catch {
      res.statusCode = 500;
      res.end("Server error");
    }
  });
}

// try preferred port, then increment a few times if busy
function listen(server, port, triesLeft) {
  return new Promise((resolve, reject) => {
    const onError = (err) => {
      server.removeListener("error", onError);
      if (err.code === "EADDRINUSE" && triesLeft > 0) {
        listen(server, port + 1, triesLeft - 1).then(resolve, reject);
      } else {
        reject(err);
      }
    };
    server.once("error", onError);
    server.listen(port, "127.0.0.1", () => {
      server.removeListener("error", onError);
      resolve(port);
    });
  });
}

let win = null;

async function boot() {
  if (!fs.existsSync(path.join(DIST, "index.html"))) {
    console.error("Chưa có dist/. Chạy `npm run build` trước.");
    app.quit();
    return;
  }
  const server = createServer();
  const port = await listen(server, PREFERRED_PORT, 20);
  console.log(`DiaCompanion serving on http://localhost:${port}`);

  win = new BrowserWindow({
    width: 1440,
    height: 900,
    backgroundColor: "#f7f8fa",
    webPreferences: {
      preload: path.join(__dirname, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });
  await win.loadURL(`http://localhost:${port}`);
  win.on("closed", () => {
    win = null;
  });
}

app.whenReady().then(boot);
app.on("activate", () => {
  if (BrowserWindow.getAllWindows().length === 0) boot();
});
app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});
