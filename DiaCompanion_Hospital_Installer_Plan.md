# DiaCompanion Lite --- Hospital Installer Plan

> **Mục tiêu:** chuyển bản `DiaCompanion-Lite-Docker-v2` từ hình thức
> triển khai bằng Docker Compose sang **một bộ cài Windows
> (`Setup.exe`)** có thể cài đặt offline tại bệnh viện, bao gồm Backend,
> Web, AI runtime + toàn bộ thư viện AI + model weights, database và cấu
> hình cần thiết.
>
> **Nguyên tắc:** giữ Docker deployment hiện tại làm phương án
> backup/reproducible deployment. Installer là một deployment target
> mới, không phá bản Docker đang chạy ổn.

------------------------------------------------------------------------

## 1. Mục tiêu cuối cùng

Sau khi hoàn thành, bệnh viện chỉ cần:

``` text
DiaCompanion-Hospital-Setup.exe
        ↓
Install
        ↓
SQL Server Express
Backend Service
AI Service
Web
Storage
Database
Configuration
        ↓
Health Check
        ↓
http://<server-ip>:8080
```

Máy bệnh viện **không cần** người vận hành tự cài:

-   Docker Desktop
-   Docker Engine
-   Python
-   pip
-   Node.js
-   npm
-   .NET SDK
-   NuGet
-   các Python package AI

Các công cụ build/development chỉ cần trên máy đóng gói.

------------------------------------------------------------------------

# 2. Phạm vi bản Installer

## 2.1 Thành phần phải đóng gói

-   [ ] ASP.NET Core Backend đã publish Release.
-   [ ] React/Vite `dist-lite`.
-   [ ] Python runtime cho AI.
-   [ ] FastAPI + Uvicorn.
-   [ ] PyTorch CPU.
-   [ ] TorchVision.
-   [ ] TensorFlow CPU.
-   [ ] Segmentation Models PyTorch.
-   [ ] Albumentations.
-   [ ] OpenCV.
-   [ ] NumPy.
-   [ ] Pillow.
-   [ ] scikit-image.
-   [ ] AI source code.
-   [ ] Model 1 weights.
-   [ ] Model 2 weights.
-   [ ] Model 3 weights.
-   [ ] SQL Server Express installer/package hoặc cơ chế cài SQL Server
    được xác định.
-   [ ] Database schema.
-   [ ] Lite seed scripts.
-   [ ] Initial account generation.
-   [ ] Storage directories.
-   [ ] Application configuration.
-   [ ] Windows Services.
-   [ ] Firewall rule nếu cần.
-   [ ] Health-check script.
-   [ ] Uninstaller.

## 2.2 Những thứ KHÔNG nên yêu cầu máy bệnh viện cài thủ công

-   [ ] Python
-   [ ] pip
-   [ ] Node.js
-   [ ] npm
-   [ ] .NET SDK
-   [ ] Docker Desktop
-   [ ] Visual Studio
-   [ ] Git

------------------------------------------------------------------------

# 3. Kiến trúc deployment mục tiêu

``` text
Windows Hospital Server
│
├── SQL Server 2022 Express
│       └── DiaCompanion
│
├── DiaCompanion Backend Service
│       └── ASP.NET Core 8
│             └── HTTP :8080 hoặc localhost port nội bộ
│
├── DiaCompanion AI Service
│       └── Embedded Python Runtime
│             ├── FastAPI
│             ├── PyTorch
│             ├── TorchVision
│             ├── TensorFlow CPU
│             ├── OpenCV
│             ├── SMP
│             ├── Albumentations
│             ├── scikit-image
│             └── 3 model weights
│
├── DiaCompanion Web
│       └── React static files
│
├── Storage
│       ├── fundus/
│       └── ai_masks/
│
└── Configuration
        ├── backend config
        ├── AI config
        └── installer config
```

### Dependency order

``` text
SQL Server
    ↓
Database initialization
    ↓
AI Service
    ↓
Backend
    ↓
Web
```

------------------------------------------------------------------------

# 4. Giai đoạn 0 --- Freeze bản Docker hiện tại

> Không sửa deployment Docker đang ổn. Dùng nó làm baseline để so sánh
> Installer.

-   [ ] Tag/commit chính xác bản Lite hiện tại.
-   [ ] Lưu `docker-compose.yml`.
-   [ ] Lưu các Dockerfile hiện tại.
-   [ ] Ghi lại version image:
    -   [ ] SQL Server
    -   [ ] Python
    -   [ ] .NET
    -   [ ] Node
    -   [ ] Nginx
-   [ ] Ghi lại cấu hình `.env` dùng khi test.
-   [ ] Ghi lại model weights và SHA-256 của từng file.
-   [ ] Chạy Docker baseline end-to-end:
    -   [ ] Login
    -   [ ] Create patient
    -   [ ] Create visit
    -   [ ] Input measurements
    -   [ ] Upload OD/OS
    -   [ ] Quality check
    -   [ ] Run AI
    -   [ ] Review AI
    -   [ ] Approve/Override
    -   [ ] Close visit
    -   [ ] Progression
    -   [ ] Research CSV export
-   [ ] Lưu kết quả baseline để so sánh với Installer.

------------------------------------------------------------------------

# 5. Giai đoạn 1 --- Chốt môi trường Windows mục tiêu

## 5.1 Operating System

-   [ ] Chốt Windows 10/11 hoặc Windows Server version hỗ trợ.
-   [ ] Kiểm tra x64.
-   [ ] Kiểm tra quyền Administrator khi cài.
-   [ ] Kiểm tra Windows Defender/antivirus không chặn AI runtime.
-   [ ] Kiểm tra policy của bệnh viện đối với Windows Service.

## 5.2 CPU/GPU

Bản Docker hiện tại đang dùng:

``` text
PyTorch CPU
TensorFlow CPU
```

Vì vậy bản Installer đầu tiên nên giữ **CPU deployment** để giảm biến
số.

-   [ ] Xác định CPU tối thiểu.
-   [ ] Xác định RAM tối thiểu.
-   [ ] Đo thời gian inference thực tế trên máy mục tiêu.
-   [ ] Nếu sau này cần GPU:
    -   [ ] tạo package GPU riêng;
    -   [ ] khóa NVIDIA driver/CUDA/PyTorch/TensorFlow compatibility;
    -   [ ] không trộn GPU dependency vào package CPU đầu tiên.

------------------------------------------------------------------------

# 6. Giai đoạn 2 --- Freeze Python AI environment

Đây là phần quan trọng nhất của Installer.

## 6.1 Không dùng version mở

Hiện `ai_service/requirements.txt` đang dùng nhiều dependency dạng:

``` text
package>=version
```

Không nên dùng trực tiếp cho hospital installer.

-   [ ] Xác định version thực tế đã chạy thành công.
-   [ ] Tạo `requirements.lock.txt`.
-   [ ] Pin chính xác:
    -   [ ] fastapi
    -   [ ] uvicorn
    -   [ ] pydantic
    -   [ ] torch
    -   [ ] torchvision
    -   [ ] tensorflow
    -   [ ] segmentation-models-pytorch
    -   [ ] albumentations
    -   [ ] opencv-python
    -   [ ] numpy
    -   [ ] Pillow
    -   [ ] scikit-image
-   [ ] Ghi lại Python version chính xác.
-   [ ] Kiểm tra TensorFlow/Keras compatibility với `best_model.keras`.
-   [ ] Kiểm tra PyTorch/TorchVision compatibility với `.pth`.

## 6.2 Tạo runtime offline

Không để installer chạy:

``` text
pip install -r requirements.txt
```

từ Internet.

Thay vào đó:

-   [ ] Build Python runtime trước.
-   [ ] Cài toàn bộ dependency vào runtime staging directory.
-   [ ] Hoặc chuẩn bị wheelhouse offline.
-   [ ] Kiểm tra native DLL.
-   [ ] Kiểm tra OpenCV runtime dependency.
-   [ ] Kiểm tra TensorFlow runtime dependency.
-   [ ] Kiểm tra PyTorch runtime dependency.
-   [ ] Kiểm tra multiprocessing/subprocess nếu có.
-   [ ] Kiểm tra import toàn bộ package sau khi copy sang máy sạch.

Mục tiêu:

``` text
Hospital machine
    ↓
copy runtime
    ↓
python.exe
    ↓
FastAPI
    ↓
AI works
```

Không cần Internet.

------------------------------------------------------------------------

# 7. Giai đoạn 3 --- Đóng gói AI Models

AI hiện có 3 model branch.

## Model 1 --- DR grading

-   [ ] EfficientNet-B4.
-   [ ] Xác định chính xác số fold weights được giao.
-   [ ] Copy weights vào: `models/model_1/weights/`
-   [ ] Ghi SHA-256.
-   [ ] Kiểm tra model loading trên máy sạch.
-   [ ] Kiểm tra output R0--R4.
-   [ ] Kiểm tra warm-up inference.

## Model 2 --- Lesion segmentation

-   [ ] U-Net.
-   [ ] Copy: `tjdr_unet_v4_best.pth`
-   [ ] Ghi SHA-256.
-   [ ] Kiểm tra MA/HE/EX/SE.
-   [ ] Kiểm tra mask output.
-   [ ] Kiểm tra output directory.

## Model 3 --- Vessel / fractal

-   [ ] Keras model.
-   [ ] Copy: `best_model.keras`
-   [ ] Ghi SHA-256.
-   [ ] Kiểm tra TensorFlow load.
-   [ ] Kiểm tra vessel mask.
-   [ ] Kiểm tra fractal output.
-   [ ] Kiểm tra OD/OS handling.

## Model manifest

Tạo:

``` text
ai/models/model-manifest.json
```

Ví dụ:

``` json
{
  "model_1": {
    "version": "....",
    "file": "....",
    "sha256": "...."
  },
  "model_2": {
    "version": "....",
    "file": "....",
    "sha256": "...."
  },
  "model_3": {
    "version": "....",
    "file": "....",
    "sha256": "...."
  }
}
```

-   [ ] Installer kiểm tra hash trước khi start AI.
-   [ ] Nếu thiếu/sai weight → AI Service không start hoặc báo lỗi rõ
    ràng.
-   [ ] Không cho phép silently chạy model khác version.

------------------------------------------------------------------------

# 8. Giai đoạn 4 --- Publish Backend

Backend hiện là ASP.NET Core 8.

-   [ ] `dotnet build`.
-   [ ] Chạy test compile trên máy build.
-   [ ] `dotnet publish -c Release`.
-   [ ] Quyết định framework-dependent hay self-contained.

Khuyến nghị:

``` text
win-x64
self-contained
```

để giảm dependency bên ngoài.

-   [ ] Kiểm tra `appsettings`.
-   [ ] Không hard-code password.
-   [ ] Không hard-code JWT secret.
-   [ ] Không ghi connection string có password vào log.
-   [ ] Đặt secret/config qua installer hoặc protected config.
-   [ ] Kiểm tra `/health`.
-   [ ] Kiểm tra AI service URL.
-   [ ] Kiểm tra storage path.

------------------------------------------------------------------------

# 9. Giai đoạn 5 --- Publish Frontend

Frontend đã có:

``` text
npm run build:lite
```

-   [ ] Chạy `npm ci`.
-   [ ] Chạy `npm run build:lite`.
-   [ ] Chạy `tsc`.
-   [ ] Kiểm tra `dist-lite`.
-   [ ] Copy `config.js`.
-   [ ] Cấu hình API origin.
-   [ ] Không đóng gói `node_modules`.
-   [ ] Không yêu cầu Node.js trên máy bệnh viện.

Frontend output cuối:

``` text
web/
├── index.html
├── assets/
└── config.js
```

------------------------------------------------------------------------

# 10. Giai đoạn 6 --- Database Installer

Đây là điểm cần xử lý kỹ vì Lite README hiện ghi:

> Repo không có một script tạo schema từ đầu; Database folder chủ yếu là
> patch.

Do đó:

-   [ ] Tạo **một schema baseline duy nhất** cho bản Installer.
-   [ ] Không bắt installer chạy tuần tự toàn bộ patch cũ nếu không cần.
-   [ ] Generate schema từ database chuẩn đã kiểm chứng.
-   [ ] Review schema.
-   [ ] Test trên database trống.
-   [ ] Tạo database `DiaCompanion`.
-   [ ] Tạo login/user phù hợp.
-   [ ] Apply schema.
-   [ ] Apply Lite seed.
-   [ ] Seed ModelVersion.
-   [ ] Seed initial clinical account.
-   [ ] Seed Research account nếu được cấu hình.
-   [ ] Xóa temporary seed/password artifacts sau khi init.

## Database migration strategy

Chọn một trong hai:

### Phương án A --- Fresh install

``` text
Create DB
↓
Apply complete schema.sql
↓
Seed
```

### Phương án B --- Upgrade

``` text
Existing DB
↓
Migration/version check
↓
Apply required migrations
```

**Installer v1 nên ưu tiên Fresh Install.**

Upgrade/migration có thể làm ở phase sau.

------------------------------------------------------------------------

# 11. Giai đoạn 7 --- Storage

Tạo:

``` text
C:\ProgramData\DiaCompanion\
├── storage\
│   ├── fundus\
│   └── ai_masks\
├── logs\
├── config\
└── backup\
```

Không nên lưu dữ liệu bệnh nhân bên trong:

``` text
C:\Program Files\
```

-   [ ] Tạo thư mục storage.
-   [ ] Set NTFS permission.
-   [ ] Backend có quyền read/write.
-   [ ] AI có quyền read fundus/write masks.
-   [ ] User bình thường không được tùy ý sửa model weights.
-   [ ] Model weights để read-only nếu có thể.
-   [ ] Test upload.
-   [ ] Test AI mask.
-   [ ] Test delete/void logic.
-   [ ] Test backup.

------------------------------------------------------------------------

# 12. Giai đoạn 8 --- Tách Web khỏi Docker/Nginx

Có hai phương án.

## Phương án khuyến nghị

Cho ASP.NET Core serve static React files.

``` text
Browser
   ↓
Backend :8080
   ├── React static files
   └── /api/*
```

-   [ ] Copy `dist-lite` vào Backend publish directory.
-   [ ] Enable static files/fallback routing.
-   [ ] Kiểm tra React Router.
-   [ ] Kiểm tra `/fundus/:id`.
-   [ ] Kiểm tra refresh page.
-   [ ] Kiểm tra `/api/*`.

Ưu điểm:

-   Không cần Nginx.
-   Không cần Node.
-   Không cần thêm Windows service.
-   Installer đơn giản hơn.

## Phương án thay thế

Giữ Nginx Windows:

``` text
Nginx
 ├── static React
 └── /api → Backend
```

Chỉ chọn nếu có lý do kỹ thuật rõ ràng.

------------------------------------------------------------------------

# 13. Giai đoạn 9 --- Windows Services

Tạo ít nhất:

``` text
DiaCompanionAI
DiaCompanionBackend
```

SQL Server do SQL Server installer/service quản lý.

## AI Service

``` text
DiaCompanionAI
    ↓
embedded python
    ↓
uvicorn
    ↓
127.0.0.1:8000
```

-   [ ] Service auto-start.
-   [ ] Restart on failure.
-   [ ] Log stdout/stderr.
-   [ ] Health check `/health`.
-   [ ] Start after storage is ready.

## Backend

``` text
DiaCompanionBackend
    ↓
ASP.NET Core
    ↓
:8080
```

-   [ ] Service auto-start.
-   [ ] Restart on failure.
-   [ ] Depends on SQL Server.
-   [ ] Depends on AI service.
-   [ ] `/health`.

## Start order

``` text
SQL Server
   ↓
DB initialization
   ↓
AI
   ↓
Backend
```

------------------------------------------------------------------------

# 14. Giai đoạn 10 --- Installer

Khuyến nghị dùng **Inno Setup** cho Windows Installer v1.

Tạo:

``` text
installer/
├── DiaCompanion.iss
├── assets/
├── scripts/
├── config/
└── publish/
```

Installer cần thực hiện:

-   [ ] Check Windows x64.
-   [ ] Check Administrator.
-   [ ] Check disk space.
-   [ ] Check RAM/CPU requirement.
-   [ ] Install SQL Server Express nếu chưa có.
-   [ ] Create application directories.
-   [ ] Copy AI runtime.
-   [ ] Copy AI weights.
-   [ ] Copy Backend.
-   [ ] Copy Web.
-   [ ] Create storage directories.
-   [ ] Generate secrets.
-   [ ] Generate application configuration.
-   [ ] Create database.
-   [ ] Run seed.
-   [ ] Register AI service.
-   [ ] Register Backend service.
-   [ ] Configure firewall.
-   [ ] Start services.
-   [ ] Run health checks.
-   [ ] Create desktop/browser shortcut.
-   [ ] Write installation log.

------------------------------------------------------------------------

# 15. Giai đoạn 11 --- Configuration Wizard

Không nên bắt kỹ thuật viên sửa `.env` bằng tay.

Installer nên hỏi:

### Hospital

-   [ ] Hospital name.
-   [ ] License/research code.
-   [ ] Installation directory.

### Server

-   [ ] Web port.
-   [ ] LAN access: Yes/No.
-   [ ] Server IP/documentation.

### Initial account

-   [ ] Doctor email.
-   [ ] Doctor name.
-   [ ] Research account: Yes/No.

### Database

-   [ ] SQL Server instance.
-   [ ] SA/setup credentials nếu cần.

### AI

-   [ ] CPU mode.
-   [ ] AI model directory.
-   [ ] Model validation.

------------------------------------------------------------------------

# 16. Giai đoạn 12 --- Network / Firewall

Mặc định:

``` text
AI :8000
    ↓
localhost only

Backend
    ↓
LAN port 8080
```

Không expose AI service ra LAN.

-   [ ] AI bind `127.0.0.1`.
-   [ ] Backend gọi AI qua localhost.
-   [ ] Chỉ mở backend/web port ra LAN.
-   [ ] Không mở SQL Server 1433 nếu không cần.
-   [ ] Windows Firewall rule chỉ cho subnet cần thiết.
-   [ ] Test từ máy bác sĩ khác trong LAN.
-   [ ] Test không truy cập được `:8000` từ máy ngoài.

------------------------------------------------------------------------

# 17. Giai đoạn 13 --- Secrets

Installer phải tạo:

``` text
JWT signing key
DB password
other application secrets
```

-   [ ] Không commit secret.
-   [ ] Không ghi secret vào Git.
-   [ ] Không đưa secret thật vào installer source.
-   [ ] Không log secret.
-   [ ] Không hiển thị DB password trong log.
-   [ ] Initial password chỉ hiển thị một lần.
-   [ ] Bắt buộc đổi password lần đầu.
-   [ ] Xóa temporary credential file.

------------------------------------------------------------------------

# 18. Giai đoạn 14 --- Health Check

Tạo script:

``` text
installer/scripts/health-check.ps1
```

Kiểm tra:

``` text
[ ] SQL Server running
[ ] Database exists
[ ] Backend service running
[ ] Backend /health = OK
[ ] AI service running
[ ] AI /health = OK
[ ] Model 1 loaded
[ ] Model 2 loaded
[ ] Model 3 loaded
[ ] Storage writable
[ ] Web accessible
[ ] API accessible
```

Installer chỉ báo:

``` text
Installation completed successfully
```

khi các critical check pass.

------------------------------------------------------------------------

# 19. Giai đoạn 15 --- End-to-End Acceptance Test

Trên **một máy Windows sạch**, không có:

-   [ ] Python
-   [ ] Node
-   [ ] Docker
-   [ ] .NET SDK

cài sẵn.

Chạy:

``` text
Setup.exe
```

Sau đó kiểm tra toàn bộ flow Lite:

``` text
Login
  ↓
Create Patient
  ↓
Create Visit
  ↓
Enter HbA1c/BP/Glucose
  ↓
Upload OD
  ↓
Upload OS
  ↓
Image Quality
  ↓
Run AI
  ↓
DR Grade
  ↓
Lesion
  ↓
Vessel/Fractal
  ↓
Deferral
  ↓
Doctor Approve/Override
  ↓
Close Visit
  ↓
Patient result
  ↓
Progression
  ↓
Research CSV
```

------------------------------------------------------------------------

# 20. Giai đoạn 16 --- AI-specific Acceptance Test

Tối thiểu chuẩn bị một bộ test cố định.

## Model 1

-   [ ] 10--20 ảnh test.
-   [ ] Expected output range R0--R4.
-   [ ] Record inference time.

## Model 2

-   [ ] 10--20 ảnh.
-   [ ] Check mask generated.
-   [ ] Check MA/HE/EX/SE output.

## Model 3

-   [ ] OD test.
-   [ ] OS test.
-   [ ] Check vessel mask.
-   [ ] Check FD.
-   [ ] Check quadrant output.
-   [ ] Check no accidental OS orientation inversion.

## Full AI

-   [ ] All 3 models load in same process.
-   [ ] First inference.
-   [ ] Second inference.
-   [ ] Repeated inference.
-   [ ] Multiple patients.
-   [ ] Multiple visits.
-   [ ] Multiple images.

------------------------------------------------------------------------

# 21. Giai đoạn 17 --- Failure / Recovery Test

Phải test:

-   [ ] AI service stopped.
-   [ ] Backend stopped.
-   [ ] SQL Server stopped.
-   [ ] Missing model weight.
-   [ ] Corrupted model weight.
-   [ ] Invalid fundus image.
-   [ ] Storage unavailable.
-   [ ] Disk almost full.
-   [ ] Machine reboot.
-   [ ] Service restart.
-   [ ] Network temporarily disconnected.

Expected behavior phải rõ:

``` text
AI unavailable
    ↓
Backend does NOT create fake diagnosis
    ↓
User receives error
    ↓
Service can restart
    ↓
System recovers
```

------------------------------------------------------------------------

# 22. Giai đoạn 18 --- Uninstall / Reinstall

## Uninstall

-   [ ] Stop Backend service.
-   [ ] Stop AI service.
-   [ ] Remove application binaries.
-   [ ] Remove services.
-   [ ] Keep clinical data by default.
-   [ ] Keep database by default.
-   [ ] Ask before deleting patient data.
-   [ ] Keep backup.

## Reinstall

-   [ ] Detect existing database.
-   [ ] Do not overwrite data accidentally.
-   [ ] Reinstall services.
-   [ ] Reconnect existing DB.
-   [ ] Validate model versions.

------------------------------------------------------------------------

# 23. Giai đoạn 19 --- Backup / Restore

Tạo tài liệu:

``` text
HOSPITAL_BACKUP.md
```

Backup gồm:

``` text
SQL database
+
fundus/
+
ai_masks/
+
configuration
+
model manifest
```

-   [ ] SQL backup script.
-   [ ] Storage backup script.
-   [ ] Restore procedure.
-   [ ] Test restore on another machine.
-   [ ] Document backup frequency.
-   [ ] Document retention policy.

------------------------------------------------------------------------

# 24. Giai đoạn 20 --- Logging / Diagnostics

Tạo:

``` text
C:\ProgramData\DiaCompanion\logs\
```

Phân tách:

``` text
backend.log
ai.log
installer.log
healthcheck.log
```

Không ghi:

-   [ ] password
-   [ ] JWT secret
-   [ ] SQL connection string có password

Có ghi:

-   [ ] startup
-   [ ] shutdown
-   [ ] model load
-   [ ] model version
-   [ ] inference error
-   [ ] service error
-   [ ] health check
-   [ ] database connection failure

------------------------------------------------------------------------

# 25. Giai đoạn 21 --- Versioning

Đặt version thống nhất:

``` text
DiaCompanion Lite
Application Version
Backend Version
AI Service Version
Model 1 Version
Model 2 Version
Model 3 Version
Database Schema Version
Installer Version
```

Ví dụ:

``` text
Application: 1.0.0
AI Service: 1.0.0
Model 1: M1-2026-08
Model 2: M2-2026-08
Model 3: M3-2026-08
DB Schema: 1
Installer: 1.0.0
```

Installer ghi version vào:

``` text
SystemConfig / ModelVersions
```

nếu schema hiện tại hỗ trợ.

------------------------------------------------------------------------

# 26. Giai đoạn 22 --- Build Pipeline

Tạo một script duy nhất:

``` text
build-installer.ps1
```

Luồng:

``` text
Clean
 ↓
Validate models
 ↓
dotnet build
 ↓
dotnet publish
 ↓
npm ci
 ↓
npm run build:lite
 ↓
Prepare Python runtime
 ↓
Copy AI source
 ↓
Copy weights
 ↓
Prepare DB schema
 ↓
Prepare installer staging
 ↓
Build Inno Setup
 ↓
SHA256
 ↓
Output Setup.exe
```

Output:

``` text
release/
└── DiaCompanion-Lite-Hospital-Setup-x64-1.0.0.exe
```

------------------------------------------------------------------------

# 27. Giai đoạn 23 --- Offline Installer Validation

Đây là test bắt buộc.

Trên máy sạch:

``` text
Internet = OFF
```

Sau đó:

``` text
Setup.exe
```

Nếu cài thành công và AI chạy:

``` text
PASS
```

Nếu installer cần:

``` text
pip install
npm install
docker pull
nuget restore
```

thì:

``` text
FAIL
```

------------------------------------------------------------------------

# 28. Giai đoạn 24 --- So sánh với Docker Baseline

Sau khi Installer chạy:

  Test                  Docker   Installer
  ------------------- -------- -----------
  Login                   PASS       \[ \]
  Create patient          PASS       \[ \]
  Create visit            PASS       \[ \]
  Upload OD               PASS       \[ \]
  Upload OS               PASS       \[ \]
  AI DR                   PASS       \[ \]
  AI lesion               PASS       \[ \]
  AI vessel/fractal       PASS       \[ \]
  Deferral                PASS       \[ \]
  Approve                 PASS       \[ \]
  Override                PASS       \[ \]
  Close visit             PASS       \[ \]
  Progression             PASS       \[ \]
  Research CSV            PASS       \[ \]
  Reboot recovery          N/A       \[ \]
  Offline install          N/A       \[ \]

Mục tiêu:

> **Installer behavior = Docker baseline behavior**

ngoại trừ khác biệt deployment/runtime.

------------------------------------------------------------------------

# 29. Giai đoạn 25 --- Tài liệu bàn giao bệnh viện

Tạo tối thiểu:

``` text
Hospital_Installation_Guide.md
Hospital_Admin_Guide.md
Hospital_Operation_Guide.md
Hospital_Backup_Restore.md
Hospital_Troubleshooting.md
System_Requirements.md
AI_Model_Manifest.md
CHANGELOG.md
```

## Installation Guide

-   Setup
-   configuration
-   initial account
-   first login

## Operation Guide

-   Login
-   Patient
-   Visit
-   Fundus
-   AI
-   Doctor confirmation
-   Close visit
-   CSV research export

## Troubleshooting

Ví dụ:

``` text
AI unavailable
→ Check DiaCompanionAI service

Database unavailable
→ Check SQL Server service

Web unavailable
→ Check DiaCompanionBackend

AI model not found
→ Check model manifest/weights
```

------------------------------------------------------------------------

# 30. Definition of Done

Installer chỉ được coi là hoàn thành khi tất cả điều kiện sau đạt:

-   [ ] Setup chạy trên Windows sạch.
-   [ ] Không cần Docker.
-   [ ] Không cần Python cài sẵn.
-   [ ] Không cần Node cài sẵn.
-   [ ] Không cần .NET SDK.
-   [ ] Không cần Internet sau khi có bộ cài.
-   [ ] SQL Server được cài/khởi tạo.
-   [ ] Database được tạo đúng.
-   [ ] Backend service tự start.
-   [ ] AI service tự start.
-   [ ] 3 model load thành công.
-   [ ] Web chạy.
-   [ ] LAN client truy cập được.
-   [ ] AI service không exposed trực tiếp ra LAN.
-   [ ] Full Lite workflow pass.
-   [ ] Reboot pass.
-   [ ] Service recovery pass.
-   [ ] Backup/restore pass.
-   [ ] Uninstall không xóa dữ liệu ngoài ý muốn.
-   [ ] Model hash được kiểm tra.
-   [ ] Version được ghi nhận.
-   [ ] Có installation/operation/troubleshooting guide.
-   [ ] Docker baseline vẫn chạy độc lập.

------------------------------------------------------------------------

# 31. Thứ tự thực hiện đề xuất

Không làm Installer ngay từ đầu. Làm theo thứ tự:

``` text
PHASE 1
Freeze Docker baseline
        ↓
PHASE 2
Freeze AI Python dependencies
        ↓
PHASE 3
Prepare model manifest + weights
        ↓
PHASE 4
Publish Backend
        ↓
PHASE 5
Build Frontend
        ↓
PHASE 6
Create standalone DB schema
        ↓
PHASE 7
Create embedded AI runtime
        ↓
PHASE 8
Create Windows Services
        ↓
PHASE 9
Integrate Web + Backend
        ↓
PHASE 10
Create Inno Setup
        ↓
PHASE 11
Fresh-machine installation test
        ↓
PHASE 12
E2E test
        ↓
PHASE 13
Offline test
        ↓
PHASE 14
Reboot/recovery test
        ↓
PHASE 15
Backup/restore test
        ↓
PHASE 16
Hospital handover package
```

------------------------------------------------------------------------

# 32. Các vấn đề cần giải quyết trước khi bắt đầu coding Installer

Theo bản `DiaCompanion-Lite-Docker-v2` hiện tại, cần chốt trước:

1.  **Python version chính xác** của AI runtime.
2.  **PyTorch version chính xác**.
3.  **TorchVision version chính xác**.
4.  **TensorFlow version chính xác**.
5.  Model 1 weights chính thức gồm những file nào.
6.  Model 2 weight chính thức.
7.  Model 3 weight chính thức.
8.  Schema baseline của SQL Server.
9.  Windows version mục tiêu.
10. CPU-only hay có GPU package.
11. Port Web chính thức.
12. Có cho phép LAN access hay chỉ localhost.
13. Storage location chính thức.
14. Initial account policy.
15. Có cài SQL Server Express tự động hay yêu cầu SQL Server có sẵn.
16. Có cần upgrade installer hay chỉ fresh install ở v1.

------------------------------------------------------------------------

# 33. Deliverables cuối cùng

Sau toàn bộ công việc, repository nên có:

``` text
installer/
├── DiaCompanion.iss
├── scripts/
│   ├── build-installer.ps1
│   ├── health-check.ps1
│   ├── install-db.ps1
│   ├── configure.ps1
│   └── register-services.ps1
├── config/
│   └── templates/
├── db/
│   └── schema.sql
├── services/
│   ├── DiaCompanionAI.xml
│   └── DiaCompanionBackend.xml
└── README.md

release/
└── DiaCompanion-Lite-Hospital-Setup-x64-1.0.0.exe
```

Kèm:

``` text
docs/
├── Hospital_Installation_Guide.md
├── Hospital_Admin_Guide.md
├── Hospital_Operation_Guide.md
├── Hospital_Backup_Restore.md
├── Hospital_Troubleshooting.md
├── System_Requirements.md
└── AI_Model_Manifest.md
```

------------------------------------------------------------------------

## Ghi chú riêng cho bản `DiaCompanion-Lite-Docker-v2`

Bản hiện tại đã có sẵn nhiều thành phần thuận lợi để chuyển sang
Installer:

-   `ai_service/requirements.txt`
-   3 AI model runners
-   Docker AI image
-   Docker Backend publish process
-   Lite seed scripts
-   Lite schema generation path
-   `build:lite`
-   `public/config.js`
-   health endpoints
-   model-version handling
-   storage separation
-   Docker service dependency order

Do đó **không cần viết lại hệ thống**. Công việc chủ yếu là tạo một
**Windows deployment layer** thay thế Docker Compose và biến các
dependency hiện đang được Docker cung cấp thành các dependency được đóng
gói trong Installer.
