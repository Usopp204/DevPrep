# FLOW — `AcceptIntegrityPolicy` · BL#63, 64 · UC-`chưa có`

> Đặt tại `docs/diagrams/FLOWS/AcceptIntegrityPolicy.md`
> Trạng thái: `DRAFT` — Khảo sát: lô B, 2026-08-03
>
> ⚠️ **UC-id còn trống.** Bảng use-case của nhóm CHƯA có ê-líp cho chức năng này
> (`DIAGRAM_SCOPE.md` §3.1) — cần nhóm bổ sung trước khi chốt `uc_id`. Spec YAML tạm để `uc_id: ""`.

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Bắt ứng viên đọc và chấp nhận Quy tắc liêm chính phỏng vấn trước khi vào phòng; ghi cờ `IntegrityPolicyAccepted` vào DB bằng UPDATE có điều kiện (atomic) |
| Điểm vào (user thao tác gì) | Bị guard của trang Room chuyển hướng sang `GET /Interview/IntegrityPolicy?id=…`; tick ô "I have read and understand the interview rules" rồi bấm **Enter Interview** |
| Kết thúc (user thấy gì) | Chuyển thẳng vào phòng phỏng vấn (`/Interview/Room/{id}`); nếu lỗi → về Dashboard kèm `TempData["ErrorMessage"]` |
| Quy mô | `9` participant · `63` message · `0` alt / `5` opt · trang Sequence 1800×2815 |
| Đối chiếu ước lượng RECON | RECON đoán `14` · thật `63` · hệ số `4.5×` |
| Service ngoài quyền sở hữu | không |
| **Gọi BYPASS Gateway?** | không |
| Nhà cung cấp AI | không |
| Ghi DB | `InterviewSessions` — GET chỉ ĐỌC `IntegrityPolicyAccepted`/`IntegrityPolicyAcceptedAt`; POST atomic `ExecuteUpdateAsync` 3 cột (`IntegrityPolicyAccepted`, `IntegrityPolicyAcceptedAt`, `UpdatedAt`) |
| Trừ credit | không (credit bị trừ ở `GenerateQuestionAsync` khi thật sự vào phòng — thuộc #6) |
| KI liên quan | `KI-03` (response của request thua race dựng từ bản đọc cũ) |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| Toàn bộ guard phía trước trong action `Room` (status guard, entry-gate guard) | [InterviewController.cs:193-244](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L193-L244) | Thuộc `EnterInterviewRoom` / `#14 ValidateEntryGateStatus`. Chỉ **cú redirect** sang trang policy được vẽ (message 1). |
| Phần còn lại của action `Room` sau khi policy đã accept (`GenerateQuestionAsync`, trừ credit) | [InterviewController.cs:259+](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L259) | Thuộc `EnterInterviewRoom`. |
| `RequireIntegrityPolicyAccepted` (guard tầng 2 phía InterviewAPI) | [SessionService.cs:51-56](../../../source/InterviewAPI/Services/SessionService.cs#L51-L56) | Chạy ở các endpoint phục vụ câu hỏi (`GenerateQuestion`, `SendMessage`, `Resume`, `Hint`), KHÔNG nằm trong 2 endpoint của chức năng này. |
| CSS/animation của trang policy (orbs, glass card) | IntegrityPolicy.cshtml:21-24 | Trình bày, không phải luồng. |
| `SessionOwnsAsync` (`SessionService.cs:1629-1633`) | [SessionService.cs:1629-1633](../../../source/InterviewAPI/Services/SessionService.cs#L1629-L1633) | Helper của endpoint khác, không được 2 endpoint này gọi. |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Guard integrity-policy trong action `Room` → redirect sang trang policy | `EnterInterviewRoom` | message 1 `Navigate to the Room; its integrity-policy guard redirects to /Interview/IntegrityPolicy?id={sessionId}` | InterviewController.cs:247-257 |
| Mở phòng phỏng vấn sau khi accept | `EnterInterviewRoom` | message 42 `The interview Room opens (the Room guard now passes)` | InterviewController.cs:186, 254-257 |

> Cả 2 boundary đều là **1 message duy nhất** (không mở rộng bên trong) — đây là lý do trang này
> giữ được 63 message thay vì nuốt luôn #6.

### Gỡ từ note trên diagram — boundary

- **(cạnh message `E1.1`)** The same GetIntegrityPolicyStatusAsync call is made from TWO places: the Room action's guard (EnterInterviewRoom, which is what redirects here) and this IntegrityPolicy action. Only the second one is drawn - the first is the boundary that produces message 1. *(gỡ từ note trên diagram)*


---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 0 | User | actor | — | — |
| 1 | :Integrity Policy UI | box | `WebClient/Views/Interview/IntegrityPolicy.cshtml` (trang + script inline) | — |
| 2 | :InterviewController (WebClient) | box | `WebClient/Controllers/Interview/InterviewController.cs` | — |
| 3 | :InterviewService (WebClient) | box | `WebClient/Services/Interview/InterviewService.cs` | — |
| 4 | :API Gateway | box | `ApiGateway` (YARP :7000) | — |
| 5 | :InterviewSessionController (InterviewAPI) | box | `InterviewAPI/Controllers/InterviewSessionController.cs` | có |
| 6 | :SessionService (InterviewAPI) | box | `InterviewAPI/Services/SessionService.cs` | có |
| 7 | :SessionRepository (InterviewAPI) | box | `InterviewAPI/Repositories/SessionRepository.cs` | có |
| 8 | SQL Server | cylinder | `DevPrep_Interview` | — |

> Actor trực tiếp — user tự tick checkbox và bấm nút.

## 4. BẢNG MESSAGE

### Pha 1 — GET trạng thái + hiển thị trang policy

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Integrity Policy UI | call | `1. Redirected to /Interview/IntegrityPolicy [see #4]` | — | InterviewController.cs:247-257 | boundary | 1. Navigate to the Room; its integrity-policy guard redirects to /Interview/IntegrityPolicy?id={sessionId} (guard belongs to EnterInterviewRoom) |
| 2 | :Integrity Policy UI | :InterviewController (WebClient) | call | `2. GET /Interview/IntegrityPolicy?id={sessionId}` | — | InterviewController.cs:152-153 |  | — |
| 3 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `3. GetIntegrityPolicyStatusAsync(id, ct)` | — | InterviewController.cs:155 |  | — |
| 4 | :InterviewService (WebClient) | :API Gateway | call | `4. GET api/interview/sessions/{sessionId}/integrity-policy` | — | InterviewService.cs:556-559 |  | — |
| 5 | :API Gateway | :InterviewSessionController | call | `5. Forward (route interview-route, no prefix strip)` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 6 | :InterviewSessionController | (self) | call | `6. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | InterviewSessionController.cs:200 | self | — |
| 7 | :InterviewSessionController | :SessionService | call | `7. GetIntegrityPolicyStatusAsync(userId, sessionId, ct)` | — | InterviewSessionController.cs:201 |  | — |
| 8 | :SessionService | :SessionRepository | call | `8. GetByIdAsync(sessionId, ct)` | — | SessionService.cs:1637 |  | — |
| 9 | :SessionRepository | SQL Server | call | `9. SELECT InterviewSession WHERE SessionId AND !IsDeleted` | — | SessionRepository.cs:21-24 |  | — |
| 10 | SQL Server | :SessionRepository | ret | `10. Return session row / null` | — | SessionRepository.cs:21-24 |  | — |
| 11 | :SessionRepository | :SessionService | ret | `11. Return InterviewSession?` | — | SessionRepository.cs:21-24 |  | — |
| E1.1 | :SessionService | :InterviewSessionController | ret | `E1.1. throw KeyNotFoundException("Session not found.")` | E1 | SessionService.cs:1638-1639 | throw | — |
| E1.2 | :InterviewSessionController | :API Gateway | ret | `E1.2. Return 404 ApiResponse.Fail(ex.Message)` | E1 | InterviewSessionController.cs:204-207 |  | — |
| E1.3 | :API Gateway | :InterviewService (WebClient) | ret | `E1.3. Return 404` | E1 | ApiGateway/appsettings.json:49-54 |  | — |
| E1.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `E1.4. Return ApiCallResult.Fail("Session not found.", 404)` | E1 | InterviewService.cs:636-639 |  | — |
| E1.5 | :InterviewController (WebClient) | :Integrity Policy UI | ret | `E1.5. 302 RedirectToAction(Index)` | E1 | InterviewController.cs:156-160 |  | E1.5. TempData["ErrorMessage"] = error; 302 RedirectToAction(Index) |
| E1.6 | :Integrity Policy UI | User | ret | `E1.6. Interview dashboard with the error message` | E1 | InterviewController.cs:159 |  | — |
| 12 | :SessionService | :InterviewSessionController | ret | `12. Return IntegrityPolicyStatusResponse{Accepted, AcceptedAt}` | — | SessionService.cs:1641-1645 |  | — |
| 13 | :InterviewSessionController | :API Gateway | ret | `13. Return 200 ApiResponse.Ok(result)` | — | InterviewSessionController.cs:202 |  | — |
| 14 | :API Gateway | :InterviewService (WebClient) | ret | `14. Return 200` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 15 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `15. Return ApiCallResult.Ok(IntegrityPolicyStatusResponse, 200)` | — | InterviewService.cs:662, 556-559 |  | — |
| E2.1 | :InterviewController (WebClient) | :Integrity Policy UI | ret | `E2.1. 302 RedirectToAction(Room, { id })` | E2 | InterviewController.cs:162-165 |  | — |
| E2.2 | :Integrity Policy UI | User | ret | `E2.2. Interview Room [see #4]` | E2 | InterviewController.cs:164 | boundary | E2.2. The interview Room opens directly - the policy page is skipped |
| 16 | :InterviewController (WebClient) | :Integrity Policy UI | ret | `16. Return 200 View IntegrityPolicy` | — | InterviewController.cs:167-172 |  | 16. Return 200 View IntegrityPolicy + no-store headers (Cache-Control / Pragma / Expires) |
| 17 | :Integrity Policy UI | User | ret | `17. Show Integrity Policy page` | — | IntegrityPolicy.cshtml:34-108 |  | 17. Policy page: 5 rule cards, "max 3 violations" warning, AI-monitoring panel; Enter Interview button disabled |

### Pha 2 — POST chấp nhận

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 18 | User | :Integrity Policy UI | call | `18. Tick "I have read and understand the interview rules"` | — | IntegrityPolicy.cshtml:99-102 |  | — |
| 19 | :Integrity Policy UI | (self) | call | `19. enterBtn.disabled = !checkbox.checked` | — | IntegrityPolicy.cshtml:120-122 | self | — |
| 20 | :Integrity Policy UI | User | ret | `20. Enter Interview button enabled` | — | IntegrityPolicy.cshtml:121 |  | — |
| 21 | User | :Integrity Policy UI | call | `21. Click Enter Interview (form submit)` | — | IntegrityPolicy.cshtml:106-109 |  | — |
| 22 | :Integrity Policy UI | (self) | call | `22. enterBtn.disabled = true` | — | IntegrityPolicy.cshtml:124-127 | self | 22. enterBtn.disabled = true; enterBtn.textContent = "Entering interview..." |
| 23 | :Integrity Policy UI | :InterviewController (WebClient) | call | `23. POST /Interview/AcceptIntegrityPolicy?id={id}` | — | IntegrityPolicy.cshtml:106-107, InterviewController.cs:175-177 |  | 23. POST /Interview/AcceptIntegrityPolicy?id={sessionId} (antiforgery token) |
| 24 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `24. AcceptIntegrityPolicyAsync(id, ct)` | — | InterviewController.cs:179 |  | — |
| 25 | :InterviewService (WebClient) | :API Gateway | call | `25. POST api/interview/sessions/{id}/integrity-policy/accept` | — | InterviewService.cs:561-564 |  | 25. POST api/interview/sessions/{sessionId}/integrity-policy/accept |
| 26 | :API Gateway | :InterviewSessionController | call | `26. Forward (route interview-route, no prefix strip)` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 27 | :InterviewSessionController | (self) | call | `27. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | InterviewSessionController.cs:215 | self | — |
| 28 | :InterviewSessionController | :SessionService | call | `28. AcceptIntegrityPolicyAsync(userId, sessionId, ct)` | — | InterviewSessionController.cs:216 |  | — |
| 29 | :SessionService | :SessionRepository | call | `29. GetByIdAsync(sessionId, ct)` | — | SessionService.cs:1650 |  | — |
| 30 | :SessionRepository | SQL Server | call | `30. SELECT InterviewSession WHERE SessionId AND !IsDeleted` | — | SessionRepository.cs:21-24 |  | — |
| 31 | SQL Server | :SessionRepository | ret | `31. Return session row / null` | — | SessionRepository.cs:21-24 |  | — |
| 32 | :SessionRepository | :SessionService | ret | `32. Return InterviewSession?` | — | SessionRepository.cs:21-24 |  | — |
| F1.1 | :SessionService | :InterviewSessionController | ret | `F1.1. throw KeyNotFoundException("Session not found.")` | F1 | SessionService.cs:1651-1652 | throw | — |
| F1.2 | :InterviewSessionController | :API Gateway | ret | `F1.2. Return 404 ApiResponse.Fail(ex.Message)` | F1 | InterviewSessionController.cs:219-222 |  | — |
| F1.3 | :API Gateway | :InterviewService (WebClient) | ret | `F1.3. Return 404` | F1 | ApiGateway/appsettings.json:49-54 |  | — |
| F1.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `F1.4. Return ApiCallResult.Fail("Session not found.", 404)` | F1 | InterviewService.cs:636-639 |  | — |
| F1.5 | :InterviewController (WebClient) | :Integrity Policy UI | ret | `F1.5. 302 RedirectToAction(Index)` | F1 | InterviewController.cs:180-184 |  | F1.5. TempData["ErrorMessage"] = error; 302 RedirectToAction(Index) |
| F1.6 | :Integrity Policy UI | User | ret | `F1.6. Interview dashboard with the error message` | F1 | InterviewController.cs:183 |  | — |
| F2.1 | :SessionService | :InterviewSessionController | ret | `F2.1. throw InvalidOperationException` | F2 | SessionService.cs:1654-1655 | throw | F2.1. throw InvalidOperationException("Session cannot accept policy at this status.") |
| F2.2 | :InterviewSessionController | :API Gateway | ret | `F2.2. Return 400 ApiResponse.Fail(ex.Message)` | F2 | InterviewSessionController.cs:223-226 |  | — |
| F2.3 | :API Gateway | :InterviewService (WebClient) | ret | `F2.3. Return 400` | F2 | ApiGateway/appsettings.json:49-54 |  | — |
| F2.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `F2.4. Return ApiCallResult.Fail(message, 400)` | F2 | InterviewService.cs:636-639 |  | — |
| F2.5 | :InterviewController (WebClient) | :Integrity Policy UI | ret | `F2.5. 302 RedirectToAction(Index)` | F2 | InterviewController.cs:180-184 |  | F2.5. TempData["ErrorMessage"] = error; 302 RedirectToAction(Index) |
| F2.6 | :Integrity Policy UI | User | ret | `F2.6. Interview dashboard with the error message` | F2 | InterviewController.cs:183 |  | — |
| 33 | :SessionService | :SessionRepository | call | `33. AcceptIntegrityPolicyIfAsync(sessionId, ct)` | — | SessionService.cs:1657 |  | — |
| 34 | :SessionRepository | SQL Server | call | `34. UPDATE InterviewSessions SET IntegrityPolicyAccepted` | — | SessionRepository.cs:234-243 |  | 34. UPDATE InterviewSessions SET IntegrityPolicyAccepted=1, IntegrityPolicyAcceptedAt=now, UpdatedAt=now WHERE SessionId AND !IntegrityPolicyAccepted AND !IsDeleted (atomic ExecuteUpdateAsync) |
| 35 | SQL Server | :SessionRepository | ret | `35. Return rows affected` | — | SessionRepository.cs:240 |  | — |
| 36 | :SessionRepository | :SessionService | ret | `36. Return updated = rowsAffected > 0` | — | SessionRepository.cs:242 |  | — |
| G.1 | :SessionService | (self) | call | `G.1. session.IntegrityPolicyAccepted = true` | G | SessionService.cs:1658-1662 | self | G.1. session.IntegrityPolicyAccepted = true; session.IntegrityPolicyAcceptedAt = VietnamTime.Now |
| 37 | :SessionService | :InterviewSessionController | ret | `37. Return IntegrityPolicyStatusResponse{Accepted, AcceptedAt}` | — | SessionService.cs:1664-1668 |  | — |
| 38 | :InterviewSessionController | :API Gateway | ret | `38. Return 200 ApiResponse.Ok(result)` | — | InterviewSessionController.cs:217 |  | — |
| 39 | :API Gateway | :InterviewService (WebClient) | ret | `39. Return 200` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 40 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `40. Return ApiCallResult.Ok(IntegrityPolicyStatusResponse, 200)` | — | InterviewService.cs:662, 561-564 |  | — |
| 41 | :InterviewController (WebClient) | :Integrity Policy UI | ret | `41. 302 RedirectToAction(Room, { id })` | — | InterviewController.cs:186 |  | — |
| 42 | :Integrity Policy UI | User | ret | `42. Interview Room [see #4]` | — | InterviewController.cs:254-257 | boundary | 42. The interview Room opens - the Room guard now passes because IntegrityPolicyAccepted is true in the database |

**Cờ:** `self` · `throw` · `boundary`

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| E1 | opt | `[session is null OR session.UserId != userId]` | E1.1 → E1.6 | — | — |
| E2 | opt | `[result.Data.Accepted == true - the policy was already accepted]` | E2.1 → E2.2 | — | — |
| F1 | opt | `[session is null OR session.UserId != userId]` | F1.1 → F1.6 | — | — |
| F2 | opt | `[session.Status != "Initialized"]` | F2.1 → F2.6 | — | — |
| G | opt | `[updated == true - this request won the atomic UPDATE]` | G.1 | — | — |

> Không có khung `alt` nào: mọi rẽ nhánh trong code đều là `if` không `else`
> (`SessionService.cs:1638-1662`, `InterviewController.cs:156-165, 180-184`).
> `G` là `opt` **tự khép kín** (không mở/đóng call nào) → luồng vẫn chạy tiếp vào message 37.

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | 33-36 | `AcceptIntegrityPolicyIfAsync is a single atomic conditional UPDATE (WHERE !IntegrityPolicyAccepted), so two near-simultaneous submits can never both write - exactly one affects a row.` |
| N2 | G.1 | `KI-03: when the atomic UPDATE loses the race (updated == false) there is no write-back, and the response is built from the session object read BEFORE the update - so the losing request can still report Accepted=false / AcceptedAt=null even though the database already says true. Drawn as the normal flow (race condition, per the diagram rules); the user never notices because InterviewController redirects to the Room regardless of the payload.` |
| N3 | 1 | `The same GetIntegrityPolicyStatusAsync call is made from TWO places: the Room action's guard (EnterInterviewRoom, which is what redirects here) and this IntegrityPolicy action. Only the second one is drawn - the first is the boundary that produces message 1.` |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | File:Line | Liệt kê | Nguồn |
|---|---|---|---|---|---|
| 1 | InterviewController (WebClient) | class | WebClient/Controllers/Interview/InterviewController.cs:17, 152-187 | SUBSET | local |
| 2 | Controller | framework-base | ASP.NET Core MVC | FULL (rỗng) | SHARED |
| 3 | IInterviewService (WebClient) | interface | WebClient/Services/Interview/InterviewService.cs:78-86 | SUBSET | local |
| 4 | InterviewService (WebClient) | class | WebClient/Services/Interview/InterviewService.cs:89, 556-564, 621-700 | SUBSET | local |
| 5 | IApiClient | interface | WebClient/Services/Shared/IApiClient.cs:3-6 | FULL | SHARED |
| 6 | ApiClient | class | WebClient/Services/Shared/ApiClient.cs:3-6 | FULL | SHARED |
| 7 | IntegrityPolicyStatusResponse (WebClient) | DTO | WebClient/Models/Interview/IntegrityPolicyStatusResponse.cs:3-7 | FULL | local |
| 8 | ApiCallResult<T> | DTO | WebClient/Models/Shared/ApiCallResult.cs:3-23 | FULL | local |
| 9 | ApiResponse<T> (WebClient) | DTO | WebClient/Models/Shared/ApiResponse.cs:5-12 | FULL | local |
| 10 | ControllerBase | framework-base | ASP.NET Core | FULL (rỗng) | SHARED |
| 11 | InterviewSessionController (InterviewAPI) | class | InterviewAPI/Controllers/InterviewSessionController.cs:15, 195-227 | SUBSET | local |
| 12 | ISessionService | interface | InterviewAPI/Services/Interfaces/ISessionService.cs:55-56 | SUBSET | local |
| 13 | SessionService (InterviewAPI) | class | InterviewAPI/Services/SessionService.cs:45, 1635-1669 | SUBSET | local |
| 14 | ApiResponse<T> (InterviewAPI) | DTO | InterviewAPI/DTOs/ApiResponse.cs | FULL | SHARED |
| 15 | IntegrityPolicyStatusResponse (InterviewAPI) | DTO | InterviewAPI/DTOs/Session/IntegrityPolicyStatusResponse.cs:3-7 | FULL | local |
| 16 | ISessionRepository | interface | InterviewAPI/Repositories/Interfaces/ISessionRepository.cs | SUBSET | local |
| 17 | SessionRepository (InterviewAPI) | class | InterviewAPI/Repositories/SessionRepository.cs:21-24, 234-243 | SUBSET | local |
| 18 | InterviewSession | entity | InterviewAPI/Models/InterviewSession.cs | FULL | SHARED |
| 19 | InterviewDbContext | class | InterviewAPI/Models/InterviewDbContext.cs + .Biometrics.cs | FULL | SHARED |
| 20 | DbContext | framework-base | EF Core | FULL (rỗng) | SHARED |

## 8. CLASS — chi tiết

### `InterviewController (WebClient)` — `SUBSET`
- Field `- interviewService: IInterviewService`
- Method `+ IntegrityPolicy(id: Guid, cancellationToken): Task<IActionResult>`,
  `+ AcceptIntegrityPolicy(id: Guid, cancellationToken): Task<IActionResult>`

### `IInterviewService` / `InterviewService (WebClient)` — `SUBSET`
- `+ GetIntegrityPolicyStatusAsync(...)`, `+ AcceptIntegrityPolicyAsync(...)`; class thêm
  `- SendAsync<T>(...)`, `- ReadErrorMessageAsync(...)`, field `- JsonOptions`, `- apiClient`.

### `InterviewSessionController (InterviewAPI)` — `SUBSET`
- 3 field DI; method `+ GetIntegrityPolicyStatus(...)`, `+ AcceptIntegrityPolicy(...)`

### `SessionService (InterviewAPI)` — `SUBSET`
- 10 field DI; method `+ GetIntegrityPolicyStatusAsync(...)`, `+ AcceptIntegrityPolicyAsync(...)`

### `ISessionRepository` / `SessionRepository (InterviewAPI)` — `SUBSET`
- `+ GetByIdAsync(...)`, `+ AcceptIntegrityPolicyIfAsync(...)`

Không có class SHARED mới trong chức năng này.

## 9. QUAN HỆ CLASS

| Loại | From | To | Label |
|---|---|---|---|
| extends | InterviewController (WebClient) | Controller | — |
| use | InterviewController (WebClient) | IInterviewService (WebClient) | Use |
| implements | InterviewService (WebClient) | IInterviewService (WebClient) | — |
| use | InterviewService (WebClient) | IApiClient | Use |
| implements | ApiClient | IApiClient | — |
| use | InterviewService (WebClient) | IntegrityPolicyStatusResponse (WebClient) | Use |
| use | InterviewService (WebClient) | ApiCallResult<T> | Use |
| use | InterviewService (WebClient) | ApiResponse<T> (WebClient) | Use |
| extends | InterviewSessionController (InterviewAPI) | ControllerBase | — |
| use | InterviewSessionController (InterviewAPI) | ISessionService | Use |
| use | InterviewSessionController (InterviewAPI) | ApiResponse<T> (InterviewAPI) | Use |
| implements | SessionService (InterviewAPI) | ISessionService | — |
| use | SessionService (InterviewAPI) | ISessionRepository | Use |
| use | SessionService (InterviewAPI) | IntegrityPolicyStatusResponse (InterviewAPI) | Use |
| implements | SessionRepository (InterviewAPI) | ISessionRepository | — |
| composition | SessionRepository (InterviewAPI) | InterviewDbContext | — |
| extends | InterviewDbContext | DbContext | — |
| composition | InterviewDbContext | InterviewSession | `1..*` |

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| KI-03 | SessionService.cs:1657-1668 | Response của request thua atomic update dựng từ đối tượng `session` đọc TRƯỚC update → có thể báo `Accepted=false` dù DB đã `true` | race condition | Vẽ luồng thường (`opt G` đúng như `if (updated)` trong code) + note N2 | Có (đã tồn tại từ RECON) |

> Không phát hiện anomaly MỚI. Đã kiểm thêm: `GET` trả `Cache-Control: no-store` nên nút Back không
> lấy lại trang policy từ bfcache ([InterviewController.cs:167-169](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L167-L169)) — đúng, không phải lỗi.


### Gỡ từ note trên diagram — anomaly

- **(cạnh message `7`)** KI-03: when the atomic UPDATE loses the race (updated == false) there is no write-back, and the response is built from the session object read BEFORE the update - so the losing request can still report Accepted=false / AcceptedAt=null even though the database already says true. Drawn as the normal flow (race condition, per the diagram rules); the user never notices because InterviewController redirects to the Room regardless of the payload. *(gỡ từ note trên diagram)*

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | `GetIntegrityPolicyStatusAsync` được gọi từ 2 nơi (guard trong `Room` và action `IntegrityPolicy`) | **Chỉ vẽ lời gọi trong action `IntegrityPolicy`; lời gọi trong guard `Room` thu về đúng 1 message boundary (message 1)** | Đúng quy tắc BOUNDARY §2 — nếu mở rộng guard `Room` thì trang này sẽ nuốt luôn EnterInterviewRoom. |
| 2 | GET và POST là 2 request riêng | **Vẽ tuần tự trên 1 trang (pha 1 rồi pha 2)** | Đúng thực tế: phải xem trang mới bấm được nút. Không dùng `alt` vì không loại trừ nhau. |
| 3 | Nhánh `if (updated)` — vẽ `alt` (thắng/thua race) hay `opt`? | **`opt` với guard `[updated == true]`**, kèm note KI-03 mô tả nhánh thua | Code là `if` không `else`; và quy tắc §10 nói **race condition → vẽ luồng thường**. |
| 4 | 2 nhánh lỗi POST (F1 404, F2 400) | **Tách riêng** | Khác HTTP status (404 vs 400) → không đủ điều kiện gộp. |
| 5 | `uc_id` | **Để trống `""`** | Nhóm chưa có ê-líp use-case (DIAGRAM_SCOPE §3.1). Tên trang sẽ là `Sequence - Accept Integrity Policy [BL#63,#64]`, điền UC sau. |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `4`)** AcceptIntegrityPolicyIfAsync is a single atomic conditional UPDATE (WHERE !IntegrityPolicyAccepted), so two near-simultaneous submits can never both write - exactly one affects a row. *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| `uc_id` của chức năng | Bảng use-case của nhóm chưa có ê-líp cho AcceptIntegrityPolicy | Nhóm bổ sung ê-líp vào UC diagram rồi cập nhật `uc_id` trong `specs/acceptintegritypolicy.yaml` | luồng 1 (đã biết từ DIAGRAM_SCOPE §3.1) |
