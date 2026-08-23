# FLOW — `ValidateEntryGateStatus` · BL#49 · UC-10

> Đặt tại `docs/diagrams/FLOWS/ValidateEntryGateStatus.md`
>
> Trạng thái: `DRAFT` — Khảo sát: luồng 1, lô `A`, `2026-08-02`

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Quyết định read-only: user có được vào phòng phỏng vấn không (đã enroll face? đã pass Entry check? còn lượt thử?) |
| Điểm vào (user thao tác gì) | **2 nguồn gọi khác nhau, CÙNG 1 endpoint** — xem §11 mục cần duyệt trước khi vẽ: **(A)** mỗi lần trình duyệt tải trang `/Interview/Verify?id=...` (server-side, `InterviewController.Verify` GET — đây là lời gọi CHÍNH, quyết định điều hướng) · **(B)** JS gọi lại (client-side, `VerificationController.EntryGate`) sau mỗi lần verify-face thất bại ở #13, để lấy số lượt còn mới nhất mà không reload trang |
| Kết thúc (user thấy gì) | (A) Bị điều hướng thẳng tới Room / Profile-Identity / Dashboard-with-error, HOẶC thấy trang Verify với "Attempts left: N" · (B) JS cập nhật số hiển thị, hoặc điều hướng nếu hết lượt/đã pass |
| Quy mô | 12 participant · 47 dòng trong bảng §4 (38 Nhánh A + 9 Nhánh B, dùng chung phần đuôi service/repo — xem §11 mục 1 về đề xuất rút gọn Nhánh B khi vẽ thật) · 4 alt (A/B/C/D) / 2 opt (F1/F2) |
| Service ngoài quyền sở hữu | không |
| **Gọi BYPASS Gateway?** | không |
| Nhà cung cấp AI | không |
| Ghi DB | không (toàn bộ read-only) |
| Trừ credit | không |
| KI liên quan | không |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| — | — | không có mục nào bị loại trừ |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Nhánh (B) được kích hoạt SAU một lần verify-face thất bại | `#13 VerifyFaceEntryGate` | *(nguồn gọi, không vẽ ngược — chỉ note)* | `entry-verify.js:219` |
| `gate.Allowed == true` → điều hướng thẳng Room | `#4 StartInterviewSession` | `Redirect to Room` | `InterviewController.cs:112-113` |
| `!gate.FaceEnrolled` → điều hướng Profile/Identity | `#1/#2 Enroll Face/Voice Biometric` | `Redirect to Profile/Identity` | `InterviewController.cs:117-119` |

---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 1 | User | actor | — | — |
| 2 | :Verify UI | box | `Views/Interview/Verify.cshtml` + `entry-verify.js` | — |
| 3 | :InterviewController (WebClient) | box | `Controllers/Interview/InterviewController.cs` | — |
| 4 | :VerificationController (WebClient) | box | `Controllers/Interview/VerificationController.cs` | — |
| 5 | :VerificationService (WebClient) | box | `Services/Interview/VerificationService.cs` | — |
| 6 | :API Gateway | box | `interview-route` | có |
| 7 | :SessionVerificationController (InterviewAPI) | box | `Controllers/SessionVerificationController.cs` | — |
| 8 | :BiometricVerificationService (InterviewAPI) | box | `Services/BiometricVerificationService.cs` | — |
| 9 | :SessionRepository (InterviewAPI) | box | `Repositories/SessionRepository.cs` (SUBSET) | — |
| 10 | :ProfileBiometricsRepository (InterviewAPI) | box | `Repositories/ProfileBiometricsRepository.cs` | — |
| 11 | :SessionVerificationEventRepository (InterviewAPI) | box | `Repositories/SessionVerificationEventRepository.cs` | — |
| 12 | SQL Server | cylinder | `DevPrep_Interview` | — |

## 4. BẢNG MESSAGE

### Nhánh A — page load (`InterviewController.Verify`, nguồn CHÍNH)

| # | From | To | call/ret | Label (tiếng Anh) | Frame (guard) | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Verify UI | call | `1. Navigate to /Interview/Verify?id={id}` | — | `Views/Interview/Verify.cshtml` |  | Navigate to `/Interview/Verify?id=...` (redirect từ Start, hoặc F5/back) |
| 2 | :Verify UI | :InterviewController (WebClient) | call | `2. GET Interview/Verify?id=...` | — | `InterviewController.cs:103` |  | GET Interview/Verify?id=... |
| 3 | :InterviewController (WebClient) | :VerificationService (WebClient) | call | `3. GetEntryGateAsync(id)` | — | `InterviewController.cs:105` |  | GetEntryGateAsync(id) |
| 4 | :VerificationService (WebClient) | :API Gateway | call | `4. GET api/interview/sessions/{sessionId}/verify/entry-gate` | — | `VerificationService.cs:89-90` |  | GET api/interview/sessions/{sessionId}/verify/entry-gate |
| 5 | :API Gateway | :SessionVerificationController (InterviewAPI) | call | `5. Forward request to InterviewAPI (no prefix strip)` | — | `ApiGateway/appsettings.json:49-54` |  | Forward (route `interview-route`, no prefix strip) |
| 6 | :SessionVerificationController (InterviewAPI) | :SessionVerificationController (InterviewAPI) | call | `6. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | `SessionVerificationController.cs:136` | self | Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)) |
| 7 | :SessionVerificationController (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `7. GetEntryGateStatusAsync(userId, sessionId)` | — | `SessionVerificationController.cs:137` |  | GetEntryGateStatusAsync(userId, sessionId) |
| 8 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `8. maxRetries = config.GetValue("Biometrics:EntryMaxRetries")` | — | `BiometricVerificationService.cs:194` | self | maxRetries = config.GetValue("Biometrics:EntryMaxRetries", 3) |
| F1.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | Return `EntryGateStatusResponse{FaceEnrolled=true, Allowed=true, AttemptsRemaining=maxRetries, Reason="Biometric verification is disabled."}` | opt F1 `[Biometrics:Enabled == false]` | `BiometricVerificationService.cs:196-203` |  | — |
| 9 | :BiometricVerificationService (InterviewAPI) | :SessionRepository (InterviewAPI) | call | `9. GetByIdAsync(sessionId) — via EnsureSessionOwnedAsync` | `[Biometrics:Enabled == true]` | `BiometricVerificationService.cs:205,635-640` |  | GetByIdAsync(sessionId)` (qua `EnsureSessionOwnedAsync`) |
| 10 | :SessionRepository (InterviewAPI) | SQL Server | call | `10. SELECT InterviewSession by SessionId` | — | `ISessionRepository.cs:8` |  | SELECT InterviewSession by SessionId |
| 11 | SQL Server | :SessionRepository (InterviewAPI) | ret | `11. Return session row / null` | — | — |  | Return session row / null |
| 12 | :SessionRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `12. Return InterviewSession?` | — | — |  | Return `InterviewSession? |
| F2.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `throw KeyNotFoundException("Session not found.")` | opt F2 `[session null OR session.UserId != userId]` | `BiometricVerificationService.cs:638-639` | throw | — |
| F2.2 | :SessionVerificationController (InterviewAPI) | :API Gateway | ret | Return 404 `ApiResponse.Fail(ex.Message)` | opt F2 | `SessionVerificationController.cs:140-142` |  | — |
| F2.3 | :API Gateway | :VerificationService (WebClient) | ret | Return 404 | opt F2 | — |  | — |
| F2.4 | :VerificationService (WebClient) | :InterviewController (WebClient) | ret | Return `(false, null, error)` | opt F2 | `VerificationService.cs:92-95` |  | — |
| F2.5 | :InterviewController (WebClient) | :Verify UI | ret | `TempData["ErrorMessage"]` + redirect `Index` (Dashboard) | opt F2 | `InterviewController.cs:107-109` |  | — |
| F2.6 | :Verify UI | User | ret | Hiện Dashboard với thông báo lỗi | opt F2 | — |  | — |
| 13 | :BiometricVerificationService (InterviewAPI) | :ProfileBiometricsRepository (InterviewAPI) | call | `13. GetAsync(userId, "Face")` | — | `BiometricVerificationService.cs:207` |  | GetAsync(userId, "Face") |
| 14 | :ProfileBiometricsRepository (InterviewAPI) | SQL Server | call | `14. SELECT ProfileBiometric WHERE UserId AND Modality="Face"` | — | `ProfileBiometricsRepository.cs:16-19` |  | SELECT ProfileBiometric WHERE UserId AND Modality="Face" |
| 15 | SQL Server | :ProfileBiometricsRepository (InterviewAPI) | ret | `15. Return row / null` | — | — |  | Return row / null |
| 16 | :ProfileBiometricsRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `16. Return ProfileBiometric?` | — | — |  | Return `ProfileBiometric? |
| A.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | Return `EntryGateStatusResponse{FaceEnrolled=false, Allowed=false, AttemptsRemaining=maxRetries, Reason="Please enroll your face..."}` | alt A `[faceRow is null]` | `BiometricVerificationService.cs:208-217` |  | — |
| 17 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationEventRepository (InterviewAPI) | call | `17. ListBySessionAsync(sessionId, "Entry")` | alt A `[faceRow exists]` | `BiometricVerificationService.cs:219` |  | ListBySessionAsync(sessionId, "Entry") |
| 18 | :SessionVerificationEventRepository (InterviewAPI) | SQL Server | call | `18. SELECT SessionVerificationEvents WHERE Phase="Entry"` | — | `SessionVerificationEventRepository.cs:47-55` |  | SELECT SessionVerificationEvents WHERE SessionId AND Phase="Entry" |
| 19 | SQL Server | :SessionVerificationEventRepository (InterviewAPI) | ret | `19. Return event list` | — | — |  | Return event list |
| 20 | :SessionVerificationEventRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `20. Return List<SessionVerificationEvent>` | — | — |  | Return `List<SessionVerificationEvent> |
| 21 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `21. Compute facePassed / failedAttempts / attemptsRemaining` | — | `BiometricVerificationService.cs:220-223` | self | Tính `facePassed` / `failedAttempts` / `attemptsRemaining |
| B.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | Return `EntryGateStatusResponse{Allowed=true, Reason="Entry verification passed."}` | alt B `[facePassed == true]` | `BiometricVerificationService.cs:225-234` |  | — |
| B.2 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | Return `EntryGateStatusResponse{Allowed=false, Reason="Maximum verification attempts reached..." \| "Face verification required..."}` | alt B `[facePassed == false]` | `BiometricVerificationService.cs:237-246` | |
| 22 | :SessionVerificationController (InterviewAPI) | :API Gateway | ret | `22. Return 200 ApiResponse.Ok(result)` | — | `SessionVerificationController.cs:138` |  | Return 200 `ApiResponse.Ok(result) |
| 23 | :API Gateway | :VerificationService (WebClient) | ret | `23. Return 200` | — | — |  | Return 200 |
| 24 | :VerificationService (WebClient) | :InterviewController (WebClient) | ret | `24. Return (true, gate, null)` | — | `VerificationService.cs:92-97` |  | Return `(true, gate, null) |
| C.1 | :InterviewController (WebClient) | :Verify UI | ret | Redirect `Room` *(boundary #4)* | alt C `[gate.Allowed == true]` | `InterviewController.cs:112-113` | boundary | — |
| C.2 | :InterviewController (WebClient) | :Verify UI | ret | `TempData["InfoMessage"]` + Redirect `Profile/Identity` *(boundary #1/#2)* | alt C `[!gate.Allowed AND !gate.FaceEnrolled]` | `InterviewController.cs:115-120` | boundary | — |
| C.3 | :InterviewController (WebClient) | :Verify UI | ret | `TempData["ErrorMessage"]` + Redirect `Index` (hết lượt) | alt C `[!gate.Allowed AND gate.FaceEnrolled AND AttemptsRemaining <= 0]` | `InterviewController.cs:122-127` |  | — |
| C.4 | :InterviewController (WebClient) | :Verify UI | ret | `View(VerifyViewModel{SessionId, AttemptsRemaining})` | alt C `[còn lượt, render trang]` | `InterviewController.cs:129` |  | — |
| 25 | :Verify UI | User | ret | `25. Show Verify page or redirect` | — | — |  | Hiện kết quả tương ứng nhánh C (điều hướng hoặc trang Verify với "Attempts left: N") |

### Nhánh B — JS re-poll (`VerificationController.EntryGate`, sau 1 lần verify-face thất bại — boundary từ #13)

| # | From | To | call/ret | Label (tiếng Anh) | Frame (guard) | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 26 | :Verify UI | :VerificationController (WebClient) | call | `26. Re-poll entry-gate status [see #13]` | — | `entry-verify.js:219` |  | GET Verification/EntryGate?sessionId=... |
| 27 | :VerificationController (WebClient) | :VerificationService (WebClient) | call | `27. GET Verification/EntryGate?sessionId=...` | — | `VerificationController.cs:81` |  | GetEntryGateAsync(sessionId) |
| 28 | :VerificationService (WebClient) | :API Gateway | call | `28. GetEntryGateAsync(sessionId)` | — | `VerificationService.cs:89-90` |  | GET api/interview/sessions/{sessionId}/verify/entry-gate |
| — | ... | ... | ... | *(đuôi giống hệt message 5→21 của Nhánh A — cùng endpoint, cùng service/repo/DB)* | — | *(xem Nhánh A)* |  | — |
| 29 | :API Gateway | :VerificationService (WebClient) | ret | `29. GET api/interview/sessions/{sessionId}/verify/entry-gate` | — | — |  | Return 200 `EntryGateStatusResponse |
| 30 | :VerificationService (WebClient) | :VerificationController (WebClient) | ret | `30. Return 200 EntryGateStatusResponse` | — | `VerificationService.cs:92-97` |  | Return `(true, gate, null) |
| 31 | :VerificationController (WebClient) | :Verify UI | ret | `31. Return (true, gate, null)` | — | `VerificationController.cs:87` |  | Return 200 JSON `{allowed, attemptsRemaining, ...} |
| D.1 | :Verify UI | User | ret | `[gate.allowed]` → `completeSuccessfulVerification()` *(boundary #11, chuyển Integrity Policy)* | alt D `[gate.allowed == true]` | `entry-verify.js:221-224` | boundary | — |
| D.2 | :Verify UI | User | ret | `[attemptsRemaining <= 0]` → hiện "Maximum attempts reached" 1.6s → redirect Dashboard | alt D `[gate.allowed == false AND attemptsRemaining <= 0]` | `entry-verify.js:225-231` |  | — |
| D.3 | :Verify UI | User | ret | `[else]` → cập nhật "Attempts left: N" + hiện lỗi lần verify trước + nút "Try again" | alt D `[còn lượt]` | `entry-verify.js:220,235-240` |  | — |

**Cờ:** `[best-effort]` · `self` · `throw` · `boundary` (theo §2)

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong |
|---|---|---|---|---|
| F1 | opt | `[Biometrics:Enabled == false]` | 8 → F1.1 | — |
| F2 | opt | `[session is null OR session.UserId != userId]` | 9 → F2.6 | — |
| A | alt | `[faceRow is null]` / `[faceRow exists]` | A.1 / 17 | — |
| B | alt | `[facePassed == true]` / `[facePassed == false]` | B.1 / B.2 | — |
| C | alt | `[Allowed]` / `[!FaceEnrolled]` / `[hết lượt]` / `[còn lượt]` | C.1 / C.2 / C.3 / C.4 | — |
| D | alt | `[allowed]` / `[hết lượt]` / `[còn lượt]` (Nhánh B, JS) | D.1 / D.2 / D.3 | — |

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | 2, 26 | This endpoint is invoked from TWO different WebClient actions for two different purposes: `InterviewController.Verify` decides page routing on every load; `VerificationController.EntryGate` is a lightweight JSON re-poll used only after a failed face-verify attempt (see #13). Both call the exact same InterviewAPI endpoint and business logic. |
| N2 | comment tại `SessionVerificationController.cs:129-130` | The controller's own comment ("not called from any Room UI yet") is stale/misleading — it means "not called from the Room page", not "never called": `entry-verify.js:219` (Verify page, before Room) calls it routinely. |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | File:Line | Liệt kê | Nguồn |
|---|---|---|---|---|---|
| 1 | InterviewController (WebClient) | class | `Controllers/Interview/InterviewController.cs:17` | SUBSET | local |
| 2 | VerificationController (WebClient) | class | `Controllers/Interview/VerificationController.cs:9` | SUBSET | local |
| 3 | IVerificationService / VerificationService (WebClient) | interface/class | `Services/Interview/VerificationService.cs` | FULL | local *(đã dán ở #12)* |
| 4 | EntryGateStatusResponse | DTO | `DTOs/Biometrics/EntryGateStatusResponse.cs:7-14` | FULL | local |
| 5 | ApiResponse\<T\> (InterviewAPI) | class | `InterviewAPI/DTOs/ApiResponse.cs` | FULL | **SHARED** |
| 6 | SessionVerificationController (InterviewAPI) | class | `Controllers/SessionVerificationController.cs:15` | SUBSET | local |
| 7 | IBiometricVerificationService / BiometricVerificationService | interface/class | `Services/BiometricVerificationService.cs` | SUBSET | local |
| 8 | ISessionRepository / SessionRepository | interface/class | `Repositories/SessionRepository.cs` | SUBSET | local |
| 9 | InterviewSession | entity | `Models/InterviewSession.cs` | FULL | **SHARED** *(đã dán ở #12)* |
| 10 | IProfileBiometricsRepository / ProfileBiometricsRepository / ProfileBiometric | interface/class/entity | `Repositories/ProfileBiometricsRepository.cs` | FULL | local *(đã dán ở #12)* |
| 11 | ISessionVerificationEventRepository / SessionVerificationEventRepository | interface/class | `Repositories/SessionVerificationEventRepository.cs` | FULL | local *(đã dán ở #13)* |
| 12 | SessionVerificationEvent | entity | `Models/SessionVerificationEvent.cs` | FULL | **SHARED** *(đã dán ở #13)* |
| 13 | VerifyViewModel | ViewModel | `Models/Interview/VerificationModels.cs:62-66` | FULL | local |
| 14 | InterviewDbContext / ControllerBase / DbContext / Controller | DbContext/framework | — | FULL | local/**SHARED** *(đã dán ở #12)* |

## 8. CLASS — chi tiết

### `InterviewController` (`WebClient`)
- File: `Controllers/Interview/InterviewController.cs:17` — Liệt kê: SUBSET
- Field: `- interviewService: IInterviewService`
- Field: `- biometricsService: IBiometricsService`
- Field: `- verificationService: IVerificationService`
- Field: `- feedbackService: IFeedbackService`
- Field: `- paymentService: IPaymentService`
- Method: `+ Verify(id: Guid, ct: CancellationToken): Task<IActionResult>`
- Method: `+ VerifyFailed(): IActionResult`
- *(SUBSET)* Đã bỏ toàn bộ action khác (`Index`, `Start`, `Room`, `Message`, `Hint`, `Pause`, ...) — thuộc #1-#11/#17/#18, không liên quan #14.

### `VerificationController` (`WebClient`)
- File: `Controllers/Interview/VerificationController.cs:9` — Liệt kê: SUBSET
- Field: `- verificationService: IVerificationService`
- Method: `+ EntryGate(sessionId: Guid, ct: CancellationToken): Task<IActionResult>`
- *(SUBSET)* Đã bỏ `Challenge`, `VerifyFace`, `VerifyInSession`, `VerifyVoice` — thuộc #12/#13/#15/#16.

### `EntryGateStatusResponse` (`InterviewAPI`)
- File: `DTOs/Biometrics/EntryGateStatusResponse.cs:7-14` — Liệt kê: FULL
- Field: `+ FaceEnrolled: bool`
- Field: `+ Allowed: bool`
- Field: `+ AttemptsUsed: int`
- Field: `+ AttemptsRemaining: int`
- Field: `+ Reason: string`

### `SessionVerificationController` (`InterviewAPI`)
- File: `Controllers/SessionVerificationController.cs:15` — Liệt kê: SUBSET
- Field: `- _service: IBiometricVerificationService`
- Field: `- _sessionService: ISessionService`
- Method: `+ GetEntryGateStatus(sessionId: Guid, ct: CancellationToken): Task<IActionResult>`
- *(SUBSET)* Đã bỏ các action khác — thuộc #12/#13/#15/#16.

### `IBiometricVerificationService` / `BiometricVerificationService` (`InterviewAPI`)
- File: `Services/BiometricVerificationService.cs:12` — Liệt kê: SUBSET
- Field: `- _biometricsRepository: IProfileBiometricsRepository`
- Field: `- _sessionRepository: ISessionRepository`
- Field: `- _eventRepository: ISessionVerificationEventRepository`
- Field: `- _configuration: IConfiguration`
- Method: `+ GetEntryGateStatusAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task<EntryGateStatusResponse>`
- Method: `- EnsureSessionOwnedAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task`
- *(SUBSET)* Đã bỏ các method khác — thuộc #12/#13/#15/#16.

### `ISessionRepository` / `SessionRepository` (`InterviewAPI`)
- File: `Repositories/SessionRepository.cs` — Liệt kê: SUBSET
- Method: `+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>`

### `VerifyViewModel` (`WebClient`)
- File: `Models/Interview/VerificationModels.cs:62-66` — Liệt kê: FULL
- Field: `+ SessionId: Guid`
- Field: `+ AttemptsRemaining: int`

**`InterviewSession`, `ProfileBiometric`+repo, `SessionVerificationEvent`+repo, `ApiResponse<T>`, `InterviewDbContext`, framework boxes — đã dán đủ ở `GetVerificationChallenge.md`/`VerifyFaceEntryGate.md` §8** — chỉ ghi tên.

## 9. QUAN HỆ CLASS

| Loại | From | To | Label |
|---|---|---|---|
| extends | InterviewController (WebClient) | Controller | — |
| extends | VerificationController (WebClient) | Controller | — |
| use | InterviewController (WebClient) | IVerificationService | Use |
| use | VerificationController (WebClient) | IVerificationService | Use |
| use | InterviewController (WebClient) | VerifyViewModel | Use |
| extends | SessionVerificationController (InterviewAPI) | ControllerBase | — |
| use | SessionVerificationController (InterviewAPI) | IBiometricVerificationService | Use |
| implements | BiometricVerificationService (InterviewAPI) | IBiometricVerificationService | — |
| use | BiometricVerificationService (InterviewAPI) | ISessionRepository | Use |
| use | BiometricVerificationService (InterviewAPI) | IProfileBiometricsRepository | Use |
| use | BiometricVerificationService (InterviewAPI) | ISessionVerificationEventRepository | Use |
| use | BiometricVerificationService (InterviewAPI) | EntryGateStatusResponse | Use |

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| — | `SessionVerificationController.cs:129-130` | Comment lệch: nói endpoint "chưa được gọi từ Room UI nào" — dễ hiểu nhầm thành "chưa từng gọi", thực tế được gọi thường xuyên từ trang Verify (2 nguồn, xem §0) | comment/tài liệu lệch code | Vẽ hành vi THẬT (endpoint được gọi 2 nơi), note N2 giải thích comment lệch | Không cần KI riêng — mức độ quá nhỏ (chỉ gây hiểu nhầm khi đọc code, không ảnh hưởng hành vi), đã ghi đủ ở note N2 trong file này |

## 11. QUYẾT ĐỊNH CẦN DUYENTQ DUYỆT

| # | Vấn đề | Phương án | Đề xuất |
|---|---|---|---|
| 1 | #14 có 2 nguồn gọi khác nhau (page-load server-side vs JS re-poll client-side) dẫn tới CÙNG 1 chuỗi service/repo/DB — file này vẽ CẢ HAI thành "Nhánh A" và "Nhánh B" trong cùng 1 bảng message, đánh số liên tục | (a) Giữ cả 2 nhánh trong 1 file (đã làm) · (b) Chỉ vẽ nhánh A (chính), nhánh B chỉ note tham chiếu ngắn (giống cách #13 tham chiếu NGƯỢC vào đây) | Cần duyệt: nếu muốn diagram gọn hơn, có thể bỏ Nhánh B khỏi sequence (giữ lại trong bảng message dạng rút gọn) vì #13 đã boundary-reference vào đây rồi — tránh vẽ 2 lần cùng 1 lời gọi ở 2 file khác nhau. Đề xuất: **rút gọn Nhánh B xuống còn 1 boundary note** khi vẽ thật, giữ đầy đủ trong FLOW.md này để không mất thông tin. |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `32.1`)** This endpoint is invoked from TWO different WebClient actions: InterviewController.Verify decides page routing on every page load (Nhanh A); VerificationController.EntryGate is a lightweight JSON re-poll used only after a failed face-verify attempt (see #13, Nhanh B). Both call the exact same InterviewAPI endpoint and business logic - Nhanh B is drawn abbreviated (messages 26-32) since its InterviewAPI-side logic is identical to messages 5-24 of Nhanh A. *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| Bảng §4 Nhánh A chỉ ghi 1 dòng (`F1.1`) cho nhánh `[Biometrics:Enabled == false]`, nhưng để đóng đúng ngăn xếp call (6 lời gọi đang mở tính tới điểm đó: User→UI→InterviewController→VerificationService→Gateway→SessionVerificationController→BiometricVerificationService) cần đủ 6 message return, không phải 1 | Luồng 1 chỉ ghi return trực tiếp từ `BiometricVerificationService`, không lần tiếp chuỗi unwind lên tới Actor — cùng loại thiếu sót đã gặp ở `GetVerificationChallenge.md` §12 (nhánh disabled) | Đã tự bù bằng cách copy đúng cấu trúc unwind 6 tầng đã có sẵn ở F2 của CHÍNH file này (12.1→12.6), đổi nội dung: vì `Allowed=true` nên 2 message cuối là "Redirect to Room" thay vì lỗi — khớp đúng nhánh `[gate.Allowed==true]` của alt C | luồng 2 |
| Message 26 (đầu Nhánh B) trong bảng §4 không có cột "Cờ" đánh dấu `self`, nhưng về ý nghĩa nghiệp vụ đây là hành động JS tự động (không phải user bấm) — khi dựng YAML, nếu để `self: true` thì 3 nhánh trả lời cuối (D.1/D.2/D.3, đều là `ret` từ UI về User) sẽ không có `call` nào để khớp (self-message không mở ngăn xếp) → validator báo "return không khớp call nào" | Đây là hệ quả kỹ thuật của cách sinh sequence (mọi `ret` cần đúng 1 `call` đối xứng còn mở), không phải thiếu dữ kiện từ code | Đã đổi message 26 thành `call` thật từ `User`, ghi rõ trong nhãn là "(indirect - automatic JS...)" để không hiểu nhầm thành user bấm — giữ đúng tinh thần "actor luôn là điểm mở đầu ngăn xếp" đã áp dụng cho các file có trigger gián tiếp khác (#15, #17) | luồng 2 |
