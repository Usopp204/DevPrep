# FLOW — `ViewEvaluationReport` · BL#74, 76, 68, 69 · UC-`chưa có`

> Đặt tại `docs/diagrams/FLOWS/ViewEvaluationReport.md`
> Trạng thái: `DRAFT` — Khảo sát: lô D, 2026-08-03

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Mở trang báo cáo đánh giá của một phiên đã kết thúc: lấy transcript (ngữ cảnh + danh sách câu hỏi), lấy báo cáo tổng hợp (sinh mới nếu chưa có), lấy điểm từng câu, rồi ghép lại thành 1 trang |
| Điểm vào (user thao tác gì) | Mở link `GET /Evaluation/Report?sessionId=…&durationActualSec=…` — phát ra từ cuối phiên phỏng vấn (`#9 EndInterviewSession` message D-a.2) và từ Interview History |
| Kết thúc (user thấy gì) | Trang báo cáo: điểm tổng + Grade, điểm theo 3 trục rubric, strengths/improvements, danh sách câu hỏi kèm điểm và nhận xét AI. Phiên chưa `Completed` hoặc chưa có đánh giá → vẫn render trang, kèm `EmptyReason` |
| Quy mô | `16` participant · `111` message · `40` class · `6` alt / `3` opt · trang Sequence 3490×5027 · trang Class 7250×1282 |
| Đối chiếu ước lượng RECON | RECON đoán `20-24` · thật `111` · hệ số `5.0×` |
| Service ngoài quyền sở hữu | ApiGateway (chỉ định tuyến 4 request Browser→WebClient→API) |
| **Gọi BYPASS Gateway?** | **CÓ — mọi hop EvaluationAPI → InterviewAPI đi thẳng `:7005`** (`EvaluationAPI/Program.cs:49-55`, `BaseAddress = INTERVIEW_API_BASEURL`). 5 lần trong luồng này (3× `/owns` + 2× `/feedback-eligibility`). 4 request từ WebClient thì **có** qua Gateway |
| Nhà cung cấp AI | **Gemini** (`evaluator.aggregate` → `_summary_prompt`) — **chỉ ở nhánh sinh báo cáo mới** |
| Ghi DB | `DevPrep_Evaluation`: INSERT `FeedbackReports` (1 hàng/phiên) + INSERT `PerformanceMetrics` (1-2 hàng) — chỉ khi sinh mới. Đọc: `FeedbackReports`, `AnswerEvaluations`. `DevPrep_Interview` chỉ ĐỌC (transcript, owns, eligibility) |
| Trừ credit | không |
| KI liên quan | **KI-08** (câu trả lời chấm lỗi im lặng → báo cáo thiếu câu đó, không phân biệt được với "chưa chấm") |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| Khối feedback trong action `Report`: `GetSessionFeedbackEligibilityAsync` + `GetMyInterviewFeedbackAsync` + `TempData` form | [WebClient/Controllers/Evaluation/EvaluationController.cs:30-56](../../../source/WebClient/Controllers/Evaluation/EvaluationController.cs#L30-L56) | Thuộc tính năng **Submit Interview Feedback** (RECON ghi rõ "ngoài phạm vi"). Chỉ quyết định có hiện form góp ý hay không, **không** ảnh hưởng nội dung báo cáo. Xem note N4 + §11 quyết định 1. |
| `EvaluationController.GetPerformance` (EvaluationAPI) + `GetPerformanceReportAsync` + 6 nhánh exception có kiểu | [EvaluationAPI/Controllers/EvaluationController.cs:106-147](../../../source/EvaluationAPI/Controllers/EvaluationController.cs#L106-L147) | Endpoint khác (báo cáo hiệu năng), **không** được trang Report gọi — đã grep `WebClient/Services/Evaluation/EvaluationService.cs`, chỉ có 4 lời gọi ở §4. |
| `AiBuildReportResponse.Categories` (thẻ điểm theo category do ai-service tính) | [EvaluationAPI/Services/EvaluationService.cs:116-136](../../../source/EvaluationAPI/Services/EvaluationService.cs#L116-L136) | EvaluationAPI **cố ý bỏ qua** và tự tính điểm theo 3 trục rubric từ `CriteriaScoresJson` (comment code giải thích: category thật là "React", "PostgreSQL"… nên tra theo tên luôn rơi về overall). Gộp 1 câu vào nhãn message `H-b.6`. |
| Nhánh lỗi hạ tầng ai-service (`EnsureSuccess` → `HttpRequestException`) | [EvaluationAPI/Clients/AiServiceClient.cs:67-81](../../../source/EvaluationAPI/Clients/AiServiceClient.cs#L67-L81) | Tiền lệ lô A/C/D: kết cục trùng khít nhánh `H-a` đã vẽ (400 → `emptyReason`, trang vẫn render). |
| Nội dung `ChatHistory` trong `TranscriptResponse` | [SessionService.cs:1570-1583](../../../source/InterviewAPI/Services/SessionService.cs#L1570-L1583) | Trang Report chỉ dùng `Questions` + `Position`/`Level`/`Status`; `ChatHistory` do `#4 ViewInterviewHistory` dùng. Vẫn ghi trong nhãn message 13 vì cùng 1 response. |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Link vào trang Report sau khi phiên kết thúc | `#9 EndInterviewSession` | message 1 (nhãn ghi rõ nguồn link) | interview-room.js:717-720 |
| Kiểm quyền sở hữu bên trong InterviewAPI (`Owns` → `SessionService.OwnsSessionAsync` → `SessionRepository.GetByIdAsync`) | hạ tầng IDOR dùng chung của EvaluationAPI (đã vẽ nguyên tắc ở `#17`) | **1 cặp call+ret** tới `:InterviewSessionController` — lặp 3 lần (msg 19-20, F.4-F.5, 25-26) | InterviewSessionController.cs:185-193, SessionService.cs:1629-1633 |
| Cổng "phiên đã Completed" (`feedback-eligibility` → `SessionService.GetSessionFeedbackEligibilityAsync` → `SessionRepository.GetFeedbackEligibilitySessionAsync`) | dùng chung với tính năng Submit Interview Feedback | **1 cặp call+ret** tới `:InterviewSessionController` — lặp 2 lần (msg C-b.3/C-b.4, G-b.3/G-b.4) | InterviewSessionController.cs:148-164, SessionService.cs:1527-1542 |
| Nguồn của các hàng `AnswerEvaluations` được đọc ở đây | `#17 EvaluateInterviewAnswer` | *(không vẽ message — chỉ note N3)* | EvaluationService.cs:68 |

---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 0 | User | actor | — | — |
| 1 | :Report Page UI | box | `WebClient/Views/Evaluation/Report.cshtml` | — |
| 2 | :EvaluationController (WebClient) | box | `WebClient/Controllers/Evaluation/EvaluationController.cs:20-59` | — |
| 3 | :EvaluationService (WebClient) | box | `WebClient/Services/Evaluation/EvaluationService.cs:35-169` | — |
| 4 | :API Gateway | box | `ApiGateway` (YARP :7000) | — |
| 5 | :InterviewSessionController (InterviewAPI) | box | `InterviewAPI/Controllers/InterviewSessionController.cs:148-193` | có (transcript) / **không (owns, eligibility — bypass :7005)** |
| 6 | :SessionService (InterviewAPI) | box | `InterviewAPI/Services/SessionService.cs:1548-1600` | có |
| 7 | :SessionRepository (InterviewAPI) | box | `InterviewAPI/Repositories/SessionRepository.cs:42-46` | có |
| 8 | :EvaluationController (EvaluationAPI) | box | `EvaluationAPI/Controllers/EvaluationController.cs:61-165` | có |
| 9 | :InterviewApiClient (EvaluationAPI) | box | `EvaluationAPI/Clients/InterviewApiClient.cs` | **không (bypass :7005)** |
| 10 | :EvaluationService (EvaluationAPI) | box | `EvaluationAPI/Services/EvaluationService.cs:85-252` | — |
| 11 | :EvaluationRepository (EvaluationAPI) | box | `EvaluationAPI/Repositories/EvaluationRepository.cs` | — |
| 12 | :AiServiceClient (EvaluationAPI) | box | `EvaluationAPI/Clients/AiServiceClient.cs:51-66` | — |
| 13 | :ai-service /internal/report | box | `ai-service/app/api/internal.py:115-129` → `evaluator.py:184-215` | — |
| 14 | SQL Server (DevPrep_Interview) | cylinder | `InterviewAPI/Models/InterviewDbContext.cs` | — |
| 15 | SQL Server (DevPrep_Evaluation) | cylinder | `EvaluationAPI/Data/EvaluationDbContext.cs` | — |

> Actor trực tiếp — user mở trang và chờ kết quả.

## 4. BẢNG MESSAGE

### Pha 1 — mở trang + lấy transcript

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Report Page UI | call | `1. Open the evaluation report for a session [see #9]` | — | interview-room.js:717-720 | boundary | 1. Open the evaluation report for a session (link from the end of the interview, #9 EndInterviewSession, or from Interview History) |
| 2 | :Report Page UI | :EvaluationController (WebClient) | call | `2. GET /Evaluation/Report?sessionId={id}&durationActualSec={n}` | — | WebClient/EvaluationController.cs:20 |  | — |
| 3 | :EvaluationController (WebClient) | :EvaluationService (WebClient) | call | `3. GetReportAsync(sessionId, durationActualSec, ct)` | — | WebClient/EvaluationController.cs:22 |  | — |
| 4 | :EvaluationService (WebClient) | :API Gateway | call | `4. GET api/interview/sessions/{sessionId}/transcript` | — | WebClient/EvaluationService.cs:40-43 |  | — |
| 5 | :API Gateway | :InterviewSessionController | call | `5. Forward (route interview-route, no prefix strip)` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 6 | :InterviewSessionController | (self) | call | `6. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | InterviewSessionController.cs:171 | self | — |
| 7 | :InterviewSessionController | :SessionService | call | `7. GetTranscriptAsync(userId, sessionId, ct)` | — | InterviewSessionController.cs:172 |  | — |
| 8 | :SessionService | :SessionRepository | call | `8. GetWithFullHistoryAsync(sessionId, ct)` | — | SessionService.cs:1551 |  | — |
| 9 | :SessionRepository | SQL Server (DevPrep_Interview) | call | `9. SELECT InterviewSession + SessionQuestions + ChatMessages` | — | SessionRepository.cs:42-46 |  | 9. SELECT InterviewSession + SessionQuestions (by OrderIndex) + ChatMessages (by SentAt) |
| 10 | SQL Server (DevPrep_Interview) | :SessionRepository | ret | `10. Return the session graph / null` | — | SessionRepository.cs:42-46 |  | — |
| 11 | :SessionRepository | :SessionService | ret | `11. Return InterviewSession?` | — | SessionRepository.cs:42-46 |  | — |
| A.1 | :SessionService | :InterviewSessionController | ret | `A.1. throw InvalidOperationException("Session not found.")` | A | SessionService.cs:1552-1553 | throw | — |
| A.2 | :InterviewSessionController | :API Gateway | ret | `A.2. Return 400 ApiResponse.Fail(ex.Message)` | A | InterviewSessionController.cs:175-178 |  | — |
| A.3 | :API Gateway | :EvaluationService (WebClient) | ret | `A.3. Return 400` | A | ApiGateway/appsettings.json:49-54 |  | — |
| A.4 | :EvaluationService (WebClient) | :EvaluationController (WebClient) | ret | `A.4. Return (false, null, "Session not found.")` | A | WebClient/EvaluationService.cs:44-45, 149-152 |  | — |
| A.5 | :EvaluationController (WebClient) | :Report Page UI | ret | `A.5. 302 RedirectToAction("Index", "Interview")` | A | WebClient/EvaluationController.cs:24-28 |  | A.5. TempData["ErrorMessage"] = error; 302 RedirectToAction("Index", "Interview") |
| A.6 | :Report Page UI | User | ret | `A.6. Interview dashboard with the error message` | A | WebClient/EvaluationController.cs:27 |  | — |
| 12 | :SessionService | (self) | call | `12. Keep only questions that have a ChatMessage` | — | SessionService.cs:1559-1583 | self | 12. Keep only questions that already have a ChatMessage (never-asked questions are hidden); flatten every ChatMessage into ChatHistory ordered by SentAt |
| 13 | :SessionService | :InterviewSessionController | ret | `13. Return TranscriptResponse` | — | SessionService.cs:1584-1600 |  | 13. Return TranscriptResponse{SessionId, Position, Level, Status, DurationMinutes, TotalQuestions, AnsweredQuestions, Questions[], ChatHistory[]} |
| 14 | :InterviewSessionController | :API Gateway | ret | `14. Return 200 ApiResponse.Ok(result)` | — | InterviewSessionController.cs:173 |  | — |
| 15 | :API Gateway | :EvaluationService (WebClient) | ret | `15. Return 200` | — | ApiGateway/appsettings.json:49-54 |  | — |
| B.1 | :EvaluationService (WebClient) | :EvaluationController (WebClient) | ret | `B.1. Return (true, ReportViewModel{EmptyReason})` | B | WebClient/EvaluationService.cs:47-68 |  | B.1. Return (true, ReportViewModel{Position, Level, Status, Questions, EmptyReason = "This interview was cancelled / expired, so no evaluation report was generated."}) - NO EvaluationAPI call is made at all |
| B.2 | :EvaluationController (WebClient) | :Report Page UI | ret | `B.2. Return View Report` | B | WebClient/EvaluationController.cs:58 |  | — |
| B.3 | :Report Page UI | User | ret | `B.3. Show report page with EmptyReason, no scores` | B | WebClient/EvaluationController.cs:58 |  | B.3. Report page explaining why there is no report; the question list is still shown, with no scores |

### Pha 2 — lấy báo cáo đã có (`GET .../report`)

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 16 | :EvaluationService (WebClient) | :API Gateway | call | `16. GET api/evaluation/sessions/{sessionId}/report` | — | WebClient/EvaluationService.cs:71-74 |  | — |
| 17 | :API Gateway | :EvaluationController (EvaluationAPI) | call | `17. Forward (route evaluation-route, no prefix strip)` | — | ApiGateway/appsettings.json:67-72 |  | — |
| 18 | :EvaluationController (EvaluationAPI) | :InterviewApiClient (EvaluationAPI) | call | `18. OwnsSessionAsync(sessionId, bearerToken)` | — | EvaluationAPI/EvaluationController.cs:32-36, 89 |  | 18. OwnsSessionAsync(sessionId, bearer token) - IDOR guard, runs on EVERY EvaluationAPI action |
| 19 | :InterviewApiClient (EvaluationAPI) | :InterviewSessionController | call | `19. GET api/interview/sessions/{id}/owns` | — | InterviewApiClient.cs:27-32, EvaluationAPI/Program.cs:49-55 | boundary | 19. GET api/interview/sessions/{sessionId}/owns - DIRECT to InterviewAPI :7005, NOT through the Gateway |
| 20 | :InterviewSessionController | :InterviewApiClient (EvaluationAPI) | ret | `20. Return 200 ApiResponse.Ok / 404 {error}` | — | InterviewSessionController.cs:185-193 | boundary | 20. 200 ApiResponse.Ok / 404 ApiResponse.Fail (resolved by SessionService.OwnsSessionAsync + SessionRepository.GetByIdAsync) |
| 21 | :InterviewApiClient (EvaluationAPI) | :EvaluationController (EvaluationAPI) | ret | `21. Return bool` | — | InterviewApiClient.cs:33-43 |  | 21. Return bool - any transport error is logged and also returns false (fail closed) |
| C-a.1 | :EvaluationController (EvaluationAPI) | :API Gateway | ret | `C-a.1. Return 403 {error}` | C-a | EvaluationAPI/EvaluationController.cs:38-39, 90 |  | C-a.1. Return 403 ApiResponse.Fail("You do not have access to this session.") |
| C-a.2 | :API Gateway | :EvaluationService (WebClient) | ret | `C-a.2. Return 403` | C-a | WebClient/EvaluationService.cs:149-152, 76 |  | C-a.2. Return 403 - report stays null, the flow continues to the build attempt |
| C-b.1 | :EvaluationController (EvaluationAPI) | :EvaluationService (EvaluationAPI) | call | `C-b.1. GetExistingReportAsync(sessionId, bearerToken, ct)` | C-b | EvaluationAPI/EvaluationController.cs:92-95 |  | — |
| C-b.2 | :EvaluationService (EvaluationAPI) | :InterviewApiClient (EvaluationAPI) | call | `C-b.2. GetSessionFeedbackEligibilityAsync(...)` | C-b | EvaluationAPI/EvaluationService.cs:205, 244-252 |  | C-b.2. GetSessionFeedbackEligibilityAsync(sessionId, bearerToken, ct) - EnsureCompletedSessionAsync gate |
| C-b.3 | :InterviewApiClient (EvaluationAPI) | :InterviewSessionController | call | `C-b.3. GET api/interview/sessions/{id}/feedback-eligibility` | C-b | InterviewApiClient.cs:53-60 | boundary | C-b.3. GET api/interview/sessions/{sessionId}/feedback-eligibility - DIRECT to InterviewAPI :7005 |
| C-b.4 | :InterviewSessionController | :InterviewApiClient (EvaluationAPI) | ret | `C-b.4. Return 200 SessionFeedbackEligibilityResponse` | C-b | InterviewSessionController.cs:148-164, SessionService.cs:1527-1542 | boundary | C-b.4. 200 SessionFeedbackEligibilityResponse{SessionId, Status, Eligible = Status == "Completed"} |
| C-b.5 | :InterviewApiClient (EvaluationAPI) | :EvaluationService (EvaluationAPI) | ret | `C-b.5. Return SessionFeedbackEligibilityResult` | C-b | InterviewApiClient.cs:61 |  | — |
| D-a.1 | :EvaluationService (EvaluationAPI) | :EvaluationController (EvaluationAPI) | ret | `D-a.1. throw SessionNotEligibleForPerformanceReportException` | D-a | EvaluationAPI/EvaluationService.cs:250-251 | throw | D-a.1. throw SessionNotEligibleForPerformanceReportException(status) |
| D-a.2 | :EvaluationController (EvaluationAPI) | :API Gateway | ret | `D-a.2. Return 400 {error}` | D-a | EvaluationAPI/EvaluationController.cs:100-103 |  | D-a.2. Return 400 ApiResponse.Fail(ex.Message) - the generic catch, not the typed 409 used by GetPerformance |
| D-a.3 | :API Gateway | :EvaluationService (WebClient) | ret | `D-a.3. Return 400 - report stays null` | D-a | WebClient/EvaluationService.cs:149-152 |  | — |
| D-b.1 | :EvaluationService (EvaluationAPI) | :EvaluationRepository (EvaluationAPI) | call | `D-b.1. GetReportBySessionAsync(sessionId, ct)` | D-b | EvaluationAPI/EvaluationService.cs:206 |  | — |
| D-b.2 | :EvaluationRepository (EvaluationAPI) | SQL Server (DevPrep_Evaluation) | call | `D-b.2. SELECT FeedbackReport WHERE SessionId` | D-b | EvaluationRepository.cs:53-56 |  | — |
| D-b.3 | SQL Server (DevPrep_Evaluation) | :EvaluationRepository (EvaluationAPI) | ret | `D-b.3. Return row / null` | D-b | EvaluationRepository.cs:53-56 |  | — |
| D-b.4 | :EvaluationRepository (EvaluationAPI) | :EvaluationService (EvaluationAPI) | ret | `D-b.4. Return FeedbackReport?` | D-b | EvaluationRepository.cs:53-56 |  | — |
| E-a.1 | :EvaluationService (EvaluationAPI) | :EvaluationController (EvaluationAPI) | ret | `E-a.1. Return null` | E-a | EvaluationAPI/EvaluationService.cs:207 |  | — |
| E-a.2 | :EvaluationController (EvaluationAPI) | :API Gateway | ret | `E-a.2. Return 404 {error}` | E-a | EvaluationAPI/EvaluationController.cs:96-97 |  | E-a.2. Return 404 ApiResponse.Fail("No report found for this session.") |
| E-a.3 | :API Gateway | :EvaluationService (WebClient) | ret | `E-a.3. Return 404 - report stays null` | E-a | WebClient/EvaluationService.cs:149-152, 76 |  | — |
| E-b.1 | :EvaluationService (EvaluationAPI) | :EvaluationController (EvaluationAPI) | ret | `E-b.1. Return ReportResponse` | E-b | EvaluationAPI/EvaluationService.cs:254-270 |  | E-b.1. Return ReportResponse{ReportId, OverallScore, TechnicalScore, TheoryScore, CommunicationScore, ProblemSolvingScore, Summary, Strengths, Improvements, Recommendations, Grade, GeneratedAt} |
| E-b.2 | :EvaluationController (EvaluationAPI) | :API Gateway | ret | `E-b.2. Return 200 ApiResponse.Ok(result)` | E-b | EvaluationAPI/EvaluationController.cs:98 |  | — |
| E-b.3 | :API Gateway | :EvaluationService (WebClient) | ret | `E-b.3. Return 200` | E-b | WebClient/EvaluationService.cs:76 |  | E-b.3. Return 200 - report = the existing report, the build step below is skipped |

### Pha 3 — sinh báo cáo mới (`POST .../report`, chỉ khi chưa có)

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| F.1 | :EvaluationService (WebClient) | :API Gateway | call | `F.1. POST api/evaluation/sessions/{id}/report` | F | WebClient/EvaluationService.cs:81-84 |  | F.1. POST api/evaluation/sessions/{sessionId}/report { durationActualSec } |
| F.2 | :API Gateway | :EvaluationController (EvaluationAPI) | call | `F.2. Forward (route evaluation-route, no prefix strip)` | F | ApiGateway/appsettings.json:67-72 |  | — |
| F.3 | :EvaluationController (EvaluationAPI) | :InterviewApiClient (EvaluationAPI) | call | `F.3. OwnsSessionAsync(sessionId, bearer token) - IDOR guard` | F | EvaluationAPI/EvaluationController.cs:67 |  | — |
| F.4 | :InterviewApiClient (EvaluationAPI) | :InterviewSessionController | call | `F.4. GET api/interview/sessions/{id}/owns` | F | InterviewApiClient.cs:27-32 | boundary | F.4. GET api/interview/sessions/{sessionId}/owns - DIRECT to InterviewAPI :7005 |
| F.5 | :InterviewSessionController | :InterviewApiClient (EvaluationAPI) | ret | `F.5. 200 / 404` | F | InterviewSessionController.cs:185-193 | boundary | — |
| F.6 | :InterviewApiClient (EvaluationAPI) | :EvaluationController (EvaluationAPI) | ret | `F.6. Return bool` | F | InterviewApiClient.cs:33-43 |  | — |
| G-a.1 | :EvaluationController (EvaluationAPI) | :API Gateway | ret | `G-a.1. Return 403 {error}` | G-a | EvaluationAPI/EvaluationController.cs:67-68 |  | G-a.1. Return 403 ApiResponse.Fail("You do not have access to this session.") |
| G-a.2 | :API Gateway | :EvaluationService (WebClient) | ret | `G-a.2. Return 403` | G-a | WebClient/EvaluationService.cs:86-95 |  | G-a.2. Return 403 -> emptyReason = the 403 message; the page still renders |
| G-b.1 | :EvaluationController (EvaluationAPI) | :EvaluationService (EvaluationAPI) | call | `G-b.1. BuildReportAsync(sessionId, req, bearerToken, ct)` | G-b | EvaluationAPI/EvaluationController.cs:70-74 |  | — |
| G-b.2 | :EvaluationService (EvaluationAPI) | :InterviewApiClient (EvaluationAPI) | call | `G-b.2. GetSessionFeedbackEligibilityAsync(...)` | G-b | EvaluationAPI/EvaluationService.cs:88, 244-252 |  | G-b.2. GetSessionFeedbackEligibilityAsync(...) - EnsureCompletedSessionAsync gate |
| G-b.3 | :InterviewApiClient (EvaluationAPI) | :InterviewSessionController | call | `G-b.3. GET api/interview/sessions/{id}/feedback-eligibility` | G-b | InterviewApiClient.cs:53-60 | boundary | G-b.3. GET api/interview/sessions/{sessionId}/feedback-eligibility - DIRECT to InterviewAPI :7005 |
| G-b.4 | :InterviewSessionController | :InterviewApiClient (EvaluationAPI) | ret | `G-b.4. 200 SessionFeedbackEligibilityResponse{Status, Eligible}` | G-b | InterviewSessionController.cs:148-164 | boundary | — |
| G-b.5 | :InterviewApiClient (EvaluationAPI) | :EvaluationService (EvaluationAPI) | ret | `G-b.5. Return SessionFeedbackEligibilityResult` | G-b | InterviewApiClient.cs:61 |  | — |
| G-b.6 | :EvaluationService (EvaluationAPI) | :EvaluationRepository (EvaluationAPI) | call | `G-b.6. ReportExistsAsync(sessionId, ct)` | G-b | EvaluationAPI/EvaluationService.cs:91 |  | — |
| G-b.7 | :EvaluationRepository (EvaluationAPI) | SQL Server (DevPrep_Evaluation) | call | `G-b.7. SELECT EXISTS FeedbackReports WHERE SessionId` | G-b | EvaluationRepository.cs:50-51 |  | — |
| G-b.8 | SQL Server (DevPrep_Evaluation) | :EvaluationRepository (EvaluationAPI) | ret | `G-b.8. Return bool` | G-b | EvaluationRepository.cs:50-51 |  | — |
| G-b.9 | :EvaluationRepository (EvaluationAPI) | :EvaluationService (EvaluationAPI) | ret | `G-b.9. Return bool` | G-b | EvaluationRepository.cs:50-51 |  | — |
| G-b.10 | :EvaluationService (EvaluationAPI) | :EvaluationRepository (EvaluationAPI) | call | `G-b.10. GetEvaluationsBySessionAsync(sessionId, ct)` | G-b | EvaluationAPI/EvaluationService.cs:95 |  | — |
| G-b.11 | :EvaluationRepository (EvaluationAPI) | SQL Server (DevPrep_Evaluation) | call | `G-b.11. SELECT AnswerEvaluations WHERE SessionId` | G-b | EvaluationRepository.cs:31-35 |  | G-b.11. SELECT AnswerEvaluations WHERE SessionId ORDER BY GeneratedAt |
| G-b.12 | SQL Server (DevPrep_Evaluation) | :EvaluationRepository (EvaluationAPI) | ret | `G-b.12. Return rows` | G-b | EvaluationRepository.cs:31-35 |  | — |
| G-b.13 | :EvaluationRepository (EvaluationAPI) | :EvaluationService (EvaluationAPI) | ret | `G-b.13. Return List<AnswerEvaluation>` | G-b | EvaluationRepository.cs:31-35 |  | — |
| H-a.1 | :EvaluationService (EvaluationAPI) | :EvaluationController (EvaluationAPI) | ret | `H-a.1. throw SessionNotEligibleForPerformanceReportException` | H-a | EvaluationAPI/EvaluationService.cs:88, 91-92, 96-97 | throw | H-a.1. throw SessionNotEligibleForPerformanceReportException / InvalidOperationException("Report already generated." / "No evaluations found for this session.") |
| H-a.2 | :EvaluationController (EvaluationAPI) | :API Gateway | ret | `H-a.2. Return 400 ApiResponse.Fail(ex.Message)` | H-a | EvaluationAPI/EvaluationController.cs:77-80 |  | — |
| H-a.3 | :API Gateway | :EvaluationService (WebClient) | ret | `H-a.3. Return 400 {error}` | H-a | WebClient/EvaluationService.cs:86-95 |  | H-a.3. Return 400 -> emptyReason = ex.Message; the page still renders with the transcript only |
| H-b.1 | :EvaluationService (EvaluationAPI) | :AiServiceClient (EvaluationAPI) | call | `H-b.1. BuildReportAsync(AiBuildReportRequest)` | H-b | EvaluationAPI/EvaluationService.cs:100-114 |  | H-b.1. BuildReportAsync(AiBuildReportRequest{duration_actual_sec, evaluations[{category, score, criteria}]}) |
| H-b.2 | :AiServiceClient (EvaluationAPI) | :ai-service | call | `H-b.2. POST /internal/report (header X-Internal-Api-Key)` | H-b | EvaluationAPI/Clients/AiServiceClient.cs:51-58 |  | — |
| H-b.3 | :ai-service | (self) | call | `H-b.3. aggregate(); GeminiJudge summarizes the summary` | H-b | internal.py:115-129, evaluator.py:184-215 | self | H-b.3. aggregate(): overall = mean of the per-answer scores; category cards grouped by category; GeminiJudge summarizes strengths / areas_to_improve; praise is a canned band string |
| H-b.4 | :ai-service | :AiServiceClient (EvaluationAPI) | ret | `H-b.4. Return 200 {overall_score, categories, summary, duration}` | H-b | evaluator.py:209-215, 220-224 |  | H-b.4. 200 {overall_score, num_questions, categories[], summary{strengths[], areas_to_improve[], praise}, duration} |
| H-b.5 | :AiServiceClient (EvaluationAPI) | :EvaluationService (EvaluationAPI) | ret | `H-b.5. Return AiBuildReportResponse` | H-b | EvaluationAPI/Clients/AiServiceClient.cs:59-60 |  | — |
| H-b.6 | :EvaluationService (EvaluationAPI) | (self) | call | `H-b.6. Average the criteria; Grade = A/B/C/D; build the rows` | H-b | EvaluationAPI/EvaluationService.cs:116-180 | self | H-b.6. Average knowledge / communication / problem_solving across every CriteriaScoresJson (the ai-service category cards are deliberately ignored); Grade = A/B/C/D by overall score; build FeedbackReport + PerformanceMetric rows |
| H-b.7 | :EvaluationService (EvaluationAPI) | :EvaluationRepository (EvaluationAPI) | call | `H-b.7. AddReportAsync(report, ct)` | H-b | EvaluationAPI/EvaluationService.cs:155 |  | — |
| H-b.8 | :EvaluationRepository (EvaluationAPI) | SQL Server (DevPrep_Evaluation) | call | `H-b.8. INSERT FeedbackReport (SaveChangesAsync)` | H-b | EvaluationRepository.cs:37-42 |  | — |
| H-b.9 | SQL Server (DevPrep_Evaluation) | :EvaluationRepository (EvaluationAPI) | ret | `H-b.9. Return rows affected` | H-b | EvaluationRepository.cs:40 |  | — |
| H-b.10 | :EvaluationRepository (EvaluationAPI) | :EvaluationService (EvaluationAPI) | ret | `H-b.10. Return FeedbackReport` | H-b | EvaluationRepository.cs:41 |  | — |
| H-b.11 | :EvaluationService (EvaluationAPI) | :EvaluationRepository (EvaluationAPI) | call | `H-b.11. AddMetricsAsync(metrics, ct)` | H-b | EvaluationAPI/EvaluationService.cs:180 |  | — |
| H-b.12 | :EvaluationRepository (EvaluationAPI) | SQL Server (DevPrep_Evaluation) | call | `H-b.12. INSERT PerformanceMetrics (OverallScore, TechnicalScore)` | H-b | EvaluationRepository.cs:44-48 |  | H-b.12. INSERT PerformanceMetrics (OverallScore, plus TechnicalScore when it is not null) |
| H-b.13 | SQL Server (DevPrep_Evaluation) | :EvaluationRepository (EvaluationAPI) | ret | `H-b.13. Return rows affected` | H-b | EvaluationRepository.cs:47 |  | — |
| H-b.14 | :EvaluationRepository (EvaluationAPI) | :EvaluationService (EvaluationAPI) | ret | `H-b.14. Return` | H-b | EvaluationRepository.cs:47 |  | — |
| H-b.15 | :EvaluationService (EvaluationAPI) | :EvaluationController (EvaluationAPI) | ret | `H-b.15. Return ReportResponse` | H-b | EvaluationAPI/EvaluationService.cs:183-198 |  | H-b.15. Return ReportResponse{ReportId, OverallScore, TechnicalScore, TheoryScore = null, CommunicationScore, ProblemSolvingScore, Summary, Strengths, Improvements, Recommendations, Grade, GeneratedAt} |
| H-b.16 | :EvaluationController (EvaluationAPI) | :API Gateway | ret | `H-b.16. Return 200 ApiResponse.Ok(result)` | H-b | EvaluationAPI/EvaluationController.cs:75 |  | — |
| H-b.17 | :API Gateway | :EvaluationService (WebClient) | ret | `H-b.17. Return 200 -> report = the freshly built report` | H-b | WebClient/EvaluationService.cs:86-89 |  | — |

### Pha 4 — lấy điểm từng câu + dựng trang

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 22 | :EvaluationService (WebClient) | :API Gateway | call | `22. GET api/evaluation/sessions/{sessionId}/answers` | — | WebClient/EvaluationService.cs:98-101 |  | — |
| 23 | :API Gateway | :EvaluationController (EvaluationAPI) | call | `23. Forward (route evaluation-route, no prefix strip)` | — | ApiGateway/appsettings.json:67-72 |  | — |
| 24 | :EvaluationController (EvaluationAPI) | :InterviewApiClient (EvaluationAPI) | call | `24. OwnsSessionAsync(sessionId, bearer token) - IDOR guard` | — | EvaluationAPI/EvaluationController.cs:155 |  | — |
| 25 | :InterviewApiClient (EvaluationAPI) | :InterviewSessionController | call | `25. GET api/interview/sessions/{id}/owns` | — | InterviewApiClient.cs:27-32 | boundary | 25. GET api/interview/sessions/{sessionId}/owns - DIRECT to InterviewAPI :7005 |
| 26 | :InterviewSessionController | :InterviewApiClient (EvaluationAPI) | ret | `26. 200 / 404` | — | InterviewSessionController.cs:185-193 | boundary | — |
| 27 | :InterviewApiClient (EvaluationAPI) | :EvaluationController (EvaluationAPI) | ret | `27. Return bool` | — | InterviewApiClient.cs:33-43 |  | — |
| J-a.1 | :EvaluationController (EvaluationAPI) | :API Gateway | ret | `J-a.1. Return 403 {error}` | J-a | EvaluationAPI/EvaluationController.cs:155-156 |  | J-a.1. Return 403 ApiResponse.Fail("You do not have access to this session.") |
| J-a.2 | :API Gateway | :EvaluationService (WebClient) | ret | `J-a.2. Return 403` | J-a | WebClient/EvaluationService.cs:102-103 |  | J-a.2. Return 403 - the per-question score list stays empty; the page still renders |
| J-b.1 | :EvaluationController (EvaluationAPI) | :EvaluationService (EvaluationAPI) | call | `J-b.1. GetAnswerEvaluationsAsync(sessionId, ct)` | J-b | EvaluationAPI/EvaluationController.cs:158 |  | J-b.1. GetAnswerEvaluationsAsync(sessionId, ct) - NOTE: this action has NO Completed-session gate |
| J-b.2 | :EvaluationService (EvaluationAPI) | :EvaluationRepository (EvaluationAPI) | call | `J-b.2. GetEvaluationsBySessionAsync(sessionId, ct)` | J-b | EvaluationAPI/EvaluationService.cs:229 |  | — |
| J-b.3 | :EvaluationRepository (EvaluationAPI) | SQL Server (DevPrep_Evaluation) | call | `J-b.3. SELECT AnswerEvaluations WHERE SessionId` | J-b | EvaluationRepository.cs:31-35 |  | J-b.3. SELECT AnswerEvaluations WHERE SessionId ORDER BY GeneratedAt |
| J-b.4 | SQL Server (DevPrep_Evaluation) | :EvaluationRepository (EvaluationAPI) | ret | `J-b.4. Return rows` | J-b | EvaluationRepository.cs:31-35 |  | — |
| J-b.5 | :EvaluationRepository (EvaluationAPI) | :EvaluationService (EvaluationAPI) | ret | `J-b.5. Return List<AnswerEvaluation>` | J-b | EvaluationRepository.cs:31-35 |  | — |
| J-b.6 | :EvaluationService (EvaluationAPI) | :EvaluationController (EvaluationAPI) | ret | `J-b.6. Return List<EvaluateAnswerResponse>` | J-b | EvaluationAPI/EvaluationService.cs:230-241 |  | — |
| J-b.7 | :EvaluationController (EvaluationAPI) | :API Gateway | ret | `J-b.7. Return 200 ApiResponse.Ok(result)` | J-b | EvaluationAPI/EvaluationController.cs:159 |  | — |
| J-b.8 | :API Gateway | :EvaluationService (WebClient) | ret | `J-b.8. Return 200` | J-b | WebClient/EvaluationService.cs:98-103 |  | — |
| 28 | :EvaluationService (WebClient) | (self) | call | `28. Join the evaluations onto the ordered transcript questions` | — | WebClient/EvaluationService.cs:102-130 | self | 28. Index the evaluations by SessionQuestionId and join them onto the ordered transcript questions (Score / AiFeedback / Strengths per question); EmptyReason is kept only when report is still null |
| 29 | :EvaluationService (WebClient) | :EvaluationController (WebClient) | ret | `29. Return (true, ReportViewModel)` | — | WebClient/EvaluationService.cs:121-131 |  | 29. Return (true, ReportViewModel{SessionId, Position, Level, Status, Report, Questions[], EmptyReason}) |
| 30 | :EvaluationController (WebClient) | :Report Page UI | ret | `30. Return View Report` | — | WebClient/EvaluationController.cs:58 |  | — |
| 31 | :Report Page UI | User | ret | `31. Show report page: score, Grade, per-question list` | — | WebClient/EvaluationController.cs:58 |  | 31. Report page: overall score + Grade, the three rubric axis scores, strengths / improvements, and the per-question list with scores and AI feedback |

**Cờ:** `self` · `throw` · `boundary`

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| A | opt | `[session is null OR session.UserId != userId]` | A.1 → A.6 | — | — |
| B | opt | `[transcript.Status != "Completed" - Cancelled, Expired, or still running]` | B.1 → B.3 | — | 2 (Cancelled/Expired: cùng 200, chỉ khác 1 câu trong `EmptyReason` — ghi cả 2 trong nhãn) |
| C | alt | C-a: `[the caller does not own the session]` · C-b: `[the caller owns the session]` | C-a.1 → E-b.3 | — | — |
| D | alt | D-a: `[eligibility.Status != "Completed" OR !eligibility.Eligible]` · D-b: `[Completed and eligible]` | D-a.1 → E-b.3 | C-b | — |
| E | alt | E-a: `[no FeedbackReport row yet]` · E-b: `[a FeedbackReport row already exists]` | E-a.1 → E-b.3 | D-b | — |
| F | opt | `[report is still null - the GET above returned nothing usable]` | F.1 → H-b.17 | — | — |
| G | alt | G-a: `[the caller does not own the session]` · G-b: `[the caller owns the session]` | G-a.1 → H-b.17 | F | — |
| H | alt | H-a: `[not eligible OR a report already exists OR there are 0 evaluations]` · H-b: `[eligible, no report yet, at least 1 evaluation]` | H-a.1 → H-b.17 | G-b | **3** (cùng HTTP 400 **và** cùng kết cục user thấy: `emptyReason` + trang vẫn render) |
| J | alt | J-a: `[the caller does not own the session]` · J-b: `[the caller owns the session]` | J-a.1 → J-b.8 | — | — |

> `F` là `opt` **tự khép kín** (chỉ mở/đóng các call của chính nó) → luồng vẫn chạy tiếp vào
> message 22 dù guard đúng hay sai.

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | 18-21 | `The IDOR guard runs on EVERY EvaluationAPI action (3 times on this page) and always calls InterviewAPI :7005 DIRECTLY, never through the Gateway - Evaluation tables carry no UserId of their own, so ownership can only be answered by InterviewAPI. Any transport error also returns false (fail closed).` |
| N2 | C-b.2 / G-b.2 | `EnsureCompletedSessionAsync reuses the FEEDBACK eligibility endpoint as a "session is Completed" gate (Eligible = Status == "Completed"). GetAnswerEvaluations deliberately has no such gate, which is why per-question scores can still load when the report itself is refused.` |
| N3 | J-b.3 | `The AnswerEvaluations rows read here are written one-by-one during the interview by #17 EvaluateInterviewAnswer. KI-08: an answer whose background grading failed is simply missing - the page cannot tell "not graded yet" from "grading failed", it only shows a question with no score.` |
| N4 | 29-31 | `After GetReportAsync returns, the Report action also loads the interview-feedback form state (eligibility + already-submitted feedback). That belongs to the Submit Interview Feedback feature and is intentionally not drawn - it never changes the report content itself.` |
| N5 | F | `The report is built LAZILY on the first view: nothing generates it when the interview ends. durationActualSec comes from the query string the Room appends when it redirects here.` |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | Liệt kê | Nguồn |
|---|---|---|---|---|
| 1 | EvaluationController (WebClient) | class | SUBSET (`Report`) | local |
| 2 | Controller | framework-base | FULL (rỗng) | SHARED |
| 3 | IEvaluationService (WebClient) | interface | FULL | local |
| 4 | EvaluationService (WebClient) | class | SUBSET | local |
| 5 | IApiClient / 6 ApiClient | interface/class | FULL | SHARED |
| 7 | ReportViewModel | DTO | FULL | local |
| 8 | ReportQuestionViewModel | DTO | FULL | local |
| 9 | ApiResponse<T> (WebClient) | DTO | FULL | local |
| 10 | ControllerBase | framework-base | FULL (rỗng) | SHARED |
| 11 | InterviewSessionController (InterviewAPI) | class | SUBSET (`GetTranscript`, `Owns`, `GetSessionFeedbackEligibility`) | local |
| 12 | ISessionService | interface | SUBSET | local |
| 13 | SessionService (InterviewAPI) | class | SUBSET | local |
| 14 | ISessionRepository / 15 SessionRepository (InterviewAPI) | interface/class | SUBSET | local |
| 16 | TranscriptResponse / 17 TranscriptQuestionDto | DTO | FULL | local |
| 18 | SessionFeedbackEligibilityResponse | DTO | FULL | local |
| 19 | ApiResponse<T> (InterviewAPI) | DTO | FULL | SHARED |
| 20 | EvaluationController (EvaluationAPI) | class | SUBSET | local |
| 21 | IEvaluationService (EvaluationAPI) | interface | SUBSET | local |
| 22 | EvaluationService (EvaluationAPI) | class | SUBSET | local |
| 23 | ApiResponse<T> (EvaluationAPI) | DTO | FULL | SHARED |
| 24 | ReportResponse / 25 BuildReportRequest / 26 EvaluateAnswerResponse | DTO | FULL | local |
| 27 | IInterviewApiClient / 28 InterviewApiClient (EvaluationAPI) | interface/class | FULL / SUBSET | local |
| 29 | SessionFeedbackEligibilityResult | DTO | FULL | local |
| 30 | IAiServiceClient (EvaluationAPI) / 31 AiServiceClient (EvaluationAPI) | interface/class | FULL / SUBSET | local |
| 32 | AiBuildReportRequest / 33 AiBuildReportResponse | DTO | FULL | local |
| 34 | IEvaluationRepository / 35 EvaluationRepository (EvaluationAPI) | interface/class | FULL | local |
| 36 | FeedbackReport / 37 PerformanceMetric / 38 AnswerEvaluation | entity | FULL | local |
| 39 | EvaluationDbContext | class | FULL | local |
| 40 | DbContext | framework-base | FULL (rỗng) | SHARED |

## 8. CLASS — chi tiết

### `EvaluationService (WebClient)` — `SUBSET`
- Field `- JsonOptions`, `- apiClient: IApiClient`
- Method `+ GetReportAsync(...)`, `- SendAsync<T>(...)`, `- ReadError(raw: string): string?`
- *(SUBSET)* Đã bỏ `GetOverview()`.

### `SessionService (InterviewAPI)` — `SUBSET`
- 10 field DI; method `+ GetTranscriptAsync(...)`, `+ OwnsSessionAsync(...)`,
  `+ GetSessionFeedbackEligibilityAsync(...)`

### `EvaluationService (EvaluationAPI)` — `SUBSET`
- 3 field DI; method `+ BuildReportAsync(...)`, `+ GetExistingReportAsync(...)`,
  `+ GetAnswerEvaluationsAsync(...)`, `- EnsureCompletedSessionAsync(...)`,
  `- ToReportResponse(report: FeedbackReport): ReportResponse`

Không có class SHARED mới trong chức năng này.

## 9. QUAN HỆ CLASS

Xem `specs/viewevaluationreport_classes.yaml` khoá `relations:`.

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| KI-08 | `SessionService.cs:1708-1737` (ghi) ↔ `WebClient/EvaluationService.cs:102-130` (đọc) | Câu trả lời chấm nền lỗi → không có hàng `AnswerEvaluations` → trang hiện câu hỏi **không điểm**, không phân biệt được "chưa chấm" / "chấm lỗi" | fire-and-forget | **Vẽ thật** + note N3 | Có (đã tồn tại) |

> Đã kiểm và **bác** 3 nghi vấn:
> (a) `FeedbackReport.Summary = aiResponse.Summary?.Praise` — `evaluator.aggregate` **có** trả
> `summary.praise` (chuỗi theo dải điểm, `evaluator.py:176-181, 214`) → không phải luôn rỗng.
> (b) `AiBuildReportResponse.Categories` không được dùng — **cố ý**, comment
> `EvaluationService.cs:118-120` giải thích rõ; EvaluationAPI tự tính 3 trục rubric.
> (c) `Category = e.QuestionContent` khi dựng `AiEvalRow` (`EvaluationService.cs:105`) — truyền
> nguyên văn câu hỏi vào trường `category`. Vô hại vì `categories` đầu ra bị bỏ qua (điểm b);
> chỉ khiến `by_cat` bên ai-service gom mỗi câu thành 1 nhóm. Không đổi kết quả người dùng thấy →
> ghi lại ở đây, **không** lập KI.


### Gỡ từ note trên diagram — anomaly

- **(cạnh message `A.1`)** The AnswerEvaluations rows read here are written one-by-one during the interview by #17 EvaluateInterviewAnswer. KI-08: an answer whose background grading failed is simply missing - the page cannot tell "not graded yet" from "grading failed", it only shows a question with no score. *(gỡ từ note trên diagram)*

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | Khối feedback trong action `Report` (eligibility + my-feedback + TempData form) | **Không vẽ (§1) + note N4** | RECON ghi rõ thuộc tính năng Submit Interview Feedback, ngoài phạm vi; nó chỉ bật/tắt form góp ý, không đổi nội dung báo cáo. Vẽ vào sẽ phải thêm 2 participant và ~12 message cho một tính năng khác. |
| 2 | 5 lần gọi chéo EvaluationAPI → InterviewAPI (3× `/owns`, 2× `/feedback-eligibility`) | **Mỗi lần thu về đúng 1 cặp call+ret tới `:InterviewSessionController`** (chuỗi nội bộ `SessionService` + `SessionRepository` + `DevPrep_Interview` ghi ở §2), giống cách đã làm ở `#17` | Vẽ đầy đủ 5 lần sẽ thêm ~40 message và buộc `:SessionService (InterviewAPI)` nhận mũi tên vòng ngược từ phía phải. Cơ chế BOUNDARY đã được duyệt cho đúng tình huống này. |
| 3 | 3 nhánh lỗi của `BuildReport` (chưa Completed / đã có báo cáo / 0 đánh giá) | **Gộp thành 1 section `H-a`, guard ghép OR** | Thoả cả hai điều kiện gộp: cùng HTTP 400 **và** cùng kết cục user thấy (`emptyReason` + trang vẫn render với transcript). Nhãn `H-a.1` liệt kê đủ 3 exception. |
| 4 | 2 lý do `EmptyReason` ở `opt B` (Cancelled vs Expired) | **Gộp 1 section, nhãn ghi cả 2 câu** | Cùng 200, cùng cấu trúc trang; chỉ khác 1 từ trong câu giải thích. |
| 5 | 2 CSDL khác nhau | **Vẽ 2 trụ**: `SQL Server (DevPrep_Interview)` và `SQL Server (DevPrep_Evaluation)` | Đây là chức năng đầu tiên trong bộ diagram chạm cả 2 DB trong **cùng một** trang; dùng 1 trụ chung sẽ ám chỉ sai là dùng chung DB. |
| 6 | 403 ở `GET .../report` **không** làm dừng luồng (WebClient vẫn POST build rồi vẫn render trang) | **Vẽ đúng như code**: `C-a` chỉ đóng tới `:EvaluationService (WebClient)` rồi luồng chạy tiếp | Đây là hành vi thật và là lý do trang vẫn render kèm `EmptyReason` thay vì báo lỗi. |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `4`)** The IDOR guard runs on EVERY EvaluationAPI action (3 times on this page) and always calls InterviewAPI :7005 DIRECTLY, never through the Gateway - Evaluation tables carry no UserId of their own, so ownership can only be answered by InterviewAPI. Any transport error also returns false (fail closed). *(gỡ từ note trên diagram)*
- **(cạnh message `8`)** EnsureCompletedSessionAsync reuses the FEEDBACK eligibility endpoint as a "session is Completed" gate (Eligible = Status == "Completed"). GetAnswerEvaluations deliberately has no such gate, which is why per-question scores can still load when the report itself is refused. *(gỡ từ note trên diagram)*
- **(cạnh message `A.5`)** After GetReportAsync returns, the Report action also loads the interview-feedback form state (eligibility + already-submitted feedback). That belongs to the Submit Interview Feedback feature and is intentionally not drawn - it never changes the report content itself. *(gỡ từ note trên diagram)*
- **(cạnh message `13`)** The report is built LAZILY on the first view: nothing generates it when the interview ends. durationActualSec comes from the query string the Room appends when it redirects here. *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| `uc_id` của chức năng | RECON không ghi UC-id cho #18 | Nhóm xác nhận ê-líp use-case rồi điền vào `specs/viewevaluationreport.yaml` | luồng 1 |
| BL#68 và BL#69 ứng với phần nào | File `SEP490_Backlog__2_.xlsx` không có trong repo; RECON chỉ liệt kê số hiệu. Nghi là 2 mục thuộc khối feedback đã loại ở §1 | Nhóm đối chiếu backlog: nếu BL#68/69 thật sự là "Submit Interview Feedback" thì tách sang file diagram riêng; nếu không, bổ sung vào trang này | luồng 2 |
