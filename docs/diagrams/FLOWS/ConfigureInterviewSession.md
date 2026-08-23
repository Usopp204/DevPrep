# FLOW — `ConfigureInterviewSession` · BL#31, 32, 33, 36 · UC-`chưa có`

> Đặt tại `docs/diagrams/FLOWS/ConfigureInterviewSession.md`
> Trạng thái: `DRAFT` — Khảo sát: lô E, 2026-08-03

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Cấu hình một phiên phỏng vấn mới trên Dashboard: nạp danh mục Role/Technology từ ai-service (BL#36), rồi khi bấm **Start** chạy 3 lệnh PUT tuần tự tạo/cập nhật `InterviewSessions` (duration → level → interaction mode) |
| Điểm vào (user thao tác gì) | Mở Dashboard (`/Interview`) — catalog tự nạp; chọn Role / Level / Duration / JD / Focus Areas rồi bấm **Start Interview** |
| Kết thúc (user thấy gì) | Chuyển sang màn hình xác minh khuôn mặt của phiên mới (`#13 VerifyFaceEntryGate`). Đã có phiên đang chạy → chuyển thẳng vào Room phiên đó. Lỗi → quay lại Dashboard kèm banner lỗi, form giữ nguyên dữ liệu đã nhập |
| Quy mô | `12` participant · `118` message · `30` class · `0` alt / `9` opt · trang Sequence 2450×4787 · trang Class 5690×1210 |
| Đối chiếu ước lượng RECON | RECON đoán `34` · thật `118` · hệ số `3.5×` |
| Service ngoài quyền sở hữu | ApiGateway (định tuyến) |
| **Gọi BYPASS Gateway?** | không — cả 4 request (catalog + 3 config) đều qua ApiGateway :7000 |
| Nhà cung cấp AI | **không gọi Gemini.** `/catalog` chỉ đọc danh mục ChromaDB nội bộ (`ai-service/app/api/catalog.py:20-33`) |
| Ghi DB | `InterviewSessions` — INSERT 1 hàng ở lệnh **duration** đầu tiên (`Status="Configured"`), rồi UPDATE `Level`/`UpdatedAt` và `InteractionMode`/`UpdatedAt` ở 2 lệnh sau |
| Trừ credit | không (trừ credit xảy ra ở `EnterInterviewRoom`, lúc lấy câu hỏi đầu tiên) |
| KI liên quan | **KI-13 (mới)** — lỗi 400 của `/Interview/Catalog` không kích hoạt nhánh `.catch`, user thấy thông điệp sai "No technologies found for this role." |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| `POST api/interview/sessions/init` (lệnh thứ 4 trong cùng `StartInterviewAsync`) | [InterviewService.cs:183-188](../../../source/WebClient/Services/Interview/InterviewService.cs#L183-L188) | Thuộc `#4 StartInterviewSession` (lập kế hoạch câu hỏi + tạo room). Vẽ thu gọn đúng 1 cặp call+ret (message 65-66) — xem §2. |
| `InterviewController.Index` / `BuildDashboardAsync` (recent sessions, credit balance, active-session banner) | [InterviewController.cs:27-51](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L27-L51) | Nội dung trang Dashboard, thuộc `#4`/`#8`. Ở đây chỉ vẽ **fetch catalog** chạy sau khi trang đã render. |
| `AutoResumeIfPauseExpiredAsync` + `FinalizeIfTimeExpiredAsync` bên trong `GetActiveSessionAsync` | [SessionService.cs:992-994](../../../source/InterviewAPI/Services/SessionService.cs#L992-L994) | Self-heal thuộc `#8 PauseResumeSession` / `#9 EndInterviewSession`. Ghi 1 câu trong nhãn message 26, không mở rộng. |
| Toàn bộ chuỗi `biometricsService.GetStatusAsync` bên trong WebClient | [BiometricsService.cs:25-37](../../../source/WebClient/Services/Profile/BiometricsService.cs#L25-L37) | Đã vẽ ĐẦY ĐỦ ở `#1 EnrollFaceBiometric` pha 0. Ở đây thu về 1 cặp call+ret (message 19-20) — xem §2. |
| Nhánh factory mặc định của `ConfigLevelAsync` / `ConfigInteractionAsync` (tạo phiên mới với `DurationMinutes=30` / `Level="Junior"` hard-code) | [SessionService.cs:369-381, 403-415](../../../source/InterviewAPI/Services/SessionService.cs#L369-L415) | **Không tới được từ UI**: `StartInterviewAsync` luôn gọi duration TRƯỚC và luôn truyền `SessionId` cho 2 lệnh sau (`InterviewService.cs:171, 178`). Chỉ chạm được nếu gọi thẳng API. Xem §10. |
| `POPULAR` filter / panel "All technologies" / ô tìm kiếm | [dashboard.js:10-12, 48-92](../../../source/WebClient/wwwroot/js/pages/dashboard.js#L48-L92) | Chi tiết dựng UI chip, gộp 1 câu vào nhãn message 15. |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Cổng sinh trắc trước khi tạo phiên | `#1 EnrollFaceBiometric` (pha 0 GET status) | message 19 `GetStatusAsync(ct) - biometric entry gate` ↔ message 20 | InterviewController.cs:61-67 |
| `POST .../init` sau 3 lệnh config | `#4 StartInterviewSession` | message 65 `POST api/interview/sessions/init { SessionId, Position, Jd, FocusAreas }` ↔ message 66 | InterviewService.cs:183-188 |
| Chuyển sang màn hình Verify của phiên mới | `#13 VerifyFaceEntryGate` | message 69 (nhãn ghi rõ) | InterviewController.cs:99 |
| Chuyển vào Room khi đã có phiên đang chạy | `EnterInterviewRoom` | message D.6 (nhãn ghi rõ) | InterviewController.cs:80-81 |

---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 0 | User | actor | — | — |
| 1 | :Interview Dashboard UI | box | `Views/Interview/Index.cshtml` + `wwwroot/js/pages/dashboard.js` | — |
| 2 | :InterviewController (WebClient) | box | `WebClient/Controllers/Interview/InterviewController.cs:54-100, 141-150` | — |
| 3 | :BiometricsService (WebClient) | box | `WebClient/Services/Profile/BiometricsService.cs` | — |
| 4 | :InterviewService (WebClient) | box | `WebClient/Services/Interview/InterviewService.cs:112-194` | — |
| 5 | :API Gateway | box | `ApiGateway` (YARP :7000) | — |
| 6 | :InterviewSessionController (InterviewAPI) | box | `InterviewAPI/Controllers/InterviewSessionController.cs:32-44, 386-449` | có |
| 7 | :SessionService (InterviewAPI) | box | `InterviewAPI/Services/SessionService.cs:330-430, 1602-1627` | có |
| 8 | :SessionRepository (InterviewAPI) | box | `InterviewAPI/Repositories/SessionRepository.cs:21-53` | có |
| 9 | :AiServiceClient (InterviewAPI) | box | `InterviewAPI/Clients/AiServiceClient.cs:389-403` | có |
| 10 | :ai-service /catalog | box | `ai-service/app/api/catalog.py:20-33` | có |
| 11 | SQL Server | cylinder | `DevPrep_Interview` | — |

> `:BiometricsService (WebClient)` được đặt ngay sau `:InterviewController (WebClient)` (nhóm theo
> tầng WebClient) thay vì theo đúng thứ tự gọi lần đầu — xem §11 quyết định 1.

## 4. BẢNG MESSAGE

### Pha 0 — nạp catalog (BL#36), chạy khi trang Dashboard vừa render

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Interview Dashboard UI | call | `1. Open the interview dashboard` | — | Index.cshtml:43-48, dashboard.js:2-5 |  | 1. Open the interview dashboard - the page renders first, then dashboard.js fetches the catalog asynchronously |
| 2 | :Interview Dashboard UI | :InterviewController (WebClient) | call | `2. GET /Interview/Catalog` | — | dashboard.js:94, Index.cshtml:48 |  | 2. GET /Interview/Catalog (fetch, independent of the form submit) |
| 3 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `3. GetCatalogAsync(ct)` | — | InterviewController.cs:143 |  | — |
| 4 | :InterviewService (WebClient) | :API Gateway | call | `4. GET api/interview/catalog` | — | InterviewService.cs:115-116 |  | — |
| 5 | :API Gateway | :InterviewSessionController | call | `5. Forward (route interview-route, no prefix strip)` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 6 | :InterviewSessionController | :AiServiceClient | call | `6. GetCatalogAsync(ct)` | — | InterviewSessionController.cs:37 |  | — |
| 7 | :AiServiceClient | :ai-service | call | `7. GET /catalog (header X-Internal-Api-Key)` | — | AiServiceClient.cs:389-393 |  | — |
| 8 | :ai-service | (self) | call | `8. chroma_store.catalog()` | — | catalog.py:20-33 | self | 8. chroma_store.catalog(): roles = indexed positions minus the cross-cutting tracks; technologies[role] = that role's own categories + the Database / Language tracks |
| A.1 | :ai-service | :AiServiceClient | ret | `A.1. Non-2xx / no response` | A | AiServiceClient.cs:392-401 |  | — |
| A.2 | :AiServiceClient | :InterviewSessionController | ret | `A.2. throw (logged: Call to ai-service GET /catalog failed)` | A | AiServiceClient.cs:398-402 | throw | — |
| A.3 | :InterviewSessionController | :API Gateway | ret | `A.3. Return 400 ApiResponse.Fail(ex.Message)` | A | InterviewSessionController.cs:40-43 |  | — |
| A.4 | :API Gateway | :InterviewService (WebClient) | ret | `A.4. Return 400` | A | ApiGateway/appsettings.json:49-54 |  | — |
| A.5 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `A.5. Return (false, null, error)` | A | InterviewService.cs:117, 636-639 |  | — |
| A.6 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `A.6. Return 400 { error: "Could not load the catalog." }` | A | InterviewController.cs:144-147 |  | — |
| A.7 | :Interview Dashboard UI | (self) | call | `A.7. response.json() parses the 400 body; techByRole = {}` | A | dashboard.js:94-108 | self | A.7. response.json() still parses the 400 body, so the .catch fallback never runs: techByRole = {}, catalog.roles is not an array -> the hard-coded Backend / Frontend options stay (KI-13) |
| A.8 | :Interview Dashboard UI | User | ret | `A.8. Show "No technologies found for this role."` | A | dashboard.js:54-57, 109-113 |  | A.8. "No technologies found for this role." - the misleading message, instead of the intended "Could not load technologies. You can still start; the whole role will be covered." hint |
| 9 | :ai-service | :AiServiceClient | ret | `9. 200 { roles: [...], technologies: { role: [...] } }` | — | catalog.py:26-33 |  | — |
| 10 | :AiServiceClient | :InterviewSessionController | ret | `10. Return AiCatalogResponse{Roles, Technologies}` | — | AiServiceClient.cs:394-396 |  | — |
| 11 | :InterviewSessionController | :API Gateway | ret | `11. Return 200 ApiResponse.Ok(catalog)` | — | InterviewSessionController.cs:38 |  | — |
| 12 | :API Gateway | :InterviewService (WebClient) | ret | `12. Return 200` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 13 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `13. Return (true, CatalogResponse, null)` | — | InterviewService.cs:117 |  | — |
| 14 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `14. Return 200 JSON (catalog)` | — | InterviewController.cs:149 |  | 14. Return 200 JSON (the catalog, unwrapped from the ApiResponse envelope) |
| 15 | :Interview Dashboard UI | (self) | call | `15. Replace Job Role options; renderFocus()` | — | dashboard.js:96-108, 48-92 | self | 15. Replace the Job Role options with roles[]; renderFocus(): up to 12 popular chips for the selected role plus an "All technologies (n)" panel with a search box |
| 16 | :Interview Dashboard UI | User | ret | `16. Show Job Role list and Focus Area chips` | — | dashboard.js:48-92 |  | 16. Dashboard ready: Job Role list and Focus Area chips populated from the indexed dataset |

### Pha 1 — bấm Start: cổng sinh trắc + 3 lệnh config

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 17 | User | :Interview Dashboard UI | call | `17. Choose Job Role / Level / Duration / JD / Focus Areas` | — | Index.cshtml:43-48 |  | 17. Choose Job Role / Level / Duration / JD / Focus Areas, then click Start Interview |
| 18 | :Interview Dashboard UI | :InterviewController (WebClient) | call | `18. POST /Interview/Start (form + antiforgery token)` | — | Index.cshtml:43-49, InterviewController.cs:54-56 |  | — |
| 19 | :InterviewController (WebClient) | :BiometricsService (WebClient) | call | `19. GetStatusAsync(ct) [see #1]` | — | InterviewController.cs:61 | boundary | 19. GetStatusAsync(ct) - biometric entry gate (full chain drawn in #1 EnrollFaceBiometric phase 0) |
| 20 | :BiometricsService (WebClient) | :InterviewController (WebClient) | ret | `20. Return (Success, BiometricStatusModel, Error)` | — | BiometricsService.cs:30-36 | boundary | 20. Return (Success, BiometricStatusModel{FaceEnrolled, VoiceEnrolled}, Error) |
| B.1 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `B.1. 302 RedirectToAction("Identity", "Profile")` | B | InterviewController.cs:62-67 |  | B.1. TempData["InfoMessage"] = "Ban can xac thuc khuon mat (enroll) truoc khi bat dau phong van."; 302 RedirectToAction("Identity", "Profile") |
| B.2 | :Interview Dashboard UI | User | ret | `B.2. Profile > Identity page [see #1]` | B | InterviewController.cs:66 | boundary | B.2. Profile > Identity verification page (#1 EnrollFaceBiometric) |
| 21 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `21. StartInterviewAsync(...)` | — | InterviewController.cs:69-71 |  | 21. StartInterviewAsync(durationMinutes, level, position, jd, focusAreas, ct) |
| 22 | :InterviewService (WebClient) | :API Gateway | call | `22. PUT api/interview/sessions/config/duration` | — | InterviewService.cs:124-133 |  | 22. PUT api/interview/sessions/config/duration { DurationMinutes } - no SessionId; bespoke call because only this one can return the raw 409 body |
| 23 | :API Gateway | :InterviewSessionController | call | `23. Forward (route interview-route, no prefix strip)` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 24 | :InterviewSessionController | (self) | call | `24. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | InterviewSessionController.cs:391 | self | — |
| 25 | :InterviewSessionController | :SessionService | call | `25. ConfigDurationAsync(userId, req, ct)` | — | InterviewSessionController.cs:392 |  | — |
| C.1 | :SessionService | :InterviewSessionController | ret | `C.1. throw InvalidOperationException` | C | SessionService.cs:332-333 | throw | C.1. throw InvalidOperationException("Invalid duration. Must be 15, 30, 45, or 60.") |
| C.2 | :InterviewSessionController | :API Gateway | ret | `C.2. Return 400 ApiResponse.Fail(ex.Message)` | C | InterviewSessionController.cs:401-404 |  | — |
| C.3 | :API Gateway | :InterviewService (WebClient) | ret | `C.3. Return 400` | C | ApiGateway/appsettings.json:49-54 |  | — |
| C.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `C.4. Return StartInterviewResult.Failure(message)` | C | InterviewService.cs:135-152 |  | — |
| C.5 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `C.5. Return View Index` | C | InterviewController.cs:84-93 |  | C.5. Return View Index (dashboard rebuilt, ErrorMessage set, the form keeps every value the user typed) |
| C.6 | :Interview Dashboard UI | User | ret | `C.6. Dashboard with the error banner` | C | Index.cshtml:38-41 |  | — |
| 26 | :SessionService | :SessionRepository | call | `26. ResolveSessionAsync(null) -> GetActiveSessionAsync(...)` | — | SessionService.cs:335, 1605-1612, 982-994 |  | 26. ResolveSessionAsync(sessionId = null) -> GetActiveSessionAsync(userId, ct); the service-level call first lazily auto-resumes an expired pause and finalizes an expired session (#8 / #9) |
| 27 | :SessionRepository | SQL Server | call | `27. SELECT InterviewSession WHERE Status IN ("InProgress",...)` | — | SessionRepository.cs:28-33 |  | 27. SELECT newest InterviewSession WHERE UserId AND Status IN ("InProgress","Paused") AND !IsDeleted |
| 28 | SQL Server | :SessionRepository | ret | `28. Return row / null` | — | SessionRepository.cs:28-33 |  | — |
| 29 | :SessionRepository | :SessionService | ret | `29. Return InterviewSession?` | — | SessionRepository.cs:28-33 |  | — |
| D.1 | :SessionService | :InterviewSessionController | ret | `D.1. throw SessionGuardException(409, "active_session_exists")` | D | SessionService.cs:1613-1616 | throw | D.1. throw SessionGuardException(409, "active_session_exists", "You already have an interview in progress.", activeSessionId) |
| D.2 | :InterviewSessionController | :API Gateway | ret | `D.2. Return 409 {error, activeSessionId}` | D | InterviewSessionController.cs:395-400 |  | D.2. Return 409 { error: "active_session_exists", message, activeSessionId } - raw body, NOT the ApiResponse envelope |
| D.3 | :API Gateway | :InterviewService (WebClient) | ret | `D.3. Return 409` | D | ApiGateway/appsettings.json:49-54 |  | — |
| D.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `D.4. Return StartInterviewResult.ActiveSessionConflict(...)` | D | InterviewService.cs:137-151 |  | D.4. Return StartInterviewResult.ActiveSessionConflict(activeSessionId) |
| D.5 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `D.5. 302 RedirectToAction(Room, { id = activeSessionId })` | D | InterviewController.cs:73-82 |  | D.5. TempData["RoomInfoMessage"] = "Resuming your session in progress."; 302 RedirectToAction(Room, { id = activeSessionId }) |
| D.6 | :Interview Dashboard UI | User | ret | `D.6. Interview Room of the session in progress [see #4]` | D | InterviewController.cs:81 | boundary | D.6. The interview Room of the session already in progress (EnterInterviewRoom) - this is a redirect, not an error |
| 30 | :SessionService | :SessionRepository | call | `30. CreateAsync(InterviewSession{Status="Configured"})` | — | SessionService.cs:336-347, 1618 |  | 30. CreateAsync(InterviewSession{SessionId=Guid.NewGuid(), UserId, Status="Configured", Level="Junior", InteractionMode="Text", QuestionType="Mixed", DurationMinutes=req.DurationMinutes, CreatedAt, UpdatedAt}) |
| 31 | :SessionRepository | SQL Server | call | `31. INSERT InterviewSession (SaveChangesAsync)` | — | SessionRepository.cs:48-53 |  | — |
| 32 | SQL Server | :SessionRepository | ret | `32. Return rows affected` | — | SessionRepository.cs:51 |  | — |
| 33 | :SessionRepository | :SessionService | ret | `33. Return InterviewSession` | — | SessionRepository.cs:52 |  | — |
| 34 | :SessionService | :InterviewSessionController | ret | `34. Return ConfigResponse{UpdatedField="DurationMinutes"}` | — | SessionService.cs:356-361 |  | 34. Return ConfigResponse{SessionId, UpdatedField="DurationMinutes", UpdatedValue} |
| 35 | :InterviewSessionController | :API Gateway | ret | `35. Return 200 ApiResponse.Ok(result)` | — | InterviewSessionController.cs:393 |  | — |
| 36 | :API Gateway | :InterviewService (WebClient) | ret | `36. Return 200` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 37 | :InterviewService (WebClient) | :API Gateway | call | `37. PUT api/interview/sessions/config/level` | — | InterviewService.cs:167-172 |  | 37. PUT api/interview/sessions/config/level { SessionId = the id just created, Level } |
| 38 | :API Gateway | :InterviewSessionController | call | `38. Forward (route interview-route, no prefix strip)` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 39 | :InterviewSessionController | :SessionService | call | `39. ConfigLevelAsync(userId from the JWT claim, req, ct)` | — | InterviewSessionController.cs:413-414 |  | — |
| E1.1 | :SessionService | :InterviewSessionController | ret | `E1.1. throw InvalidOperationException("Invalid level.")` | E1 | SessionService.cs:366-367 | throw | — |
| E1.2 | :InterviewSessionController | :API Gateway | ret | `E1.2. Return 400 ApiResponse.Fail(ex.Message)` | E1 | InterviewSessionController.cs:423-426 |  | — |
| E1.3 | :API Gateway | :InterviewService (WebClient) | ret | `E1.3. Return 400` | E1 | ApiGateway/appsettings.json:49-54 |  | — |
| E1.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `E1.4. Return StartInterviewResult.Failure(error)` | E1 | InterviewService.cs:173-174 |  | — |
| E1.5 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `E1.5. Return View Index (dashboard + ErrorMessage)` | E1 | InterviewController.cs:84-93 |  | — |
| E1.6 | :Interview Dashboard UI | User | ret | `E1.6. Dashboard with the error banner` | E1 | Index.cshtml:38-41 |  | E1.6. Dashboard with the error banner - the session row created above stays behind as an orphan draft |
| 40 | :SessionService | :SessionRepository | call | `40. ResolveSessionAsync(sessionId) -> GetByIdAsync(...)` | — | SessionService.cs:369, 1621 |  | 40. ResolveSessionAsync(sessionId) -> GetByIdAsync(sessionId, ct) |
| 41 | :SessionRepository | SQL Server | call | `41. SELECT InterviewSession WHERE SessionId AND !IsDeleted` | — | SessionRepository.cs:21-24 |  | — |
| 42 | SQL Server | :SessionRepository | ret | `42. Return row / null` | — | SessionRepository.cs:21-24 |  | — |
| 43 | :SessionRepository | :SessionService | ret | `43. Return InterviewSession?` | — | SessionRepository.cs:21-24 |  | — |
| E2.1 | :SessionService | :InterviewSessionController | ret | `E2.1. throw InvalidOperationException` | E2 | SessionService.cs:1622-1625 | throw | E2.1. throw InvalidOperationException("Session not found.") / ("Session already started.") |
| E2.2 | :InterviewSessionController | :API Gateway | ret | `E2.2. Return 400 ApiResponse.Fail(ex.Message)` | E2 | InterviewSessionController.cs:423-426 |  | — |
| E2.3 | :API Gateway | :InterviewService (WebClient) | ret | `E2.3. Return 400` | E2 | ApiGateway/appsettings.json:49-54 |  | — |
| E2.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `E2.4. Return StartInterviewResult.Failure(error)` | E2 | InterviewService.cs:173-174 |  | — |
| E2.5 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `E2.5. Return View Index (dashboard + ErrorMessage)` | E2 | InterviewController.cs:84-93 |  | — |
| E2.6 | :Interview Dashboard UI | User | ret | `E2.6. Dashboard with the error banner` | E2 | Index.cshtml:38-41 |  | — |
| 44 | :SessionService | :SessionRepository | call | `44. SaveChangesAsync(ct)` | — | SessionService.cs:383-388 |  | 44. SaveChangesAsync(ct) after setting session.Level = req.Level and UpdatedAt = now |
| 45 | :SessionRepository | SQL Server | call | `45. UPDATE InterviewSession SET Level, UpdatedAt` | — | SessionRepository.cs:348 |  | — |
| 46 | SQL Server | :SessionRepository | ret | `46. Return rows affected` | — | SessionRepository.cs:348 |  | — |
| 47 | :SessionRepository | :SessionService | ret | `47. Return` | — | SessionRepository.cs:348 |  | — |
| 48 | :SessionService | :InterviewSessionController | ret | `48. Return ConfigResponse{UpdatedField="Level"}` | — | SessionService.cs:390-395 |  | 48. Return ConfigResponse{SessionId, UpdatedField="Level", UpdatedValue} |
| 49 | :InterviewSessionController | :API Gateway | ret | `49. Return 200 ApiResponse.Ok(result)` | — | InterviewSessionController.cs:415 |  | — |
| 50 | :API Gateway | :InterviewService (WebClient) | ret | `50. Return 200` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 51 | :InterviewService (WebClient) | :API Gateway | call | `51. PUT api/interview/sessions/config/interaction` | — | InterviewService.cs:176-179, 92 |  | 51. PUT api/interview/sessions/config/interaction { SessionId, Mode = "Text" (the WebClient constant, the form has no voice/text switch) } |
| 52 | :API Gateway | :InterviewSessionController | call | `52. Forward (route interview-route, no prefix strip)` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 53 | :InterviewSessionController | :SessionService | call | `53. ConfigInteractionAsync(userId from the JWT claim, req, ct)` | — | InterviewSessionController.cs:435-436 |  | — |
| F1.1 | :SessionService | :InterviewSessionController | ret | `F1.1. throw InvalidOperationException("Invalid interaction mode.")` | F1 | SessionService.cs:400-401 | throw | — |
| F1.2 | :InterviewSessionController | :API Gateway | ret | `F1.2. Return 400 ApiResponse.Fail(ex.Message)` | F1 | InterviewSessionController.cs:445-448 |  | — |
| F1.3 | :API Gateway | :InterviewService (WebClient) | ret | `F1.3. Return 400` | F1 | ApiGateway/appsettings.json:49-54 |  | — |
| F1.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `F1.4. Return StartInterviewResult.Failure(error)` | F1 | InterviewService.cs:180-181 |  | — |
| F1.5 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `F1.5. Return View Index (dashboard + ErrorMessage)` | F1 | InterviewController.cs:84-93 |  | — |
| F1.6 | :Interview Dashboard UI | User | ret | `F1.6. Dashboard with the error banner` | F1 | Index.cshtml:38-41 |  | — |
| 54 | :SessionService | :SessionRepository | call | `54. ResolveSessionAsync(sessionId) -> GetByIdAsync(...)` | — | SessionService.cs:403, 1621 |  | 54. ResolveSessionAsync(sessionId) -> GetByIdAsync(sessionId, ct) |
| 55 | :SessionRepository | SQL Server | call | `55. SELECT InterviewSession WHERE SessionId AND !IsDeleted` | — | SessionRepository.cs:21-24 |  | — |
| 56 | SQL Server | :SessionRepository | ret | `56. Return row / null` | — | SessionRepository.cs:21-24 |  | — |
| 57 | :SessionRepository | :SessionService | ret | `57. Return InterviewSession?` | — | SessionRepository.cs:21-24 |  | — |
| F2.1 | :SessionService | :InterviewSessionController | ret | `F2.1. throw InvalidOperationException` | F2 | SessionService.cs:1622-1625 | throw | F2.1. throw InvalidOperationException("Session not found.") / ("Session already started.") |
| F2.2 | :InterviewSessionController | :API Gateway | ret | `F2.2. Return 400 ApiResponse.Fail(ex.Message)` | F2 | InterviewSessionController.cs:445-448 |  | — |
| F2.3 | :API Gateway | :InterviewService (WebClient) | ret | `F2.3. Return 400` | F2 | ApiGateway/appsettings.json:49-54 |  | — |
| F2.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `F2.4. Return StartInterviewResult.Failure(error)` | F2 | InterviewService.cs:180-181 |  | — |
| F2.5 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `F2.5. Return View Index (dashboard + ErrorMessage)` | F2 | InterviewController.cs:84-93 |  | — |
| F2.6 | :Interview Dashboard UI | User | ret | `F2.6. Dashboard with the error banner` | F2 | Index.cshtml:38-41 |  | — |
| 58 | :SessionService | :SessionRepository | call | `58. SaveChangesAsync(ct)` | — | SessionService.cs:417-422 |  | 58. SaveChangesAsync(ct) after setting session.InteractionMode = req.Mode and UpdatedAt = now |
| 59 | :SessionRepository | SQL Server | call | `59. UPDATE InterviewSession SET InteractionMode, UpdatedAt` | — | SessionRepository.cs:348 |  | — |
| 60 | SQL Server | :SessionRepository | ret | `60. Return rows affected` | — | SessionRepository.cs:348 |  | — |
| 61 | :SessionRepository | :SessionService | ret | `61. Return` | — | SessionRepository.cs:348 |  | — |
| 62 | :SessionService | :InterviewSessionController | ret | `62. Return ConfigResponse{UpdatedField="InteractionMode"}` | — | SessionService.cs:424-429 |  | 62. Return ConfigResponse{SessionId, UpdatedField="InteractionMode", UpdatedValue} |
| 63 | :InterviewSessionController | :API Gateway | ret | `63. Return 200 ApiResponse.Ok(result)` | — | InterviewSessionController.cs:437 |  | — |
| 64 | :API Gateway | :InterviewService (WebClient) | ret | `64. Return 200` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 65 | :InterviewService (WebClient) | :API Gateway | call | `65. POST api/interview/sessions/init [see #4]` | — | InterviewService.cs:183-186 | boundary | 65. POST api/interview/sessions/init { SessionId, Position, Jd, FocusAreas } - the whole init flow (question planning + room creation) is #4 StartInterviewSession |
| 66 | :API Gateway | :InterviewService (WebClient) | ret | `66. Return 200 InitResponse / non-2xx` | — | InterviewService.cs:187 | boundary | — |
| G.1 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `G.1. Return StartInterviewResult.Failure(...)` | G | InterviewService.cs:187-188 |  | G.1. Return StartInterviewResult.Failure(init.Error ?? "Could not initialize the session.") |
| G.2 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `G.2. Return View Index (dashboard + ErrorMessage)` | G | InterviewController.cs:84-93 |  | — |
| G.3 | :Interview Dashboard UI | User | ret | `G.3. Dashboard with the error banner` | G | Index.cshtml:38-41 |  | — |
| 67 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `67. Return StartInterviewResult.Ok(sessionId)` | — | InterviewService.cs:193 |  | — |
| 68 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `68. 302 RedirectToAction(Verify, { id = sessionId })` | — | InterviewController.cs:99 |  | — |
| 69 | :Interview Dashboard UI | User | ret | `69. Biometric entry-gate screen [see #13]` | — | InterviewController.cs:96-99 | boundary | 69. Biometric entry-gate screen for the new session (#13 VerifyFaceEntryGate) - no credit has been charged yet |

**Cờ:** `self` · `throw` · `boundary`

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| A | opt | `[ai-service GET /catalog fails - non-2xx, unreachable, or unreadable JSON]` | A.1 → A.8 | — | — |
| B | opt | `[status loaded AND !FaceEnrolled]` | B.1 → B.2 | — | — |
| C | opt | `[DurationMinutes not in (15, 30, 45, 60)]` | C.1 → C.6 | — | — |
| D | opt | `[an InProgress / Paused session already exists for this user]` | D.1 → D.6 | — | — |
| E1 | opt | `[Level not in (Intern, Fresher, Junior, Middle, Senior, Lead)]` | E1.1 → E1.6 | — | — |
| E2 | opt | `[session is null OR session.UserId != userId OR session.Status != "Configured"]` | E2.1 → E2.6 | — | **2** (cùng 400, cùng banner Dashboard; 2 câu thông điệp ghi đủ trong nhãn `E2.1`) |
| F1 | opt | `[Mode not in ("Text", "Voice")]` | F1.1 → F1.6 | — | — |
| F2 | opt | `[session is null OR session.UserId != userId OR session.Status != "Configured"]` | F2.1 → F2.6 | — | **2** (như E2) |
| G | opt | `[the init call failed - see #4 StartInterviewSession]` | G.1 → G.3 | — | — |

> Không có khung `alt` nào: mọi rẽ nhánh trong code là `if ... throw` / early-return, không có
> `else` (`SessionService.cs:332-333, 366-367, 400-401, 1605-1625`;
> `InterviewController.cs:62-67, 73-94`).

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | 1-2 | `The catalog fetch is asynchronous and completely independent of the form submit: dashboard.js fires it once the page is already rendered. It is drawn as phase 0 only because it happens first in time, not because Start waits for it.` |
| N2 | A.7 | `KI-13: the fetch chain has NO error check. A 400 from /Interview/Catalog still parses as JSON, so the .catch fallback ("Could not load technologies. You can still start...") never runs. The user is told "No technologies found for this role." instead - which is wrong: the role does have technologies, the lookup just failed.` |
| N3 | 22 / 30 | `Only the FIRST config call (duration) can create a session, because only it is sent without a SessionId. The 409 "active_session_exists" guard therefore exists only on this call - the level and interaction calls always carry the id created here.` |
| N4 | E1.6 | `A failure on the level or interaction call leaves the InterviewSession row already INSERTed by the duration call behind, with Status="Configured". Nothing deletes it; the next Start creates another row (a Configured session does not block a new one - only InProgress/Paused does).` |
| N5 | 51 | `InteractionMode is hard-coded to "Text" by the WebClient (InterviewService.InteractionMode constant). The dashboard form has no Text/Voice switch, so the "Voice" branch of this endpoint is only reachable by calling the API directly.` |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | Liệt kê | Nguồn |
|---|---|---|---|---|
| 1 | InterviewController (WebClient) | class | SUBSET (`Catalog`, `Start`) | local |
| 2 | Controller | framework-base | FULL (rỗng) | SHARED |
| 3 | IInterviewService (WebClient) | interface | SUBSET | local |
| 4 | InterviewService (WebClient) | class | SUBSET | local |
| 5 | IBiometricsService (WebClient) | interface | FULL | local |
| 6 | IApiClient / 7 ApiClient | interface/class | FULL | SHARED |
| 8 | DashboardViewModel | DTO | FULL | local |
| 9 | StartInterviewResult | DTO | FULL | local |
| 10 | CatalogResponse (WebClient) | DTO | FULL | local |
| 11 | ConfigResponse (WebClient) | DTO | FULL | local |
| 12 | ApiResponse<T> (WebClient) | DTO | FULL | local |
| 13 | ControllerBase | framework-base | FULL (rỗng) | SHARED |
| 14 | InterviewSessionController (InterviewAPI) | class | SUBSET | local |
| 15 | ISessionService | interface | SUBSET | local |
| 16 | SessionService (InterviewAPI) | class | SUBSET | local |
| 17 | SessionGuardException | exception | FULL | local |
| 18 | ApiResponse<T> (InterviewAPI) | DTO | FULL | SHARED |
| 19 | ConfigDurationRequest / 20 ConfigLevelRequest / 21 ConfigInteractionRequest / 22 ConfigResponse | DTO | FULL | local |
| 23 | AiCatalogResponse | DTO | FULL | local |
| 24 | IAiServiceClient | interface | FULL | local |
| 25 | AiServiceClient (InterviewAPI) | class | FULL | SHARED |
| 26 | ISessionRepository / 27 SessionRepository (InterviewAPI) | interface/class | SUBSET | local |
| 28 | InterviewSession | entity | FULL | SHARED |
| 29 | InterviewDbContext / 30 DbContext | class/framework-base | FULL | SHARED |

## 8. CLASS — chi tiết

### `InterviewController (WebClient)` — `SUBSET`
- Field `- interviewService: IInterviewService`, `- biometricsService: IBiometricsService`
- Method `+ Catalog(cancellationToken)`, `+ Start(model: DashboardViewModel, cancellationToken)`

### `SessionService (InterviewAPI)` — `SUBSET`
- 10 field DI + 3 mảng hằng `ValidDurations` / `ValidLevels` / `ValidModes`
- Method `+ ConfigDurationAsync(...)`, `+ ConfigLevelAsync(...)`, `+ ConfigInteractionAsync(...)`,
  `+ GetActiveSessionAsync(...)`, `- ResolveSessionAsync(...)`

### `SessionRepository (InterviewAPI)` — `SUBSET`
- `+ GetByIdAsync(...)`, `+ GetActiveSessionAsync(...)`, `+ CreateAsync(...)`, `+ SaveChangesAsync(...)`

Không có class SHARED mới trong chức năng này.

## 9. QUAN HỆ CLASS

Xem `specs/configureinterviewsession_classes.yaml` khoá `relations:`.

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| **KI-13** | `dashboard.js:94-113` | Chuỗi `fetch(catalogUrl).then(r => r.json()).then(...)` **không kiểm `response.ok`**. Khi server trả 400, `r.json()` vẫn parse được `{error:…}` nên `.catch` KHÔNG chạy; `techByRole` thành `{}` và user thấy **"No technologies found for this role."** thay vì thông điệp dự phòng đã viết sẵn ở `.catch` | bug hành vi có hậu quả thật (thông điệp sai) | **Vẽ hành vi THẬT** (`opt A`, message A.7/A.8) + note N2 | **Có — thêm mới KI-13** |
| — | `SessionService.cs:369-381, 403-415` | Factory mặc định của `ConfigLevelAsync`/`ConfigInteractionAsync` hard-code `DurationMinutes=30` / `Level="Junior"` | code chết với UI hiện tại | **Không vẽ** (§1) — `StartInterviewAsync` luôn gọi duration trước và luôn gửi `SessionId` cho 2 lệnh sau. Vẫn là code sống nếu gọi thẳng API | không (không phải bug, chỉ là nhánh UI không chạm tới) |
| — | `InterviewService.cs:173-181` | Lỗi ở lệnh level/interaction để lại hàng `InterviewSessions` đã INSERT (Status="Configured") không ai dọn | rác dữ liệu mức thấp | **Vẽ thật** + note N4 | không — không gây lỗi cho user (phiên `Configured` không chặn Start lần sau), chỉ là hàng thừa. Ghi lại để nhóm quyết |


### Gỡ từ note trên diagram — anomaly

- **(cạnh message `8`)** KI-13: the fetch chain has NO response.ok check. A 400 from /Interview/Catalog still parses as JSON, so the .catch fallback ("Could not load technologies. You can still start...") never runs. The user is told "No technologies found for this role." instead - which is wrong: the role does have technologies, the lookup just failed. *(gỡ từ note trên diagram)*

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | `:BiometricsService (WebClient)` chỉ xuất hiện ở pha 1, nếu xếp theo đúng "thứ tự gọi lần đầu" thì nó nằm sau `:ai-service` | **Xếp ngay sau `:InterviewController (WebClient)`** (nhóm theo tầng) | `QUY_TAC_VE_DIAGRAM.md` §1.1 yêu cầu bám tầng WebClient → Gateway → API; để 1 service WebClient nằm bên phải ai-service sẽ khiến người đọc tưởng nó là service backend. Ghi lại để rà. |
| 2 | 2 guard của `ResolveSessionAsync` ("Session not found." / "Session already started.") | **Gộp 1 `opt` (E2, F2), nhãn `.1` ghi ĐỦ cả 2 câu** | Cùng helper, cùng HTTP 400, cùng bề mặt hiển thị (banner Dashboard). Không viết tắt: cả 2 câu đều nằm nguyên văn trong nhãn. |
| 3 | Guard "Invalid level" / "Invalid duration" / "Invalid interaction mode" | **Tách riêng khỏi nhóm trên** | Xảy ra ở **vị trí khác** trong hàm (validate trước khi resolve session) → gộp sẽ sai thứ tự. |
| 4 | `POST .../init` (lệnh thứ 4 cùng hàm) | **Boundary 1 cặp call+ret** tới `:API Gateway` + note | Toàn bộ init thuộc `#4`; mở rộng sẽ nuốt luôn chức năng đó. Vẽ tới Gateway (không tới controller) vì đó đúng là hop tiếp theo thật. |
| 5 | Cổng sinh trắc đầu action `Start` | **Boundary 1 cặp call+ret** tới `:BiometricsService (WebClient)` | Chuỗi đầy đủ đã vẽ ở `#1` pha 0. |
| 6 | `Guid.Parse(...)` lặp 3 lần (mỗi lệnh config) | **Chỉ vẽ self-message ở lệnh đầu (msg 24)**; 2 lệnh sau ghi "userId from the JWT claim" trong nhãn call | Cùng 1 dòng code lặp lại, vẽ 3 lần tốn 180px mà không thêm thông tin. |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `4`)** The catalog fetch is asynchronous and completely independent of the form submit: dashboard.js fires it once the page is already rendered. It is drawn as phase 0 only because it happens first in time, not because Start waits for it. *(gỡ từ note trên diagram)*
- **(cạnh message `A.3`)** Only the FIRST config call (duration) can create a session, because only it is sent without a SessionId. The 409 "active_session_exists" guard therefore exists only on this call - the level and interaction calls always carry the id created here. *(gỡ từ note trên diagram)*
- **(cạnh message `A.8`)** A failure on the level or interaction call leaves the InterviewSession row already INSERTed by the duration call behind, with Status="Configured". Nothing deletes it; the next Start creates another row (a Configured session does not block a new one - only InProgress/Paused does). *(gỡ từ note trên diagram)*
- **(cạnh message `11`)** InteractionMode is hard-coded to "Text" by the WebClient (InterviewService.InteractionMode constant). The dashboard form has no Text/Voice switch, so the "Voice" branch of this endpoint is only reachable by calling the API directly. *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| `uc_id` của chức năng | RECON không ghi UC-id cho #3 | Nhóm xác nhận ê-líp use-case rồi điền vào `specs/configureinterviewsession.yaml` | luồng 1 |
