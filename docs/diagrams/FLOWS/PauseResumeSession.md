# FLOW — `PauseResumeSession` · BL#39, 40, 62, 65 · UC-`09`

> Đặt tại `docs/diagrams/FLOWS/PauseResumeSession.md`
> Trạng thái: `DRAFT` — Khảo sát: lô B, 2026-08-03

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Cho ứng viên tạm dừng phiên phỏng vấn đang chạy (đóng băng countdown + idle clock, che khung chat) và mở lại, trong hạn mức 3 lần / tổng 300 giây mỗi phiên |
| Điểm vào (user thao tác gì) | Bấm nút **Pause Interview** trong Room; bấm **Resume Interview** trên lớp phủ pause (hoặc quota pause về 0 → tự gọi resume) |
| Kết thúc (user thấy gì) | Pause: lớp phủ "Interview Paused" + dòng quota, mọi nút nhập bị khoá. Resume: lớp phủ biến mất, countdown chạy tiếp từ `RemainingSeconds` server trả về |
| Quy mô | `9` participant · `91` message · `0` alt / `8` opt · trang Sequence 1790×4159 |
| Đối chiếu ước lượng RECON | RECON đoán `16` · thật `91` · hệ số `5.7×` |
| Service ngoài quyền sở hữu | không |
| **Gọi BYPASS Gateway?** | không — cả 2 endpoint đều đi qua ApiGateway :7000 (`InterviewService` dùng `apiClient.HttpClient`, base = Gateway) |
| Nhà cung cấp AI | không |
| Ghi DB | `InterviewSessions` UPDATE `Status`/`PausedAt`/`PauseCount`/`UpdatedAt` (pause) và `Status`/`PausedAt=NULL`/`TotalPausedSeconds`/`UpdatedAt` (unpause) — qua `SaveChangesAsync`, KHÔNG atomic |
| Trừ credit | không |
| KI liên quan | `KI-06` (pause/unpause không atomic), `KI-07` (không có poll loop `/timer`) |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| `AutoResumeIfPauseExpiredAsync` (self-heal pause quá hạn) | [SessionService.cs:113-128](../../../source/InterviewAPI/Services/SessionService.cs#L113-L128) | KHÔNG nằm trong `PauseSessionAsync`/`UnpauseSessionAsync`. Chỉ được gọi từ `ResumeSessionAsync` (:1273), `GetActiveSessionAsync` (:992), `GetTimerAsync` (:966) — 3 luồng khác. Vẽ vào đây sẽ sai luồng. |
| `TryAutoResumeExpiredPauseAsync` (atomic `ExecuteUpdateAsync`) | [SessionRepository.cs:315-328](../../../source/InterviewAPI/Repositories/SessionRepository.cs#L315-L328) | Chỉ là repo-call của self-heal trên. Cùng lý do. |
| `GetTimerAsync` + endpoint `GET .../timer` | [SessionService.cs:959-980](../../../source/InterviewAPI/Services/SessionService.cs#L959-L980), [InterviewSessionController.cs:230-243](../../../source/InterviewAPI/Controllers/InterviewSessionController.cs#L230-L243) | Dead endpoint từ góc nhìn Browser (KI-07): grep toàn bộ `InterviewService.cs` không có caller. Comment trong code mô tả nó như "poll loop" nhưng không tồn tại. |
| Draft `sessionStorage` của ô nhập câu trả lời | [interview-room.js:788-820](../../../source/WebClient/wwwroot/js/pages/interview-room.js#L788-L820) | Chỉ giữ nội dung ô input khi F5, KHÔNG lưu trạng thái pause. Không thuộc luồng này (RECON cảnh báo dễ vẽ nhầm). |
| `updatePauseQuotaUI()` khi load trang (đọc `data-pause-*` từ Razor) | [Room.cshtml:62-64](../../../source/WebClient/Views/Interview/Room.cshtml#L62-L64) | Khởi tạo trạng thái ban đầu của trang Room, thuộc chức năng EnterInterviewRoom. |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| *(không có)* | — | — | — |

> Luồng này khép kín: Browser → WebClient → Gateway → InterviewAPI → SQL. Không gọi ai-service,
> không gọi service khác, không tái dùng luồng chức năng khác.

---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 0 | User | actor | — | — |
| 1 | :Interview Room UI | box | `wwwroot/js/pages/interview-room.js` | — |
| 2 | :InterviewController (WebClient) | box | `WebClient/Controllers/Interview/InterviewController.cs` | — |
| 3 | :InterviewService (WebClient) | box | `WebClient/Services/Interview/InterviewService.cs` | — |
| 4 | :API Gateway | box | `ApiGateway` (YARP :7000) | — |
| 5 | :InterviewSessionController (InterviewAPI) | box | `InterviewAPI/Controllers/InterviewSessionController.cs` | có |
| 6 | :SessionService (InterviewAPI) | box | `InterviewAPI/Services/SessionService.cs` | có |
| 7 | :SessionRepository (InterviewAPI) | box | `InterviewAPI/Repositories/SessionRepository.cs` | có |
| 8 | SQL Server | cylinder | `DevPrep_Interview` | — |

> Actor `User` là actor **trực tiếp** cho cả 2 pha (bấm nút). Nhánh auto-resume (quota về 0) chạy
> khi user vẫn đang ngồi trước màn hình pause → theo quy ước ACTOR GIÁN TIẾP vẫn giữ actor `User`,
> nhãn message 27 ghi rõ trigger tự động.

## 4. BẢNG MESSAGE

### Pha 1 — Pause

| # | From | To | call/ret | Label (tiếng Anh) | Frame (guard) | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Interview Room UI | call | `1. Click Pause Interview` | — | interview-room.js:299 |  | — |
| 2 | :Interview Room UI | :Interview Room UI | call | `2. pauseInterview(): guard, then pauseBtn.disabled = true` | — | interview-room.js:264-266 | self | — |
| F1.1 | :Interview Room UI | :Interview Room UI | call | `F1.1. return early - no request sent` | F1 | interview-room.js:265 | self | — |
| F1.2 | :Interview Room UI | User | ret | `F1.2. Nothing happens (Pause button state unchanged)` | F1 | interview-room.js:265 |  | — |
| 3 | :Interview Room UI | :InterviewController (WebClient) | call | `3. POST /Interview/Pause?id={sessionId} (postProxy fetch)` | — | interview-room.js:268, 141-146 |  | — |
| 4 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `4. PauseSessionAsync(id, ct)` | — | InterviewController.cs:554 |  | — |
| 5 | :InterviewService (WebClient) | :API Gateway | call | `5. POST api/interview/sessions/{sessionId}/pause` | — | InterviewService.cs:297-300 |  | — |
| 6 | :API Gateway | :InterviewSessionController (InterviewAPI) | call | `6. Forward (route interview-route, no prefix strip)` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 7 | :InterviewSessionController (InterviewAPI) | (self) | call | `7. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | InterviewSessionController.cs:302 | self | — |
| 8 | :InterviewSessionController (InterviewAPI) | :SessionService (InterviewAPI) | call | `8. PauseSessionAsync(userId, sessionId, ct)` | — | InterviewSessionController.cs:303 |  | — |
| 9 | :SessionService (InterviewAPI) | :SessionRepository (InterviewAPI) | call | `9. GetByIdAsync(sessionId, ct)` | — | SessionService.cs:1124 |  | — |
| 10 | :SessionRepository (InterviewAPI) | SQL Server | call | `10. SELECT InterviewSession WHERE SessionId AND !IsDeleted` | — | SessionRepository.cs:21-24 |  | — |
| 11 | SQL Server | :SessionRepository (InterviewAPI) | ret | `11. Return session row / null` | — | SessionRepository.cs:21-24 |  | — |
| 12 | :SessionRepository (InterviewAPI) | :SessionService (InterviewAPI) | ret | `12. Return InterviewSession?` | — | SessionRepository.cs:21-24 |  | — |
| F2.1 | :SessionService (InterviewAPI) | :InterviewSessionController (InterviewAPI) | ret | `F2.1. throw InvalidOperationException("Session not found.")` | F2 | SessionService.cs:1125-1126 | throw | — |
| F2.2 | :InterviewSessionController (InterviewAPI) | :API Gateway | ret | `F2.2. Return 400 ApiResponse.Fail(ex.Message)` | F2 | InterviewSessionController.cs:306-308 |  | — |
| F2.3 | :API Gateway | :InterviewService (WebClient) | ret | `F2.3. Return 400` | F2 | ApiGateway/appsettings.json:49-54 |  | — |
| F2.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `F2.4. Return ApiCallResult.Fail(message, 400)` | F2 | InterviewService.cs:636-639 |  | — |
| F2.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `F2.5. Return 400 { error }` | F2 | InterviewController.cs:556-559 |  | — |
| F2.6 | :Interview Room UI | User | ret | `F2.6. appendBubble("AI", "Could not pause: ...")` | F2 | interview-room.js:274-277 |  | F2.6. appendBubble("AI", "Could not pause: Session not found.") + updatePauseQuotaUI() |
| F3.1 | :SessionService (InterviewAPI) | :InterviewSessionController (InterviewAPI) | ret | `F3.1. throw InvalidOperationException` | F3 | SessionService.cs:1127-1128 | throw | F3.1. throw InvalidOperationException("Only an in-progress session can be paused.") |
| F3.2 | :InterviewSessionController (InterviewAPI) | :API Gateway | ret | `F3.2. Return 400 ApiResponse.Fail(ex.Message)` | F3 | InterviewSessionController.cs:306-308 |  | — |
| F3.3 | :API Gateway | :InterviewService (WebClient) | ret | `F3.3. Return 400` | F3 | ApiGateway/appsettings.json:49-54 |  | — |
| F3.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `F3.4. Return ApiCallResult.Fail(message, 400)` | F3 | InterviewService.cs:636-639 |  | — |
| F3.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `F3.5. Return 400 { error }` | F3 | InterviewController.cs:556-559 |  | — |
| F3.6 | :Interview Room UI | User | ret | `F3.6. appendBubble("AI", "Could not pause: ...")` | F3 | interview-room.js:274-277 |  | F3.6. appendBubble("AI", "Could not pause: Only an in-progress session can be paused.") |
| F4.1 | :SessionService (InterviewAPI) | :InterviewSessionController (InterviewAPI) | ret | `F4.1. throw InvalidOperationException` | F4 | SessionService.cs:1130-1131 | throw | F4.1. throw InvalidOperationException("Pause limit reached (3 pauses per session).") |
| F4.2 | :InterviewSessionController (InterviewAPI) | :API Gateway | ret | `F4.2. Return 400 ApiResponse.Fail(ex.Message)` | F4 | InterviewSessionController.cs:306-308 |  | — |
| F4.3 | :API Gateway | :InterviewService (WebClient) | ret | `F4.3. Return 400` | F4 | ApiGateway/appsettings.json:49-54 |  | — |
| F4.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `F4.4. Return ApiCallResult.Fail(message, 400)` | F4 | InterviewService.cs:636-639 |  | — |
| F4.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `F4.5. Return 400 { error }` | F4 | InterviewController.cs:556-559 |  | — |
| F4.6 | :Interview Room UI | User | ret | `F4.6. appendBubble("AI", "Could not pause: ...")` | F4 | interview-room.js:274-277 |  | F4.6. appendBubble("AI", "Could not pause: Pause limit reached (3 pauses per session).") |
| F5.1 | :SessionService (InterviewAPI) | :InterviewSessionController (InterviewAPI) | ret | `F5.1. throw InvalidOperationException` | F5 | SessionService.cs:1132-1133 | throw | F5.1. throw InvalidOperationException("Total pause time limit reached (300 seconds per session).") |
| F5.2 | :InterviewSessionController (InterviewAPI) | :API Gateway | ret | `F5.2. Return 400 ApiResponse.Fail(ex.Message)` | F5 | InterviewSessionController.cs:306-308 |  | — |
| F5.3 | :API Gateway | :InterviewService (WebClient) | ret | `F5.3. Return 400` | F5 | ApiGateway/appsettings.json:49-54 |  | — |
| F5.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `F5.4. Return ApiCallResult.Fail(message, 400)` | F5 | InterviewService.cs:636-639 |  | — |
| F5.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `F5.5. Return 400 { error }` | F5 | InterviewController.cs:556-559 |  | — |
| F5.6 | :Interview Room UI | User | ret | `F5.6. appendBubble("AI", "Could not pause: ...")` | F5 | interview-room.js:274-277 |  | F5.6. appendBubble("AI", "Could not pause: Total pause time limit reached (300 seconds per session).") |
| 13 | :SessionService (InterviewAPI) | (self) | call | `13. session.Status="Paused"; PausedAt=now; PauseCount+=1` | — | SessionService.cs:1135-1138 | self | 13. session.Status="Paused"; PausedAt=now; PauseCount+=1; UpdatedAt=now |
| 14 | :SessionService (InterviewAPI) | :SessionRepository (InterviewAPI) | call | `14. SaveChangesAsync(ct)` | — | SessionService.cs:1139 |  | — |
| 15 | :SessionRepository (InterviewAPI) | SQL Server | call | `15. UPDATE InterviewSessions SET Status, PausedAt, PauseCount` | — | SessionRepository.cs:348 |  | 15. UPDATE InterviewSession SET Status, PausedAt, PauseCount, UpdatedAt |
| 16 | SQL Server | :SessionRepository (InterviewAPI) | ret | `16. Return rows affected` | — | SessionRepository.cs:348 |  | — |
| 17 | :SessionRepository (InterviewAPI) | :SessionService (InterviewAPI) | ret | `17. Return` | — | SessionRepository.cs:348 |  | — |
| 18 | :SessionService (InterviewAPI) | (self) | call | `18. PausesRemaining, PausedSecondsRemaining recomputed` | — | SessionService.cs:1141-1147, 90-96 | self | 18. PausesRemaining = Max(0, 3 - PauseCount); PausedSecondsRemaining = Max(0, 300 - GetEffectivePausedSeconds(session)) |
| 19 | :SessionService (InterviewAPI) | :InterviewSessionController (InterviewAPI) | ret | `19. Return PauseResponse` | — | SessionService.cs:1141-1147 |  | 19. Return PauseResponse{Status, PauseCount, PausesRemaining, PausedSecondsRemaining} |
| 20 | :InterviewSessionController (InterviewAPI) | :API Gateway | ret | `20. Return 200 ApiResponse.Ok(result)` | — | InterviewSessionController.cs:304 |  | — |
| 21 | :API Gateway | :InterviewService (WebClient) | ret | `21. Return 200` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 22 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `22. Return ApiCallResult.Ok(PauseResponse, 200)` | — | InterviewService.cs:662, 301 |  | — |
| 23 | :InterviewController (WebClient) | :Interview Room UI | ret | `23. Return 200 JSON (PauseResponse)` | — | InterviewController.cs:561 |  | — |
| 24 | :Interview Room UI | (self) | call | `24. Apply data.*; setStatusDisplay("Paused"); setPausedUI(true)` | — | interview-room.js:269-273 | self | 24. pauseCount/pausesRemaining/pausedSecondsRemaining = data.*; setStatusDisplay("Paused"); setPausedUI(true) |
| 25 | :Interview Room UI | (self) | call | `25. startPauseQuotaTick()` | — | interview-room.js:259, 217-234 | self | 25. startPauseQuotaTick(): 1s interval ticks pausedSecondsRemaining down |
| 26 | :Interview Room UI | User | ret | `26. Show pause overlay; countdown frozen; controls disabled` | — | interview-room.js:248-262 |  | 26. Pause overlay shown; countdown + idle clock frozen; input/send/skip/hint/mic disabled; quota line updated |

### Pha 2 — Resume

| # | From | To | call/ret | Label (tiếng Anh) | Frame (guard) | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 27 | User | :Interview Room UI | call | `27. Click Resume Interview OR [auto] quota hit 0` | — | interview-room.js:300, 229-232 |  | 27. Click Resume Interview  OR  [auto] local pause quota hit 0 -> resumeInterview() |
| 28 | :Interview Room UI | (self) | call | `28. resumeInterview(): guard, then resumeBtn.disabled = true` | — | interview-room.js:281-282 | self | — |
| F6.1 | :Interview Room UI | (self) | call | `F6.1. return early - no request sent` | F6 | interview-room.js:281 | self | — |
| F6.2 | :Interview Room UI | User | ret | `F6.2. Nothing happens (still on the paused screen)` | F6 | interview-room.js:281 |  | — |
| 29 | :Interview Room UI | :InterviewController (WebClient) | call | `29. POST /Interview/Unpause?id={sessionId} (postProxy fetch)` | — | interview-room.js:284, 141-146 |  | — |
| 30 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `30. UnpauseSessionAsync(id, ct)` | — | InterviewController.cs:584 |  | — |
| 31 | :InterviewService (WebClient) | :API Gateway | call | `31. POST api/interview/sessions/{sessionId}/unpause` | — | InterviewService.cs:307-310 |  | — |
| 32 | :API Gateway | :InterviewSessionController (InterviewAPI) | call | `32. Forward (route interview-route, no prefix strip)` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 33 | :InterviewSessionController (InterviewAPI) | (self) | call | `33. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | InterviewSessionController.cs:318 | self | — |
| 34 | :InterviewSessionController (InterviewAPI) | :SessionService (InterviewAPI) | call | `34. UnpauseSessionAsync(userId, sessionId, ct)` | — | InterviewSessionController.cs:319 |  | — |
| 35 | :SessionService (InterviewAPI) | :SessionRepository (InterviewAPI) | call | `35. GetByIdAsync(sessionId, ct)` | — | SessionService.cs:1152 |  | — |
| 36 | :SessionRepository (InterviewAPI) | SQL Server | call | `36. SELECT InterviewSession WHERE SessionId AND !IsDeleted` | — | SessionRepository.cs:21-24 |  | — |
| 37 | SQL Server | :SessionRepository (InterviewAPI) | ret | `37. Return session row / null` | — | SessionRepository.cs:21-24 |  | — |
| 38 | :SessionRepository (InterviewAPI) | :SessionService (InterviewAPI) | ret | `38. Return InterviewSession?` | — | SessionRepository.cs:21-24 |  | — |
| F7.1 | :SessionService (InterviewAPI) | :InterviewSessionController (InterviewAPI) | ret | `F7.1. throw InvalidOperationException("Session not found.")` | F7 | SessionService.cs:1153-1154 | throw | — |
| F7.2 | :InterviewSessionController (InterviewAPI) | :API Gateway | ret | `F7.2. Return 400 ApiResponse.Fail(ex.Message)` | F7 | InterviewSessionController.cs:322-324 |  | — |
| F7.3 | :API Gateway | :InterviewService (WebClient) | ret | `F7.3. Return 400` | F7 | ApiGateway/appsettings.json:49-54 |  | — |
| F7.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `F7.4. Return ApiCallResult.Fail(message, 400)` | F7 | InterviewService.cs:636-639 |  | — |
| F7.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `F7.5. Return 400 { error }` | F7 | InterviewController.cs:586-589 |  | — |
| F7.6 | :Interview Room UI | User | ret | `F7.6. appendBubble("AI", "Could not resume: ...")` | F7 | interview-room.js:292-296 |  | F7.6. appendBubble("AI", "Could not resume: Session not found.") + resumeBtn re-enabled (finally) |
| F8.1 | :SessionService (InterviewAPI) | :InterviewSessionController (InterviewAPI) | ret | `F8.1. throw InvalidOperationException("Session is not paused.")` | F8 | SessionService.cs:1155-1156 | throw | — |
| F8.2 | :InterviewSessionController (InterviewAPI) | :API Gateway | ret | `F8.2. Return 400 ApiResponse.Fail(ex.Message)` | F8 | InterviewSessionController.cs:322-324 |  | — |
| F8.3 | :API Gateway | :InterviewService (WebClient) | ret | `F8.3. Return 400` | F8 | ApiGateway/appsettings.json:49-54 |  | — |
| F8.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `F8.4. Return ApiCallResult.Fail(message, 400)` | F8 | InterviewService.cs:636-639 |  | — |
| F8.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `F8.5. Return 400 { error }` | F8 | InterviewController.cs:586-589 |  | — |
| F8.6 | :Interview Room UI | User | ret | `F8.6. appendBubble("AI", "Could not resume: ...")` | F8 | interview-room.js:292-296 |  | F8.6. appendBubble("AI", "Could not resume: Session is not paused.") + resumeBtn re-enabled (finally) |
| 39 | :SessionService (InterviewAPI) | (self) | call | `39. TotalPausedSeconds += now - PausedAt; Status="InProgress"` | — | SessionService.cs:1158-1161 | self | 39. TotalPausedSeconds += (now - PausedAt).TotalSeconds; PausedAt=null; Status="InProgress"; UpdatedAt=now |
| 40 | :SessionService (InterviewAPI) | :SessionRepository (InterviewAPI) | call | `40. SaveChangesAsync(ct)` | — | SessionService.cs:1162 |  | — |
| 41 | :SessionRepository (InterviewAPI) | SQL Server | call | `41. UPDATE InterviewSessions SET Status, TotalPausedSeconds` | — | SessionRepository.cs:348 |  | 41. UPDATE InterviewSession SET Status, PausedAt = NULL, TotalPausedSeconds, UpdatedAt |
| 42 | SQL Server | :SessionRepository (InterviewAPI) | ret | `42. Return rows affected` | — | SessionRepository.cs:348 |  | — |
| 43 | :SessionRepository (InterviewAPI) | :SessionService (InterviewAPI) | ret | `43. Return` | — | SessionRepository.cs:348 |  | — |
| 44 | :SessionService (InterviewAPI) | (self) | call | `44. elapsed = now - StartedAt - paused; remaining = Max(0, ...)` | — | SessionService.cs:1164-1167 | self | 44. elapsed = (now - StartedAt) - GetEffectivePausedSeconds(session); remaining = Max(0, DurationMinutes*60 - elapsed) |
| 45 | :SessionService (InterviewAPI) | :InterviewSessionController (InterviewAPI) | ret | `45. Return UnpauseResponse` | — | SessionService.cs:1169-1176 |  | 45. Return UnpauseResponse{Status, RemainingSeconds, PauseCount, PausesRemaining, PausedSecondsRemaining} |
| 46 | :InterviewSessionController (InterviewAPI) | :API Gateway | ret | `46. Return 200 ApiResponse.Ok(result)` | — | InterviewSessionController.cs:320 |  | — |
| 47 | :API Gateway | :InterviewService (WebClient) | ret | `47. Return 200` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 48 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `48. Return ApiCallResult.Ok(UnpauseResponse, 200)` | — | InterviewService.cs:662, 311 |  | — |
| 49 | :InterviewController (WebClient) | :Interview Room UI | ret | `49. Return 200 JSON (UnpauseResponse)` | — | InterviewController.cs:591 |  | — |
| 50 | :Interview Room UI | (self) | call | `50. Apply data.*; setPausedUI(false); stopPauseQuotaTick()` | — | interview-room.js:285-291 | self | 50. remainingSeconds/pauseCount/... = data.*; updateTimerDisplay(); setStatusDisplay("InProgress"); setPausedUI(false) -> stopPauseQuotaTick() |
| 51 | :Interview Room UI | User | ret | `51. Hide overlay; countdown resumes; inputs re-enabled` | — | interview-room.js:248-262, 294-296 |  | 51. Overlay hidden; countdown resumes from RemainingSeconds; input/send/skip/hint/mic re-enabled; Resume button re-enabled (finally) |

**Cờ:** `self` · `throw`

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| F1 | opt | `[paused OR ended OR !pauseUrl]` | F1.1 → F1.2 | — | — |
| F2 | opt | `[session is null OR session.UserId != userId]` | F2.1 → F2.6 | — | — |
| F3 | opt | `[session.Status != "InProgress"]` | F3.1 → F3.6 | — | — |
| F4 | opt | `[session.PauseCount >= MaxPauseCount (3)]` | F4.1 → F4.6 | — | — |
| F5 | opt | `[session.TotalPausedSeconds >= MaxTotalPausedSeconds (300)]` | F5.1 → F5.6 | — | — |
| F6 | opt | `[!paused OR !unpauseUrl]` | F6.1 → F6.2 | — | — |
| F7 | opt | `[session is null OR session.UserId != userId]` | F7.1 → F7.6 | — | — |
| F8 | opt | `[session.Status != "Paused" OR session.PausedAt is null]` | F8.1 → F8.6 | — | — |

> Tất cả là `opt` chứ không phải `alt`: mỗi guard là một `if ... throw` early-return trong code
> ([SessionService.cs:1125-1133](../../../source/InterviewAPI/Services/SessionService.cs#L1125-L1133),
> [:1153-1156](../../../source/InterviewAPI/Services/SessionService.cs#L1153-L1156)) — không có `else`.

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | 13-17 | `PauseSessionAsync/UnpauseSessionAsync use a plain read-then-write (GetByIdAsync + SaveChangesAsync), NOT an atomic conditional UPDATE - two near-simultaneous Pause requests can both pass the quota guard (KI-06). The atomic ExecuteUpdateAsync in SessionRepository.TryAutoResumeExpiredPauseAsync belongs to the self-heal path (ResumeSessionAsync / GetActiveSessionAsync), not to this flow.` |
| N2 | 25 | `startPauseQuotaTick is a display-only countdown. The server stays the source of truth: it overwrites pausedSecondsRemaining on the next Resume response. When it reaches 0 it calls resumeInterview() itself - the same call as the Resume button (message 27).` |
| N3 | 27 | `WebClient never polls GET .../timer, so the server-side self-heal AutoResumeIfPauseExpiredAsync only fires when the user reloads the Room or the Dashboard (KI-07). The comment in SessionService.cs:99-100 that says "on the poll loop" does not match the code.` |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | File:Line | Liệt kê | Nguồn |
|---|---|---|---|---|---|
| 1 | InterviewController (WebClient) | class | WebClient/Controllers/Interview/InterviewController.cs:17, 551-562, 581-592 | SUBSET | local |
| 2 | Controller | framework-base | ASP.NET Core MVC | FULL (rỗng) | SHARED (**tạo mới**) |
| 3 | IInterviewService (WebClient) | interface | WebClient/Services/Interview/InterviewService.cs:11, 39-43 | SUBSET | local |
| 4 | InterviewService (WebClient) | class | WebClient/Services/Interview/InterviewService.cs:89, 294-312, 621-700 | SUBSET | local |
| 5 | IApiClient | interface | WebClient/Services/Shared/IApiClient.cs:3-6 | FULL | SHARED (**tạo mới**) |
| 6 | ApiClient | class | WebClient/Services/Shared/ApiClient.cs:3-6 | FULL | SHARED (**tạo mới**) |
| 7 | PauseResponse (WebClient) | DTO | WebClient/Models/Interview/PauseResponse.cs:4-10 | FULL | local |
| 8 | UnpauseResponse (WebClient) | DTO | WebClient/Models/Interview/UnpauseResponse.cs:4-11 | FULL | local |
| 9 | ApiCallResult<T> | DTO | WebClient/Models/Shared/ApiCallResult.cs:3-23 | FULL | local |
| 10 | ApiResponse<T> (WebClient) | DTO | WebClient/Models/Shared/ApiResponse.cs:5-12 | FULL | local (KHÔNG include `ApiResponse.yaml` — khác thân class) |
| 11 | ControllerBase | framework-base | ASP.NET Core | FULL (rỗng) | SHARED |
| 12 | InterviewSessionController (InterviewAPI) | class | InterviewAPI/Controllers/InterviewSessionController.cs:15, 296-326 | SUBSET | local |
| 13 | ISessionService | interface | InterviewAPI/Services/Interfaces/ISessionService.cs:28-29 | SUBSET | local |
| 14 | SessionService (InterviewAPI) | class | InterviewAPI/Services/SessionService.cs:45, 1122-1177 | SUBSET | local |
| 15 | ApiResponse<T> (InterviewAPI) | DTO | InterviewAPI/DTOs/ApiResponse.cs | FULL | SHARED |
| 16 | PauseResponse (InterviewAPI) | DTO | InterviewAPI/DTOs/Session/PauseResponse.cs:3-9 | FULL | local |
| 17 | UnpauseResponse (InterviewAPI) | DTO | InterviewAPI/DTOs/Session/UnpauseResponse.cs:3-10 | FULL | local |
| 18 | ISessionRepository | interface | InterviewAPI/Repositories/Interfaces/ISessionRepository.cs | SUBSET | local |
| 19 | SessionRepository (InterviewAPI) | class | InterviewAPI/Repositories/SessionRepository.cs:9-24, 348 | SUBSET | local |
| 20 | InterviewSession | entity | InterviewAPI/Models/InterviewSession.cs | FULL | SHARED |
| 21 | InterviewDbContext | class | InterviewAPI/Models/InterviewDbContext.cs + .Biometrics.cs | FULL | SHARED (**tạo mới**) |
| 22 | DbContext | framework-base | EF Core | FULL (rỗng) | SHARED (**tạo mới**) |

## 8. CLASS — chi tiết

### `InterviewController (WebClient)`
- File: `WebClient/Controllers/Interview/InterviewController.cs:17` — Liệt kê: `SUBSET`
- Field: `- interviewService: IInterviewService`
- Method: `+ Pause(id: Guid, cancellationToken: CancellationToken): Task<IActionResult>`
- Method: `+ Unpause(id: Guid, cancellationToken: CancellationToken): Task<IActionResult>`
- *(SUBSET)* Đã bỏ 4 field DI khác (`biometricsService`, `verificationService`, `feedbackService`,
  `paymentService`) và ~28 action ngoài chức năng này.

### `IInterviewService (WebClient)` / `InterviewService (WebClient)`
- File: `WebClient/Services/Interview/InterviewService.cs:11` / `:89` — Liệt kê: `SUBSET`
- Interface method: `+ PauseSessionAsync(...)`, `+ UnpauseSessionAsync(...)`
- Class thêm: `- JsonOptions: JsonSerializerOptions`, `- apiClient: IApiClient`,
  `- SendAsync<T>(...)`, `- ReadErrorMessageAsync(...)`
- *(SUBSET)* Đã bỏ ~23 method ngoài chức năng này.

### `InterviewSessionController (InterviewAPI)`
- File: `InterviewAPI/Controllers/InterviewSessionController.cs:15` — Liệt kê: `SUBSET`
- Field: `- _service: ISessionService`, `- _aiServiceClient: IAiServiceClient`, `- _paymentApiClient: IPaymentApiClient`
- Method: `+ Pause(...)`, `+ Unpause(...)`

### `SessionService (InterviewAPI)`
- File: `InterviewAPI/Services/SessionService.cs:45` — Liệt kê: `SUBSET`
- Field: 10 field DI (liệt kê đủ, `SessionService.cs:169-178`)
- Method: `+ PauseSessionAsync(...)`, `+ UnpauseSessionAsync(...)`, `- GetEffectivePausedSeconds(session)`
- *(SUBSET)* Đã bỏ ~50 method ngoài chức năng này.

### `SessionRepository (InterviewAPI)` / `ISessionRepository`
- Liệt kê: `SUBSET` — `+ GetByIdAsync(...)`, `+ SaveChangesAsync(...)`

**Class SHARED lần đầu xuất hiện trong toàn dự án** (đã tạo file trong `specs/_shared/classes/`):

- `Controller.yaml` — hộp rỗng, framework MVC WebClient.
- `DbContext.yaml` — hộp rỗng, framework EF Core.
- `IApiClient.yaml` — `+ HttpClient: HttpClient`.
- `ApiClient.yaml` — `+ ApiClient(httpClient: HttpClient)`, `+ HttpClient: HttpClient`.
- `InterviewDbContext.yaml` — 8 DbSet + 2 constructor (nội dung y hệt bản khai cục bộ trong
  `verifyfaceinsession_classes.yaml`, đã đối chiếu từng dòng).

## 9. QUAN HỆ CLASS

| Loại | From | To | Label |
|---|---|---|---|
| extends | InterviewController (WebClient) | Controller | — |
| use | InterviewController (WebClient) | IInterviewService (WebClient) | Use |
| implements | InterviewService (WebClient) | IInterviewService (WebClient) | — |
| use | InterviewService (WebClient) | IApiClient | Use |
| implements | ApiClient | IApiClient | — |
| use | InterviewService (WebClient) | PauseResponse (WebClient) | Use |
| use | InterviewService (WebClient) | UnpauseResponse (WebClient) | Use |
| use | InterviewService (WebClient) | ApiCallResult<T> | Use |
| use | InterviewService (WebClient) | ApiResponse<T> (WebClient) | Use |
| extends | InterviewSessionController (InterviewAPI) | ControllerBase | — |
| use | InterviewSessionController (InterviewAPI) | ISessionService | Use |
| use | InterviewSessionController (InterviewAPI) | ApiResponse<T> (InterviewAPI) | Use |
| implements | SessionService (InterviewAPI) | ISessionService | — |
| use | SessionService (InterviewAPI) | ISessionRepository | Use |
| use | SessionService (InterviewAPI) | PauseResponse (InterviewAPI) | Use |
| use | SessionService (InterviewAPI) | UnpauseResponse (InterviewAPI) | Use |
| implements | SessionRepository (InterviewAPI) | ISessionRepository | — |
| composition | SessionRepository (InterviewAPI) | InterviewDbContext | — |
| extends | InterviewDbContext | DbContext | — |
| composition | InterviewDbContext | InterviewSession | `1..*` |

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| KI-06 | SessionService.cs:1122-1177 | Pause/Unpause dùng read-then-write, không atomic → double-click có thể vượt quota | race condition | Vẽ luồng thường + note N1 | Có (đã tồn tại từ RECON) |
| KI-07 | SessionService.cs:99-100, interview-room.js:224-227 | Comment nói self-heal chạy "on the poll loop" nhưng không có poll loop | comment lệch code | Vẽ hành vi THẬT (không vẽ poll) + note N3 | Có (đã tồn tại từ RECON) |

> Không phát hiện anomaly MỚI khi khảo sát lô B cho chức năng này.


### Gỡ từ note trên diagram — anomaly

- **(cạnh message `2`)** PauseSessionAsync/UnpauseSessionAsync use a plain read-then-write (GetByIdAsync + SaveChangesAsync), NOT an atomic conditional UPDATE - two near-simultaneous Pause requests can both pass the quota guard (KI-06). The atomic ExecuteUpdateAsync in SessionRepository.TryAutoResumeExpiredPauseAsync belongs to the self-heal path (ResumeSessionAsync / GetActiveSessionAsync), which is a different flow and is not drawn here. *(gỡ từ note trên diagram)*
- **(cạnh message `6`)** WebClient never polls GET .../timer, so the server-side self-heal AutoResumeIfPauseExpiredAsync only fires when the user reloads the Room or the Dashboard (KI-07). The comment in SessionService.cs:99-100 that says it runs on the poll loop does not match the code. *(gỡ từ note trên diagram)*

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | 4 nhánh lỗi Pause (F2-F5) cùng HTTP 400 và cùng khuôn bong bóng `Could not pause: …` — gộp hay tách? | **Tách riêng, expand đủ 6 message mỗi nhánh** | Điều kiện gộp yêu cầu "cùng nội dung user thấy"; 4 exception message khác nhau nên chữ trong bong bóng khác nhau. Đúng tiền lệ lô A (`verifyfaceinsession.yaml` tách từng `opt` theo từng message exception). |
| 2 | Pause và Resume là 2 thao tác riêng, không phải if/else | **Vẽ tuần tự trên 1 trang: pha 1 Pause (msg 1-26) rồi pha 2 Resume (msg 27-51)** | Không dùng `alt` vì code không có rẽ nhánh; thứ tự này đúng thực tế (phải pause mới resume được). |
| 3 | Resume có 2 nguồn trigger (nút Resume, tick quota về 0) | **Một message 27 duy nhất, nhãn ghi cả 2 nguồn** | Cùng gọi `resumeInterview()`, downstream giống hệt — vẽ 2 lần sẽ nhân đôi 25 message mà không thêm thông tin. |
| 4 | Có vẽ `VietnamTime` (helper tĩnh) vào class diagram không? | **Không** | Giữ nhất quán với lô A: `verifyfaceinsession_classes.yaml` cũng dùng `VietnamTime.Now` mà không vẽ class này. |
| 5 | `AutoResumeIfPauseExpiredAsync` (self-heal) | **Không vẽ, đưa vào §1** | Không nằm trong stack call của Pause/Unpause — thuộc `ResumeSessionAsync`/`GetActiveSessionAsync`. |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `F1.2`)** startPauseQuotaTick is a display-only countdown. The server stays the source of truth and overwrites pausedSecondsRemaining on the next Resume response. When the local counter reaches 0 it calls resumeInterview() itself - the same call as the Resume button (message 27). *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| *(không có)* | — | — | — |
