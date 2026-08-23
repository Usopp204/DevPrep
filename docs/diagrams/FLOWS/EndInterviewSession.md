# FLOW — `EndInterviewSession` · BL#37, 61 · UC-`09`

> Đặt tại `docs/diagrams/FLOWS/EndInterviewSession.md`
> Trạng thái: `DRAFT` — Khảo sát: lô B, 2026-08-03

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Kết thúc một phiên phỏng vấn: (BL#37) tự chốt khi hết giờ / hết câu hỏi → `Completed` hoặc `Expired`; (BL#61) ứng viên chủ động huỷ → `Cancelled`. Cả 2 đều đóng `InterviewRoom`. |
| Điểm vào (user thao tác gì) | BL#37: countdown về 0 (tự động, user đang ngồi trước màn hình) hoặc server báo hết câu hỏi. BL#61: bấm **Cancel Interview** rồi xác nhận trong hộp thoại |
| Kết thúc (user thấy gì) | `Completed` → chuyển sang trang Evaluation report. `Expired`/`Cancelled` → chuyển sang trang Session details |
| Quy mô | `9` participant · `105` message · `6` alt / `6` opt · trang Sequence 1790×5351 |
| Đối chiếu ước lượng RECON | RECON đoán `16` · thật `105` · hệ số `6.6×` |
| Service ngoài quyền sở hữu | không |
| **Gọi BYPASS Gateway?** | không |
| Nhà cung cấp AI | không (TTS đọc lời tạm biệt là `speak()` thuộc #6 VoiceInteraction, xem §1) |
| Ghi DB | `InterviewSessions` UPDATE `Status`/`EndedAt`/`UpdatedAt`; `InterviewRooms` UPDATE `Status="Closed"`/`ClosedAt`. Complete còn ĐỌC `ChatMessages` qua `HasSubstantiveAnswerAsync` |
| Trừ credit | không — và **không hoàn credit** khi huỷ (cảnh báo này nằm trong hộp thoại xác nhận) |
| KI liên quan | không (không phát hiện KI mới) |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| `FinalizeIfTimeExpiredAsync` (lazy finalize khi user quay lại phiên bỏ dở) | [SessionService.cs:143-160](../../../source/InterviewAPI/Services/SessionService.cs#L143-L160) | Cùng "chốt Completed/Expired" nhưng là luồng khác (`ResumeSessionAsync`, `SendMessageAsync`), không nằm trong `CompleteSessionAsync`. |
| `EndSessionForCheatingAsync` (đóng phiên vì gian lận) | [SessionService.cs:1234-1250](../../../source/InterviewAPI/Services/SessionService.cs#L1234-L1250) (nhánh trong `ReportCameraViolationAsync`) | Thuộc `#10 ReportCameraViolation` / `#15 VerifyFaceInSession`, không phải endpoint `/complete` hay `/end`. |
| `speak(closing)` — đọc lời tạm biệt bằng TTS | [interview-room.js:712-716](../../../source/WebClient/wwwroot/js/pages/interview-room.js#L712-L716) | Chi tiết của `#6 VoiceInteraction`. Ở đây chỉ ghi vào nhãn self-message là "chờ speak xong, tối đa 10s" — không mở rộng chuỗi gọi TTS. |
| `window.confirm` fallback khi trình duyệt không hỗ trợ `<dialog>` | [interview-room.js:649-657](../../../source/WebClient/wwwroot/js/pages/interview-room.js#L649-L657) | Nhánh tương thích trình duyệt, cùng cảnh báo và cùng kết cục — không đổi luồng nghiệp vụ. |
| `SendMessageAsync` phát hiện hết câu hỏi / hết giờ | ngoài phạm vi | Thuộc `#5 ConductInterviewQA`. Ở đây chỉ là **nguồn kích hoạt** của message 1, ghi vào nhãn. |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| *(không có cặp call+return sang chức năng khác)* | — | — | — |

> `completeInterview()` được gọi từ 5 chỗ, trong đó 4 chỗ (`handleSessionExpired`, `sendAnswer`,
> `skip`, `hint`) thuộc `#5 ConductInterviewQA`. Chúng không phải "gọi sang chức năng khác" mà là
> **caller** của chức năng này → ghi gộp vào nhãn message 1, không vẽ thêm participant.

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

> Nhánh auto-complete chạy khi user **vẫn đang ngồi trước màn hình** (countdown chạy trong tab đang
> mở) → theo quy ước ACTOR GIÁN TIẾP vẫn giữ actor `User`, nhãn message 1 ghi rõ `[auto]`.

## 4. BẢNG MESSAGE

> Toàn bộ nằm trong 1 khung `alt` cấp cao nhất (2 nhánh kết thúc loại trừ nhau).

### Nhánh A — auto-complete (BL#37), guard `[interview deadline reached OR the server reports the session is over]`

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Interview Room UI | call | `1. [auto] countdown hit 0 -> completeInterview()` | A | interview-room.js:606-608, 830, 867, 942, 998 |  | 1. [auto] countdown hit 0 -> completeInterview() (also reached from sendAnswer / skip / hint when the server reports the session is over) |
| 2 | :Interview Room UI | (self) | call | `2. completeInterview(): cleanupTransientUi(); ended=true` | A | interview-room.js:677-692 | self | 2. completeInterview(): guard ended; cleanupTransientUi(); ended=true; clear camera/skip timers; clearDraft(); hideIdleBanner(); disableLifecycleControls(); setStatusDisplay("Finalizing...") |
| 3 | :Interview Room UI | :InterviewController (WebClient) | call | `3. POST /Interview/CompleteSession?id={id}` | A | interview-room.js:696, 141-146 |  | 3. POST /Interview/CompleteSession?id={sessionId} (postProxy fetch) |
| 4 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `4. CompleteSessionAsync(id, ct)` | A | InterviewController.cs:528 |  | — |
| 5 | :InterviewService (WebClient) | :API Gateway | call | `5. POST api/interview/sessions/{sessionId}/complete` | A | InterviewService.cs:277-280 |  | — |
| 6 | :API Gateway | :InterviewSessionController (InterviewAPI) | call | `6. Forward (route interview-route, no prefix strip)` | A | ApiGateway/appsettings.json:49-54 |  | — |
| 7 | :InterviewSessionController (InterviewAPI) | (self) | call | `7. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | A | InterviewSessionController.cs:269 | self | — |
| 8 | :InterviewSessionController (InterviewAPI) | :SessionService (InterviewAPI) | call | `8. CompleteSessionAsync(userId, sessionId, ct)` | A | InterviewSessionController.cs:270 |  | — |
| 9 | :SessionService (InterviewAPI) | :SessionRepository (InterviewAPI) | call | `9. GetByIdAsync(sessionId, ct)` | A | SessionService.cs:1054 |  | — |
| 10 | :SessionRepository (InterviewAPI) | SQL Server | call | `10. SELECT InterviewSession WHERE SessionId AND !IsDeleted` | A | SessionRepository.cs:21-24 |  | — |
| 11 | SQL Server | :SessionRepository (InterviewAPI) | ret | `11. Return session row / null` | A | SessionRepository.cs:21-24 |  | — |
| 12 | :SessionRepository (InterviewAPI) | :SessionService (InterviewAPI) | ret | `12. Return InterviewSession?` | A | SessionRepository.cs:21-24 |  | — |
| E1.1 | :SessionService | :InterviewSessionController | ret | `E1.1. throw InvalidOperationException("Session not found.")` | E1 | SessionService.cs:1055-1056 | throw | — |
| E1.2 | :InterviewSessionController | :API Gateway | ret | `E1.2. Return 400 ApiResponse.Fail(ex.Message)` | E1 | InterviewSessionController.cs:273-276 |  | — |
| E1.3 | :API Gateway | :InterviewService (WebClient) | ret | `E1.3. Return 400` | E1 | ApiGateway/appsettings.json:49-54 |  | — |
| E1.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `E1.4. Return ApiCallResult.Fail(message, 400)` | E1 | InterviewService.cs:636-639 |  | — |
| E1.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `E1.5. Return 400 { error }` | E1 | InterviewController.cs:530-533 |  | — |
| E1.6 | :Interview Room UI | User | ret | `E1.6. appendBubble("AI", "Could not finalize: ...")` | E1 | interview-room.js:697-701 |  | E1.6. appendBubble("AI", "Could not finalize the interview: Session not found.") then redirect to the Session details page |
| E2.1-E2.6 | *(cùng chuỗi 6 message như E1)* |  |  | `E2.1. throw InvalidOperationException("Session cannot complete from its current state.")` … `E2.6. appendBubble(...) then redirect to the Session details page` | E2 | SessionService.cs:1059-1060 | throw | — |
| E3.1-E3.6 | *(cùng chuỗi 6 message như E1)* |  |  | `E3.1. throw InvalidOperationException("Session cannot complete before its deadline.")` … `E3.6. appendBubble(...) then redirect to the Session details page` | E3 | SessionService.cs:1062-1066 | throw | — |
| B-a.1 | :SessionService | (self) | call | `B-a.1. Reuse the stored Status/EndedAt` | B-a | SessionService.cs:1057-1058 | self | B-a.1. Reuse the stored Status/EndedAt - no status change, no room close, no SaveChanges |
| B-b.1 | :SessionService | :SessionRepository | call | `B-b.1. GetRoomBySessionAsync(sessionId, ct)` | B-b | SessionService.cs:1068 |  | — |
| B-b.2 | :SessionRepository | SQL Server | call | `B-b.2. SELECT InterviewRoom WHERE SessionId` | B-b | SessionRepository.cs:216-217 |  | — |
| B-b.3 | SQL Server | :SessionRepository | ret | `B-b.3. Return room row / null` | B-b | SessionRepository.cs:216-217 |  | — |
| B-b.4 | :SessionRepository | :SessionService | ret | `B-b.4. Return InterviewRoom?` | B-b | SessionRepository.cs:216-217 |  | — |
| B-b.5 | :SessionService | :SessionRepository | call | `B-b.5. HasSubstantiveAnswerAsync(sessionId, ct)` | B-b | SessionService.cs:1069 |  | — |
| B-b.6 | :SessionRepository | SQL Server | call | `B-b.6. SELECT EXISTS ChatMessages WHERE MessageType="Answer"` | B-b | SessionRepository.cs:104-113 |  | B-b.6. SELECT EXISTS ChatMessages JOIN SessionQuestions WHERE SessionId AND SenderType="User" AND MessageType="Answer" AND Content non-empty AND AnswerReason NOT IN ("TimedOut","Skipped") |
| B-b.7 | SQL Server | :SessionRepository | ret | `B-b.7. Return bool` | B-b | SessionRepository.cs:104-113 |  | — |
| B-b.8 | :SessionRepository | :SessionService | ret | `B-b.8. Return bool` | B-b | SessionRepository.cs:104-113 |  | — |
| B-b.9 | :SessionService | (self) | call | `B-b.9. candidateWasActiveAtDeadline -> Status "Completed"/"Expired"` | B-b | SessionService.cs:1070-1076 | self | B-b.9. candidateWasActiveAtDeadline = hasSubstantiveAnswer AND room?.Status == "Active"; session.Status = candidateWasActiveAtDeadline ? "Completed" : "Expired"; EndedAt = now |
| C-a.1 | :SessionService | :SessionRepository | call | `C-a.1. UpdateRoomAsync(room{Status="Closed"}, ct)` | C-a | SessionService.cs:1078-1083 |  | C-a.1. UpdateRoomAsync(room{Status="Closed", ClosedAt=session.EndedAt}, ct) |
| C-a.2 | :SessionRepository | SQL Server | call | `C-a.2. UPDATE InterviewSessions SET Status, EndedAt + InterviewRoom` | C-a | SessionRepository.cs:220-221 |  | C-a.2. UPDATE InterviewSession SET Status, EndedAt, UpdatedAt + UPDATE InterviewRoom SET Status, ClosedAt (one SaveChangesAsync, EF change-tracking) |
| C-a.3 | SQL Server | :SessionRepository | ret | `C-a.3. Return rows affected` | C-a | SessionRepository.cs:220-221 |  | — |
| C-a.4 | :SessionRepository | :SessionService | ret | `C-a.4. Return` | C-a | SessionRepository.cs:220-221 |  | — |
| C-b.1 | :SessionService | :SessionRepository | call | `C-b.1. SaveChangesAsync(ct)` | C-b | SessionService.cs:1086 |  | — |
| C-b.2 | :SessionRepository | SQL Server | call | `C-b.2. UPDATE InterviewSession SET Status, EndedAt, UpdatedAt` | C-b | SessionRepository.cs:348 |  | — |
| C-b.3 | SQL Server | :SessionRepository | ret | `C-b.3. Return rows affected` | C-b | SessionRepository.cs:348 |  | — |
| C-b.4 | :SessionRepository | :SessionService | ret | `C-b.4. Return` | C-b | SessionRepository.cs:348 |  | — |
| 13 | :SessionService | :InterviewSessionController | ret | `13. Return EndResponse{Status, EndedAt}` | A | SessionService.cs:1058, 1089 |  | — |
| 14 | :InterviewSessionController | :API Gateway | ret | `14. Return 200 ApiResponse.Ok(result)` | A | InterviewSessionController.cs:271 |  | — |
| 15 | :API Gateway | :InterviewService (WebClient) | ret | `15. Return 200` | A | ApiGateway/appsettings.json:49-54 |  | — |
| 16 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `16. Return ApiCallResult.Ok(EndResponse, 200)` | A | InterviewService.cs:662, 281 |  | — |
| 17 | :InterviewController (WebClient) | :Interview Room UI | ret | `17. Return 200 { status }` | A | InterviewController.cs:535 |  | — |
| D-a.1 | :Interview Room UI | (self) | call | `D-a.1. appendBubble("AI", closing); setStatusDisplay("Completed")` | D-a | interview-room.js:703-716 | self | D-a.1. appendBubble("AI", thank-you closing); setStatusDisplay("Completed"); wait for speak(closing) capped at 10s when AI Voice is on, else wait 2500ms |
| D-a.2 | :Interview Room UI | User | ret | `D-a.2. Redirect to the Evaluation report page [see #18]` | D-a | interview-room.js:717-720 |  | D-a.2. Redirect to the Evaluation report page (?sessionId=...&durationActualSec=...) |
| D-b.1 | :Interview Room UI | (self) | call | `D-b.1. appendBubble("AI", expired closing); setStatusDisplay(...)` | D-b | interview-room.js:703-716 | self | D-b.1. appendBubble("AI", "That wraps up this interview. The session has expired and remains available in your history."); setStatusDisplay(status); same speak/wait |
| D-b.2 | :Interview Room UI | User | ret | `D-b.2. Redirect to the Session details page` | D-b | interview-room.js:717-720 |  | — |

### Nhánh B — huỷ chủ động (BL#61), guard `[candidate ends the interview from the Cancel Interview button]`

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 18 | User | :Interview Room UI | call | `18. Click Cancel Interview (cancel-interview-btn)` | B | interview-room.js:1026, Room.cshtml:270 |  | — |
| 19 | :Interview Room UI | (self) | call | `19. openCancelConfirmation(): cancelInterviewDialog.showModal()` | B | interview-room.js:646-661, Room.cshtml:278-293 | self | 19. openCancelConfirmation(): guard ended; cancelInterviewDialog.showModal() - warns: no evaluation report, no interview feedback, no credit refund |
| F.1 | :Interview Room UI | (self) | call | `F.1. closeCancelConfirmation(): cancelInterviewDialog.close()` | F | interview-room.js:636-638, 663, 668-671 | self | — |
| F.2 | :Interview Room UI | User | ret | `F.2. Dialog closed - the interview continues unchanged` | F | interview-room.js:636-638 |  | — |
| 20 | :Interview Room UI | (self) | call | `20. cancelInterview(): cleanupTransientUi(); ended=true` | B | interview-room.js:664-667, 723-739 | self | 20. Confirm clicked -> closeCancelConfirmation(); cancelInterview(): guard ended; cleanupTransientUi(); ended=true; clear camera/skip/countdown timers; clearDraft(); hideIdleBanner(); disableLifecycleControls(); setStatusDisplay("Cancelling...") |
| 21 | :Interview Room UI | :InterviewController (WebClient) | call | `21. POST /Interview/EndSession?id={sessionId} (postProxy fetch)` | B | interview-room.js:742, 141-146 |  | — |
| 22 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `22. EndSessionAsync(id, ct)` | B | InterviewController.cs:541 |  | — |
| 23 | :InterviewService (WebClient) | :API Gateway | call | `23. POST api/interview/sessions/{sessionId}/end` | B | InterviewService.cs:287-290 |  | — |
| 24 | :API Gateway | :InterviewSessionController | call | `24. Forward (route interview-route, no prefix strip)` | B | ApiGateway/appsettings.json:49-54 |  | — |
| 25 | :InterviewSessionController | (self) | call | `25. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | B | InterviewSessionController.cs:286 | self | — |
| 26 | :InterviewSessionController | :SessionService | call | `26. EndSessionAsync(userId, sessionId, ct)` | B | InterviewSessionController.cs:287 |  | — |
| 27 | :SessionService | :SessionRepository | call | `27. GetByIdAsync(sessionId, ct)` | B | SessionService.cs:1094 |  | — |
| 28 | :SessionRepository | SQL Server | call | `28. SELECT InterviewSession WHERE SessionId AND !IsDeleted` | B | SessionRepository.cs:21-24 |  | — |
| 29 | SQL Server | :SessionRepository | ret | `29. Return session row / null` | B | SessionRepository.cs:21-24 |  | — |
| 30 | :SessionRepository | :SessionService | ret | `30. Return InterviewSession?` | B | SessionRepository.cs:21-24 |  | — |
| G1.1-G1.6 | *(chuỗi 6 message như E1)* |  |  | `G1.1. throw InvalidOperationException("Session not found.")` … `G1.6. ended=false; appendBubble("AI", "Could not cancel the interview: Session not found."); Cancel button re-enabled; setPausedUI(paused)` | G1 | SessionService.cs:1095-1096; interview-room.js:745-750 | throw | — |
| G2.1-G2.6 | *(chuỗi 6 message như E1)* |  |  | `G2.1. throw InvalidOperationException("Session cannot be cancelled from its current state.")` … `G2.6. ended=false; appendBubble(...); Cancel button re-enabled; setPausedUI(paused)` | G2 | SessionService.cs:1099-1100 | throw | — |
| H-a.1 | :SessionService | (self) | call | `H-a.1. Reuse the stored Status/EndedAt` | H-a | SessionService.cs:1097-1098 | self | H-a.1. Reuse the stored Status/EndedAt - no status change, no room close, no SaveChanges |
| H-b.1 | :SessionService | (self) | call | `H-b.1. session.Status="Cancelled"; EndedAt=now; UpdatedAt=now` | H-b | SessionService.cs:1102-1105 | self | — |
| H-b.2 | :SessionService | :SessionRepository | call | `H-b.2. GetRoomBySessionAsync(sessionId, ct)` | H-b | SessionService.cs:1107 |  | — |
| H-b.3 | :SessionRepository | SQL Server | call | `H-b.3. SELECT InterviewRoom WHERE SessionId` | H-b | SessionRepository.cs:216-217 |  | — |
| H-b.4 | SQL Server | :SessionRepository | ret | `H-b.4. Return room row / null` | H-b | SessionRepository.cs:216-217 |  | — |
| H-b.5 | :SessionRepository | :SessionService | ret | `H-b.5. Return InterviewRoom?` | H-b | SessionRepository.cs:216-217 |  | — |
| J-a.1 | :SessionService | :SessionRepository | call | `J-a.1. UpdateRoomAsync(room{Status="Closed", ClosedAt=now}, ct)` | J-a | SessionService.cs:1108-1113 |  | — |
| J-a.2 | :SessionRepository | SQL Server | call | `J-a.2. UPDATE InterviewSessions SET Status, EndedAt + InterviewRoom` | J-a | SessionRepository.cs:220-221 |  | J-a.2. UPDATE InterviewSession SET Status, EndedAt, UpdatedAt + UPDATE InterviewRoom SET Status, ClosedAt (one SaveChangesAsync) |
| J-a.3 | SQL Server | :SessionRepository | ret | `J-a.3. Return rows affected` | J-a | SessionRepository.cs:220-221 |  | — |
| J-a.4 | :SessionRepository | :SessionService | ret | `J-a.4. Return` | J-a | SessionRepository.cs:220-221 |  | — |
| J-b.1 | :SessionService | :SessionRepository | call | `J-b.1. SaveChangesAsync(ct)` | J-b | SessionService.cs:1116 |  | — |
| J-b.2 | :SessionRepository | SQL Server | call | `J-b.2. UPDATE InterviewSession SET Status, EndedAt, UpdatedAt` | J-b | SessionRepository.cs:348 |  | — |
| J-b.3 | SQL Server | :SessionRepository | ret | `J-b.3. Return rows affected` | J-b | SessionRepository.cs:348 |  | — |
| J-b.4 | :SessionRepository | :SessionService | ret | `J-b.4. Return` | J-b | SessionRepository.cs:348 |  | — |
| 31 | :SessionService | :InterviewSessionController | ret | `31. Return EndResponse{Status, EndedAt}` | B | SessionService.cs:1098, 1119 |  | — |
| 32 | :InterviewSessionController | :API Gateway | ret | `32. Return 200 ApiResponse.Ok(result)` | B | InterviewSessionController.cs:288 |  | — |
| 33 | :API Gateway | :InterviewService (WebClient) | ret | `33. Return 200` | B | ApiGateway/appsettings.json:49-54 |  | — |
| 34 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `34. Return ApiCallResult.Ok(EndResponse, 200)` | B | InterviewService.cs:662, 291 |  | — |
| 35 | :InterviewController (WebClient) | :Interview Room UI | ret | `35. Return 200 { status }` | B | InterviewController.cs:548 |  | — |
| 36 | :Interview Room UI | (self) | call | `36. setStatusDisplay(completion.status)` | B | interview-room.js:743 | self | — |
| 37 | :Interview Room UI | User | ret | `37. Redirect to the Session details page` | B | interview-room.js:744 |  | — |

**Cờ:** `self` · `throw`

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| A/B | alt | A: `[interview deadline reached OR the server reports the session is over]` · B: `[candidate ends the interview from the Cancel Interview button]` | 1 → 37 | — | — |
| E1 | opt | `[session is null OR session.UserId != userId]` | E1.1 → E1.6 | A | — |
| E2 | opt | `[session.Status is not terminal AND session.Status != "InProgress"]` | E2.1 → E2.6 | A | — |
| E3 | opt | `[session.Status == "InProgress" AND (session.StartedAt is null OR elapsed < DurationMinutes * 60)]` | E3.1 → E3.6 | A | — |
| B (alt) | alt | B-a: `[IsTerminalStatus(session.Status) - already Completed / Cancelled / Expired]` · B-b: `[session.Status == "InProgress" AND elapsed >= DurationMinutes * 60]` | B-a.1 → C-b.4 | A | — |
| C | alt | C-a: `[room is not null]` · C-b: `[room is null]` | C-a.1 → C-b.4 | B-b | — |
| D | alt | D-a: `[completion.status == "Completed"]` · D-b: `[completion.status != "Completed"]` | D-a.1 → D-b.2 | A | — |
| F | opt | `[user clicks Continue Interview, presses Esc, or clicks the backdrop]` | F.1 → F.2 | B | — |
| G1 | opt | `[session is null OR session.UserId != userId]` | G1.1 → G1.6 | B | — |
| G2 | opt | `[session.Status is not terminal AND session.Status not in ("Initialized","InProgress","Paused")]` | G2.1 → G2.6 | B | — |
| H | alt | H-a: `[IsTerminalStatus(session.Status)]` · H-b: `[session.Status in ("Initialized","InProgress","Paused")]` | H-a.1 → J-b.4 | B | — |
| J | alt | J-a: `[room is not null]` · J-b: `[room is null]` | J-a.1 → J-b.4 | H-b | — |

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | 1 | `completeInterview() has 5 call sites: the countdown reaching 0, and four "the server says the session is over" paths in sendAnswer / skip / hint / handleSessionExpired (#5 ConductInterviewQA). All four send exactly the same request, which is why they are drawn as one entry message. In the handleSessionExpired case the session was ALREADY finalized server-side, so this call only fetches the decided status - that is branch B-a.` |
| N2 | B-b.9 | `"Completed" requires BOTH a substantive answer (not empty, not Skipped, not TimedOut) AND the room still being "Active". Otherwise the session is finalized as "Expired" - the candidate keeps the history entry but gets no evaluation report.` |
| N3 | 19 | `Cancelling is deliberately irreversible and unpaid-for: no evaluation report, no interview feedback, and the credit already spent is NOT refunded. EndSessionAsync calls no PaymentAPI client at all.` |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | File:Line | Liệt kê | Nguồn |
|---|---|---|---|---|---|
| 1 | InterviewController (WebClient) | class | WebClient/Controllers/Interview/InterviewController.cs:17, 525-549 | SUBSET | local |
| 2 | Controller | framework-base | ASP.NET Core MVC | FULL (rỗng) | SHARED |
| 3 | IInterviewService (WebClient) | interface | WebClient/Services/Interview/InterviewService.cs:33-37 | SUBSET | local |
| 4 | InterviewService (WebClient) | class | WebClient/Services/Interview/InterviewService.cs:89, 274-292, 621-700 | SUBSET | local |
| 5 | IApiClient | interface | WebClient/Services/Shared/IApiClient.cs:3-6 | FULL | SHARED |
| 6 | ApiClient | class | WebClient/Services/Shared/ApiClient.cs:3-6 | FULL | SHARED |
| 7 | EndResponse (WebClient) | DTO | WebClient/Models/Interview/EndResponse.cs:4-8 | FULL | local |
| 8 | ApiCallResult<T> | DTO | WebClient/Models/Shared/ApiCallResult.cs:3-23 | FULL | local |
| 9 | ApiResponse<T> (WebClient) | DTO | WebClient/Models/Shared/ApiResponse.cs:5-12 | FULL | local |
| 10 | ControllerBase | framework-base | ASP.NET Core | FULL (rỗng) | SHARED |
| 11 | InterviewSessionController (InterviewAPI) | class | InterviewAPI/Controllers/InterviewSessionController.cs:15, 262-294 | SUBSET | local |
| 12 | ISessionService | interface | InterviewAPI/Services/Interfaces/ISessionService.cs:25-26 | SUBSET | local |
| 13 | SessionService (InterviewAPI) | class | InterviewAPI/Services/SessionService.cs:45, 1052-1120 | SUBSET | local |
| 14 | ApiResponse<T> (InterviewAPI) | DTO | InterviewAPI/DTOs/ApiResponse.cs | FULL | SHARED |
| 15 | EndResponse (InterviewAPI) | DTO | InterviewAPI/DTOs/Session/EndResponse.cs:3-7 | FULL | local |
| 16 | ISessionRepository | interface | InterviewAPI/Repositories/Interfaces/ISessionRepository.cs | SUBSET | local |
| 17 | SessionRepository (InterviewAPI) | class | InterviewAPI/Repositories/SessionRepository.cs:9-24, 104-113, 216-221, 348 | SUBSET | local |
| 18 | InterviewSession | entity | InterviewAPI/Models/InterviewSession.cs | FULL | SHARED |
| 19 | InterviewRoom | entity | InterviewAPI/Models/InterviewRoom.cs | FULL | SHARED |
| 20 | SessionQuestion | entity | InterviewAPI/Models/SessionQuestion.cs | FULL | SHARED (**tạo mới**) |
| 21 | ChatMessage | entity | InterviewAPI/Models/ChatMessage.cs | FULL | SHARED (**tạo mới**) |
| 22 | InterviewDbContext | class | InterviewAPI/Models/InterviewDbContext.cs + .Biometrics.cs | FULL | SHARED |
| 23 | DbContext | framework-base | EF Core | FULL (rỗng) | SHARED |

## 8. CLASS — chi tiết

### `InterviewController (WebClient)` — `SUBSET`
- Field: `- interviewService: IInterviewService`
- Method: `+ CompleteSession(...)`, `+ EndSession(...)`
- *(SUBSET)* Đã bỏ 4 field DI khác và ~28 action ngoài chức năng này.

### `IInterviewService` / `InterviewService (WebClient)` — `SUBSET`
- `+ CompleteSessionAsync(...)`, `+ EndSessionAsync(...)`; class thêm `- SendAsync<T>(...)`,
  `- ReadErrorMessageAsync(...)`, field `- JsonOptions`, `- apiClient`.

### `InterviewSessionController (InterviewAPI)` — `SUBSET`
- Field: `- _service`, `- _aiServiceClient`, `- _paymentApiClient`
- Method: `+ CompleteSession(...)`, `+ EndSession(...)`

### `SessionService (InterviewAPI)` — `SUBSET`
- 10 field DI (liệt kê đủ)
- Method: `+ CompleteSessionAsync(...)`, `+ EndSessionAsync(...)`, `- IsTerminalStatus(status)`,
  `- GetEffectivePausedSeconds(session)`

### `ISessionRepository` / `SessionRepository (InterviewAPI)` — `SUBSET`
- `+ GetByIdAsync(...)`, `+ GetRoomBySessionAsync(...)`, `+ UpdateRoomAsync(...)`,
  `+ HasSubstantiveAnswerAsync(...)`, `+ SaveChangesAsync(...)`

**Class SHARED lần đầu xuất hiện trong toàn dự án** (đã tạo file):
`SessionQuestion.yaml`, `ChatMessage.yaml` — entity FULL, đủ 100% cột + navigation property.

## 9. QUAN HỆ CLASS

| Loại | From | To | Label |
|---|---|---|---|
| extends | InterviewController (WebClient) | Controller | — |
| use | InterviewController (WebClient) | IInterviewService (WebClient) | Use |
| implements | InterviewService (WebClient) | IInterviewService (WebClient) | — |
| use | InterviewService (WebClient) | IApiClient | Use |
| implements | ApiClient | IApiClient | — |
| use | InterviewService (WebClient) | EndResponse (WebClient) | Use |
| use | InterviewService (WebClient) | ApiCallResult<T> | Use |
| use | InterviewService (WebClient) | ApiResponse<T> (WebClient) | Use |
| extends | InterviewSessionController (InterviewAPI) | ControllerBase | — |
| use | InterviewSessionController (InterviewAPI) | ISessionService | Use |
| use | InterviewSessionController (InterviewAPI) | ApiResponse<T> (InterviewAPI) | Use |
| implements | SessionService (InterviewAPI) | ISessionService | — |
| use | SessionService (InterviewAPI) | ISessionRepository | Use |
| use | SessionService (InterviewAPI) | EndResponse (InterviewAPI) | Use |
| implements | SessionRepository (InterviewAPI) | ISessionRepository | — |
| composition | SessionRepository (InterviewAPI) | InterviewDbContext | — |
| extends | InterviewDbContext | DbContext | — |
| composition | InterviewDbContext | InterviewSession | `1..*` |
| composition | InterviewDbContext | InterviewRoom | `1..*` |
| composition | InterviewSession | SessionQuestion | `1..*` |
| composition | SessionQuestion | ChatMessage | `1..*` |
| composition | InterviewSession | InterviewRoom | `1..1` |

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| — | — | Không phát hiện anomaly mới | — | — | — |

> Đã kiểm tra riêng nghi vấn `room.Status == "Active"`: `InterviewDbContext` đặt DEFAULT
> `"Open"` cho cột này ([InterviewDbContext.cs:42](../../../source/InterviewAPI/Models/InterviewDbContext.cs#L42)),
> nhưng `InitAsync` luôn ghi tường minh `Status = "Active"` khi tạo room
> ([SessionService.cs:539](../../../source/InterviewAPI/Services/SessionService.cs#L539)) → default
> không bao giờ có hiệu lực, so sánh `"Active"` là đúng. **Không phải bug.**

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | 2 nhánh kết thúc (auto-complete BL#37 vs huỷ chủ động BL#61) trên cùng 1 trang | **1 khung `alt` cấp cao nhất, 2 section** | Loại trừ nhau (một phiên chỉ kết thúc một kiểu). Vẽ tuần tự như #8 sẽ sai vì cờ `ended` chặn gọi cái thứ hai. |
| 2 | Thứ tự trong code: null-check → terminal early-return → invalid-state throw → deadline throw. Vẽ theo thứ tự đó thì nhánh terminal phải là `opt` thoát sớm và phải nhân đôi cả chuỗi return + khung `alt` trình duyệt | **Vẽ 3 nhánh throw thành `opt` (E1/E2/E3) trước, rồi `alt` 2 nhánh (terminal / finalize) hội tụ về 1 chuỗi return dùng chung** | Guard ghi rõ `session.Status is not terminal AND ...` nên logic vẫn khớp code 100%; tiết kiệm ~430px và tránh vẽ trùng khung `alt` phía trình duyệt. Thứ tự đọc lệch source, logic không lệch. |
| 3 | Hộp thoại xác nhận huỷ có 2 hành động user (bấm nút → xác nhận) | **1 message `18` mở hộp thoại + `opt F` cho nhánh "user rút lại", nhánh xác nhận đi tiếp trong cùng message stack** | Giữ đủ nhánh user-visible "rút lại" mà không phải mở/đóng thêm 1 cặp call-return với Actor. |
| 4 | Gán `room.Status="Closed"; ClosedAt` — self-message riêng hay gộp vào nhãn call? | **Gộp vào nhãn `UpdateRoomAsync(room{Status="Closed", ClosedAt=...})`** | Đúng tiền lệ lô A (`verifyfaceinsession.yaml` J.10). Tiết kiệm 120px. |
| 5 | `speak(closing)` (TTS lời tạm biệt) | **Không mở rộng, chỉ ghi trong nhãn self-message** | Thuộc #6 VoiceInteraction (§1). |
| 6 | Có vẽ `ChatMessage`/`SessionQuestion` không, khi `HasSubstantiveAnswerAsync` chỉ đọc qua LINQ? | **Có, tạo file shared** | Query chạm thật 2 bảng này; cả hai đều nằm trong danh sách SHARED đã duyệt. |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `2`)** completeInterview() has 5 call sites: the countdown reaching 0, and four "the server says the session is over" paths in sendAnswer / skip / hint / handleSessionExpired (#5 ConductInterviewQA). All four send exactly the same request, which is why they are drawn as one entry message. In the handleSessionExpired case the session was ALREADY finalized server-side, so the call only fetches the decided status - that is branch B-a. *(gỡ từ note trên diagram)*
- **(cạnh message `6`)** "Completed" requires BOTH a substantive answer (not empty, not Skipped, not TimedOut) AND the room still being "Active". Otherwise the session is finalized as "Expired" - the candidate keeps the history entry but gets no evaluation report. *(gỡ từ note trên diagram)*
- **(cạnh message `9`)** Cancelling is deliberately irreversible and unpaid-for: no evaluation report, no interview feedback, and the credit already spent is NOT refunded. EndSessionAsync calls no PaymentAPI client at all. *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| *(không có)* | — | — | — |
