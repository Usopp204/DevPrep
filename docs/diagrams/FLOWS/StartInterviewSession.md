# FLOW — `StartInterviewSession` · BL#55, 45, 46 · UC-`chưa có`

> Đặt tại `docs/diagrams/FLOWS/StartInterviewSession.md`
> Trạng thái: `DRAFT` — Khảo sát: lô G, 2026-08-03
>
> ⚠️ **File này là NỬA ĐẦU của card `#4` trong RECON.** Card gốc gộp cả `POST .../init` lẫn toàn
> bộ luồng vào phòng phỏng vấn → đo thật ~175 message / ~6560px, vượt cả 2 ngưỡng (150 message,
> 5500px). Theo quyết định của DuyenTQ (lô G), card `#4` được tách làm 2 file:
> **`StartInterviewSession`** (file này — `POST .../init`) và
> **`EnterInterviewRoom`** (vào phòng: 3 guard + `questions/generate` + trừ/hoàn credit + room tour,
> BL#54, 38).

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Khởi tạo phiên đã cấu hình: gọi ai-service lập **toàn bộ** kế hoạch câu hỏi bằng RAG trên ChromaDB, lưu `SessionQuestions`, chuyển `Configured → Initialized`, mở `InterviewRoom` |
| Điểm vào (user thao tác gì) | Không có thao tác mới — đây là lệnh thứ 4 (và cuối) trong cùng `StartInterviewAsync` mà `#3 ConfigureInterviewSession` đã bắt đầu khi user bấm **Start Interview** |
| Kết thúc (user thấy gì) | Chuyển sang màn hình xác minh khuôn mặt của phiên mới (`#13 VerifyFaceEntryGate`). Lỗi → quay lại Dashboard kèm banner lỗi, form giữ nguyên dữ liệu |
| Quy mô | `12` participant · `58` message · `31` class · `0` alt / `3` opt · trang Sequence 2580×2459 · trang Class 4620×2048 |
| Đối chiếu ước lượng RECON | RECON đoán `110-130` cho **cả card #4** · nửa đầu này `58` (nửa sau `EnterInterviewRoom` = `118`) |
| Service ngoài quyền sở hữu | ApiGateway (định tuyến) |
| **Gọi BYPASS Gateway?** | **không** ở file này (trừ/hoàn credit nằm ở `EnterInterviewRoom`) |
| Nhà cung cấp AI | **ChromaDB (RAG retrieval), KHÔNG gọi Gemini ở bước lập kế hoạch** — `build_question_pool` chỉ lọc metadata (`chroma_store.py:219-244`), không có semantic search, không có LLM |
| Ghi DB | `SessionQuestions` INSERT hàng loạt · `InterviewSessions` UPDATE `Status="Initialized"`/`TotalQuestions`/`UpdatedAt` · `InterviewRooms` INSERT (`Status="Active"`) |
| Trừ credit | **không** — credit bị trừ ở `EnterInterviewRoom` (lúc phục vụ câu hỏi đầu tiên), sau khi qua Verify + Integrity Policy |
| KI liên quan | không (không phát hiện KI mới) |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| 3 lệnh PUT `config/duration` · `config/level` · `config/interaction` | [InterviewService.cs:124-181](../../../source/WebClient/Services/Interview/InterviewService.cs#L124-L181) | Thuộc `#3 ConfigureInterviewSession`, đã vẽ đầy đủ ở đó. Ở đây chỉ vẽ 3 message boundary dẫn nhập (1-3). |
| Cổng sinh trắc ở đầu action `Start` (`biometricsService.GetStatusAsync`) | [InterviewController.cs:61-67](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L61-L67) | Đã vẽ ở `#3` (message 19-20 của file đó) và chi tiết ở `#1 EnrollFaceBiometric`. |
| Toàn bộ luồng vào phòng (`Room` action, `questions/generate`, trừ/hoàn credit, room tour) | InterviewController.cs:193-287, SessionService.cs:555-697 | Đã tách sang **`EnterInterviewRoom`** — xem cảnh báo đầu file. |
| Bước "Step B: TODO deduct credit" (code bị comment) | [SessionService.cs:453-455](../../../source/InterviewAPI/Services/SessionService.cs#L453-L455) | Code đã comment, không chạy. Việc trừ credit thật nằm ở `GenerateQuestionAsync` (`EnterInterviewRoom`). |
| Chi tiết `_weave` / `_plan_quotas` / `target_questions` | [interviewer.py:366-420](../../../ai-service/app/services/interviewer.py#L366-L420) | Thuật toán trộn 3 track — gộp 1 câu vào nhãn message 19. |
| Nhánh lỗi hạ tầng của `AiServiceClient.PlanQuestionsAsync` (ai-service unreachable / non-2xx) | [AiServiceClient.cs:20-35](../../../source/InterviewAPI/Clients/AiServiceClient.cs#L20-L35) | Tiền lệ lô A/C/D/F: ném ra ngoài, rơi vào `catch (Exception)` chung của controller → kết cục trùng khít `opt C` đã vẽ. |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| 3 lệnh config đi trước trong cùng `StartInterviewAsync` | `#3 ConfigureInterviewSession` | message 1-3 (nhãn ghi rõ "đã chạy xong ở #3") | InterviewService.cs:120-181 |
| Chuyển sang màn hình Verify của phiên mới | `#13 VerifyFaceEntryGate` | message 40 | InterviewController.cs:96-99 |
| Bước kế tiếp sau Verify | `EnterInterviewRoom` | ghi trong nhãn message 40 | InterviewController.cs:193 |

### Gỡ từ note trên diagram — boundary

- **(cạnh message `A.5`)** This is the SECOND biometric gate on the same click: WebClient already checked FaceEnrolled in the Start action (drawn in #3 ConfigureInterviewSession). This one is server-side so a direct API call cannot skip it. *(gỡ từ note trên diagram)*


---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 0 | User | actor | — | — |
| 1 | :Interview Dashboard UI | box | `Views/Interview/Index.cshtml` | — |
| 2 | :InterviewController (WebClient) | box | `WebClient/Controllers/Interview/InterviewController.cs:54-100` | — |
| 3 | :InterviewService (WebClient) | box | `WebClient/Services/Interview/InterviewService.cs:120-194` | — |
| 4 | :API Gateway | box | `ApiGateway` (YARP :7000) | — |
| 5 | :InterviewSessionController (InterviewAPI) | box | `InterviewAPI/Controllers/InterviewSessionController.cs:586-600` | có |
| 6 | :SessionService (InterviewAPI) | box | `InterviewAPI/Services/SessionService.cs:432-553` | có |
| 7 | :ProfileBiometricsRepository (InterviewAPI) | box | `InterviewAPI/Repositories/ProfileBiometricsRepository.cs:16-19` | có |
| 8 | :AiServiceClient (InterviewAPI) | box | `InterviewAPI/Clients/AiServiceClient.cs:20-35` | có |
| 9 | :ai-service /internal/questions/plan | box | `ai-service/app/api/internal.py:49-63` → `interviewer.py:366-420` → `chroma_store.py:219-244` | có |
| 10 | :SessionRepository (InterviewAPI) | box | `InterviewAPI/Repositories/SessionRepository.cs:21-66` | có |
| 11 | SQL Server | cylinder | `DevPrep_Interview` | — |

## 4. BẢNG MESSAGE

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Interview Dashboard UI | call | `1. Click Start Interview [see #3]` | — | Index.cshtml:43-48 | boundary | 1. Click Start Interview - the three config PUTs of #3 ConfigureInterviewSession have already succeeded and returned the new sessionId |
| 2 | :Interview Dashboard UI | :InterviewController (WebClient) | call | `2. POST /Interview/Start` | — | InterviewController.cs:54-56 | boundary | 2. POST /Interview/Start (form + antiforgery token) - the same request that ran #3 |
| 3 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `3. StartInterviewAsync(...)` | — | InterviewController.cs:69-71, InterviewService.cs:183 | boundary | 3. StartInterviewAsync(...) continues: duration / level / interaction are configured, now the init call |
| 4 | :InterviewService (WebClient) | :API Gateway | call | `4. POST api/interview/sessions/init` | — | InterviewService.cs:183-186 |  | 4. POST api/interview/sessions/init { SessionId, Position, Jd, FocusAreas } |
| 5 | :API Gateway | :InterviewSessionController | call | `5. Forward (route interview-route, no prefix strip)` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 6 | :InterviewSessionController | (self) | call | `6. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | InterviewSessionController.cs:592 | self | — |
| 7 | :InterviewSessionController | :SessionService | call | `7. InitAsync(userId, req, ct)` | — | InterviewSessionController.cs:593 |  | — |
| 8 | :SessionService | :SessionRepository | call | `8. GetByIdAsync(req.SessionId, ct) - Step A` | — | SessionService.cs:435 |  | — |
| 9 | :SessionRepository | SQL Server | call | `9. SELECT InterviewSession WHERE SessionId AND !IsDeleted` | — | SessionRepository.cs:21-24 |  | — |
| 10 | SQL Server | :SessionRepository | ret | `10. Return row / null` | — | SessionRepository.cs:21-24 |  | — |
| 11 | :SessionRepository | :SessionService | ret | `11. Return InterviewSession?` | — | SessionRepository.cs:21-24 |  | — |
| A.1 | :SessionService | :InterviewSessionController | ret | `A.1. throw InvalidOperationException` | A | SessionService.cs:436-439 | throw | A.1. throw InvalidOperationException("Session not found.") / ("Session already initialized.") |
| A.2 | :InterviewSessionController | :API Gateway | ret | `A.2. Return 400 ApiResponse.Fail(ex.Message)` | A | InterviewSessionController.cs:596-599 |  | — |
| A.3 | :API Gateway | :InterviewService (WebClient) | ret | `A.3. Return 400` | A | ApiGateway/appsettings.json:49-54 |  | — |
| A.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `A.4. Return StartInterviewResult.Failure(...)` | A | InterviewService.cs:187-188 |  | A.4. Return StartInterviewResult.Failure(init.Error ?? "Could not initialize the session.") |
| A.5 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `A.5. Return View Index` | A | InterviewController.cs:84-93 |  | A.5. Return View Index (dashboard rebuilt, ErrorMessage set, the form keeps every value the user typed) |
| A.6 | :Interview Dashboard UI | User | ret | `A.6. Dashboard with the error banner` | A | Index.cshtml:38-41 |  | — |
| 12 | :SessionService | :ProfileBiometricsRepository | call | `12. GetAsync(userId, "Face", ct) [see #3]` | — | SessionService.cs:444-447 |  | 12. GetAsync(userId, "Face", ct) - Step A.1 server-side biometric gate, only when Biometrics:Enabled |
| 13 | :ProfileBiometricsRepository | SQL Server | call | `13. SELECT ProfileBiometric WHERE UserId AND Modality="Face"` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| 14 | SQL Server | :ProfileBiometricsRepository | ret | `14. Return row / null` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| 15 | :ProfileBiometricsRepository | :SessionService | ret | `15. Return ProfileBiometric?` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| B.1 | :SessionService | :InterviewSessionController | ret | `B.1. throw InvalidOperationException` | B | SessionService.cs:448-450 | throw | B.1. throw InvalidOperationException("Please enroll your face before starting an interview.") - the API cannot be bypassed by skipping the UI |
| B.2 | :InterviewSessionController | :API Gateway | ret | `B.2. Return 400 ApiResponse.Fail(ex.Message)` | B | InterviewSessionController.cs:596-599 |  | — |
| B.3 | :API Gateway | :InterviewService (WebClient) | ret | `B.3. Return 400` | B | ApiGateway/appsettings.json:49-54 |  | — |
| B.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `B.4. Return StartInterviewResult.Failure(error)` | B | InterviewService.cs:187-188 |  | — |
| B.5 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `B.5. Return View Index (dashboard + ErrorMessage)` | B | InterviewController.cs:84-93 |  | — |
| B.6 | :Interview Dashboard UI | User | ret | `B.6. Dashboard with the error banner` | B | Index.cshtml:38-41 |  | — |
| 16 | :SessionService | (self) | call | `16. session.Position = req.Position; build AiQuestionPlanRequest` | — | SessionService.cs:459-471 | self | 16. session.Position = req.Position when provided; build AiQuestionPlanRequest{Position, Level, FocusAreas, DurationMin, CvText = null, JdText} |
| 17 | :SessionService | :AiServiceClient | call | `17. PlanQuestionsAsync(planRequest, ct) - Step C` | — | SessionService.cs:476 |  | — |
| 18 | :AiServiceClient | :ai-service | call | `18. POST /internal/questions/plan (header X-Internal-Api-Key)` | — | AiServiceClient.cs:20-26 |  | — |
| 19 | :ai-service | (self) | call | `19. build_interview_plan()` | — | internal.py:49-63, interviewer.py:366-420, chroma_store.py:219-244 | self | 19. build_interview_plan(): target_questions(duration) -> quotas, then chroma_store.build_question_pool x3 (Problem Solving, System Design for Middle+, core by focus areas or by role), weave the three tracks, drop repeats by qid and by normalized text - metadata filter only, NO semantic search and NO Gemini call |
| 20 | :ai-service | :AiServiceClient | ret | `20. Return 200 {questions}` | — | internal.py:56-63 |  | 20. 200 { questions: [{ order_index, question, reference_answer, category, difficulty }] } |
| 21 | :AiServiceClient | :SessionService | ret | `21. Return AiQuestionPlanResponse{Questions}` | — | AiServiceClient.cs:27-28 |  | — |
| C.1 | :SessionService | :InterviewSessionController | ret | `C.1. throw InvalidOperationException` | C | SessionService.cs:484-491 | throw | C.1. throw InvalidOperationException("AI service returned no questions. Please try again.") - init is aborted so the session is never left half-initialized |
| C.2 | :InterviewSessionController | :API Gateway | ret | `C.2. Return 400 ApiResponse.Fail(ex.Message)` | C | InterviewSessionController.cs:596-599 |  | — |
| C.3 | :API Gateway | :InterviewService (WebClient) | ret | `C.3. Return 400` | C | ApiGateway/appsettings.json:49-54 |  | — |
| C.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `C.4. Return StartInterviewResult.Failure(error)` | C | InterviewService.cs:187-188 |  | — |
| C.5 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `C.5. Return View Index (dashboard + ErrorMessage)` | C | InterviewController.cs:84-93 |  | — |
| C.6 | :Interview Dashboard UI | User | ret | `C.6. Dashboard with the error banner` | C | Index.cshtml:38-41 |  | — |
| 22 | :SessionService | (self) | call | `22. Map each planned question to SessionQuestion` | — | SessionService.cs:493-505 | self | 22. Step D: map each planned question to SessionQuestion{SessionQuestionId, SessionId, OrderIndex, Content, ReferenceAnswer (stored server-side only, never sent to the browser), IsAiGenerated=true, ContextSource="QuestionBank", AskedAt=null} |
| 23 | :SessionService | :SessionRepository | call | `23. AddSessionQuestionsAsync(questions, ct) - Step E` | — | SessionService.cs:510 |  | — |
| 24 | :SessionRepository | SQL Server | call | `24. INSERT SessionQuestions in one batch (SaveChangesAsync)` | — | SessionRepository.cs:55-59 |  | — |
| 25 | SQL Server | :SessionRepository | ret | `25. Return rows affected` | — | SessionRepository.cs:58 |  | — |
| 26 | :SessionRepository | :SessionService | ret | `26. Return` | — | SessionRepository.cs:58 |  | — |
| 27 | :SessionService | :SessionRepository | call | `27. SaveChangesAsync(ct)` | — | SessionService.cs:528-531 |  | 27. SaveChangesAsync(ct) after Status = "Initialized", TotalQuestions = questions.Count, UpdatedAt = now - Step F; StartedAt stays null on purpose |
| 28 | :SessionRepository | SQL Server | call | `28. UPDATE InterviewSessions SET Status` | — | SessionRepository.cs:348 |  | 28. UPDATE InterviewSession SET Status, TotalQuestions, UpdatedAt |
| 29 | SQL Server | :SessionRepository | ret | `29. Return rows affected` | — | SessionRepository.cs:348 |  | — |
| 30 | :SessionRepository | :SessionService | ret | `30. Return` | — | SessionRepository.cs:348 |  | — |
| 31 | :SessionService | :SessionRepository | call | `31. CreateRoomAsync(InterviewRoom)` | — | SessionService.cs:534-541 |  | 31. CreateRoomAsync(InterviewRoom{RoomId, SessionId, ChannelName = "room-" + sessionId, Status="Active", InitializedAt = now}) - Step G |
| 32 | :SessionRepository | SQL Server | call | `32. INSERT InterviewRoom (SaveChangesAsync)` | — | SessionRepository.cs:61-66 |  | — |
| 33 | SQL Server | :SessionRepository | ret | `33. Return rows affected` | — | SessionRepository.cs:64 |  | — |
| 34 | :SessionRepository | :SessionService | ret | `34. Return InterviewRoom` | — | SessionRepository.cs:65 |  | — |
| 35 | :SessionService | :InterviewSessionController | ret | `35. Return InitResponse` | — | SessionService.cs:543-552 |  | 35. Return InitResponse{SessionId, TotalQuestions, RoomId, FirstQuestion} - Step H |
| 36 | :InterviewSessionController | :API Gateway | ret | `36. Return 200 ApiResponse.Ok(result)` | — | InterviewSessionController.cs:594 |  | — |
| 37 | :API Gateway | :InterviewService (WebClient) | ret | `37. Return 200` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 38 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `38. Return StartInterviewResult.Ok(sessionId)` | — | InterviewService.cs:190-193 |  | 38. Return StartInterviewResult.Ok(sessionId) - question generation and the credit deduction happen later, in the Room action |
| 39 | :InterviewController (WebClient) | :Interview Dashboard UI | ret | `39. 302 RedirectToAction(Verify, { id = sessionId })` | — | InterviewController.cs:96-99 |  | — |
| 40 | :Interview Dashboard UI | User | ret | `40. Biometric entry-gate screen [see #13]` | — | InterviewController.cs:99 | boundary | 40. Biometric entry-gate screen for the new session (#13 VerifyFaceEntryGate); once it passes, the Room opens - see EnterInterviewRoom |

**Cờ:** `self` · `throw` · `boundary`

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| A | opt | `[session is null OR session.UserId != userId OR session.Status != "Configured"]` | A.1 → A.6 | — | **2** (cùng 400 qua đúng một `catch (Exception)`, cùng banner Dashboard; nhãn `A.1` ghi đủ 2 câu) |
| B | opt | `[Biometrics:Enabled AND this user has no Face enrollment]` | B.1 → B.6 | — | — |
| C | opt | `[the plan is null OR it returned 0 questions]` | C.1 → C.6 | — | — |

> Không có khung `alt` nào: cả 3 rẽ nhánh là `if ... throw` early-return, không có `else`
> (`SessionService.cs:436-439, 448-450, 484-491`).

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | 19 | `Question planning is pure RAG retrieval: chroma_store.build_question_pool filters by metadata (position / difficulty / category) with NO semantic search and NO Gemini call. Gemini only appears later, when answers are graded (#17 EvaluateInterviewAnswer).` |
| N2 | 22 | `ReferenceAnswer is persisted on SessionQuestion but never leaves the server: InitResponse carries only SessionId / TotalQuestions / RoomId / FirstQuestion, and every later question DTO omits it too. It exists purely so #17 can grade against it.` |
| N3 | 27 | `StartedAt is deliberately NOT set here. The candidate still has to pass Verify (up to 3 face attempts) and read the Integrity Policy - about 20s measured - so the interview clock only starts at the Initialized -> InProgress transition inside GenerateQuestionAsync (see EnterInterviewRoom).` |
| N4 | 12 | `This is the SECOND biometric gate on the same click: WebClient already checked FaceEnrolled in the Start action (#3, drawn there). This one is server-side so a direct API call cannot skip it.` |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | Liệt kê | Nguồn |
|---|---|---|---|---|
| 1 | InterviewController (WebClient) | class | SUBSET (`Start`) | local |
| 2 | Controller | framework-base | FULL (rỗng) | SHARED |
| 3 | IInterviewService (WebClient) | interface | SUBSET | local |
| 4 | InterviewService (WebClient) | class | SUBSET | local |
| 5 | IApiClient / 6 ApiClient | interface/class | FULL | SHARED |
| 7 | DashboardViewModel / 8 StartInterviewResult / 9 ApiResponse<T> (WebClient) | DTO | FULL | local |
| 10 | ControllerBase | framework-base | FULL (rỗng) | SHARED |
| 11 | InterviewSessionController (InterviewAPI) | class | SUBSET (`Init`) | local |
| 12 | ISessionService / 13 SessionService (InterviewAPI) | interface/class | SUBSET | local |
| 14 | ApiResponse<T> (InterviewAPI) | DTO | FULL | SHARED |
| 15 | InitRequest / 16 InitResponse | DTO | FULL | local |
| 17 | IAiServiceClient | interface | FULL | local |
| 18 | AiServiceClient (InterviewAPI) | class | FULL | SHARED |
| 19 | AiQuestionPlanRequest / 20 AiQuestionPlanResponse / 21 AiPlannedQuestion | DTO | FULL | local |
| 22 | IProfileBiometricsRepository / 23 ProfileBiometricsRepository (InterviewAPI) | interface/class | FULL | local |
| 24 | ISessionRepository / 25 SessionRepository (InterviewAPI) | interface/class | SUBSET | local |
| 26 | InterviewSession / 27 SessionQuestion / 28 InterviewRoom | entity | FULL | SHARED |
| 29 | ProfileBiometric | entity | FULL | local |
| 30 | InterviewDbContext / 31 DbContext | class/framework-base | FULL | SHARED |

## 8. CLASS — chi tiết

### `SessionService (InterviewAPI)` — `SUBSET`
- 10 field DI + hằng `BiometricsEnabled`
- Method `+ InitAsync(userId, req, ct): Task<InitResponse>`
- *(SUBSET)* Đã bỏ ~50 method ngoài chức năng này.

### `SessionRepository (InterviewAPI)` — `SUBSET`
- `+ GetByIdAsync`, `+ AddSessionQuestionsAsync`, `+ CreateRoomAsync`, `+ SaveChangesAsync`

### `InterviewService (WebClient)` — `SUBSET`
- `+ StartInterviewAsync(...)` + `- SendAsync<T>(...)`

Không có class SHARED mới trong chức năng này.

## 9. QUAN HỆ CLASS

Xem `specs/startinterviewsession_classes.yaml` khoá `relations:`.

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| — | — | Không phát hiện anomaly mới | — | — | — |

> Đã kiểm và **bác** 3 nghi vấn:
> (a) KI-01 (forward status 2xx) **không xuất hiện** trong action `Start` — nó trả `View`/`Redirect`,
> không forward status code thô ([InterviewController.cs:84-99](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L84-L99)).
> (b) Cổng sinh trắc bị kiểm 2 lần (WebClient + InterviewAPI) — **cố ý**, comment
> [SessionService.cs:441-443](../../../source/InterviewAPI/Services/SessionService.cs#L441-L443) ghi rõ
> lớp server để không bypass được bằng gọi API trực tiếp (note N4).
> (c) `Category`/`Difficulty` do ai-service trả về **bị bỏ** khi map sang `SessionQuestion` — đúng
> như comment ở `SessionService.cs:66-68` đã ghi (bảng `SessionQuestions` không có 2 cột này); hệ quả
> đã được ghi nhận sẵn ở chỗ khác (`GetIdleThresholds` luôn rơi về Medium), không phải phát hiện mới.

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | Card `#4` trong RECON vượt cả 2 ngưỡng (~175 message / ~6560px) | **Tách 2 file** — `StartInterviewSession` (init) và `EnterInterviewRoom` (vào phòng) | **Đã hỏi và được DuyenTQ duyệt** (lô G) đúng theo mục DỪNG VÀ HỎI #3. Ranh giới tách trùng đúng ranh giới HTTP request: init là 1 request, vào phòng là 1 request khác (sau Verify). |
| 2 | 2 câu lỗi của Step A ("Session not found." / "Session already initialized.") | **Gộp 1 `opt A`**, nhãn `A.1` ghi đủ cả 2 câu | Cùng `catch (Exception)`, cùng 400, cùng banner Dashboard — đúng điều kiện gộp đã dùng ở lô E/F. |
| 3 | Nhánh lỗi hạ tầng ai-service | **Không vẽ (§1)** | Tiền lệ lô A/C/D/F. |
| 4 | Có vẽ `:ProfileBiometricsRepository` riêng không (chỉ 4 message)? | **Có** | Đây là guard nghiệp vụ thật (chặn phiên khi chưa enroll mặt), không phải hạ tầng; gộp vào self-message sẽ giấu mất 1 lần chạm DB. |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `4`)** Question planning is pure RAG retrieval: chroma_store.build_question_pool filters by metadata (position / difficulty / category) with NO semantic search and NO Gemini call. Gemini only appears later, when answers are graded (#17 EvaluateInterviewAnswer). *(gỡ từ note trên diagram)*
- **(cạnh message `8`)** ReferenceAnswer is persisted on SessionQuestion but never leaves the server: InitResponse carries only SessionId / TotalQuestions / RoomId / FirstQuestion, and every later question DTO omits it too. It exists purely so #17 can grade against it. *(gỡ từ note trên diagram)*
- **(cạnh message `A.1`)** StartedAt is deliberately NOT set here. The candidate still has to pass Verify (up to 3 face attempts) and read the Integrity Policy - about 20s measured - so the interview clock only starts at the Initialized -> InProgress transition inside GenerateQuestionAsync (see EnterInterviewRoom). *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| `uc_id` của chức năng | RECON không ghi UC-id cho #4 | Nhóm xác nhận ê-líp use-case rồi điền vào `specs/startinterviewsession.yaml` và `specs/enterinterviewroom.yaml` | luồng 1 |
| BL#54 và BL#38 ứng với phần nào của card #4 | File backlog không có trong repo | Sau khi tách, `EnterInterviewRoom` giữ BL#54 (credit) và BL#38 (room tour) — cần nhóm xác nhận lại | luồng 2 |
