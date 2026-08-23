# FLOW — `ConductInterviewQA` · BL#56, 57, 47, 59, 60 · UC-`chưa có`

> Đặt tại `docs/diagrams/FLOWS/ConductInterviewQA.md`
> Trạng thái: `DRAFT` — Khảo sát: lô F, 2026-08-03

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Một lượt hỏi–đáp trong phòng phỏng vấn: nộp câu trả lời (hoặc **Skip** / **auto-advance** khi hết giờ chờ), server lưu `ChatMessage`, đẩy chấm điểm nền, rồi phát câu hỏi kế tiếp hoặc kết thúc phiên |
| Điểm vào (user thao tác gì) | 3 lối vào cùng gọi 1 endpoint: gõ câu trả lời rồi **Send**; bấm **Skip**; hoặc **[auto]** đồng hồ idle chạm ngưỡng cứng |
| Kết thúc (user thấy gì) | Bong bóng "đang gõ" rồi câu hỏi kế tiếp + ô nhập mở lại; hoặc hết câu → `completeInterview()` chuyển sang màn hình kết thúc (`#9`) |
| Quy mô | `11` participant · `120` message · `34` class · `2` alt / `7` opt · trang Sequence 2320×5147 · trang Class 6090×2494 |
| Đối chiếu ước lượng RECON | RECON đoán `45-55` · thật `120` · hệ số `2.4×` |
| Service ngoài quyền sở hữu | ApiGateway (định tuyến) |
| **Gọi BYPASS Gateway?** | **CÓ (gián tiếp)** — `ScheduleGrading` → `EvaluationApiClient` gọi THẲNG EvaluationAPI `:7006` (`InterviewAPI/Program.cs:50, 89-95`). Ở trang này chỉ vẽ 1 cặp boundary, chi tiết ở `#17` |
| Nhà cung cấp AI | **không nằm trên critical path.** Gemini chỉ chạy ở nhánh chấm điểm **nền** (`#17`); TTS đọc câu hỏi mới thuộc `#6` |
| Ghi DB | `ChatMessages` INSERT ×2 (User Answer + AI Question) · `InterviewSessions` atomic +1 `AnsweredQuestions`, `LastSkipAtUtc`, và Status/EndedAt khi hết câu · `SessionQuestions` UPDATE `AskedAt` · `InterviewRooms` UPDATE `Closed` |
| Trừ credit | không (trừ ở `EnterInterviewRoom`, lúc lấy câu hỏi đầu tiên) |
| KI liên quan | **KI-01** (pattern forward status 2xx ở `InterviewController.Message`) · **KI-08** (chấm nền lỗi im lặng) — không phát hiện KI mới |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| Toàn bộ `ScheduleGrading` → `EvaluationApiClient` → EvaluationAPI → ai-service → Gemini | [SessionService.cs:1708-1737](../../../source/InterviewAPI/Services/SessionService.cs#L1708-L1737) | Đã vẽ ĐẦY ĐỦ ở `#17 EvaluateInterviewAnswer`. Ở đây thu về đúng 1 cặp call+ret (message 43-44) — xem §2. RECON cũng khuyến nghị "giữ dạng 1 opt/ref box gọn". |
| Nội dung `SettleExpiredVerificationChecksAsync` (đánh dấu check quá hạn, đếm lại strike, có thể `Cancelled` phiên) | [SessionService.cs:290-330](../../../source/InterviewAPI/Services/SessionService.cs#L290-L330) | Thuộc `#15 VerifyFaceInSession`. Vẽ 1 cặp call+ret (message 14-15) — xem §2. |
| Nội dung `AttachVerificationCheckAsync` / `GetInSessionCheckAsync` (phát `checkId` một lần cho câu hỏi mới) | [SessionService.cs:208-278](../../../source/InterviewAPI/Services/SessionService.cs#L208-L278) | Thuộc `#15`. Vẽ 1 cặp call+ret (message N-a.9 / N-a.10). |
| Nhánh `catch { DecrementAnsweredAsync(...); throw; }` (rollback claim khi một bước sau claim ném lỗi) | [SessionService.cs:903-910](../../../source/InterviewAPI/Services/SessionService.cs#L903-L910) | Lỗi **hạ tầng** (INSERT `ChatMessage` ném) — cùng loại đã loại ở lô A/C/D. Kết cục trùng khít nhánh 400 gộp đã vẽ (`opt C`). Ghi 1 câu trong nhãn message 41 + note N5 để không mất thông tin. |
| Chi tiết `startSkipCooldown()` (đếm ngược trên nút Skip) | [interview-room.js:895-916](../../../source/WebClient/wwwroot/js/pages/interview-room.js#L895-L916) | Hiệu ứng UI thuần, gộp 1 câu vào nhãn `H.6`. |
| `showTypingIndicator()` / `typingDelayFor()` | [interview-room.js:561-583](../../../source/WebClient/wwwroot/js/pages/interview-room.js#L561-L583) | Hiệu ứng UI, gộp vào nhãn message N-a.16. |
| Chi tiết `ContentModerationHelper.ContainsProfanity` / `LooksLikeGibberish` | [ContentModerationHelper.cs:47-70](../../../source/InterviewAPI/Helpers/ContentModerationHelper.cs#L47-L70) | Thuật toán lọc nội dung — gộp vào guard của `opt C`. |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Dọn các in-session check quá hạn trước khi xử lý lượt | `#15 VerifyFaceInSession` | message 14 `SettleExpiredVerificationChecksAsync(session)` ↔ 15 | SessionService.cs:706, 290-330 |
| Phát `checkId` sinh trắc cho câu hỏi mới | `#15 VerifyFaceInSession` | message N-a.9 `AttachVerificationCheckAsync(...)` ↔ N-a.10 | SessionService.cs:870, 208-215 |
| Chấm điểm nền câu trả lời vừa lưu | `#17 EvaluateInterviewAnswer` | message 43 `ScheduleGrading(...)` ↔ 44 | SessionService.cs:827 |
| Hết câu hỏi → trình duyệt gọi `completeInterview()` | `#9 EndInterviewSession` | message N-b.15 (nhãn ghi rõ) | interview-room.js:866-868, 941-943, 997-999 |
| Hết giờ → `handleSessionExpired()` | `#9 EndInterviewSession` | message E.9 (nhãn ghi rõ) | interview-room.js:828-831, 874-877 |
| Đọc to câu hỏi mới bằng TTS | `#6 VoiceInteraction` | ghi trong nhãn message N-a.16, không vẽ message riêng | interview-room.js:571 |

### Gỡ từ note trên diagram — boundary

- **(cạnh message `4`)** Closing the session here is the SAME logic as #9 EndInterviewSession's finalize, written inline. The browser still calls completeInterview() afterwards, which finds the session already terminal and simply reads back the decided status. *(gỡ từ note trên diagram)*


---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 0 | User | actor | — | — |
| 1 | :Interview Room UI | box | `wwwroot/js/pages/interview-room.js:833-1058` | — |
| 2 | :InterviewController (WebClient) | box | `WebClient/Controllers/Interview/InterviewController.cs:432-450` | — |
| 3 | :InterviewService (WebClient) | box | `WebClient/Services/Interview/InterviewService.cs:241-272` | — |
| 4 | :API Gateway | box | `ApiGateway` (YARP :7000) | — |
| 5 | :InterviewSessionController (InterviewAPI) | box | `InterviewAPI/Controllers/InterviewSessionController.cs:455-475` | có |
| 6 | :SessionService (InterviewAPI) | box | `InterviewAPI/Services/SessionService.cs:699-957` | có |
| 7 | :SessionRepository (InterviewAPI) | box | `InterviewAPI/Repositories/SessionRepository.cs` | có |
| 8 | :SessionVerificationEventRepository (InterviewAPI) | box | `InterviewAPI/Repositories/SessionVerificationEventRepository.cs` | có |
| 9 | :EvaluationApiClient (InterviewAPI) | box | `InterviewAPI/Clients/EvaluationApiClient.cs` | **không (bypass :7006)** |
| 10 | SQL Server | cylinder | `DevPrep_Interview` | — |

> Actor **trực tiếp** ở 2 lối vào (Send, Skip) và **gián tiếp** ở lối vào thứ 3 (đồng hồ idle chạm
> ngưỡng) — user vẫn đang ngồi trước màn hình nên theo quy ước vẫn giữ actor `User`, nhãn ghi `[auto]`.

## 4. BẢNG MESSAGE

### Pha 0 — 3 lối vào (khung `alt Z`)

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| Z-a.1 | User | :Interview Room UI | call | `Z-a.1. Type an answer and click Send (or press Enter)` | Z-a | interview-room.js:833, 1050-1058 |  | — |
| Z-a.2 | :Interview Room UI | (self) | call | `Z-a.2. sendAnswer(): DevPrepContentFilter.isAcceptable(content)` | Z-a | interview-room.js:833-844 | self | Z-a.2. sendAnswer(): guard !content / !currentQuestionId / ended; then DevPrepContentFilter.isAcceptable(content) - the client mirror of the server filter |
| A.1 | :Interview Room UI | (self) | call | `A.1. showContentWarning(...)` | A | interview-room.js:835, 841-844 | self | A.1. showContentWarning("Your answer appears to contain inappropriate or unreadable content. Please review it and try again.") - or simply return when a guard failed |
| A.2 | :Interview Room UI | User | ret | `A.2. Nothing is sent; the text stays in the box` | A | interview-room.js:842-843 |  | A.2. Nothing is sent; the text stays in the box so the user can fix it |
| Z-a.3 | :Interview Room UI | (self) | call | `Z-a.3. Clear the box; appendBubble("User", content)` | Z-a | interview-room.js:846-861 | self | Z-a.3. Clear the box, clearDraft(), disable input / Send / Skip, appendBubble("User", content); body = { sessionQuestionId, content } |
| Z-b.1 | User | :Interview Room UI | call | `Z-b.1. Click Skip` | Z-b | interview-room.js:918 |  | — |
| Z-b.2 | :Interview Room UI | (self) | call | `Z-b.2. skipQuestion(): append the "Question skipped" note` | Z-b | interview-room.js:918-936 | self | Z-b.2. skipQuestion(): guard !currentQuestionId / ended; disable input / Send / Skip; append the "Question skipped" note; body = { sessionQuestionId, content: "", reason: "Skipped" } |
| Z-c.1 | User | :Interview Room UI | call | `Z-c.1. [auto] Idle countdown reaches the hard deadline` | Z-c | interview-room.js:971-972 |  | Z-c.1. [auto] No interaction until the idle countdown reaches the hard deadline |
| Z-c.2 | :Interview Room UI | (self) | call | `Z-c.2. autoAdvance(): append the "Auto-advanced" note` | Z-c | interview-room.js:971-992 | self | Z-c.2. autoAdvance(): guard !currentQuestionId / ended / autoAdvanceInFlight; append the "Auto-advanced due to inactivity" note; body = { sessionQuestionId, content: "", reason: "TimedOut" } |

### Pha 1 — POST /message và các guard phía server

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | :Interview Room UI | :InterviewController (WebClient) | call | `1. POST /Interview/Message?id={id}` | — | interview-room.js:858-861, 932-936, 988-992 |  | 1. POST /Interview/Message?id={sessionId} (postProxy fetch, JSON body from whichever entry ran) |
| 2 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `2. SendMessageAsync(id, request, ct)` | — | InterviewController.cs:435-436 |  | — |
| 3 | :InterviewService (WebClient) | :API Gateway | call | `3. POST api/interview/sessions/{id}/message` | — | InterviewService.cs:244-248 |  | 3. POST api/interview/sessions/{sessionId}/message { sessionQuestionId, content, responseTimeMs, reason } |
| 4 | :API Gateway | :InterviewSessionController | call | `4. Forward (route interview-route, no prefix strip)` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 5 | :InterviewSessionController | (self) | call | `5. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | InterviewSessionController.cs:461 | self | — |
| 6 | :InterviewSessionController | :SessionService | call | `6. SendMessageAsync(userId, sessionId, req, ct)` | — | InterviewSessionController.cs:462 |  | — |
| 7 | :SessionService | :SessionRepository | call | `7. GetByIdAsync(sessionId, ct) - Step A` | — | SessionService.cs:703 |  | — |
| 8 | :SessionRepository | SQL Server | call | `8. SELECT InterviewSession WHERE SessionId AND !IsDeleted` | — | SessionRepository.cs:21-24 |  | — |
| 9 | SQL Server | :SessionRepository | ret | `9. Return row / null` | — | SessionRepository.cs:21-24 |  | — |
| 10 | :SessionRepository | :SessionService | ret | `10. Return InterviewSession?` | — | SessionRepository.cs:21-24 |  | — |
| 11 | :SessionService | :SessionVerificationEventRepository | call | `11. SettleExpiredVerificationChecksAsync(session) [see #15]` | — | SessionService.cs:706, 290-330 | boundary | 11. SettleExpiredVerificationChecksAsync(session): mark every expired pending in-session check as missed and re-count strikes (#15 VerifyFaceInSession) |
| 12 | :SessionVerificationEventRepository | :SessionService | ret | `12. Return` | — | SessionService.cs:314-330 | boundary | 12. Return - session.Status may now be "Cancelled" when the strike cap was reached |
| 13 | :SessionService | :SessionRepository | call | `13. GetQuestionByIdAsync(req.SessionQuestionId, ct)` | — | SessionService.cs:740 |  | 13. GetQuestionByIdAsync(req.SessionQuestionId, ct) - Step B, includes .Session |
| 14 | :SessionRepository | SQL Server | call | `14. SELECT SessionQuestion WHERE SessionQuestionId` | — | SessionRepository.cs:76-79 |  | 14. SELECT SessionQuestion (with Session) WHERE SessionQuestionId |
| 15 | SQL Server | :SessionRepository | ret | `15. Return row / null` | — | SessionRepository.cs:76-79 |  | — |
| 16 | :SessionRepository | :SessionService | ret | `16. Return SessionQuestion?` | — | SessionRepository.cs:76-79 |  | — |
| C.1 | :SessionService | :InterviewSessionController | ret | `C.1. throw InvalidOperationException` | C | SessionService.cs:704-705, 707-708, 741-742, 749-754 | throw | C.1. throw InvalidOperationException - "Session not found." / "Session is not in progress." / "Question not found." / "Your answer appears to contain inappropriate or unreadable content. Please review it and try again." |
| C.2 | :InterviewSessionController | :API Gateway | ret | `C.2. Return 400 {error}` | C | InterviewSessionController.cs:471-474 |  | C.2. Return 400 ApiResponse.Fail(ex.Message) - the generic catch, one code path for all four guards |
| C.3 | :API Gateway | :InterviewService (WebClient) | ret | `C.3. Return 400` | C | ApiGateway/appsettings.json:49-54 |  | — |
| C.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `C.4. Return (false, null, message, null, null, 400)` | C | InterviewService.cs:267-271 |  | C.4. Return (false, null, message, errorCode = null, retryAfterSeconds = null, 400) |
| C.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `C.5. Return 400 {error, message}` | C | InterviewController.cs:438-447 |  | C.5. Return 400 { error: null, message, retryAfterSeconds: null } - the backend status is forwarded intact |
| C.6 | :Interview Room UI | User | ret | `C.6. appendBubble("AI", "Error: <message>.")` | C | interview-room.js:878-882, 961-964 |  | C.6. appendBubble("AI", "Error: <message>. Please try again."); input / Send / Skip are re-enabled |
| 17 | :SessionService | (self) | call | `17. FinalizeIfTimeExpiredAsync(session)` | — | SessionService.cs:717, 138-147 | self | 17. FinalizeIfTimeExpiredAsync(session) - Step A.1: elapsed = (now - StartedAt) - GetEffectivePausedSeconds(session); expired when remaining < -5s grace |
| E.1 | :SessionService | :SessionRepository | call | `E.1. Finalize inline: Status = Completed / Expired [see #9]` | E | SessionService.cs:149-164 |  | E.1. Finalize inline: HasSubstantiveAnswerAsync, set Status = Completed / Expired + EndedAt, then UpdateRoomAsync(room{Status="Closed"}) - or SaveChangesAsync when there is no room (same shape as #9 EndInterviewSession) |
| E.2 | :SessionRepository | SQL Server | call | `E.2. UPDATE InterviewSessions SET Status, EndedAt + InterviewRoom` | E | SessionRepository.cs:104-113, 220-221 |  | E.2. SELECT EXISTS substantive answer; UPDATE InterviewSession SET Status, EndedAt, UpdatedAt (+ UPDATE InterviewRoom SET Status, ClosedAt) |
| E.3 | SQL Server | :SessionRepository | ret | `E.3. Return rows affected` | E | SessionRepository.cs:220-221 |  | — |
| E.4 | :SessionRepository | :SessionService | ret | `E.4. Return` | E | SessionRepository.cs:220-221 |  | — |
| E.5 | :SessionService | :InterviewSessionController | ret | `E.5. throw SessionGuardException(409, "session_time_expired")` | E | SessionService.cs:717-719 | throw | E.5. throw SessionGuardException(409, "session_time_expired", "This interview's time has run out.") |
| E.6 | :InterviewSessionController | :API Gateway | ret | `E.6. Return 409 { error: "session_time_expired", message }` | E | InterviewSessionController.cs:465-470 |  | — |
| E.7 | :API Gateway | :InterviewService (WebClient) | ret | `E.7. Return 409` | E | ApiGateway/appsettings.json:49-54 |  | — |
| E.8 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `E.8. Return 409 "session_time_expired"` | E | InterviewService.cs:269-271 |  | E.8. Return (false, null, message, "session_time_expired", null, 409) |
| E.9 | :InterviewController (WebClient) | :Interview Room UI | ret | `E.9. Return 409 { error: "session_time_expired", message }` | E | InterviewController.cs:443-446 |  | — |
| E.10 | :Interview Room UI | User | ret | `E.10. handleSessionExpired() -> completeInterview() [see #9]` | E | interview-room.js:874-877, 828-831 | boundary | E.10. err.code === "session_time_expired" -> handleSessionExpired(): "Your interview time has run out." then completeInterview() (#9 EndInterviewSession) |
| F.1 | :SessionService | :InterviewSessionController | ret | `F.1. throw SessionGuardException(403, ...)` | F | SessionService.cs:724, 51-56 | throw | F.1. throw SessionGuardException(403, "integrity_policy_not_accepted", "You must accept the interview integrity policy before continuing.") - Step A.2 |
| F.2 | :InterviewSessionController | :API Gateway | ret | `F.2. Return 403 {error, message}` | F | InterviewSessionController.cs:465-470 |  | F.2. Return 403 { error: "integrity_policy_not_accepted", message } |
| F.3 | :API Gateway | :InterviewService (WebClient) | ret | `F.3. Return 403` | F | ApiGateway/appsettings.json:49-54 |  | — |
| F.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `F.4. Return 403 "integrity_policy_not_accepted"` | F | InterviewService.cs:269-271 |  | F.4. Return (false, null, message, "integrity_policy_not_accepted", null, 403) |
| F.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `F.5. Return 403 { error, message }` | F | InterviewController.cs:445-446 |  | — |
| F.6 | :Interview Room UI | User | ret | `F.6. appendBubble("AI", "Error: <message>.")` | F | interview-room.js:878-882 |  | F.6. appendBubble("AI", "Error: <message>. Please try again.") - the Room has no special handling for 403 |
| G.1 | :SessionService | :SessionRepository | call | `G.1. TryConsumeSkipCooldownAsync(...)` | G | SessionService.cs:730-733 |  | G.1. TryConsumeSkipCooldownAsync(sessionId, Interview:SkipCooldownSeconds (10), ct) - Step A.3 |
| G.2 | :SessionRepository | SQL Server | call | `G.2. UPDATE InterviewSessions SET LastSkipAtUtc` | G | SessionRepository.cs:268-272 |  | G.2. atomic UPDATE InterviewSessions SET LastSkipAtUtc = now WHERE LastSkipAtUtc IS NULL OR DateDiffSecond(LastSkipAtUtc, now) >= cooldown |
| G.3 | SQL Server | :SessionRepository | ret | `G.3. Return rows affected` | G | SessionRepository.cs:274-292 |  | G.3. Return rows affected (0 = still cooling down; then a second SELECT reads LastSkipAtUtc to compute retryAfterSeconds) |
| G.4 | :SessionRepository | :SessionService | ret | `G.4. Return (Allowed, RetryAfterSeconds)` | G | SessionRepository.cs:277, 293 |  | — |
| H.1 | :SessionService | :InterviewSessionController | ret | `H.1. throw SessionGuardException(429, "skip_cooldown", ...)` | H | SessionService.cs:734-736 | throw | H.1. throw SessionGuardException(429, "skip_cooldown", "Please wait Ns before skipping again.", retryAfterSeconds) |
| H.2 | :InterviewSessionController | :API Gateway | ret | `H.2. Return 429 {error, message} + Retry-After` | H | InterviewSessionController.cs:465-470 |  | H.2. Return 429 { error: "skip_cooldown", message } + Retry-After header |
| H.3 | :API Gateway | :InterviewService (WebClient) | ret | `H.3. Return 429` | H | ApiGateway/appsettings.json:49-54 |  | — |
| H.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `H.4. Return 429 "skip_cooldown"` | H | InterviewService.cs:269-271 |  | H.4. Return (false, null, message, "skip_cooldown", retryAfterSeconds, 429) |
| H.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `H.5. Return 429 {error, message, retryAfterSeconds} + Retry-After` | H | InterviewController.cs:443-446 |  | H.5. Return 429 { error, message, retryAfterSeconds } + Retry-After header |
| H.6 | :Interview Room UI | User | ret | `H.6. startSkipCooldown(retryAfterSeconds)` | H | interview-room.js:956-960, 895-916 |  | H.6. The "Question skipped" note becomes "Skip did not go through - please wait before trying again."; startSkipCooldown(retryAfterSeconds) counts the Skip button down (never from the client clock) |

### Pha 2 — chống trùng, lưu câu trả lời, chấm nền

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 18 | :SessionService | :SessionRepository | call | `18. HasUserAnswerAsync(req.SessionQuestionId, ct)` | — | SessionService.cs:765 |  | 18. HasUserAnswerAsync(req.SessionQuestionId, ct) - Step B.1 existence guard |
| 19 | :SessionRepository | SQL Server | call | `19. SELECT EXISTS ChatMessages WHERE MessageType="Answer"` | — | SessionRepository.cs:87-90 |  | 19. SELECT EXISTS ChatMessages WHERE SessionQuestionId AND SenderType="User" AND MessageType="Answer" |
| 20 | SQL Server | :SessionRepository | ret | `20. Return bool` | — | SessionRepository.cs:87-90 |  | — |
| 21 | :SessionRepository | :SessionService | ret | `21. Return bool` | — | SessionRepository.cs:87-90 |  | — |
| 22 | :SessionService | :SessionRepository | call | `22. IncrementAnsweredIfAsync(...)` | — | SessionService.cs:779 |  | 22. IncrementAnsweredIfAsync(sessionId, session.AnsweredQuestions, ct) - Step B.2 atomic per-turn claim (skipped entirely when Step B.1 already matched) |
| 23 | :SessionRepository | SQL Server | call | `23. UPDATE InterviewSessions SET AnsweredQuestions + 1` | — | SessionRepository.cs:297-300 |  | 23. atomic UPDATE InterviewSessions SET AnsweredQuestions = AnsweredQuestions + 1, UpdatedAt WHERE AnsweredQuestions = @expected |
| 24 | SQL Server | :SessionRepository | ret | `24. Return rows affected` | — | SessionRepository.cs:297-304 |  | — |
| 25 | :SessionRepository | :SessionService | ret | `25. Return claimed (rowsAffected > 0)` | — | SessionRepository.cs:304 |  | — |
| L.1 | :SessionService | :SessionRepository | call | `L.1. BuildCurrentTurnResponseAsync(sessionId, currentOrderIndex)` | L | SessionService.cs:770, 785, 916-957 |  | L.1. BuildCurrentTurnResponseAsync(sessionId, currentQuestion.OrderIndex): AsNoTracking re-read of the session + GetNextUnansweredQuestionAsync - no insert, no counter change |
| L.2 | :SessionRepository | SQL Server | call | `L.2. SELECT InterviewSession + next unanswered SessionQuestion` | L | SessionService.cs:923-925, SessionRepository.cs:93-102 |  | L.2. SELECT InterviewSession (AsNoTracking) + SELECT the first SessionQuestion after OrderIndex that still has no User answer |
| L.3 | SQL Server | :SessionRepository | ret | `L.3. Return the rows` | L | SessionRepository.cs:93-102 |  | — |
| L.4 | :SessionRepository | :SessionService | ret | `L.4. Return the session + next question` | L | SessionService.cs:923-925 |  | — |
| L.5 | :SessionService | :InterviewSessionController | ret | `L.5. Return MessageResponse` | L | SessionService.cs:927-956 |  | L.5. Return MessageResponse{Ended, AnsweredQuestions, TotalQuestions, NextQuestion} - exactly the state the winning request produced |
| L.6 | :InterviewSessionController | :API Gateway | ret | `L.6. Return 200 ApiResponse.Ok(result)` | L | InterviewSessionController.cs:463 |  | — |
| L.7 | :API Gateway | :InterviewService (WebClient) | ret | `L.7. Return 200` | L | ApiGateway/appsettings.json:49-54 |  | — |
| L.8 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `L.8. Return (true, MessageResponse, null, null, null, 200)` | L | InterviewService.cs:252-261 |  | — |
| L.9 | :InterviewController (WebClient) | :Interview Room UI | ret | `L.9. Return 200 JSON (MessageResponse)` | L | InterviewController.cs:449 |  | — |
| L.10 | :Interview Room UI | User | ret | `L.10. Same screen state as the winning request` | L | interview-room.js:863-871 |  | L.10. Same screen state as the winning request - the duplicate inserts nothing and moves no counter |
| 26 | :SessionService | :SessionRepository | call | `26. AddChatMessageAsync(ChatMessage{SenderType="User"})` | — | SessionService.cs:793-820 |  | 26. AddChatMessageAsync(ChatMessage{SenderType="User", MessageType="Answer", Content, ResponseTimeMs, AnswerReason}) - Step D, after Step C decided AnswerReason = "Skipped" / "TimedOut" (only trusted when the server-recomputed idle >= hard threshold - 5s) / null |
| 27 | :SessionRepository | SQL Server | call | `27. INSERT ChatMessage (SaveChangesAsync)` | — | SessionRepository.cs:68-73 |  | — |
| 28 | SQL Server | :SessionRepository | ret | `28. Return rows affected` | — | SessionRepository.cs:71 |  | — |
| 29 | :SessionRepository | :SessionService | ret | `29. Return ChatMessage` | — | SessionRepository.cs:72 |  | — |
| 30 | :SessionService | :EvaluationApiClient | call | `30. ScheduleGrading(...) -> Task.Run [see #17] [best-effort]` | — | SessionService.cs:827, 1708-1737 | best-effort · boundary | 30. ScheduleGrading(sessionId, currentQuestion, req.Content) -> Task.Run -> EvaluateAnswerAsync(...) - Step E.1, the whole grading flow is #17 EvaluateInterviewAnswer |
| 31 | :EvaluationApiClient | :SessionService | ret | `31. Return immediately` | — | SessionService.cs:1722-1736 | best-effort · boundary | 31. Return immediately - the turn never waits; a failure is only logged (KI-08) |
| 32 | :SessionService | :SessionRepository | call | `32. GetNextUnansweredQuestionAsync(...)` | — | SessionService.cs:830-831 |  | 32. GetNextUnansweredQuestionAsync(sessionId, currentQuestion.OrderIndex, ct) - Step F |
| 33 | :SessionRepository | SQL Server | call | `33. SELECT the next SessionQuestion with no User answer` | — | SessionRepository.cs:93-102 |  | 33. SELECT the first SessionQuestion with OrderIndex > current that has no User/Answer ChatMessage, ordered by OrderIndex |
| 34 | SQL Server | :SessionRepository | ret | `34. Return row / null` | — | SessionRepository.cs:93-102 |  | — |
| 35 | :SessionRepository | :SessionService | ret | `35. Return SessionQuestion?` | — | SessionRepository.cs:93-102 |  | — |

### Pha 3 — phát câu hỏi kế tiếp hoặc kết thúc phiên (`alt N`)

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| N-a.1 | :SessionService | :SessionRepository | call | `N-a.1. AddChatMessageAsync(ChatMessage{SenderType="AI"})` | N-a | SessionService.cs:836-844 |  | N-a.1. AddChatMessageAsync(ChatMessage{SenderType="AI", MessageType="Question", Content = nextQuestion.Content}) - Step G |
| N-a.2 | :SessionRepository | SQL Server | call | `N-a.2. INSERT ChatMessage (SaveChangesAsync)` | N-a | SessionRepository.cs:68-73 |  | — |
| N-a.3 | SQL Server | :SessionRepository | ret | `N-a.3. Return rows affected` | N-a | SessionRepository.cs:71 |  | — |
| N-a.4 | :SessionRepository | :SessionService | ret | `N-a.4. Return ChatMessage` | N-a | SessionRepository.cs:72 |  | — |
| N-a.5 | :SessionService | :SessionRepository | call | `N-a.5. SaveChangesAsync(ct) after nextQuestion.AskedAt = now` | N-a | SessionService.cs:846-847 |  | N-a.5. SaveChangesAsync(ct) after setting nextQuestion.AskedAt = now |
| N-a.6 | :SessionRepository | SQL Server | call | `N-a.6. UPDATE SessionQuestion SET AskedAt` | N-a | SessionRepository.cs:348 |  | — |
| N-a.7 | SQL Server | :SessionRepository | ret | `N-a.7. Return rows affected` | N-a | SessionRepository.cs:348 |  | — |
| N-a.8 | :SessionRepository | :SessionService | ret | `N-a.8. Return` | N-a | SessionRepository.cs:348 |  | — |
| N-a.9 | :SessionService | :SessionVerificationEventRepository | call | `N-a.9. AttachVerificationCheckAsync(...) [see #15]` | N-a | SessionService.cs:870, 208-215 | boundary | N-a.9. AttachVerificationCheckAsync(response.NextQuestion, sessionId, userId): issue the one-time in-session face checkId for this question (#15 VerifyFaceInSession) |
| N-a.10 | :SessionVerificationEventRepository | :SessionService | ret | `N-a.10. Return InSessionCheckDueResponse{CheckId, ExpiresAt}` | N-a | SessionService.cs:211-214, 223-230 | boundary | N-a.10. Return InSessionCheckDueResponse{CheckId, ExpiresAt} - both null when biometrics are off or this is question 1 |
| N-a.11 | :SessionService | :InterviewSessionController | ret | `N-a.11. Return MessageResponse{Ended=false, NextQuestion}` | N-a | SessionService.cs:849-871 |  | N-a.11. Return MessageResponse{Ended=false, AnsweredQuestions = pre-claim + 1, TotalQuestions, NextQuestion{SessionQuestionId, OrderIndex, Content, IdleReminderSeconds=90, IdleHardSeconds=180, IdleElapsedSeconds=0, VerificationCheckId, VerificationCheckExpiresAt}} |
| N-a.12 | :InterviewSessionController | :API Gateway | ret | `N-a.12. Return 200 ApiResponse.Ok(result)` | N-a | InterviewSessionController.cs:463 |  | — |
| N-a.13 | :API Gateway | :InterviewService (WebClient) | ret | `N-a.13. Return 200` | N-a | ApiGateway/appsettings.json:49-54 |  | — |
| N-a.14 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `N-a.14. Return (true, MessageResponse, null, null, null, 200)` | N-a | InterviewService.cs:252-261 |  | — |
| N-a.15 | :InterviewController (WebClient) | :Interview Room UI | ret | `N-a.15. Return 200 JSON (MessageResponse)` | N-a | InterviewController.cs:449 |  | — |
| N-a.16 | :Interview Room UI | User | ret | `N-a.16. applyNextQuestion() [see #6]` | N-a | interview-room.js:863-872, 550-583 |  | N-a.16. syncAnsweredFrom(data) + updateProgressDisplay(); applyNextQuestion(): typing indicator, then the question bubble, idle timer restarted, speak() when AI Voice is on (#6), captureAndVerifyInSession() when a checkId came with it (#15); input / Send / Skip / Hint re-enabled |
| N-b.1 | :SessionService | :SessionRepository | call | `N-b.1. HasSubstantiveAnswerAsync(sessionId, ct) - Step H` | N-b | SessionService.cs:875 |  | — |
| N-b.2 | :SessionRepository | SQL Server | call | `N-b.2. SELECT EXISTS substantive answer` | N-b | SessionRepository.cs:104-113 |  | N-b.2. SELECT EXISTS ChatMessages WHERE SessionId AND SenderType="User" AND MessageType="Answer" AND Content non-empty AND AnswerReason NOT IN ("TimedOut","Skipped") |
| N-b.3 | SQL Server | :SessionRepository | ret | `N-b.3. Return bool` | N-b | SessionRepository.cs:104-113 |  | — |
| N-b.4 | :SessionRepository | :SessionService | ret | `N-b.4. Return bool` | N-b | SessionRepository.cs:104-113 |  | — |
| N-b.5 | :SessionService | :SessionRepository | call | `N-b.5. GetRoomBySessionAsync(sessionId, ct)` | N-b | SessionService.cs:875-880 |  | N-b.5. GetRoomBySessionAsync(sessionId, ct) after setting session.Status = hasSubstantiveAnswer ? "Completed" : "Expired" and EndedAt = now |
| N-b.6 | :SessionRepository | SQL Server | call | `N-b.6. SELECT InterviewRoom WHERE SessionId` | N-b | SessionRepository.cs:216-217 |  | — |
| N-b.7 | SQL Server | :SessionRepository | ret | `N-b.7. Return row / null` | N-b | SessionRepository.cs:216-217 |  | — |
| N-b.8 | :SessionRepository | :SessionService | ret | `N-b.8. Return InterviewRoom?` | N-b | SessionRepository.cs:216-217 |  | — |
| N-b.9 | :SessionService | :SessionRepository | call | `N-b.9. UpdateRoomAsync(room{Status="Closed"}) [see #9]` | N-b | SessionService.cs:881-890 |  | N-b.9. UpdateRoomAsync(room{Status="Closed", ClosedAt=now}) - or SaveChangesAsync when the session has no room (same two-branch shape as #9 EndInterviewSession) |
| N-b.10 | :SessionRepository | SQL Server | call | `N-b.10. UPDATE InterviewSessions SET Status, EndedAt + InterviewRoom` | N-b | SessionRepository.cs:220-221, 348 |  | N-b.10. UPDATE InterviewSession SET Status, EndedAt, UpdatedAt (+ UPDATE InterviewRoom SET Status, ClosedAt in the same SaveChangesAsync) |
| N-b.11 | SQL Server | :SessionRepository | ret | `N-b.11. Return rows affected` | N-b | SessionRepository.cs:220-221 |  | — |
| N-b.12 | :SessionRepository | :SessionService | ret | `N-b.12. Return` | N-b | SessionRepository.cs:220-221 |  | — |
| N-b.13 | :SessionService | :InterviewSessionController | ret | `N-b.13. Return MessageResponse{Ended=true, NextQuestion=null}` | N-b | SessionService.cs:892-901 |  | N-b.13. Return MessageResponse{Ended=true, NextQuestion=null, AnsweredQuestions = pre-claim + 1, TotalQuestions} |
| N-b.14 | :InterviewSessionController | :API Gateway | ret | `N-b.14. Return 200 ApiResponse.Ok(result)` | N-b | InterviewSessionController.cs:463 |  | — |
| N-b.15 | :API Gateway | :InterviewService (WebClient) | ret | `N-b.15. Return 200` | N-b | ApiGateway/appsettings.json:49-54 |  | — |
| N-b.16 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `N-b.16. Return (true, MessageResponse, null, null, null, 200)` | N-b | InterviewService.cs:252-261 |  | — |
| N-b.17 | :InterviewController (WebClient) | :Interview Room UI | ret | `N-b.17. Return 200 JSON (Ended = true)` | N-b | InterviewController.cs:449 |  | — |
| N-b.18 | :Interview Room UI | User | ret | `N-b.18. data.ended -> completeInterview() [see #9]` | N-b | interview-room.js:866-868, 941-943 | boundary | N-b.18. data.ended -> completeInterview(): the session is already closed server-side, so this only fetches the decided status and moves to the end screen (#9 EndInterviewSession) |

**Cờ:** `self` · `throw` · `best-effort` · `boundary`

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| Z | alt | Z-a: `[the candidate types an answer and clicks Send]` · Z-b: `[the candidate clicks Skip]` · Z-c: `[the idle countdown reaches the server-verified hard deadline]` | Z-a.1 → Z-c.2 | — | — |
| A | opt | `[no content OR no current question OR the session already ended OR the client content filter rejects the text]` | A.1 → A.2 | Z-a | **4** (cùng "không gửi request", cùng "chữ vẫn nằm trong ô") |
| C | opt | `[session is null / not owned OR session.Status != "InProgress" OR the question does not belong to this session OR the server content filter rejects a non-skip answer]` | C.1 → C.6 | — | **4** (cùng HTTP 400 qua ĐÚNG một `catch (Exception)` và cùng bong bóng `Error: <message>. Please try again.` — xem §11 quyết định 2) |
| E | opt | `[the session's time already ran out - remaining < -5s grace]` | E.1 → E.10 | — | — |
| F | opt | `[!session.IntegrityPolicyAccepted]` | F.1 → F.6 | — | — |
| G | opt | `[req.Reason == "Skipped"]` | G.1 → H.6 | — | — |
| H | opt | `[the skip cooldown has not elapsed - Allowed == false]` | H.1 → H.6 | G | — |
| L | opt | `[this question already has a User/Answer message (a late duplicate) OR the atomic claim was lost (a truly concurrent duplicate)]` | L.1 → L.10 | — | **2** (cả hai gọi cùng `BuildCurrentTurnResponseAsync`, cùng HTTP 200, cùng màn hình — xem §11 quyết định 3) |
| N | alt | N-a: `[there is a next unanswered question - Step G]` · N-b: `[every question has been answered - Step H]` | N-a.1 → N-b.18 | — | — |

> `opt G` tự khép kín khi cooldown còn hiệu lực (`H` không bắn) → luồng chạy tiếp vào message 18.

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | Z | `All three entries call the SAME endpoint with the same body shape; only "reason" differs (null / "Skipped" / "TimedOut"). Everything from message 1 onwards is shared, which is why the entries are drawn as one alt instead of three pages.` |
| N2 | 18-25 | `Two idempotency guards, on purpose. Step B.1 (HasUserAnswerAsync) catches duplicates that arrive far enough apart that each reads an already-advanced counter - real data on 2026-07-24 showed 5 auto-advance retries all inserting for the same question. Step B.2 (the atomic claim) catches TRUE simultaneity, where both requests read the same counter value. Neither alone is enough.` |
| N3 | 26 | `"TimedOut" is a client HINT that the server re-verifies: it recomputes idle from SessionQuestion.AskedAt (minus paused time) and only keeps the label when it is within 5s of the hard threshold. "Skipped" is a deliberate click and is trusted as-is.` |
| N4 | 30-31 | `ScheduleGrading is fire-and-forget (Task.Run): the turn returns without waiting, and a grading failure is only logged - no retry, no flag, no signal to the user (KI-08). The answer is still safely stored by message 26.` |
| N5 | 26 / C | `Every step after the successful claim runs inside a try/catch that calls DecrementAnsweredAsync and rethrows, so an infrastructure failure (e.g. the ChatMessage insert throwing) does not leave the turn permanently claimed. The rethrow lands in the same 400 branch drawn as opt C.` |
| N6 | N-b.9 | `Closing the session here is the SAME logic as #9 EndInterviewSession's finalize, written inline. The browser still calls completeInterview() afterwards, which finds the session already terminal and simply reads back the decided status.` |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | Liệt kê | Nguồn |
|---|---|---|---|---|
| 1 | InterviewController (WebClient) | class | SUBSET (`Message`) | local |
| 2 | Controller | framework-base | FULL (rỗng) | SHARED |
| 3 | IInterviewService (WebClient) | interface | SUBSET | local |
| 4 | InterviewService (WebClient) | class | SUBSET | local |
| 5 | IApiClient / 6 ApiClient | interface/class | FULL | SHARED |
| 7 | SendMessageRequest (WebClient) / 8 MessageResponse (WebClient) / 9 NextQuestionDto (WebClient) | DTO | FULL | local |
| 10 | ApiResponse<T> (WebClient) | DTO | FULL | local |
| 11 | ControllerBase | framework-base | FULL (rỗng) | SHARED |
| 12 | InterviewSessionController (InterviewAPI) | class | SUBSET (`SendMessage`) | local |
| 13 | ISessionService | interface | SUBSET | local |
| 14 | SessionService (InterviewAPI) | class | SUBSET | local |
| 15 | SessionGuardException | exception | FULL | local |
| 16 | ContentModerationHelper | helper | SUBSET | local |
| 17 | ApiResponse<T> (InterviewAPI) | DTO | FULL | SHARED |
| 18 | SendMessageRequest / 19 MessageResponse / 20 NextQuestionDto | DTO | FULL | local |
| 21 | IEvaluationApiClient / 22 EvaluationApiClient (InterviewAPI) | interface/class | FULL | local |
| 23 | EvaluateAnswerPayload | DTO | FULL | local |
| 24 | ISessionRepository / 25 SessionRepository (InterviewAPI) | interface/class | SUBSET | local |
| 26 | ISessionVerificationEventRepository / 27 SessionVerificationEventRepository (InterviewAPI) | interface/class | FULL | local |
| 28 | InterviewSession / 29 InterviewRoom / 30 SessionQuestion / 31 ChatMessage / 32 SessionVerificationEvent | entity | FULL | SHARED |
| 33 | InterviewDbContext / 34 DbContext | class/framework-base | FULL | SHARED |

## 8. CLASS — chi tiết

### `SessionService (InterviewAPI)` — `SUBSET`
- 10 field DI
- Method `+ SendMessageAsync(...)`, `- BuildCurrentTurnResponseAsync(...)`,
  `- FinalizeIfTimeExpiredAsync(...)`, `- SettleExpiredVerificationChecksAsync(...)`,
  `- AttachVerificationCheckAsync(...)`, `- ScheduleGrading(...)`,
  `- RequireIntegrityPolicyAccepted(session)`, `- GetIdleThresholds(difficulty)`,
  `- GetEffectivePausedSeconds(session)`
- *(SUBSET)* Đã bỏ ~45 method ngoài chức năng này.

### `SessionRepository (InterviewAPI)` — `SUBSET`
- `+ GetByIdAsync`, `+ GetQuestionByIdAsync`, `+ HasUserAnswerAsync`,
  `+ IncrementAnsweredIfAsync`, `+ DecrementAnsweredAsync`, `+ TryConsumeSkipCooldownAsync`,
  `+ AddChatMessageAsync`, `+ GetNextUnansweredQuestionAsync`, `+ HasSubstantiveAnswerAsync`,
  `+ GetRoomBySessionAsync`, `+ UpdateRoomAsync`, `+ SaveChangesAsync`

### `ContentModerationHelper` — `SUBSET`
- `+ IsAcceptable(content: string): bool` (2 helper `ContainsProfanity` / `LooksLikeGibberish`
  giữ lại vì `IsAcceptable` gọi trực tiếp)

Không có class SHARED mới trong chức năng này.

## 9. QUAN HỆ CLASS

Xem `specs/conductinterviewqa_classes.yaml` khoá `relations:`.

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| KI-01 | `InterviewController.cs:445-446` | Cùng pattern `statusCode == 0 ? 500 : statusCode` như action `Hint`: nếu upstream trả 2xx kèm `Success=false` thì lỗi sẽ được forward nguyên trạng 2xx | bug tiềm ẩn, **hiện chưa reachable** | **Vẽ hành vi thật**: `SendMessage` phía API chỉ trả 200/400/403/409/429 nên không có đường nào tạo ra "2xx + Success=false"; note không cần thiết ở đây | Có (đã tồn tại, KNOWN_ISSUES §KI-01 "Cần kiểm tra thêm") |
| KI-08 | `SessionService.cs:1708-1737` | `ScheduleGrading` fire-and-forget, lỗi chỉ log | fire-and-forget | **Vẽ thật**, cờ `[best-effort]` + note N4 | Có (đã tồn tại) |

> Đã kiểm và **bác** 2 nghi vấn:
> (a) 2 guard chống trùng (Step B.1 + B.2) trông như thừa — **không thừa**, mỗi cái bắt một loại
> trùng khác nhau, comment code có dẫn chứng dữ liệu thật (`SessionService.cs:756-786`).
> (b) `AnsweredQuestions = session.AnsweredQuestions + 1` dùng giá trị đọc TRƯỚC claim — **đúng**,
> vì atomic claim chỉ thành công khi DB vẫn khớp đúng giá trị đó (`SessionService.cs:853-858`).
> **Không phát hiện KI mới.**


### Gỡ từ note trên diagram — anomaly

- **(cạnh message `Z-c.1`)** ScheduleGrading is fire-and-forget (Task.Run): the turn returns without waiting, and a grading failure is only logged - no retry, no flag, no signal to the user (KI-08). The answer is still safely stored by message 26. *(gỡ từ note trên diagram)*

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | 3 lối vào (Send / Skip / auto-advance) cùng gọi 1 endpoint | **1 khung `alt Z` 3 nhánh ở đầu trang**, phần còn lại vẽ 1 lần | Loại trừ nhau trong 1 lượt; vẽ 3 trang hoặc 3 lần sẽ nhân ba 100 message mà không thêm thông tin. |
| 2 | 4 guard trả 400 nằm rải rác (session not found / not in progress / question not found / content filter) | **Gộp thành 1 `opt C`, guard ghép OR, nhãn `C.1` liệt kê ĐỦ 4 câu thông điệp** | **Khác cách làm ở lô B/C** — ghi rõ để rà: (a) cả 4 đi qua **đúng cùng một** `catch (Exception ex)` (`InterviewSessionController.cs:471-474`), cùng một tuple, cùng một nhánh JS generic → chuỗi return giống hệt về **code path**, chỉ khác giá trị chuỗi đã liệt kê đủ; (b) không gộp thì trang lên ~6469px, **vượt ngưỡng 5500px**. |
| 3 | 2 guard chống trùng (B.1 existence, B.2 atomic claim) đều `return BuildCurrentTurnResponseAsync(...)` | **Gộp thành 1 `opt L`**, guard ghép OR; message 22 ghi rõ "skipped entirely when Step B.1 already matched" | Cùng hàm, cùng HTTP 200, cùng màn hình. Đánh đổi duy nhất: 2 lần đọc DB vẽ nối tiếp trong khi thực tế cái sau bị bỏ qua nếu cái trước khớp — đã ghi ngay trong nhãn message 22. |
| 4 | Nhánh `catch { DecrementAnsweredAsync; throw; }` | **Không vẽ (§1)** + ghi trong nhãn message 26 và note N5 | Lỗi hạ tầng, cùng loại đã loại ở lô A/C/D; kết cục trùng khít `opt C`. |
| 5 | Nhánh room null / not null khi đóng phiên (Step H) và khi finalize hết giờ | **Gộp mỗi chỗ thành 1 cặp call+ret**, nhãn ghi cả 2 khả năng | Cặp `alt` này đã vẽ ĐẦY ĐỦ ở `#9`, `#10`, `#16`; lặp lần thứ tư tốn 338px mà không thêm thông tin. |
| 6 | `ScheduleGrading` / `SettleExpiredVerificationChecks` / `AttachVerificationCheck` | **Mỗi cái 1 cặp call+ret boundary** | Đã vẽ đầy đủ ở `#17` và `#15`; đây chính là cơ chế giữ `#5` khỏi phình. |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `Z-a.2`)** All three entries call the SAME endpoint with the same body shape; only "reason" differs (null / "Skipped" / "TimedOut"). Everything from message 1 onwards is shared, which is why the entries are drawn as one alt instead of three pages. *(gỡ từ note trên diagram)*
- **(cạnh message `A.2`)** Two idempotency guards, on purpose. Step B.1 (HasUserAnswerAsync) catches duplicates that arrive far enough apart that each reads an already-advanced counter - real data on 2026-07-24 showed 5 auto-advance retries all inserting for the same question. Step B.2 (the atomic claim) catches TRUE simultaneity, where both requests read the same counter value. Neither alone is enough. *(gỡ từ note trên diagram)*
- **(cạnh message `Z-b.1`)** "TimedOut" is a client HINT that the server re-verifies: it recomputes idle from SessionQuestion.AskedAt (minus paused time) and only keeps the label when it is within 5s of the hard threshold. "Skipped" is a deliberate click and is trusted as-is. *(gỡ từ note trên diagram)*
- **(cạnh message `1`)** Every step after the successful claim runs inside a try/catch that calls DecrementAnsweredAsync and rethrows, so an infrastructure failure (e.g. the ChatMessage insert throwing) does not leave the turn permanently claimed. The rethrow lands in the same 400 branch drawn as opt C. *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| `uc_id` của chức năng | RECON không ghi UC-id cho #5 | Nhóm xác nhận ê-líp use-case rồi điền vào `specs/conductinterviewqa.yaml` | luồng 1 |
