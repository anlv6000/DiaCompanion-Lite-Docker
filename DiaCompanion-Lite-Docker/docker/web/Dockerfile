# syntax=docker/dockerfile:1.6
# Web Lite: build React (mode lite) rồi phục vụ bằng nginx + reverse proxy /api → backend.
FROM node:20-alpine AS build
WORKDIR /web
COPY Frontend/package.json Frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY Frontend/ ./
RUN npm run build:lite

FROM nginx:1.27-alpine
COPY docker/web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist-lite/ /usr/share/nginx/html/
# Cùng origin: trình duyệt gọi /api/* và nginx chuyển tiếp → không cần CORS.
COPY docker/web/config.js /usr/share/nginx/html/config.js
EXPOSE 80
