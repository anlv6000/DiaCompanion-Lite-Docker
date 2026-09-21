# syntax=docker/dockerfile:1.6
# Build context = thư mục gốc repo.  Ba target:
#   runtime : ASP.NET Core 8 backend
#   dbinit  : tạo DB, áp schema, seed tài khoản + model, xuất dữ liệu (sqlcmd + python)
#   (schema : stage trung gian sinh schema.sql)

############################ build ############################
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Backend/DiaCompanion/DiaCompanion.csproj DiaCompanion/
RUN dotnet restore DiaCompanion/DiaCompanion.csproj
COPY Backend/DiaCompanion/ DiaCompanion/
RUN dotnet publish DiaCompanion/DiaCompanion.csproj -c Release -o /app --no-restore

############################ schema ###########################
# Ưu tiên tệp Backend/DiaCompanion/Database/lite_schema.sql nếu bạn tự đặt vào
# (xuất từ DB đang chạy của bạn — sát bản thật nhất). Nếu không có, sinh DDL từ
# chính mô hình EF Core (AppDbContext) bằng `dotnet ef dbcontext script`.
FROM build AS schema
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global dotnet-ef --version 8.0.8
# Chỉ để khởi tạo host lúc sinh script; KHÔNG kết nối CSDL.
ENV ConnectionStrings__Default="Server=localhost;Database=DiaCompanion;User Id=sa;Password=x;TrustServerCertificate=True" \
    Jwt__SigningKey="design-time-only-design-time-only-design-time-only" \
    Jwt__Issuer="design" Jwt__Audience="design" \
    ASPNETCORE_ENVIRONMENT=Development DOTNET_CLI_TELEMETRY_OPTOUT=1
RUN if [ -f DiaCompanion/Database/lite_schema.sql ]; then \
        cp DiaCompanion/Database/lite_schema.sql /schema.sql; \
    else \
        dotnet ef dbcontext script --project DiaCompanion/DiaCompanion.csproj --output /schema.sql; \
    fi \
 && test -s /schema.sql && grep -q "Patients" /schema.sql

############################ dbinit ###########################
# Dùng chính image SQL Server làm nền để có sẵn sqlcmd (mssql-tools18).
FROM mcr.microsoft.com/mssql/server:2022-latest AS dbinit
USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends python3 \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /init
COPY --from=schema /schema.sql /init/schema.sql
COPY Lite/seed_lite.py Lite/export_dataset.sql /init/
COPY docker/backend/dbinit.sh /init/dbinit.sh
RUN sed -i 's/\r$//' /init/dbinit.sh && chmod +x /init/dbinit.sh
ENTRYPOINT ["/init/dbinit.sh"]
CMD ["init"]

############################ runtime ##########################
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# UID 1654 = người dùng "app" của image .NET 8; image AI dùng cùng UID để hai
# container đọc/ghi chung volume ảnh.
RUN mkdir -p /data/storage && chown -R 1654:1654 /data
USER 1654
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0
EXPOSE 8080
ENTRYPOINT ["dotnet", "DiaCompanion.dll"]
