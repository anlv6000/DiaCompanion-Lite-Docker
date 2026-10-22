# DR AI-Diagnosis 1.3.1

Ngày phát hành: 20/10/2026. Nâng cấp từ DiaCompanion Lite 1.3.0 (hoặc cũ hơn) bằng cách chạy bộ cài mới như cài đè.

## 1. Đổi tên phần mềm thành DR AI-Diagnosis

Theo tên đề tài của bệnh viện. Đổi toàn bộ phần người dùng và người quản trị nhìn thấy:

| Thành phần | Trước (≤ 1.3.0) | Từ 1.3.1 |
|---|---|---|
| Tên hiển thị, Start Menu | DiaCompanion Lite | DR AI-Diagnosis |
| Thư mục cài | `C:\Program Files\DiaCompanion Lite\` | `C:\Program Files\DR AI-Diagnosis\` |
| Thư mục dữ liệu | `C:\ProgramData\DiaCompanion\` | `C:\ProgramData\DRAIDiagnosis\` |
| Database | `DiaCompanion` | `DRAIDiagnosis` |
| Dịch vụ Windows | `DiaCompanionAI`, `DiaCompanionBackend` | `DRAIDiagnosisAI`, `DRAIDiagnosisBackend` |
| Tệp chạy backend | `DiaCompanion.exe` | `DRAIDiagnosis.exe` |
| Tệp cài | `DiaCompanion-Lite-Hospital-Setup-x64-….exe` | `DR-AI-Diagnosis-Setup-x64-….exe` |
| Project Docker | `diacompanion-lite`, `diacompanion-test` | `dr-ai-diagnosis`, `dr-ai-diagnosis-test` |

Bộ cài tự chuyển dữ liệu của bản cũ (một lần): sao lưu rồi đổi tên database, chuyển ảnh và bản sao lưu, chép cấu hình (mật khẩu, khóa, cổng giữ nguyên), gỡ dịch vụ và thư mục cài cũ. Thư mục dữ liệu cũ được giữ lại dưới tên `DiaCompanion.da-chuyen-<ngày-giờ>` để người quản trị tự xóa sau khi kiểm tra. Chi tiết: `installer/README.md`, mục 6.

Không đổi: tên namespace và thư mục mã nguồn (`Backend/DiaCompanion/...`), vì không ai nhìn thấy và đổi chỉ thêm rủi ro. Người dùng phải đăng nhập lại một lần sau khi nâng cấp.

## 2. Chấm mù trên nhiều máy (gói chấm offline)

Tách vai trò để bảo đảm khách quan, không cần mạng LAN:

- **Máy 1** (máy cài phần mềm): bác sĩ khám chấm mù vòng 1 trước khi chạy AI, như cũ. Toàn bộ dữ liệu nằm ở đây.
- **Máy 2**: người chấm độc lập chấm vòng 2.
- **Máy 3**: người phân xử chấm vòng 3 cho các ảnh vòng 1 và 2 bất đồng.

Máy 2 và 3 không cài gì: tài khoản nghiên cứu ở máy 1 tạo **gói chấm** (trang *Chấm nhiều máy*), chép tệp zip sang, người chấm mở `index.html` bằng Chrome/Edge, chấm, xuất `KET-QUA-….json` và gửi về để nhập ở máy 1.

Bảo đảm chấm mù:

- Gói gồm ảnh đáy mắt, thông tin lâm sàng ẩn danh của từng ảnh (như trang chấm trong ứng dụng, xem mục 2b) và trang chấm. Không có mã bệnh nhân, ngày khám, ghi chú, kết quả AI hay phân độ vòng trước.
- Ảnh được bỏ metadata (EXIF, chú thích, thời điểm), đặt tên ngẫu nhiên và xáo thứ tự.
- Mỗi gói gắn với một tài khoản người chấm. Quy tắc giữ nguyên như chấm trong ứng dụng: người chấm vòng 1 không nhận gói vòng 2 của ảnh đó; người đã chấm vòng 1 hoặc 2 không phân xử ảnh đó.
- Ảnh đang nằm trong gói mở không xuất hiện ở hàng chờ trong ứng dụng và không vào gói khác cùng vòng, nên không có hai người chấm cùng một ảnh ở một vòng.
- Tệp kết quả có khóa gói và mã kiểm tra: tệp của gói khác hoặc bị sửa tay bị từ chối. Nhập lại cùng tệp (hoặc tệp xuất sau khi chấm thêm) chỉ ghi phần mới. Phân độ đã nhập không sửa được.

Chỉ tài khoản Research (hoặc Admin) tạo, tải, hủy và nhập gói; bác sĩ khám và người chấm không làm được. Mỗi thao tác được ghi nhật ký kiểm toán.

`grading.csv` thêm 2 cột `r2_source`, `r3_source` (`app` hoặc `pkg:<mã gói>`), tổng 27 cột. `dataset.csv` không đổi (59 cột).

Vẫn chấm được trong ứng dụng qua mạng LAN như 1.3.0; hai cách dùng song song được.

## 2b. Người chấm vòng 2 và 3 thấy thông tin lâm sàng ẩn danh

Theo yêu cầu nghiên cứu, người chấm vòng 2/3 chấm trong cùng điều kiện thông tin với bác sĩ vòng 1. Trang **Chấm ảnh độc lập** trong ứng dụng và trang chấm trong gói offline hiện khung **Thông tin lâm sàng**: mắt, tuổi, giới, loại và số năm mắc ĐTĐ, HbA1c lượt khám và HbA1c nền, huyết áp, đường huyết. Không có mã nghiên cứu, ngày khám, năm sinh và ghi chú tự do (có thể chứa thông tin định danh). Vẫn không thấy kết quả AI hay phân độ của người khác.

Thay đổi so với 1.3.0 (bản đó chỉ cho xem ảnh). Khi báo cáo, ghi rõ người chấm độc lập có thông tin lâm sàng; phân tích độ đồng thuận giữa vòng 1 và 2 không bị lệch điều kiện vì hai bên cùng có thông tin như nhau. Ảnh đã chấm vòng 2/3 bằng bản 1.3.0 (chỉ ảnh) nên được ghi nhận riêng nếu có.

## 3. Sửa lỗi "còn 1/3 kết quả AI chưa xác nhận"

Lỗi có hai nguyên nhân độc lập, cùng biểu hiện; bản 1.3.1 sửa cả hai.

### 3a. Hai mắt chạy AI xong cùng lúc (nguyên nhân chính, hay gặp ở lượt đầu tiên sau khi cài)

**Nguyên nhân.** Bác sĩ chấm xong hai mắt liên tiếp nên hai lần chạy AI song song. Lần chạy AI đầu tiên sau khi khởi động phải nạp model nên hai kết quả về gần như cùng lúc; hai giao dịch ghi kết quả (mức cô lập Serializable) khóa chéo nhau (deadlock). SQL Server hủy một giao dịch, phần mềm tự thử lại, nhưng lần thử lại ghi cả bản ghi của lần hỏng lẫn bản mới: một ảnh có **hai** kết quả AI còn hiệu lực. Thẻ mắt chỉ hiện kết quả mới nhất, bản kia bị ẩn, chưa xác nhận và chặn đóng lượt. Các lượt sau ít gặp vì model đã nạp sẵn, hai kết quả về lệch nhau vài giây. Ảnh không bị nạp trùng.

**Sửa.**

- Backend: khi một giao dịch phải thử lại, mọi bản ghi tạo hoặc đọc trong lần thử hỏng bị bỏ khỏi bộ nhớ trước khi thử lại.
- Database: mỗi ảnh chỉ một kết quả AI còn hiệu lực (chỉ mục duy nhất `UX_AiDiagnoses_Image_Active`, tạo khi không còn bản trùng).
- Giao diện: lượt đang mở có kết quả trùng hiện mục **Kết quả AI trùng** (Xem, Thu hồi bản cũ).
- Lượt đã đóng có kết quả trùng không thu hồi được trên giao diện; trong `dataset.csv` ảnh đó có hai dòng cùng `image_id` với kết quả AI như nhau. Khi phân tích, giữ dòng có `ai_diagnosis_id` lớn hơn. Bộ cài in số ảnh bị ảnh hưởng.

### 3b. Bấm "Lưu phân độ & chạy AI" hai lần

**Nguyên nhân.** Bấm "Lưu phân độ & chạy AI" hai lần liên tiếp (hoặc mạng chậm, bấm lại) có thể gửi hai yêu cầu nạp ảnh cho cùng một mắt. Giao diện chỉ hiện ảnh mới nhất mỗi mắt, nên ảnh thừa và kết quả AI của nó không xác nhận được và lượt không đóng được. Đây là đường lỗi thứ hai, ít gặp hơn 3a; vẫn chặn cả hai lớp.

**Sửa.**

- Giao diện: nút đang xử lý không nhận lần bấm thứ hai (sửa cả lỗi chung của nút: thuộc tính `disabled` từng ghi đè trạng thái đang xử lý). Hộp chấm mù vòng 1 chỉ gửi một lần.
- Backend: mỗi mắt của một lượt chỉ có một ảnh còn hiệu lực. Nạp ảnh thứ hai cho mắt đã có ảnh trả lỗi 409, hướng dẫn dùng "Thay ảnh". Hai yêu cầu gửi đồng thời: chỉ mục duy nhất trong database giữ lại một.
- Dữ liệu cũ: lượt đang có ảnh trùng hiện mục **Ảnh nạp trùng** liệt kê các ảnh bị ẩn, có nút Xem và Thu hồi (lý do ghi sẵn). Thu hồi ảnh thừa thì lượt đóng được bình thường. Bộ cài in số lượt bị ảnh hưởng; chỉ mục duy nhất được tạo ở lần cài đè sau khi đã hết ảnh trùng. Phần mềm không tự thu hồi ảnh nào.

## 4. Kiểm thử

Bộ kiểm thử Docker (`docker/test`) thêm 15 case: IMG-05 (nạp trùng mắt), IMG-06 (hai yêu cầu đồng thời), AI-02 (chạy AI hai mắt đồng thời), PKG-00 đến PKG-11 (gói chấm: tạo, hủy, nội dung zip, nhập, nhập lại, tệp bị sửa, phân quyền, gói vòng 3, cột nguồn trong `grading.csv`). Tổng 72 case. Checklist giao diện thêm UI-11 đến UI-14.

Chạy trong project Docker riêng, không chạy trên máy production:

```powershell
.\docker\test\run-tests.ps1 -Version 1.3.1 -Tester "Họ tên người test"
```

## 5. Nâng cấp

1. Sao lưu (Start Menu > DiaCompanion Lite > Sao lưu dữ liệu) trước khi nâng cấp.
2. Chạy `DR-AI-Diagnosis-Setup-x64-1.3.1.exe` bằng quyền Administrator.
3. Đọc trang kết quả cài đặt: dòng chuyển dữ liệu và số lượt có ảnh trùng.
4. Đăng nhập lại, mở các lượt có ảnh trùng (nếu có) và thu hồi ảnh thừa; chạy lại bộ cài một lần nữa để tạo chỉ mục.
5. Thêm tài khoản người chấm cho máy 2 và máy 3 (Start Menu > DR AI-Diagnosis > Thêm người chấm độc lập) nếu chưa có.
