# FLOW — `EvaluateInterviewAnswer` · BL#73, 75, 70 · UC-`chưa có`

> Đặt tại `docs/diagrams/FLOWS/EvaluateInterviewAnswer.md`
> Trạng thái: `DRAFT` — Khảo sát: lô D, 2026-08-03

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Chấm điểm **một** câu trả lời ngay sau khi ứng viên gửi, chạy **nền** (fire-and-forget) để lượt phỏng vấn không bị chậm: InterviewAPI → EvaluationAPI → ai-service (Gemini) → lưu 1 hàng `AnswerEvaluations` |
| Điểm vào (user thao tác gì) | **KHÔNG có thao tác trực tiếp.** `SessionService.SendMessageAsync` (Step E.1) gọi `ScheduleGrading` sau khi đã lưu câu trả lời — trigger thuộc `#5 ConductInterviewQA` |
| Kết thúc (user thấy gì) | **Người dùng KHÔNG thấy gì.** Kết quả chỉ nằm trong DB, hiển thị về sau ở `#18 ViewEvaluationReport`. Thất bại cũng im lặng (KI-08) |
| Quy mô | `10` participant · `41` message · `22` class · `0` alt / `3` opt · trang Sequence 2550×2039 · trang Class 5240×810 |
| Đối chiếu ước lượng RECON | RECON đoán `10-12` · thật `41` · hệ số `3.7×` |
| Service ngoài quyền sở hữu | không (EvaluationAPI và InterviewAPI đều thuộc quyền sửa; ApiGateway **không** tham gia chặng nền này) |
| **Gọi BYPASS Gateway?** | **CÓ — 2 chặng đi thẳng.** (1) InterviewAPI → EvaluationAPI `:7006` (`InterviewAPI/Program.cs:50, 89-95`: `BaseAddress = EVALUATION_API_BASEURL`, mặc định `http://localhost:7006`). (2) EvaluationAPI → InterviewAPI `:7005` (`EvaluationAPI/Program.cs:49-55`: `BaseAddress = INTERVIEW_API_BASEURL`, mặc định `http://localhost:7005`). **KHÔNG vẽ hop ApiGateway cho cả hai.** |
| Nhà cung cấp AI | **Gemini** (`evaluator.GeminiJudge`, model `config.GEMINI_JUDGE_MODEL`) + ChromaDB (best-effort, `enrich_for`) |
| Ghi DB | `DevPrep_Evaluation.AnswerEvaluations` — INSERT 1 hàng / (SessionId, SessionQuestionId). Đọc trước để chống chấm trùng |
| Trừ credit | không |
| KI liên quan | **KI-08** (chấm nền lỗi im lặng, không retry) · **KI-12 (mới)** endpoint ai-service `POST /evaluate` không kiểm khoá nội bộ và không có caller |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| Toàn bộ `SendMessageAsync` (lưu `ChatMessage`, tăng `AnsweredQuestions`, tìm câu hỏi kế tiếp) | [SessionService.cs:699-827](../../../source/InterviewAPI/Services/SessionService.cs#L699-L827) | Thuộc `#5 ConductInterviewQA`. Ở đây chỉ vẽ **điểm gọi** `ScheduleGrading` ở Step E.1 (dòng 827) làm message 1. |
| `ai-service POST /evaluate` (`app/api/evaluation.py:16-18`) + `evaluator.evaluate()` | [evaluation.py:16-18](../../../ai-service/app/api/evaluation.py#L16-L18), [evaluator.py:244-248](../../../ai-service/app/services/evaluator.py#L244-L248) | **Code chết với luồng .NET**: đã grep toàn bộ `source/` — không client nào gọi `/evaluate` (chỉ `/internal/evaluate-answer`). Xem **KI-12** ở §10. |
| Nhánh lỗi hạ tầng ai-service (`EnsureSuccess` ném `HttpRequestException` khi non-2xx) | [EvaluationAPI/Clients/AiServiceClient.cs:67-81](../../../source/EvaluationAPI/Clients/AiServiceClient.cs#L67-L81) | Theo tiền lệ lô A/C: lỗi hạ tầng chung cho mọi lời gọi ai-service. Kết cục giống hệt `opt C` đã vẽ (rơi về `catch` của `ScheduleGrading`, log rồi thôi). |
| `EvaluationController.GetPerformance` + `BuildReport` + `GetReport` + `GetAnswerEvaluations` | [EvaluationController.cs:61-165](../../../source/EvaluationAPI/Controllers/EvaluationController.cs#L61-L165) | Thuộc `#18 ViewEvaluationReport` / báo cáo hiệu năng. |
| `chroma_store.query_similar` bên trong `enrich_for` | [evaluator.py:233-241](../../../ai-service/app/services/evaluator.py#L233-L241) | Bọc `try/except` nuốt lỗi, chỉ làm giàu ngữ cảnh prompt — gộp vào nhãn message 17. |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Trigger: `SendMessageAsync` Step E.1 gọi `ScheduleGrading` | `#5 ConductInterviewQA` | message 1 (self-message trên `:SessionService`, ghi rõ trigger) | SessionService.cs:824-827 |
| Kiểm quyền sở hữu phiên bên trong InterviewAPI (`Owns` → `SessionService.OwnsSessionAsync` → `SessionRepository.GetByIdAsync` → `DevPrep_Interview`) | hạ tầng IDOR dùng chung của EvaluationAPI (cũng dùng ở `#18`) | **đúng 1 cặp call+return**: message 7 `GET api/interview/sessions/{sessionId}/owns` ↔ message 8 / `opt A` | InterviewSessionController.cs:185-193, SessionService.cs:1629-1633 |

---

## 3. PARTICIPANT

> ⚠️ **KHÔNG có actor hình người.** Đây là tác vụ **nền** (`Task.Run` fire-and-forget): người dùng
> đã gửi câu trả lời xong và đang đọc câu hỏi kế tiếp, không hề chờ và không hề thấy kết quả của
> luồng này. Theo quy ước ACTOR GIÁN TIẾP, participant trái nhất là **service khởi phát**
> (`:SessionService (InterviewAPI)`), kèm note N1 giải thích.

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 0 | :SessionService (InterviewAPI) | box | `InterviewAPI/Services/SessionService.cs:1708-1737` | — |
| 1 | :EvaluationApiClient (InterviewAPI) | box | `InterviewAPI/Clients/EvaluationApiClient.cs` | — |
| 2 | :EvaluationController (EvaluationAPI) | box | `EvaluationAPI/Controllers/EvaluationController.cs:41-58` | **không (bypass :7006)** |
| 3 | :InterviewApiClient (EvaluationAPI) | box | `EvaluationAPI/Clients/InterviewApiClient.cs:23-44` | — |
| 4 | :InterviewSessionController (InterviewAPI) | box | `InterviewAPI/Controllers/InterviewSessionController.cs:185-193` | **không (bypass :7005)** |
| 5 | :EvaluationService (EvaluationAPI) | box | `EvaluationAPI/Services/EvaluationService.cs:28-83` | — |
| 6 | :EvaluationRepository (EvaluationAPI) | box | `EvaluationAPI/Repositories/EvaluationRepository.cs` | — |
| 7 | :AiServiceClient (EvaluationAPI) | box | `EvaluationAPI/Clients/AiServiceClient.cs:34-49` | — |
| 8 | :ai-service /internal/evaluate-answer | box | `ai-service/app/api/internal.py:84-101` → `app/services/evaluator.py:127-167` | — |
| 9 | SQL Server (DevPrep_Evaluation) | cylinder | `EvaluationAPI/Data/EvaluationDbContext.cs` | — |

## 4. BẢNG MESSAGE

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | :SessionService | (self) | call | `1. [background] ScheduleGrading(...) [see #5]` | — | SessionService.cs:824-827 | self · boundary | 1. [background] SendMessageAsync Step E.1 -> ScheduleGrading(sessionId, currentQuestion, req.Content) right after the answer row is saved (trigger belongs to #5 ConductInterviewQA) |
| 2 | :SessionService | (self) | call | `2. Build EvaluateAnswerPayload; Task.Run(...) [best-effort]` | — | SessionService.cs:1710-1728 | self · best-effort | 2. Capture the Authorization header synchronously (HttpContext is gone inside the task); build EvaluateAnswerPayload{Question, ReferenceAnswer, Category="", Difficulty="", UserAnswer}; Task.Run(...) creates its own DI scope and resolves IEvaluationApiClient |
| 3 | :SessionService | :EvaluationApiClient | call | `3. EvaluateAnswerAsync(sessionId, sessionQuestionId, ...)` | — | SessionService.cs:1728 |  | 3. EvaluateAnswerAsync(sessionId, sessionQuestionId, payload, bearerToken, CancellationToken.None) |
| 4 | :EvaluationApiClient | :EvaluationController | call | `4. POST api/evaluation/sessions/{id}/answers/{questionId}` | — | EvaluationApiClient.cs:23-29, InterviewAPI/Program.cs:50, 89-95 |  | 4. POST api/evaluation/sessions/{sessionId}/answers/{sessionQuestionId} (Authorization forwarded) - DIRECT to EvaluationAPI :7006, NOT through the Gateway |
| 5 | :EvaluationController | :InterviewApiClient | call | `5. OwnsSessionAsync(sessionId, bearerToken)` | — | EvaluationController.cs:32-36, 48 |  | 5. OwnsSessionAsync(sessionId, bearer token from HttpContext) - IDOR guard |
| 6 | :InterviewApiClient | :InterviewSessionController | call | `6. GET api/interview/sessions/{id}/owns` | — | InterviewApiClient.cs:27-32, EvaluationAPI/Program.cs:49-55 | boundary | 6. GET api/interview/sessions/{sessionId}/owns - DIRECT to InterviewAPI :7005, NOT through the Gateway |
| A.1 | :InterviewSessionController | :InterviewApiClient | ret | `A.1. Return 404 {error} / 401 / 403` | A | InterviewSessionController.cs:191 |  | A.1. 404 ApiResponse.Fail("Session not found.") / 401 / 403 / no response at all |
| A.2 | :InterviewApiClient | :EvaluationController | ret | `A.2. Return false` | A | InterviewApiClient.cs:33-43 |  | A.2. Return false - a transient failure is logged and also denied (fail closed) |
| A.3 | :EvaluationController | :EvaluationApiClient | ret | `A.3. Return 403 {error}` | A | EvaluationController.cs:38-39, 49 |  | A.3. Return 403 ApiResponse.Fail("You do not have access to this session.") |
| A.4 | :EvaluationApiClient | :SessionService | ret | `A.4. EnsureSuccessStatusCode() throws HttpRequestException` | A | EvaluationApiClient.cs:30-38 | throw | A.4. EnsureSuccessStatusCode throws HttpRequestException (logged and rethrown) |
| A.5 | :SessionService | (self) | call | `A.5. catch: LogWarning "Background grading failed..."` | A | SessionService.cs:1730-1735 | self | A.5. catch: LogWarning "Background grading failed..." - the answer stays ungraded, no retry, nothing surfaced to the user (KI-08) |
| 7 | :InterviewSessionController | :InterviewApiClient | ret | `7. 200 ApiResponse.Ok(new {})` | — | InterviewSessionController.cs:192 |  | — |
| 8 | :InterviewApiClient | :EvaluationController | ret | `8. Return true` | — | InterviewApiClient.cs:34 |  | — |
| 9 | :EvaluationController | :EvaluationService | call | `9. EvaluateAnswerAsync(sessionId, answerId, req, ct)` | — | EvaluationController.cs:51 |  | — |
| 10 | :EvaluationService | :EvaluationRepository | call | `10. GetBySessionQuestionAsync(sessionId, answerId, ct)` | — | EvaluationService.cs:33 |  | 10. GetBySessionQuestionAsync(sessionId, answerId, ct) - idempotency guard |
| 11 | :EvaluationRepository | SQL Server (DevPrep_Evaluation) | call | `11. SELECT AnswerEvaluation WHERE SessionQuestionId` | — | EvaluationRepository.cs:25-29 |  | 11. SELECT AnswerEvaluation WHERE SessionId AND SessionQuestionId |
| 12 | SQL Server (DevPrep_Evaluation) | :EvaluationRepository | ret | `12. Return row / null` | — | EvaluationRepository.cs:25-29 |  | — |
| 13 | :EvaluationRepository | :EvaluationService | ret | `13. Return AnswerEvaluation?` | — | EvaluationRepository.cs:25-29 |  | — |
| B.1 | :EvaluationService | :EvaluationController | ret | `B.1. Return ToResponse(existing)` | B | EvaluationService.cs:33-35, 73-83 |  | B.1. Return ToResponse(existing) - no ai-service call, no second row |
| B.2 | :EvaluationController | :EvaluationApiClient | ret | `B.2. Return 200 ApiResponse.Ok(result)` | B | EvaluationController.cs:52 |  | — |
| B.3 | :EvaluationApiClient | :SessionService | ret | `B.3. Return` | B | EvaluationApiClient.cs:29-30 |  | B.3. Return - the background task only checks the status and discards the body |
| 14 | :EvaluationService | :AiServiceClient | call | `14. EvaluateAnswerAsync(AiEvaluateAnswerRequest)` | — | EvaluationService.cs:38-46 |  | 14. EvaluateAnswerAsync(AiEvaluateAnswerRequest{question, reference_answer, category, difficulty, user_answer, followups=[]}) |
| 15 | :AiServiceClient | :ai-service | call | `15. POST /internal/evaluate-answer (header X-Internal-Api-Key)` | — | EvaluationAPI/Clients/AiServiceClient.cs:34-41 |  | — |
| 16 | :ai-service | (self) | call | `16. GeminiJudge(); enrich_for(item); grade_item() -> median` | — | internal.py:85-92, evaluator.py:127-167, 233-241 | self · best-effort | 16. GeminiJudge(); enrich_for(item) pulls up to 2 similar Q&A from ChromaDB [best-effort]; grade_item() runs the judge SELF_CONSISTENCY_RUNS times and takes the median of knowledge / problem_solving / communication |
| C.1 | :ai-service | :AiServiceClient | ret | `C.1. Return 200 EvaluateOut{score = null}` | C | internal.py:94-96, evaluator.py:145-146 |  | C.1. 200 EvaluateOut{score = null, model_used} - a 200 with a NULL score, not an HTTP error |
| C.2 | :AiServiceClient | :EvaluationService | ret | `C.2. Return AiEvaluateAnswerResponse{Score = null}` | C | EvaluationAPI/Clients/AiServiceClient.cs:42-43 |  | — |
| C.3 | :EvaluationService | :EvaluationController | ret | `C.3. throw InvalidOperationException` | C | EvaluationService.cs:49-50 | throw | C.3. throw InvalidOperationException("Grading failed, please try again.") |
| C.4 | :EvaluationController | :EvaluationApiClient | ret | `C.4. Return 400 ApiResponse.Fail(ex.Message)` | C | EvaluationController.cs:54-57 |  | — |
| C.5 | :EvaluationApiClient | :SessionService | ret | `C.5. EnsureSuccessStatusCode() throws HttpRequestException` | C | EvaluationApiClient.cs:30-38 | throw | C.5. EnsureSuccessStatusCode throws HttpRequestException (logged and rethrown) |
| C.6 | :SessionService | (self) | call | `C.6. catch: LogWarning` | C | SessionService.cs:1730-1735 | self | C.6. catch: LogWarning - this answer is never graded, no retry, no flag; the final report simply misses it (KI-08) |
| 17 | :ai-service | :AiServiceClient | ret | `17. Return 200 EvaluateOut{score, criteria, analysis, ...}` | — | internal.py:97-101 |  | 17. 200 EvaluateOut{score, criteria{knowledge, problem_solving, communication}, analysis, suggested_improvement, praise, model_used} |
| 18 | :AiServiceClient | :EvaluationService | ret | `18. Return AiEvaluateAnswerResponse` | — | EvaluationAPI/Clients/AiServiceClient.cs:42-43 |  | — |
| 19 | :EvaluationService | (self) | call | `19. Map to AnswerEvaluation` | — | EvaluationService.cs:52-67 | self | 19. Map to AnswerEvaluation{EvaluationId=Guid.NewGuid(), SessionId, SessionQuestionId, QuestionContent, UserAnswer, Score, CriteriaScoresJson, Strengths=Praise, Weaknesses=Analysis, AiFeedback=Analysis, ModelUsed, GeneratedAt=VietnamTime.Now} |
| 20 | :EvaluationService | :EvaluationRepository | call | `20. AddEvaluationAsync(eval, ct)` | — | EvaluationService.cs:68 |  | — |
| 21 | :EvaluationRepository | SQL Server (DevPrep_Evaluation) | call | `21. INSERT AnswerEvaluation (SaveChangesAsync)` | — | EvaluationRepository.cs:17-22 |  | — |
| 22 | SQL Server (DevPrep_Evaluation) | :EvaluationRepository | ret | `22. Return rows affected` | — | EvaluationRepository.cs:20 |  | — |
| 23 | :EvaluationRepository | :EvaluationService | ret | `23. Return AnswerEvaluation` | — | EvaluationRepository.cs:21 |  | — |
| 24 | :EvaluationService | :EvaluationController | ret | `24. Return EvaluateAnswerResponse` | — | EvaluationService.cs:71, 73-83 |  | 24. Return EvaluateAnswerResponse{EvaluationId, SessionId, SessionQuestionId, Score, CriteriaScoresJson, Strengths, AiFeedback, ModelUsed} |
| 25 | :EvaluationController | :EvaluationApiClient | ret | `25. Return 200 ApiResponse.Ok(result)` | — | EvaluationController.cs:52 |  | — |
| 26 | :EvaluationApiClient | :SessionService | ret | `26. Return` | — | EvaluationApiClient.cs:29-30 |  | 26. Return - only the status code is checked, the body is discarded |
| 27 | :SessionService | (self) | call | `27. Background task ends [see #18]` | — | SessionService.cs:1722-1736 | self | 27. Background task ends. The row now sits in AnswerEvaluations for #18 ViewEvaluationReport; nothing is written back to InterviewAPI and the candidate is never notified |

**Cờ:** `self` · `throw` · `best-effort` · `boundary`

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| A | opt | `[ownership check fails: 404 / 401 / 403 from InterviewAPI, OR the call throws - fail closed]` | A.1 → A.5 | — | 2 (cùng `Return false` → cùng 403, cùng kết cục im lặng) |
| B | opt | `[an evaluation for this (SessionId, SessionQuestionId) already exists - replayed grading]` | B.1 → B.3 | — | — |
| C | opt | `[every judge run failed (e.g. Gemini rate limit) - grade_item returns None]` | C.1 → C.6 | — | — |

> Không có khung `alt` nào: cả 3 rẽ nhánh trong code đều là `if` không `else` / early-return
> (`EvaluationController.cs:48-49`, `EvaluationService.cs:33-35`, `:49-50`).

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | 1-2 | `NO human actor on purpose: this whole page is a fire-and-forget background task (Task.Run, SessionService.cs:1722). The candidate already got their next question and never waits for, sees, or is notified about any of this. The leftmost lifeline is the service that starts the task.` |
| N2 | 1 | `This flow runs ONCE PER ANSWER (including Skip - an empty answer is graded 0 "No answer provided." by ai-service), not once per session. Drawn once as a representative run.` |
| N3 | 4 / 6 | `Both cross-service hops bypass the API Gateway and use the service port directly: InterviewAPI -> EvaluationAPI :7006 (InterviewAPI/Program.cs:50, 89-95) and EvaluationAPI -> InterviewAPI :7005 (EvaluationAPI/Program.cs:49-55). The bearer token is forwarded by hand on each call because Evaluation tables carry no UserId of their own.` |
| N4 | C.6 / A.5 | `KI-08: every failure path ends in the same LogWarning inside ScheduleGrading. There is no retry, no "grading failed" flag on the question, and no signal to the user - the answer simply never appears in the report.` |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | File:Line | Liệt kê | Nguồn |
|---|---|---|---|---|---|
| 1 | SessionService (InterviewAPI) | class | InterviewAPI/Services/SessionService.cs:1708-1737 | SUBSET | local |
| 2 | IEvaluationApiClient | interface | InterviewAPI/Clients/IEvaluationApiClient.cs | FULL | local |
| 3 | EvaluationApiClient (InterviewAPI) | class | InterviewAPI/Clients/EvaluationApiClient.cs:6-40 | FULL | local |
| 4 | EvaluateAnswerPayload | DTO | InterviewAPI/DTOs/Evaluation/EvaluateAnswerPayload.cs | FULL | local |
| 5 | ControllerBase | framework-base | ASP.NET Core | FULL (rỗng) | SHARED |
| 6 | EvaluationController (EvaluationAPI) | class | EvaluationAPI/Controllers/EvaluationController.cs:16-58 | SUBSET | local |
| 7 | IEvaluationService | interface | EvaluationAPI/Services/Interfaces/IEvaluationService.cs | SUBSET | local |
| 8 | EvaluationService (EvaluationAPI) | class | EvaluationAPI/Services/EvaluationService.cs:12-83 | SUBSET | local |
| 9 | ApiResponse<T> (EvaluationAPI) | DTO | EvaluationAPI/DTOs/ApiResponse.cs | FULL | SHARED |
| 10 | EvaluateAnswerRequest | DTO | EvaluationAPI/DTOs/Evaluation/EvaluateAnswerRequest.cs:3-10 | FULL | local |
| 11 | EvaluateAnswerResponse | DTO | EvaluationAPI/DTOs/Evaluation/EvaluateAnswerResponse.cs:3-12 | FULL | local |
| 12 | IInterviewApiClient | interface | EvaluationAPI/Clients/IInterviewApiClient.cs | FULL | local |
| 13 | InterviewApiClient (EvaluationAPI) | class | EvaluationAPI/Clients/InterviewApiClient.cs:7-44 | SUBSET | local |
| 14 | IAiServiceClient (EvaluationAPI) | interface | EvaluationAPI/Clients/IAiServiceClient.cs | FULL | local |
| 15 | AiServiceClient (EvaluationAPI) | class | EvaluationAPI/Clients/AiServiceClient.cs:6-81 | FULL | local |
| 16 | AiEvaluateAnswerRequest | DTO | EvaluationAPI/DTOs/AiService/AiEvaluateAnswerRequest.cs:5-25 | FULL | local |
| 17 | AiEvaluateAnswerResponse | DTO | EvaluationAPI/DTOs/AiService/AiEvaluateAnswerResponse.cs:5-26 | FULL | local |
| 18 | IEvaluationRepository | interface | EvaluationAPI/Repositories/Interfaces/IEvaluationRepository.cs | FULL | local |
| 19 | EvaluationRepository (EvaluationAPI) | class | EvaluationAPI/Repositories/EvaluationRepository.cs:8-57 | FULL | local |
| 20 | AnswerEvaluation | entity | EvaluationAPI/Models/AnswerEvaluation.cs:5-31 | FULL | local |
| 21 | EvaluationDbContext | class | EvaluationAPI/Data/EvaluationDbContext.cs | FULL | local |
| 22 | DbContext | framework-base | EF Core | FULL (rỗng) | SHARED |

> ⚠️ `IAiServiceClient`/`AiServiceClient` của **EvaluationAPI** là class KHÁC hoàn toàn với bản
> InterviewAPI (`specs/_shared/classes/AiServiceClient.yaml` — 13 method biometrics/TTS/STT).
> EvaluationAPI chỉ có 3 method. **KHÔNG include file shared ở đây**, khai báo cục bộ kèm hậu tố
> `(EvaluationAPI)`.

## 8. CLASS — chi tiết

### `SessionService (InterviewAPI)` — `SUBSET`
- Field: 10 field DI (liệt kê đủ, `SessionService.cs:169-178`)
- Method: `- ScheduleGrading(sessionId: Guid, question: SessionQuestion, userAnswer: string): void`
- *(SUBSET)* Đã bỏ ~50 method ngoài chức năng này.

### `EvaluationController (EvaluationAPI)` — `SUBSET`
- Field: `- _service`, `- _interviewApiClient`, `- _httpContextAccessor`
- Method: `+ EvaluateAnswer(...)`, `- UserOwnsSessionAsync(...)`, `- Forbidden(): IActionResult`
- *(SUBSET)* Đã bỏ 4 action thuộc `#18` / báo cáo hiệu năng.

### `EvaluationService (EvaluationAPI)` — `SUBSET`
- Field: `- _repo`, `- _aiServiceClient`, `- _interviewApiClient`
- Method: `+ EvaluateAnswerAsync(...)`, `- ToResponse(e: AnswerEvaluation): EvaluateAnswerResponse`

Không có class SHARED mới trong chức năng này.

## 9. QUAN HỆ CLASS

Xem `specs/evaluateinterviewanswer_classes.yaml` khoá `relations:` (khai báo đầy đủ).

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| KI-08 | `SessionService.cs:1730-1735` | Chấm nền thất bại chỉ log, không retry, không cờ, không báo user | fire-and-forget | **Vẽ thật**, cờ `[best-effort]` + note N4 | Có (đã tồn tại) |
| **KI-12** | `ai-service/app/api/evaluation.py:16-18` | Endpoint `POST /evaluate` (a) **không** gọi `_check_key` như mọi endpoint `/internal/*`, (b) **không có caller nào** trong `source/` (đã grep), nhưng vẫn được `main.py:57` đăng ký. Nó chạy `evaluator.evaluate()` → Gemini cho mọi transcript gửi tới, không cần khoá | code chết + thiếu guard | **Không vẽ** (§1) + ghi KI | **Có — thêm mới KI-12** |

> Đã kiểm và **bác** 1 nghi vấn: RECON ghi `InterviewController.cs:445-446` (action `Message`) dính
> pattern KI-01. Đúng là có, nhưng đó là điểm gọi của `#5 ConductInterviewQA` — nằm **trước**
> `ScheduleGrading` và không ảnh hưởng nhánh nền này (nền không trả gì về browser).


### Gỡ từ note trên diagram — anomaly

- **(cạnh message `16`)** KI-08: every failure path ends in the same LogWarning inside ScheduleGrading. There is no retry, no "grading failed" flag on the question, and no signal to the user - the answer simply never appears in the report. *(gỡ từ note trên diagram)*

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | Thể hiện actor cho tác vụ nền | **KHÔNG vẽ actor hình người**; participant trái nhất = `:SessionService (InterviewAPI)` + note N1 | Đúng quy ước ACTOR GIÁN TIẾP đã chốt: user không có mặt, không chờ, không thấy kết quả. Đề bài nêu đích danh #17 là ca này. |
| 2 | Chuỗi kiểm quyền sở hữu bên trong InterviewAPI (`Owns` → `SessionService` → `SessionRepository` → `DevPrep_Interview`) | **Thu về đúng 1 cặp call+return tới `:InterviewSessionController`** + note, thay vì vẽ tiếp 3 participant nữa | (a) Đúng cơ chế BOUNDARY; (b) nếu vẽ tiếp thì `:SessionService (InterviewAPI)` phải xuất hiện **lần thứ hai** (khác DI scope) hoặc phải vẽ mũi tên vòng ngược về lifeline trái nhất — cả hai đều gây hiểu nhầm. Chuỗi nội bộ đã ghi đủ File:Line ở §2. |
| 3 | 2 CSDL khác nhau (`DevPrep_Interview` cho owns-check, `DevPrep_Evaluation` cho ghi kết quả) | **Chỉ vẽ 1 trụ `SQL Server (DevPrep_Evaluation)`** | `DevPrep_Interview` chỉ bị chạm bên trong boundary ở quyết định 2 → không vẽ. Tên trụ ghi rõ database để không hiểu nhầm là DB dùng chung. |
| 4 | `Task.Run` fire-and-forget — vẽ thế nào? | **Vẽ tuần tự bình thường**, gắn cờ `[best-effort]` ở message 2 + note N1/N4 | Generator không có khung `par`/async; quy tắc đã chốt: "fire-and-forget → vẽ thật, gắn cờ `[best-effort]`". |
| 5 | Luồng lặp mỗi câu trả lời | **Vẽ 1 lần đại diện + note N2** | Đúng khuyến nghị RECON; vẽ N lần không thêm thông tin. |
| 6 | 3 nhánh lỗi (A, B, C) | **Để riêng, không gộp** | Khác HTTP status (403 / 200 / 400) và khác hệ quả (từ chối / trả bản cũ / không chấm được). |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `10`)** NO human actor on purpose: this whole page is a fire-and-forget background task (Task.Run, SessionService.cs:1722). The candidate already got their next question and never waits for, sees, or is notified about any of this. The leftmost lifeline is the service that starts the task. *(gỡ từ note trên diagram)*
- **(cạnh message `B.1`)** This flow runs ONCE PER ANSWER (including Skip - an empty answer is graded 0 "No answer provided." by ai-service), not once per session. Drawn once as a representative run. *(gỡ từ note trên diagram)*
- **(cạnh message `B.3`)** Both cross-service hops bypass the API Gateway and use the service port directly: InterviewAPI -> EvaluationAPI :7006 (InterviewAPI/Program.cs:50, 89-95) and EvaluationAPI -> InterviewAPI :7005 (EvaluationAPI/Program.cs:49-55). The bearer token is forwarded by hand on each call because Evaluation tables carry no UserId of their own. *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| `uc_id` của chức năng | RECON không ghi UC-id cho #17 | Nhóm xác nhận ê-líp use-case rồi điền `uc_id` vào `specs/evaluateinterviewanswer.yaml` | luồng 1 |
