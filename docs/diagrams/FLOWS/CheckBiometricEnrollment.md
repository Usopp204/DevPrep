# FLOW — `CheckBiometricEnrollment` · BL#12 · UC-52

> Đặt tại `docs/diagrams/FLOWS/CheckBiometricEnrollment.md`
> File diagram: `#22` · `out/CheckBiometricEnrollment.drawio`
>
> Trạng thái: `ĐÃ DUYỆT 2026-08-14` — Khảo sát: đọc trực tiếp `source/`.
>
> **Nguồn gốc:** BL#12 trước đây được vẽ như Phase 0 của `#1 EnrollFaceBiometric` (23 message).
> `DIAGRAM_SCOPE.md:31-32` từng kết luận "tách riêng sẽ thành diagram rỗng". Kết luận đó đã bị
> duyentq đảo ngược 2026-08-14: UC-52 là use case độc lập, nên Phase 0 của `#1` được thay bằng
> tham chiếu chéo `[see #22]` và luồng chuyển về đây.

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Đọc trạng thái đã enroll sinh trắc theo từng modality (Face / Voice) của user hiện tại |
| Điểm vào (user thao tác gì) | **2 lối vào** — (A) mở `Profile > Identity verification` · (B) bấm **Start** trên Dashboard: gate chặn bắt đầu phỏng vấn khi chưa enroll face |
| Kết thúc (user thấy gì) | (A) Trang Identity hiện trạng thái Face/Voice + thời điểm cập nhật, hoặc banner lỗi · (B) Bị đẩy sang Profile/Identity để enroll, hoặc đi tiếp sang màn hình Verify |
| Quy mô | 11 participant · 40 message · 9 class · 2 alt |
| Service ngoài quyền sở hữu | không |
| **Gọi BYPASS Gateway?** | không (`profiles-biometrics-root-route`, không strip prefix) |
| Nhà cung cấp AI | không |
| Ghi DB | **không** — read-only hoàn toàn |
| Trừ credit | không |
| KI liên quan | không |

> **Đây là file mỏng nhất bộ (40 message).** Sàn trước đó là `#17 EvaluateInterviewAnswer` = 41.
> Ghi ra ở đây thay vì độn thêm message: luồng thật sự chỉ có bấy nhiêu — 1 endpoint read-only,
> 1 câu SELECT, không nhánh nghiệp vụ nào ngoài lỗi hạ tầng.

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| `EnrollAsync` (POST face/voice) | [ProfileBiometricsController.cs:49,54](../../../source/InterviewAPI/Controllers/ProfileBiometricsController.cs#L49) | Thuộc `#1` / `#2` |
| Phần `RecentSessions` của trang Identity | [ProfileController.cs:144-152](../../../source/WebClient/Controllers/Profile/ProfileController.cs#L144) | Khối "Test verification" tạm thời, best-effort, không thuộc UC-52 |
| Đuôi InterviewAPI của lối vào B | [ProfileBiometricsController.cs:30](../../../source/InterviewAPI/Controllers/ProfileBiometricsController.cs#L30) | Trùng hệt message 5-12 của lối vào A — thu gọn, xem §11 mục 2 |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Gate mở → chạy tiếp chuỗi cấu hình + init | `#3 ConfigureInterviewSession` | `B.1. StartInterviewAsync(...) [see #3]` | [InterviewController.cs:85](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L85) |
| Gate mở → tới màn hình xác thực khuôn mặt | `#13 VerifyFaceEntryGate` | `B.3. Biometric entry-gate screen [see #13]` | [InterviewController.cs:238-239](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L238) |
| Gate đóng → đẩy sang trang enroll | `#1 EnrollFaceBiometric` | `B.5. Redirected to enroll a face first [see #1]` | [InterviewController.cs:79-82](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L79) |
| Gate fail-open → server vẫn chặn lại ở InitAsync | `#4 StartInterviewSession` | `B.8. InitAsync re-checks the gate server-side [see #4]` | [SessionService.cs:490-497](../../../source/InterviewAPI/Services/SessionService.cs#L490) |

---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 1 | Candidate | actor | — | — |
| 2 | :Profile Identity UI | box | `Views/Profile/Identity.cshtml` | — |
| 3 | :Interview Dashboard UI | box | `Views/Interview/Index.cshtml` | — |
| 4 | :ProfileController (WebClient) | box | `Controllers/Profile/ProfileController.cs` | — |
| 5 | :InterviewController (WebClient) | box | `Controllers/Interview/InterviewController.cs` | — |
| 6 | :BiometricsService (WebClient) | box | `Services/Profile/BiometricsService.cs` | — |
| 7 | :API Gateway | box | `profiles-biometrics-root-route` | có |
| 8 | :ProfileBiometricsController (InterviewAPI) | box | `Controllers/ProfileBiometricsController.cs` | — |
| 9 | :ProfileBiometricsService (InterviewAPI) | box | `Services/ProfileBiometricsService.cs` (SUBSET) | — |
| 10 | :ProfileBiometricsRepository (InterviewAPI) | box | `Repositories/ProfileBiometricsRepository.cs` | — |
| 11 | SQL Server | cylinder | `DevPrep_Interview` | — |

## 4. BẢNG MESSAGE — các mốc nghiệp vụ

> Danh sách đầy đủ ở `specs/checkbiometricenrollment.yaml`.

| # | Nội dung | File:Line |
|---|---|---|
| 2 | Lối vào A — `GET /Profile/Identity` | [ProfileController.cs:134](../../../source/WebClient/Controllers/Profile/ProfileController.cs#L134) |
| 4 | `GET api/profiles/me/biometrics` | [BiometricsService.cs:28-29](../../../source/WebClient/Services/Profile/BiometricsService.cs#L28) |
| 5 | Gateway forward, không strip prefix | [appsettings.json:74-79](../../../source/ApiGateway/appsettings.json#L74) |
| 7 | `GetStatusAsync(userId, ct)` | [ProfileBiometricsService.cs:198](../../../source/InterviewAPI/Services/ProfileBiometricsService.cs#L198) |
| 8-9 | `ListByUserAsync` → `SELECT ProfileBiometrics WHERE UserId` | [ProfileBiometricsRepository.cs:22-25](../../../source/InterviewAPI/Repositories/ProfileBiometricsRepository.cs#L22) |
| 12 | Tách row theo `Modality` | [ProfileBiometricsService.cs:201-202](../../../source/InterviewAPI/Services/ProfileBiometricsService.cs#L201) |
| A.5 / A.12 | Fallback `Status = status ?? new BiometricStatusModel()` | [ProfileController.cs:157](../../../source/WebClient/Controllers/Profile/ProfileController.cs#L157) |
| A.9 | 503 khi service ném lỗi | [ProfileBiometricsController.cs:38-44](../../../source/InterviewAPI/Controllers/ProfileBiometricsController.cs#L38) |
| 13-15 | Lối vào B — gate trong `Start` | [InterviewController.cs:78](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L78) |

## 5. KHUNG alt / opt

| Khung | Guard | File:Line |
|---|---|---|
| alt A | đọc thành công / `GetStatusAsync` ném lỗi → 503 | [ProfileBiometricsController.cs:32-44](../../../source/InterviewAPI/Controllers/ProfileBiometricsController.cs#L32) |
| alt B | `statusOk AND FaceEnrolled` / `statusOk AND !FaceEnrolled` / `!statusOk` (fail-open) | [InterviewController.cs:78-83](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L78) |

## 6. NOTE

Không có (§0-bis).

## 7-9. CLASS

Xem `class_specs/checkbiometricenrollment.py` (9 class, back-end scope InterviewAPI).
`ProfileBiometricsService` chỉ liệt kê `GetStatusAsync` theo §2.3 — `EnrollAsync` thuộc `#1`/`#2`.
Không có class exception nào vì controller trả 503 cho mọi lỗi thay vì ném guard có kiểu.

## 10. ANOMALY / BUG

| # | Nội dung |
|---|---|
| A1 | **Gate ở lối vào B fail-open.** Điều kiện là `if (statusOk && biometricStatus is not null && !biometricStatus.FaceEnrolled)` ([InterviewController.cs:79](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L79)) — khi lookup lỗi (`statusOk == false`) thì **không chặn**, user đi tiếp. Không phải lỗ hổng: `InitAsync` kiểm lại phía server ([SessionService.cs:490-497](../../../source/InterviewAPI/Services/SessionService.cs#L490)) và từ chối. Vẽ thành section thứ 3 của alt B để chỗ này hiện rõ. |
| A2 | **Phase 0 của `#1` từng ghi sai mã lỗi**: nó vẽ `Return 400 ApiResponse.Fail(ex.Message)`, còn code trả **503 + thông điệp generic**. Bản vẽ ở đây theo code hiện tại. |

## 11. QUYẾT ĐỊNH ĐÃ DUYỆT

1. **Nâng thành UC độc lập** (duyentq, 2026-08-14) — đảo ngược `DIAGRAM_SCOPE.md:31-32`.
2. **Hai lối vào vẽ tuần tự (Nhánh A / Nhánh B), không gộp vào 1 alt.** Khác `#21`: ở đây hai lối vào đi qua **participant khác nhau** (`ProfileController` vs `InterviewController`), nên một `alt` sẽ để lại hai ngăn xếp khác nhau và vi phạm RULE3c. Dùng đúng tiền lệ `#14 ValidateEntryGateStatus` (Nhánh A/B tuần tự).
3. **Đuôi InterviewAPI của Nhánh B thu gọn** thành 1 cặp call+return, theo tiền lệ `#14`. Đây là lý do file còn 40 message thay vì ~48; vẽ lại y hệt 8 message sẽ là đúng loại trùng lặp mà đợt này đang gỡ bỏ.
4. **Phase 0 của `#1` thay bằng `[see #22]`** — 109 → 92 message, đã ghi `baseline_moved`.

## 12. THIẾU / CHƯA TRA ĐƯỢC

Không có.
