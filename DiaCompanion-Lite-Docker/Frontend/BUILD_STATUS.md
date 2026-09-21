# Trạng thái kiểm tra Frontend

## TypeScript

Đã chạy:

```bash
node node_modules/typescript/bin/tsc -b
```

Kết quả: **PASS**, không có lỗi TypeScript.

## Vite build

Đã thử chạy Vite build trong môi trường xử lý. Source ZIP chứa `node_modules` được cài cho Windows, còn môi trường xử lý là Linux nên Rollup thiếu optional native package `@rollup/rollup-linux-x64-gnu`.

Đây là lỗi môi trường/phụ thuộc native, không phải lỗi TypeScript của source. ZIP bàn giao không kèm `node_modules`.

Trên máy Windows của dự án, chạy lại:

```bash
npm ci
npm run build
```
