# FLOW — `EnterInterviewRoom` · BL#54, 38 · UC-`chưa có`

> Đặt tại `docs/diagrams/FLOWS/EnterInterviewRoom.md`
> Trạng thái: `DRAFT` — Khảo sát: lô G, 2026-08-03
>
> ⚠️ **File này là NỬA SAU của card `#4` trong RECON** (nửa đầu: `FLOWS/StartInterviewSession.md`).
> Card gốc đo thật ~175 message / ~6560px, vượt cả 2 ngưỡng → DuyenTQ duyệt tách 2 file (lô G).
> Ranh giới tách trùng đúng ranh giới HTTP request: `POST .../init` là 1 request (file kia),
> `GET /Interview/Room/{id}` là request khác, chạy sau khi Verify đã pass.

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Mở phòng phỏng vấn: 3 guard nối tiếp (trạng thái phiên → cổng sinh trắc → chính sách liêm chính), rồi materialize câu hỏi đầu tiên qua `questions/generate` — **đây cũng là nơi trừ credit** (BL#54) và nơi chạy Product Tour lần đầu (BL#38) |
| Điểm vào (user thao tác gì) | Xác minh khuôn mặt thành công ở màn hình Verify → tự chuyển sang `GET /Interview/Room/{id}` |
| Kết thúc (user thấy gì) | Phòng phỏng vấn: câu hỏi đầu tiên trong khung chat, đồng hồ đếm ngược chạy, camera bật. Mọi guard hỏng → về Dashboard/Verify/Integrity Policy kèm thông điệp tương ứng |
| Quy mô | `13` participant · `117` message · `33` class · `0` alt / `17` opt · trang Sequence `2720×5371` · trang Class `4840×2394` |
| Đối chiếu ước lượng RECON | RECON đoán `110-130` cho **cả card #4** · nửa sau này `117` (nửa đầu `StartInterviewSession` = `58`) |
| Service ngoài quyền sở hữu | **PaymentAPI** (:7008, trừ + hoàn credit) và ApiGateway (định tuyến) |
| **Gọi BYPASS Gateway?** | **CÓ — PaymentAPI :7008 gọi THẲNG** cho cả `DeductCreditAsync` lẫn `RefundCreditAsync`. Đã tự xác minh: `InterviewAPI/Program.cs:49, 80-87` đặt `BaseAddress = PAYMENT_API_BASEURL` (mặc định `http://localhost:7008`) → **KHÔNG vẽ hop ApiGateway** cho 2 message đó |
| Nhà cung cấp AI | không (kế hoạch câu hỏi đã lập xong ở `StartInterviewSession`) |
| Ghi DB | `SessionQuestions` UPDATE `AskedAt` · `InterviewSessions` atomic `Initialized → InProgress` + `CreditCost`/`StartedAt` · `ChatMessages` INSERT (AI Question) · `UserRoomTourStatuses` UPSERT |
| Trừ credit | **CÓ — `CreditConfig.GetCreditCost(DurationMinutes)` = 2 credit/phút**, chỉ ở lần vào phòng ĐẦU TIÊN (`isFirstEntry && statusChanged`). Có rollback trạng thái khi trừ hỏng, và **hoàn credit** khi INSERT `ChatMessage` hỏng sau khi đã trừ |
| KI liên quan | không (không phát hiện KI mới) |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| Nội dung `ResumeSessionAsync` (tái tạo lịch sử chat + câu hỏi hiện tại + timer + self-heal pause/expire) | [SessionService.cs:1267-1387](../../../source/InterviewAPI/Services/SessionService.cs#L1267-L1387) | Được gọi **2 lần** trong cùng action `Room` (status guard + re-fetch). Vẽ đầy đủ 2 lần là bất khả thi trong ngân sách trang → mỗi lần thu về 1 cặp call+ret (§2). **Đây là khoảng trống thật của bộ diagram — ghi ở §12.** |
| Nội dung cổng sinh trắc (`GetEntryGateAsync` → `BiometricVerificationService.GetEntryGateStatusAsync`) | `WebClient/Services/Profile/VerificationService.cs`, `BiometricVerificationService.cs:191-278` | Đã vẽ đầy đủ ở `#14 ValidateEntryGateStatus`. Thu về 1 cặp call+ret. |
| Nội dung cổng chính sách (`GetIntegrityPolicyStatusAsync`) | `InterviewService.cs:556-559` | Đã vẽ đầy đủ ở `#11 AcceptIntegrityPolicy`. Thu về 1 cặp call+ret. |
| Nhánh `Ended = true` của `GenerateQuestionAsync` (mọi câu đã trả lời) | [SessionService.cs:600-601](../../../source/InterviewAPI/Services/SessionService.cs#L600-L601) | **Không tới được từ action `Room`**: `SendMessageAsync` (#5) đã chuyển phiên sang `Completed`/`Expired` ngay khi câu cuối được trả lời, mà action `Room` chỉ gọi generate khi Status còn là `Initialized`/`InProgress` (`InterviewController.cs:265`). Chỉ chạm được bằng cách gọi thẳng API. |
| `runRoomTour()` / các bước popover driver.js | [interview-room.js:306-400](../../../source/WebClient/wwwroot/js/pages/interview-room.js#L306-L400) | Nội dung 6 bước tour — gộp 1 câu vào nhãn message V.1. |
| `speakGreetingAndFirstQuestion()` (TTS đọc lời chào + câu hỏi đầu) | [interview-room.js:1655-1681](../../../source/WebClient/wwwroot/js/pages/interview-room.js#L1655-L1681) | Thuộc `#6 VoiceInteraction`. Ghi trong nhãn message 10. |
| Nhánh lỗi hạ tầng của `PaymentApiClient` (exception → `Success=false`) | [PaymentApiClient.cs:55-63, 104-112](../../../source/InterviewAPI/Clients/PaymentApiClient.cs#L55-L63) | Client tự nuốt exception thành `Success=false` → **đã** nằm trong guard của `opt R` đã vẽ. |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Vào phòng sau khi Verify pass | `#13 VerifyFaceEntryGate` | message 1 | InterviewController.cs:99 |
| `GetRoomAsync` lần 1 (status guard) | *(chưa có file riêng — xem §12)* | message 3 ↔ 4 | InterviewController.cs:210, InterviewService.cs:206 |
| Cổng sinh trắc | `#14 ValidateEntryGateStatus` | message 5 ↔ 6 | InterviewController.cs:230 |
| Cổng chính sách liêm chính | `#11 AcceptIntegrityPolicy` | message 7 ↔ 8 | InterviewController.cs:247 |
| Chuyển hướng sang trang Integrity Policy | `#11 AcceptIntegrityPolicy` | message K.2 | InterviewController.cs:254-257 |
| Chuyển hướng sang trang báo cáo khi phiên đã kết thúc | `#18 ViewEvaluationReport` | message E.2 | InterviewController.cs:217-223 |
| `GetRoomAsync` lần 2 (re-fetch sau generate) | *(chưa có file riêng — xem §12)* | message L.28 ↔ L.29 | InterviewController.cs:276 |
| Trả lời câu hỏi trong phòng | `#5 ConductInterviewQA` | ghi trong nhãn message 10 | interview-room.js:833 |

### Gỡ từ note trên diagram — boundary

- **(cạnh message `4`)** GetRoomAsync is called TWICE in the same action: once as the status guard, once after generate (which materialized the first question and moved the status). Both go through GET api/interview/sessions/{id}/resume -> ResumeSessionAsync, which is not expanded on this page - see FLOWS/EnterInterviewRoom.md section 12. *(gỡ từ note trên diagram)*


---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 0 | User | actor | — | — |
| 1 | :Interview Room UI | box | `Views/Interview/Room.cshtml` + `wwwroot/js/pages/interview-room.js` | — |
| 2 | :InterviewController (WebClient) | box | `WebClient/Controllers/Interview/InterviewController.cs:193-287, 594-604` | — |
| 3 | :InterviewService (WebClient) | box | `WebClient/Services/Interview/InterviewService.cs:196-232, 324-331` | — |
| 4 | :VerificationService (WebClient) | box | `WebClient/Services/Profile/VerificationService.cs` | — |
| 5 | :API Gateway | box | `ApiGateway` (YARP :7000) | — |
| 6 | :InterviewSessionController (InterviewAPI) | box | `InterviewAPI/Controllers/InterviewSessionController.cs:50-63, 481-500` | có |
| 7 | :SessionService (InterviewAPI) | box | `InterviewAPI/Services/SessionService.cs:555-697, 1389-1407` | có |
| 8 | :SessionRepository (InterviewAPI) | box | `InterviewAPI/Repositories/SessionRepository.cs` | có |
| 9 | :SessionVerificationEventRepository (InterviewAPI) | box | `InterviewAPI/Repositories/SessionVerificationEventRepository.cs:47-55` | có |
| 10 | :PaymentApiClient (InterviewAPI) | box | `InterviewAPI/Clients/PaymentApiClient.cs:17-113` | — |
| 11 | :PaymentAPI (:7008) | box | service ngoài quyền sở hữu | **không (bypass)** |
| 12 | SQL Server | cylinder | `DevPrep_Interview` | — |

## 4. BẢNG MESSAGE

### Pha 1 — 3 guard trước khi vào phòng

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Interview Room UI | call | `1. Face verification passed -> redirected to the Room [see #13]` | — | InterviewController.cs:99 | boundary | 1. Face verification passed -> the Verify screen redirects to the Room (#13 VerifyFaceEntryGate) |
| 2 | :Interview Room UI | :InterviewController (WebClient) | call | `2. GET /Interview/Room/{id}` | — | InterviewController.cs:193-203 |  | 2. GET /Interview/Room/{id} - the action first sets Cache-Control: no-store / Pragma / Expires = 0 so Back cannot restore an ended Room from the bfcache |
| 3 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `3. GetRoomAsync(id, ct)` | — | InterviewController.cs:210, InterviewService.cs:206 | boundary | 3. GetRoomAsync(id, ct) - status guard; internally GET api/interview/sessions/{id}/resume -> SessionService.ResumeSessionAsync (see section 2) |
| 4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `4. Return (Success, RoomViewModel, Error)` | — | InterviewService.cs:206-238 | boundary | 4. Return (Success, RoomViewModel{SessionId, Status, Position, Level, RemainingSeconds, Questions, ChatHistory, PauseCount, ...}, Error) |
| D.1 | :InterviewController (WebClient) | :Interview Room UI | ret | `D.1. 302 RedirectToAction(Index)` | D | InterviewController.cs:211-215 |  | D.1. TempData["ErrorMessage"] = statusError ?? "Could not load the session."; 302 RedirectToAction(Index) |
| D.2 | :Interview Room UI | User | ret | `D.2. Interview dashboard with the error banner` | D | InterviewController.cs:214 |  | — |
| E.1 | :InterviewController (WebClient) | :Interview Room UI | ret | `E.1. 302 RedirectToAction("Report", "Evaluation", {...})` | E | InterviewController.cs:217-224 |  | E.1. TempData["ReportInfoMessage"] = "This interview has already ended."; 302 RedirectToAction("Report", "Evaluation", { sessionId, durationActualSec = 0 }) |
| E.2 | :Interview Room UI | User | ret | `E.2. Evaluation report page [see #18]` | E | InterviewController.cs:223 | boundary | E.2. Evaluation report page for the finished session (#18 ViewEvaluationReport) |
| 5 | :InterviewController (WebClient) | :VerificationService (WebClient) | call | `5. GetEntryGateAsync(id, ct) [see #14]` | — | InterviewController.cs:230 | boundary | 5. GetEntryGateAsync(id, ct) - entry gate (full chain in #14 ValidateEntryGateStatus) |
| 6 | :VerificationService (WebClient) | :InterviewController (WebClient) | ret | `6. Return (Success, EntryGateStatusModel, Error)` | — | InterviewController.cs:230 | boundary | 6. Return (Success, EntryGateStatusModel{FaceEnrolled, Allowed, AttemptsRemaining, Reason}, Error) |
| F.1 | :InterviewController (WebClient) | :Interview Room UI | ret | `F.1. 302 RedirectToAction(Index)` | F | InterviewController.cs:231-235 |  | F.1. TempData["ErrorMessage"] = "Khong the xac minh danh tinh, vui long thu lai."; 302 RedirectToAction(Index) - fail CLOSED |
| F.2 | :Interview Room UI | User | ret | `F.2. Interview dashboard with the error banner` | F | InterviewController.cs:234 |  | — |
| G.1 | :InterviewController (WebClient) | :Interview Room UI | ret | `G.1. 302 RedirectToAction(Verify, { id })` | G | InterviewController.cs:239-240 |  | G.1. 302 RedirectToAction(Verify, { id }) - retries left, send the candidate back to the face check |
| G.2 | :Interview Room UI | User | ret | `G.2. Biometric entry-gate screen (#13 VerifyFaceEntryGate)` | G | InterviewController.cs:240 | boundary | — |
| H.1 | :InterviewController (WebClient) | :Interview Room UI | ret | `H.1. 302 RedirectToAction(Index)` | H | InterviewController.cs:242-243 |  | H.1. TempData["ErrorMessage"] = gate.Reason; 302 RedirectToAction(Index) |
| H.2 | :Interview Room UI | User | ret | `H.2. Interview dashboard explaining why the gate is closed` | H | InterviewController.cs:242-243 |  | — |
| 7 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `7. GetIntegrityPolicyStatusAsync(id, ct) [see #11]` | — | InterviewController.cs:247 | boundary | 7. GetIntegrityPolicyStatusAsync(id, ct) - policy gate (full chain in #11 AcceptIntegrityPolicy) |
| 8 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `8. Return ApiCallResult<IntegrityPolicyStatusResponse>` | — | InterviewService.cs:556-559 | boundary | 8. Return ApiCallResult<IntegrityPolicyStatusResponse>{Accepted, AcceptedAt} |
| J.1 | :InterviewController (WebClient) | :Interview Room UI | ret | `J.1. 302 RedirectToAction(Index)` | J | InterviewController.cs:248-252 |  | J.1. TempData["ErrorMessage"] = policyStatus.Error ?? "Could not verify integrity policy."; 302 RedirectToAction(Index) |
| J.2 | :Interview Room UI | User | ret | `J.2. Interview dashboard with the error banner` | J | InterviewController.cs:251 |  | — |
| K.1 | :InterviewController (WebClient) | :Interview Room UI | ret | `K.1. 302 RedirectToAction(IntegrityPolicy, { id })` | K | InterviewController.cs:254-257 |  | — |
| K.2 | :Interview Room UI | User | ret | `K.2. Integrity policy page (#11 AcceptIntegrityPolicy)` | K | InterviewController.cs:256 | boundary | — |

### Pha 2 — materialize câu hỏi đầu tiên + trừ credit (`opt L`)

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| L.1 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `L.1. GenerateQuestionAsync(id, ct)` | L | InterviewController.cs:267 |  | — |
| L.2 | :InterviewService (WebClient) | :API Gateway | call | `L.2. POST api/interview/sessions/{id}/questions/generate` | L | InterviewService.cs:196-204 |  | — |
| L.3 | :API Gateway | :InterviewSessionController | call | `L.3. Forward (route interview-route, no prefix strip)` | L | ApiGateway/appsettings.json:49-54 |  | — |
| L.4 | :InterviewSessionController | (self) | call | `L.4. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | L | InterviewSessionController.cs:487 | self | — |
| L.5 | :InterviewSessionController | :SessionService | call | `L.5. GenerateQuestionAsync(userId, sessionId, ct)` | L | InterviewSessionController.cs:488 |  | — |
| L.6 | :SessionService | :SessionRepository | call | `L.6. GetWithQuestionsAsync(sessionId, ct) - Step A` | L | SessionService.cs:559 |  | — |
| L.7 | :SessionRepository | SQL Server | call | `L.7. SELECT InterviewSession + SessionQuestions` | L | SessionRepository.cs:36-39 |  | L.7. SELECT InterviewSession + SessionQuestions ORDER BY OrderIndex |
| L.8 | SQL Server | :SessionRepository | ret | `L.8. Return the session graph / null` | L | SessionRepository.cs:36-39 |  | — |
| L.9 | :SessionRepository | :SessionService | ret | `L.9. Return InterviewSession?` | L | SessionRepository.cs:36-39 |  | — |
| M.1 | :SessionService | :InterviewSessionController | ret | `M.1. throw InvalidOperationException` | M | SessionService.cs:560-563 | throw | M.1. throw InvalidOperationException("Session not found.") / ("Session not available.") |
| M.2 | :InterviewSessionController | :API Gateway | ret | `M.2. Return 400 ApiResponse.Fail(ex.Message)` | M | InterviewSessionController.cs:496-499 |  | — |
| M.3 | :API Gateway | :InterviewService (WebClient) | ret | `M.3. Return 400` | M | ApiGateway/appsettings.json:49-54 |  | — |
| M.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `M.4. Return (false, error)` | M | InterviewService.cs:196-204 |  | — |
| M.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `M.5. 302 RedirectToAction(Index)` | M | InterviewController.cs:268-272 |  | M.5. TempData["ErrorMessage"] = generateError ?? "Could not load the interview question."; 302 RedirectToAction(Index) |
| M.6 | :Interview Room UI | User | ret | `M.6. Interview dashboard with the error banner` | M | InterviewController.cs:271 |  | — |
| L.10 | :SessionService | :SessionVerificationEventRepository | call | `L.10. ListBySessionAsync(sessionId, "Entry", ct) [see #14]` | L | SessionService.cs:569-571 | boundary | L.10. ListBySessionAsync(sessionId, "Entry", ct) - Step A.1 server-enforced entry gate (#14 ValidateEntryGateStatus) |
| L.11 | :SessionVerificationEventRepository | :SessionService | ret | `L.11. Return the Entry events` | L | SessionService.cs:572-573 | boundary | L.11. Return the Entry events; at least one with Modality="Face" and Decision="Pass" is required |
| N.1 | :SessionService | :InterviewSessionController | ret | `N.1. throw InvalidOperationException` | N | SessionService.cs:574-575 | throw | N.1. throw InvalidOperationException("Identity verification required before starting the interview.") |
| N.2 | :InterviewSessionController | :API Gateway | ret | `N.2. Return 400 ApiResponse.Fail(ex.Message)` | N | InterviewSessionController.cs:496-499 |  | — |
| N.3 | :API Gateway | :InterviewService (WebClient) | ret | `N.3. Return 400` | N | ApiGateway/appsettings.json:49-54 |  | — |
| N.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `N.4. Return (false, error)` | N | InterviewService.cs:196-204 |  | — |
| N.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `N.5. TempData["ErrorMessage"]; 302 RedirectToAction(Index)` | N | InterviewController.cs:268-272 |  | — |
| N.6 | :Interview Room UI | User | ret | `N.6. Interview dashboard with the error banner` | N | InterviewController.cs:271 |  | — |
| P.1 | :SessionService | :InterviewSessionController | ret | `P.1. throw SessionGuardException(403, ...)` | P | SessionService.cs:581, 51-56 | throw | P.1. throw SessionGuardException(403, "integrity_policy_not_accepted", "You must accept the interview integrity policy before continuing.") - Step A.2 |
| P.2 | :InterviewSessionController | :API Gateway | ret | `P.2. Return 403 {error}` | P | InterviewSessionController.cs:490-495 |  | P.2. Return 403 { error: "integrity_policy_not_accepted", message } |
| P.3 | :API Gateway | :InterviewService (WebClient) | ret | `P.3. Return 403` | P | ApiGateway/appsettings.json:49-54 |  | — |
| P.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `P.4. Return (false, error)` | P | InterviewService.cs:196-204 |  | — |
| P.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `P.5. TempData["ErrorMessage"]; 302 RedirectToAction(Index)` | P | InterviewController.cs:268-272 |  | — |
| P.6 | :Interview Room UI | User | ret | `P.6. Interview dashboard with the error banner` | P | InterviewController.cs:271 |  | — |
| L.12 | :SessionService | SQL Server | call | `L.12. SELECT EXISTS ChatMessages WHERE MessageType="Answer"` | L | SessionService.cs:584-597 |  | L.12. Step B: SELECT EXISTS ChatMessages (SenderType="User", MessageType="Answer") per SessionQuestion in OrderIndex order until the first unanswered one (direct EF query on _db) |
| L.13 | SQL Server | :SessionService | ret | `L.13. Return the first unanswered SessionQuestion` | L | SessionService.cs:592-596 |  | — |
| L.14 | :SessionService | :SessionRepository | call | `L.14. SaveChangesAsync(ct) after nextQuestion.AskedAt = now` | L | SessionService.cs:604-608 |  | L.14. SaveChangesAsync(ct) after nextQuestion.AskedAt = now - Step C, only on the first serve |
| L.15 | :SessionRepository | SQL Server | call | `L.15. UPDATE SessionQuestion SET AskedAt` | L | SessionRepository.cs:348 |  | — |
| L.16 | SQL Server | :SessionRepository | ret | `L.16. Return rows affected` | L | SessionRepository.cs:348 |  | — |
| L.17 | :SessionRepository | :SessionService | ret | `L.17. Return` | L | SessionRepository.cs:348 |  | — |
| L.18 | :SessionService | :SessionRepository | call | `L.18. UpdateStatusIfAsync(..., "Initialized", "InProgress")` | L | SessionService.cs:611-614 |  | L.18. UpdateStatusIfAsync(userId, sessionId, "Initialized", "InProgress", ct) - Step D atomic |
| L.19 | :SessionRepository | SQL Server | call | `L.19. UPDATE InterviewSessions SET Status="InProgress"` | L | SessionRepository.cs:225-228 |  | L.19. atomic UPDATE InterviewSessions SET Status="InProgress", UpdatedAt WHERE UserId AND Status="Initialized" AND !IsDeleted |
| L.20 | SQL Server | :SessionRepository | ret | `L.20. Return rows affected` | L | SessionRepository.cs:225-231 |  | — |
| L.21 | :SessionRepository | :SessionService | ret | `L.21. Return statusChanged (rowsAffected > 0)` | L | SessionRepository.cs:231 |  | — |
| Q.1 | :SessionService | :PaymentApiClient | call | `Q.1. DeductCreditAsync(userId, creditCost, sessionId, ...)` | Q | SessionService.cs:619-623, CreditConfig.cs:10, 24-27 |  | Q.1. DeductCreditAsync(userId, CreditConfig.GetCreditCost(DurationMinutes) = 2 credits per minute, sessionId, "Interview session N minutes") - Step D.1 |
| Q.2 | :PaymentApiClient | :PaymentAPI (:7008) | call | `Q.2. POST /api/internal/credit/deduct` | Q | PaymentApiClient.cs:30-31, Program.cs:49, 80-87 |  | Q.2. POST /api/internal/credit/deduct { UserId, Amount, ReferenceId = sessionId, Description } - DIRECT to PaymentAPI :7008, NOT through the Gateway |
| Q.3 | :PaymentAPI (:7008) | :PaymentApiClient | ret | `Q.3. Return 200 CreditDeductionResponse / non-2xx` | Q | PaymentApiClient.cs:45-53 |  | Q.3. 200 CreditDeductionResponse{Success, BalanceAfter, AmountDeducted} / non-2xx |
| Q.4 | :PaymentApiClient | :SessionService | ret | `Q.4. Return CreditDeductionResponse` | Q | PaymentApiClient.cs:33-63 |  | Q.4. Return CreditDeductionResponse - a non-2xx or an exception is turned into Success=false, never thrown |
| R.1 | :SessionService | :SessionRepository | call | `R.1. UpdateStatusIfAsync(..., "InProgress", "Initialized")` | R | SessionService.cs:625-629 |  | R.1. UpdateStatusIfAsync(userId, sessionId, "InProgress", "Initialized", ct) - roll the status back (race-safe: only this flow can move it from InProgress here) |
| R.2 | :SessionRepository | SQL Server | call | `R.2. UPDATE InterviewSessions SET Status="Initialized"` | R | SessionRepository.cs:225-228 |  | R.2. atomic UPDATE InterviewSessions SET Status="Initialized" WHERE Status="InProgress" |
| R.3 | SQL Server | :SessionRepository | ret | `R.3. Return rows affected` | R | SessionRepository.cs:225-231 |  | — |
| R.4 | :SessionRepository | :SessionService | ret | `R.4. Return` | R | SessionRepository.cs:231 |  | — |
| R.5 | :SessionService | :InterviewSessionController | ret | `R.5. throw InvalidOperationException` | R | SessionService.cs:630 | throw | R.5. throw InvalidOperationException("Credit deduction failed: <message>") |
| R.6 | :InterviewSessionController | :API Gateway | ret | `R.6. Return 400 ApiResponse.Fail(ex.Message)` | R | InterviewSessionController.cs:496-499 |  | — |
| R.7 | :API Gateway | :InterviewService (WebClient) | ret | `R.7. Return 400` | R | ApiGateway/appsettings.json:49-54 |  | — |
| R.8 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `R.8. Return (false, error)` | R | InterviewService.cs:196-204 |  | — |
| R.9 | :InterviewController (WebClient) | :Interview Room UI | ret | `R.9. TempData["ErrorMessage"]; 302 RedirectToAction(Index)` | R | InterviewController.cs:268-272 |  | — |
| R.10 | :Interview Room UI | User | ret | `R.10. Interview dashboard with the error banner` | R | InterviewController.cs:271 |  | R.10. Interview dashboard with the error banner - no question served, the session is back to Initialized and nothing was charged |
| Q.5 | :SessionService | :SessionRepository | call | `Q.5. SaveChangesAsync after CreditCost and StartedAt = now` | Q | SessionService.cs:636-643 |  | Q.5. GetByIdAsync(sessionId) then SaveChangesAsync after CreditCost = creditCost and StartedAt = now - the interview clock starts HERE, not at Init |
| Q.6 | :SessionRepository | SQL Server | call | `Q.6. UPDATE InterviewSessions SET CreditCost, StartedAt` | Q | SessionRepository.cs:21-24, 348 |  | Q.6. SELECT InterviewSession; UPDATE InterviewSession SET CreditCost, StartedAt, UpdatedAt |
| Q.7 | SQL Server | :SessionRepository | ret | `Q.7. Return rows affected` | Q | SessionRepository.cs:348 |  | — |
| Q.8 | :SessionRepository | :SessionService | ret | `Q.8. Return` | Q | SessionRepository.cs:348 |  | — |
| L.22 | :SessionService | SQL Server | call | `L.22. SELECT EXISTS ChatMessages WHERE MessageType="Question"` | L | SessionService.cs:651-655 |  | L.22. Step E guard: SELECT EXISTS ChatMessages WHERE SessionQuestionId AND SenderType="AI" AND MessageType="Question" (defensive idempotency, direct EF query) |
| L.23 | SQL Server | :SessionService | ret | `L.23. Return alreadyAsked` | L | SessionService.cs:651-655 |  | — |
| S.1 | :SessionService | :SessionRepository | call | `S.1. AddChatMessageAsync(ChatMessage{SenderType="AI"})` | S | SessionService.cs:658-669 |  | S.1. AddChatMessageAsync(ChatMessage{SenderType="AI", MessageType="Question", Content = nextQuestion.Content}) - Step E |
| S.2 | :SessionRepository | SQL Server | call | `S.2. INSERT ChatMessage (SaveChangesAsync)` | S | SessionRepository.cs:68-73 |  | — |
| S.3 | SQL Server | :SessionRepository | ret | `S.3. Return rows affected` | S | SessionRepository.cs:71 |  | — |
| S.4 | :SessionRepository | :SessionService | ret | `S.4. Return ChatMessage` | S | SessionRepository.cs:72 |  | — |
| T.1 | :SessionService | :PaymentApiClient | call | `T.1. RefundCreditAsync(userId, creditCost, sessionId, ...)` | T | SessionService.cs:671-679 |  | T.1. RefundCreditAsync(userId, creditCost, sessionId, "Refund: failed to initialize session") - only when the credit was actually deducted on THIS call |
| T.2 | :PaymentApiClient | :PaymentAPI (:7008) | call | `T.2. POST /api/internal/credit/refund` | T | PaymentApiClient.cs:79-80 |  | T.2. POST /api/internal/credit/refund - DIRECT to PaymentAPI :7008, NOT through the Gateway |
| T.3 | :PaymentAPI (:7008) | :PaymentApiClient | ret | `T.3. Return 200 CreditDeductionResponse / non-2xx` | T | PaymentApiClient.cs:82-112 |  | T.3. 200 CreditDeductionResponse / non-2xx - a failed refund is only logged |
| T.4 | :PaymentApiClient | :SessionService | ret | `T.4. Return CreditDeductionResponse` | T | PaymentApiClient.cs:102 |  | — |
| T.5 | :SessionService | :InterviewSessionController | ret | `T.5. rethrow the original exception` | T | SessionService.cs:680 | throw | — |
| T.6 | :InterviewSessionController | :API Gateway | ret | `T.6. Return 400 ApiResponse.Fail(ex.Message)` | T | InterviewSessionController.cs:496-499 |  | — |
| T.7 | :API Gateway | :InterviewService (WebClient) | ret | `T.7. Return 400` | T | ApiGateway/appsettings.json:49-54 |  | — |
| T.8 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `T.8. Return (false, error)` | T | InterviewService.cs:196-204 |  | — |
| T.9 | :InterviewController (WebClient) | :Interview Room UI | ret | `T.9. TempData["ErrorMessage"]; 302 RedirectToAction(Index)` | T | InterviewController.cs:268-272 |  | — |
| T.10 | :Interview Room UI | User | ret | `T.10. Interview dashboard with the error banner` | T | InterviewController.cs:271 |  | T.10. Interview dashboard with the error banner; the credit has been refunded |
| L.24 | :SessionService | :InterviewSessionController | ret | `L.24. Return GenerateQuestionResponse` | L | SessionService.cs:683-696 |  | L.24. Return GenerateQuestionResponse{SessionQuestionId, OrderIndex, Content, TotalQuestions, Ended=false, IdleReminderSeconds=90, IdleHardSeconds=180, IdleElapsedSeconds} - Step F, no ReferenceAnswer |
| L.25 | :InterviewSessionController | :API Gateway | ret | `L.25. Return 200 ApiResponse.Ok(result)` | L | InterviewSessionController.cs:489 |  | — |
| L.26 | :API Gateway | :InterviewService (WebClient) | ret | `L.26. Return 200` | L | ApiGateway/appsettings.json:49-54 |  | — |
| L.27 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `L.27. Return (true, null)` | L | InterviewService.cs:196-204 |  | — |
| L.28 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `L.28. GetRoomAsync(id, ct) AGAIN` | L | InterviewController.cs:274-276 | boundary | L.28. GetRoomAsync(id, ct) AGAIN - generate materialized the first question and advanced the status, neither of which the snapshot from message 3 reflects |
| L.29 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `L.29. Return the fresh RoomViewModel` | L | InterviewService.cs:206-238 | boundary | — |
| U.1 | :InterviewController (WebClient) | :Interview Room UI | ret | `U.1. 302 RedirectToAction(Index)` | U | InterviewController.cs:277-281 |  | U.1. TempData["ErrorMessage"] = error ?? "Could not load the session."; 302 RedirectToAction(Index) |
| U.2 | :Interview Room UI | User | ret | `U.2. Interview dashboard with the error banner` | U | InterviewController.cs:280 |  | — |

### Pha 3 — render phòng + Product Tour lần đầu

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 9 | :InterviewController (WebClient) | :Interview Room UI | ret | `9. Return View Room (RoomViewModel)` | — | InterviewController.cs:286 |  | — |
| V.1 | :Interview Room UI | :InterviewController (WebClient) | call | `V.1. First-ever Room visit -> POST /Interview/TourSeen` | V | interview-room.js:1669-1681, 306-400 |  | V.1. First-ever Room visit: the driver.js product tour runs over mic / AI Voice / chat / camera / hint / skip, and on close POSTs /Interview/TourSeen |
| V.2 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `V.2. MarkRoomTourSeenAsync(ct)` | V | InterviewController.cs:594-597 |  | — |
| V.3 | :InterviewService (WebClient) | :API Gateway | call | `V.3. POST api/interview/room-tour/seen` | V | InterviewService.cs:324-331 |  | — |
| V.4 | :API Gateway | :InterviewSessionController | call | `V.4. Forward (route interview-route, no prefix strip)` | V | ApiGateway/appsettings.json:49-54 |  | — |
| V.5 | :InterviewSessionController | :SessionService | call | `V.5. MarkRoomTourSeenAsync(userId, ct)` | V | InterviewSessionController.cs:55-56 |  | — |
| V.6 | :SessionService | SQL Server | call | `V.6. UPSERT UserRoomTourStatus SET HasSeenRoomTour = true` | V | SessionService.cs:1389-1407 |  | V.6. UPSERT UserRoomTourStatus{UserId, HasSeenRoomTour = true, UpdatedAt} - ONE row per USER, ever, not per session (direct EF query on _db) |
| V.7 | SQL Server | :SessionService | ret | `V.7. Return rows affected` | V | SessionService.cs:1406 |  | — |
| V.8 | :SessionService | :InterviewSessionController | ret | `V.8. Return` | V | SessionService.cs:1406 |  | — |
| V.9 | :InterviewSessionController | :API Gateway | ret | `V.9. Return 200 ApiResponse.Ok(new {}, "Marked.")` | V | InterviewSessionController.cs:57 |  | — |
| V.10 | :API Gateway | :InterviewService (WebClient) | ret | `V.10. Return 200` | V | ApiGateway/appsettings.json:49-54 |  | — |
| V.11 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `V.11. Return (true, null)` | V | InterviewService.cs:330 |  | — |
| V.12 | :InterviewController (WebClient) | :Interview Room UI | ret | `V.12. Return 200` | V | InterviewController.cs:598-604, interview-room.js:1674-1678 |  | V.12. Return 200 - the tour never appears again for this user, and only now does the greeting start being read aloud |
| 10 | :Interview Room UI | User | ret | `10. Interview Room: first question, countdown, camera [see #5]` | — | Room.cshtml, interview-room.js:1655-1685 | boundary | 10. Interview Room: first question in the chat, countdown running, camera on, AI Voice reads the greeting and the question (#6 VoiceInteraction); answering continues in #5 ConductInterviewQA |

**Cờ:** `self` · `throw` · `boundary`

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| D | opt | `[!statusOk OR room is null]` | D.1 → D.2 | — | — |
| E | opt | `[room.Status not in ("Initialized","InProgress","Paused")]` | E.1 → E.2 | — | — |
| F | opt | `[!gateOk OR gate is null - fail closed]` | F.1 → F.2 | — | — |
| G | opt | `[!gate.Allowed AND gate.FaceEnrolled AND gate.AttemptsRemaining > 0]` | G.1 → G.2 | — | — |
| H | opt | `[!gate.Allowed AND (no face enrollment OR no attempts left)]` | H.1 → H.2 | — | — |
| J | opt | `[!policyStatus.Success]` | J.1 → J.2 | — | — |
| K | opt | `[policyStatus.Data?.Accepted != true]` | K.1 → K.2 | — | — |
| L | opt | `[room.Status is "Initialized" or "InProgress" - a Paused session already has its current question and generate would reject it]` | L.1 → U.2 | — | — |
| M | opt | `[session is null / not owned OR session.Status not in ("Initialized","InProgress")]` | M.1 → M.6 | L | **2** (cùng 400 qua đúng một `catch (Exception)`, cùng banner Dashboard) |
| N | opt | `[no Entry event with Modality="Face" and Decision="Pass"]` | N.1 → N.6 | L | — |
| P | opt | `[!session.IntegrityPolicyAccepted]` | P.1 → P.6 | L | — |
| Q | opt | `[isFirstEntry AND statusChanged - the very first Room entry]` | Q.1 → Q.8 | L | — |
| R | opt | `[!deductResult.Success - not enough credit, or PaymentAPI unreachable]` | R.1 → R.10 | Q | — |
| S | opt | `[!alreadyAsked - no AI Question message exists for this SessionQuestion yet]` | S.1 → S.4 | L | — |
| T | opt | `[the ChatMessage insert throws AFTER the credit was already deducted on this call]` | T.1 → T.10 | L | — |
| U | opt | `[the GetRoomAsync re-fetch failed]` | U.1 → U.2 | L | — |
| V | opt | `[first-ever Room visit for this user - hasSeenRoomTour is false]` | V.1 → V.12 | — | — |

> `opt L`, `opt Q` và `opt S` **tự khép kín** (chỉ mở/đóng đúng các call của chính chúng) → luồng
> vẫn chạy tiếp vào message 9 dù guard đúng hay sai.

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | 3 / L.28 | `GetRoomAsync is called TWICE in the same action: once as the status guard, once after generate (which materialized the first question and moved the status). Both go through GET api/interview/sessions/{id}/resume -> ResumeSessionAsync, which is not expanded on this page - see section 12.` |
| N2 | Q.1 | `This is where the interview is actually paid for: 2 credits per minute (CreditConfig), charged ONCE, on the first Room entry - after Verify and the Integrity Policy, never at configuration or Init time. A candidate who abandons the flow before this point is charged nothing.` |
| N3 | Q.2 / T.2 | `Both PaymentAPI calls go DIRECTLY to :7008 (InterviewAPI/Program.cs:49, 80-87), never through the Gateway. PaymentApiClient never throws: a non-2xx or a transport error is turned into Success=false, which is exactly what opt R reacts to.` |
| N4 | R / T | `Two different compensations, on purpose. opt R: the charge itself failed -> roll the status back to Initialized so a retry can start clean. opt T: the charge SUCCEEDED but storing the first question failed -> refund, because the candidate paid for an interview that never started.` |
| N5 | L.18 / Q.5 | `StartedAt is set here, not at Init: Verify (up to 3 face attempts) plus reading the Integrity Policy takes about 20s, and that time must not count against the interview clock.` |
| N6 | V | `The tour flag lives in UserRoomTourStatuses keyed by UserId only - one row per user for their whole lifetime, not per session. The tour also blocks the greeting TTS until it is closed so a first-timer is not read to while reading popovers.` |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | Liệt kê | Nguồn |
|---|---|---|---|---|
| 1 | InterviewController (WebClient) | class | SUBSET (`Room`, `TourSeen`) | local |
| 2 | Controller | framework-base | FULL (rỗng) | SHARED |
| 3 | IInterviewService (WebClient) | interface | SUBSET | local |
| 4 | InterviewService (WebClient) | class | SUBSET | local |
| 5 | IVerificationService (WebClient) | interface | SUBSET | local |
| 6 | IApiClient / 7 ApiClient | interface/class | FULL | SHARED |
| 8 | RoomViewModel / 9 EntryGateStatusModel / 10 ApiResponse<T> (WebClient) | DTO | FULL | local |
| 11 | ControllerBase | framework-base | FULL (rỗng) | SHARED |
| 12 | InterviewSessionController (InterviewAPI) | class | SUBSET (`GenerateQuestion`, `MarkRoomTourSeen`) | local |
| 13 | ISessionService / 14 SessionService (InterviewAPI) | interface/class | SUBSET | local |
| 15 | SessionGuardException | exception | FULL | local |
| 16 | CreditConfig | helper | FULL | local |
| 17 | ApiResponse<T> (InterviewAPI) | DTO | FULL | SHARED |
| 18 | GenerateQuestionResponse | DTO | FULL | local |
| 19 | IPaymentApiClient / 20 PaymentApiClient (InterviewAPI) | interface/class | FULL | local |
| 21 | CreditDeductionResponse / 22 CreditAwardResponse | DTO | FULL | local |
| 23 | ISessionRepository / 24 SessionRepository (InterviewAPI) | interface/class | SUBSET | local |
| 25 | ISessionVerificationEventRepository / 26 SessionVerificationEventRepository (InterviewAPI) | interface/class | FULL | local |
| 27 | InterviewSession / 28 SessionQuestion / 29 ChatMessage / 30 SessionVerificationEvent | entity | FULL | SHARED |
| 31 | UserRoomTourStatus | entity | FULL | local |
| 32 | InterviewDbContext / 33 DbContext | class/framework-base | FULL | SHARED |

## 8. CLASS — chi tiết

### `InterviewController (WebClient)` — `SUBSET`
- Field `- interviewService: IInterviewService`, `- verificationService: IVerificationService`
- Method `+ Room(id: Guid, cancellationToken): Task<IActionResult>`,
  `+ TourSeen(cancellationToken): Task<IActionResult>`

### `SessionService (InterviewAPI)` — `SUBSET`
- 10 field DI + hằng `BiometricsEnabled`
- Method `+ GenerateQuestionAsync(...)`, `+ MarkRoomTourSeenAsync(...)`,
  `- RequireIntegrityPolicyAccepted(session)`, `- GetIdleThresholds(difficulty)`,
  `- GetEffectivePausedSeconds(session)`

### `SessionRepository (InterviewAPI)` — `SUBSET`
- `+ GetWithQuestionsAsync`, `+ GetByIdAsync`, `+ UpdateStatusIfAsync`, `+ AddChatMessageAsync`,
  `+ SaveChangesAsync`

Không có class SHARED mới trong chức năng này.

## 9. QUAN HỆ CLASS

Xem `specs/enterinterviewroom_classes.yaml` khoá `relations:`.

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| — | — | Không phát hiện anomaly mới | — | — | — |

> Đã kiểm và **bác** 3 nghi vấn:
> (a) `opt T` (hoàn credit) trông như bù trừ cho MỌI lỗi init — **không**: nó chỉ bọc đúng lời gọi
> `AddChatMessageAsync` và chỉ chạy khi `isFirstEntry && statusChanged && creditCost > 0`
> ([SessionService.cs:671-681](../../../source/InterviewAPI/Services/SessionService.cs#L671-L681)).
> Lỗi ở `InitAsync` (chưa trừ credit) hay lỗi re-fetch `GetRoomAsync` sau đó **không** kích hoạt hoàn.
> Đây đúng như RECON đã lưu ý, và hẹp hơn tên gọi trong `DIAGRAM_SCOPE.md`.
> (b) Guard `alreadyAsked` (`SessionService.cs:651-655`) trông như thừa vì endpoint chỉ gọi 1 lần —
> **không thừa**: action `Room` gọi generate trên **mọi** lần vào/refresh phòng khi Status còn
> `Initialized`/`InProgress`, nên không có nó thì F5 sẽ chèn thêm 1 bong bóng câu hỏi trùng.
> (c) KI-01 **không xuất hiện** trong `Room`/`TourSeen` — cả hai trả `View`/`Redirect`/`Ok`, không
> forward status code thô (đã đọc toàn bộ `InterviewController.cs`).

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | Card `#4` vượt cả 2 ngưỡng | **Tách 2 file**; file này giữ toàn bộ luồng vào phòng | **Đã hỏi và được DuyenTQ duyệt** (lô G). Ranh giới trùng ranh giới HTTP request. |
| 2 | `ResumeSessionAsync` được gọi 2 lần và chưa có file diagram riêng | **Thu về 1 cặp call+ret mỗi lần + ghi §12 là khoảng trống thật của bộ diagram** | Vẽ đầy đủ 2 lần (~40 message) sẽ đẩy trang vượt ngưỡng lần nữa. Ghi rõ để nhóm quyết có làm file riêng hay không. |
| 3 | 2 câu lỗi của Step A trong `GenerateQuestionAsync` ("Session not found." / "Session not available.") | **Gộp 1 `opt M`**, nhãn ghi đủ 2 câu | Cùng `catch (Exception)`, cùng 400, cùng banner Dashboard — điều kiện gộp đã dùng ở lô E/F/G. |
| 4 | 3 guard vào phòng (status / entry gate / policy) | **Mỗi cái 1 cặp call+ret boundary** | Đã vẽ đầy đủ ở `#14` và `#11`; đây chính là cơ chế giữ trang khỏi phình. |
| 5 | Nhánh `Ended=true` của `GenerateQuestionAsync` | **Không vẽ (§1)** | Không tới được từ action `Room` (xem §1 để biết lý do chính xác). |
| 6 | `opt G` và `opt H` (2 đích chuyển hướng khác nhau khi gate từ chối) | **2 `opt` riêng, không gộp** | Cùng 302 nhưng **đích khác nhau** (Verify vs Dashboard) → user thấy 2 trang khác nhau, không đủ điều kiện gộp. |
| 7 | Vị trí `opt V` (Product Tour) so với message 10 (trả về User) | **Đặt `opt V` TRƯỚC message 10**, và bỏ message ret cuối của khung (gộp câu "tour không hiện lại nữa, giờ TTS mới đọc" vào nhãn `V.12`) | Tour do JS tự chạy sau khi render, **không** có thao tác mới của user mở nó ra, nên khung không được phép đóng lời gọi `User → UI` của message 1 (RULE3b). Đặt trước message 10 vừa đúng thứ tự thật (tour chặn TTS lời chào) vừa để khung tự khép kín. |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `D.2`)** This is where the interview is actually paid for: 2 credits per minute (CreditConfig), charged ONCE, on the first Room entry - after Verify and the Integrity Policy, never at configuration or Init time. A candidate who abandons the flow before this point is charged nothing. *(gỡ từ note trên diagram)*
- **(cạnh message `5`)** Both PaymentAPI calls go DIRECTLY to :7008 (InterviewAPI/Program.cs:49, 80-87), never through the Gateway. PaymentApiClient never throws: a non-2xx or a transport error is turned into Success=false, which is exactly what opt R reacts to. *(gỡ từ note trên diagram)*
- **(cạnh message `F.2`)** Two different compensations, on purpose. opt R: the charge itself failed -> roll the status back to Initialized so a retry can start clean. opt T: the charge SUCCEEDED but storing the first question failed -> refund, because the candidate paid for an interview that never started. *(gỡ từ note trên diagram)*
- **(cạnh message `G.2`)** StartedAt is set here, not at Init: Verify (up to 3 face attempts) plus reading the Integrity Policy takes about 20s, and that time must not count against the interview clock. *(gỡ từ note trên diagram)*
- **(cạnh message `H.2`)** The tour flag lives in UserRoomTourStatuses keyed by UserId only - one row per user for their whole lifetime, not per session. The tour also blocks the greeting TTS until it is closed so a first-timer is not read to while reading popovers. *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| `uc_id` của chức năng | RECON không ghi UC-id cho #4 | Nhóm xác nhận ê-líp use-case rồi điền vào `specs/enterinterviewroom.yaml` | luồng 1 |
| **`ResumeSessionAsync` chưa được vẽ chi tiết ở BẤT KỲ file nào** | Không thiếu dữ liệu — đã đọc đủ (`SessionService.cs:1267-1387`: self-heal pause + finalize hết giờ + settle verification check + tái tạo chat history + tính timer + `AttachVerificationCheckAsync`). Vấn đề là ngân sách trang: nó được gọi 2 lần trong file này và cũng là nguồn của `GetRoomAsync` ở nơi khác | DuyenTQ quyết: (a) chấp nhận thu gọn như hiện tại, hoặc (b) làm thêm file `ResumeInterviewSession.drawio` (~40-50 message) rồi trỏ boundary vào đó | luồng 2 |
| BL#54 / BL#38 có đúng ứng với credit và room tour không | File `SEP490_Backlog__2_.xlsx` không có trong repo | Nhóm đối chiếu backlog | luồng 2 |
