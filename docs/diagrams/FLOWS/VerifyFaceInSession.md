# FLOW — `VerifyFaceInSession` · BL#50, 51 · UC-10

> Đặt tại `docs/diagrams/FLOWS/VerifyFaceInSession.md`
>
> Trạng thái: `DRAFT` — Khảo sát: luồng 1, lô `A`, `2026-08-02`

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | 1 lần kiểm tra gian lận trong phiên: so khớp 1 frame webcam với embedding đã enroll (không challenge/liveness) + phát hiện nhiều mặt trong khung hình; tích luỹ "strike", tự kết thúc phiên nếu vượt ngưỡng (Strictness="Strict") |
| Điểm vào (user thao tác gì) | **Không phải hành động bấm trực tiếp** — tự động chạy 1 lần mỗi khi câu hỏi MỚI hiện lên màn hình (JS `applyNextQuestion`), sau khi #5 ConductInterviewQA đã đính kèm sẵn `verificationCheckId` vào response câu hỏi kế tiếp. Actor `User` kích hoạt GIÁN TIẾP qua việc trả lời/skip câu hỏi trước đó (dẫn tới câu hỏi mới) — xem §11 mục cần duyệt. Trigger phụ: camera vừa kết nối lại sau khi mất kết nối, nếu đang có sẵn 1 `verificationCheckId` chưa dùng |
| Kết thúc (user thấy gì) | Passed: không hiện gì (hoặc banner "notice" nhẹ nếu no-face lần đầu) · Strike (mismatch/multi-face/no-face-tích-luỹ): banner cảnh báo cheating + số đếm (X/Y) · Đạt ngưỡng strike (Strict): modal "Interview ended" + redirect dashboard (phiên đã bị server đóng NGAY trong response này) |
| Quy mô | 14 participant · 79 dòng trong bảng §4 (một số dòng lỗi 400/404 viết tắt dạng `F#.2..F#.6` thay vì liệt kê từng return — xem §11 mục 3; nếu expand đủ theo `validate.py` RULE3 khi lên YAML, số message thật sẽ CAO HƠN 79) · 3 alt (G: 3 nhánh ai-service; K: 4 nhánh phản ứng UI; ngoài ra 1 nhánh J lồng trong K) / 6 opt (F1-F5, J) |
| Service ngoài quyền sở hữu | không |
| **Gọi BYPASS Gateway?** | không |
| Nhà cung cấp AI | InsightFace (qua `ai-service/biometrics/router.py` endpoint `/verify`, khác endpoint `/verify-live` mà #13 dùng) |
| Ghi DB | `SessionVerificationEvents` — UPDATE (`ExecuteUpdateAsync` tiêu checkId) + INSERT (kết quả); nếu kết thúc phiên do cheating: `InterviewSessions` UPDATE (`Status="Cancelled"`) + `InterviewRooms` UPDATE (`Status="Closed"`) |
| Trừ credit | không |
| KI liên quan | KI-04 (rate-limit "25s" là config chết — cơ chế thật ở đây là 1 checkId/câu hỏi + `TryConsumeQuestionCheckAsync` atomic, KHÔNG có interval timer nào trong chính file này) |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| `pollVerificationSchedule()` + GET `verify/in-session-check` | `interview-room.js:1545-1587`; `VerificationController.cs:12-23`; `SessionVerificationController.cs:28-43` | Dead code xác nhận qua grep toàn file — hàm định nghĩa nhưng KHÔNG có nơi nào gọi nó (KI-04). Đây là code cho cơ chế "randomized cadence" đã bị bỏ, thay bằng 1-checkId/câu-hỏi. Không vẽ như luồng đang hoạt động. |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Nguồn phát `verificationCheckId` — đính vào response câu hỏi kế tiếp | `#5 ConductInterviewQA` (và `#4 StartInterviewSession` cho câu hỏi đầu qua Resume — nhưng `OrderIndex<=1` luôn trả CheckId=null nên KHÔNG có check thật cho câu hỏi đầu tiên, đúng comment `interview-room.js:579-581`) | `AttachVerificationCheckAsync(nextQuestion, sessionId, userId)` | `SessionService.cs:208-215,217-278,870` |
| Sau khi `ShouldEndSession=true`, browser hiện modal rồi điều hướng thẳng dashboard (KHÔNG gọi thêm Complete/EndSession — server đã tự đóng session trong CHÍNH response của message #15) | `#9 EndInterviewSession` (chỉ để đối chiếu logic đóng room dùng chung, KHÔNG có lời gọi HTTP nào sang #9) | *(không vẽ message — chỉ note)* | `interview-room.js:1397-1412` |

---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 1 | User | actor (gián tiếp) | — | — |
| 2 | :Interview Room UI | box | `Views/Interview/Room.cshtml` + `wwwroot/js/pages/interview-room.js` | — |
| 3 | :VerificationController (WebClient) | box | `Controllers/Interview/VerificationController.cs` | — |
| 4 | :VerificationService (WebClient) | box | `Services/Interview/VerificationService.cs` | — |
| 5 | :API Gateway | box | `interview-route` | có |
| 6 | :SessionVerificationController (InterviewAPI) | box | `Controllers/SessionVerificationController.cs` | — |
| 7 | :BiometricVerificationService (InterviewAPI) | box | `Services/BiometricVerificationService.cs` | — |
| 8 | :SessionRepository (InterviewAPI) | box | `Repositories/SessionRepository.cs` (SUBSET) | — |
| 9 | :SessionVerificationEventRepository (InterviewAPI) | box | `Repositories/SessionVerificationEventRepository.cs` | — |
| 10 | :ProfileBiometricsRepository (InterviewAPI) | box | `Repositories/ProfileBiometricsRepository.cs` | — |
| 11 | :CheatingStrikeHelper (InterviewAPI) | box | `Helpers/CheatingStrikeHelper.cs` | — |
| 12 | :AiServiceClient (InterviewAPI) | box | `Clients/AiServiceClient.cs` | — |
| 13 | :ai-service /internal/biometrics/verify | box | `ai-service/biometrics/router.py:79-114` | — |
| 14 | SQL Server | cylinder | `DevPrep_Interview` | — |

## 4. BẢNG MESSAGE

| # | From | To | call/ret | Label (tiếng Anh) | Frame (guard) | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Interview Room UI | call | `1. Next question carries a verificationCheckId [see #5]` | — | `interview-room.js:550-553` | boundary | *(gián tiếp)* Trả lời/skip câu hỏi trước → server trả câu hỏi mới kèm `verificationCheckId` *(boundary #5)* |
| 2 | :Interview Room UI | :Interview Room UI | call | `2. applyNextQuestion(): show question, after typing delay` | — | `interview-room.js:562-582` | self | applyNextQuestion()`: hiện câu hỏi, sau khi "typing" delay xong |
| 3 | :Interview Room UI | :Interview Room UI | call | `3. captureAndVerifyInSession(): grab 1 frame` | — | `interview-room.js:582,1453-1470` | self | captureAndVerifyInSession()`: grab 1 frame từ `cameraStream` đang chạy sẵn |
| F0.1 | :Interview Room UI | — | ret | `F0.1. return early - no request sent` | opt F0 `[ended OR paused OR !verifyInSessionUrl OR !checkId OR !cameraStream OR đang có request khác chạy OR checkId đã hết hạn cục bộ]` | `interview-room.js:1454-1456` | self | *(no-op, không gửi gì)* |
| 4 | :Interview Room UI | :VerificationController (WebClient) | call | `4. POST Verification/VerifyInSession (multipart)` | — | `interview-room.js:1472-1480` |  | POST Verification/VerifyInSession` (multipart: `checkId`, `frame`) |
| 5 | :VerificationController (WebClient) | :VerificationService (WebClient) | call | `5. VerifyInSessionAsync(sessionId, checkId, frame, ...)` | — | `VerificationController.cs:109-110` |  | VerifyInSessionAsync(sessionId, checkId, frame bytes, fileName, contentType) |
| 6 | :VerificationService (WebClient) | :API Gateway | call | `6. POST api/interview/sessions/{id}/verify/in-session` | — | `VerificationService.cs:118-125` |  | POST api/interview/sessions/{sessionId}/verify/in-session` (multipart `frame`, `checkId`) |
| 7 | :API Gateway | :SessionVerificationController (InterviewAPI) | call | `7. Forward (route interview-route, no prefix strip)` | — | `ApiGateway/appsettings.json:49-54` |  | Forward (route `interview-route`, no prefix strip) |
| 8 | :SessionVerificationController (InterviewAPI) | :SessionVerificationController (InterviewAPI) | call | `8. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | `SessionVerificationController.cs:164` | self | Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)) |
| 9 | :SessionVerificationController (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `9. VerifyInSessionAsync(userId, sessionId, checkId, frame)` | — | `SessionVerificationController.cs:165` |  | VerifyInSessionAsync(userId, sessionId, checkId, frame) |
| 10 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `10. EnsureEnabled()` | — | `BiometricVerificationService.cs:268,629-633` | self | EnsureEnabled() |
| F1.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `throw InvalidOperationException("Biometric verification is disabled.")` | opt F1 `[Biometrics:Enabled == false]` | `BiometricVerificationService.cs:631-632` | throw | — |
| F1.2..F1.6 | ... | ... | ret | Return 400 chuỗi ngược tới UI (giống cấu trúc F2 của #13) | opt F1 | `SessionVerificationController.cs:172-174`; `VerificationController.cs:112-113` |  | — |
| 11 | :BiometricVerificationService (InterviewAPI) | :SessionRepository (InterviewAPI) | call | `11. GetByIdAsync(sessionId)` | — | `BiometricVerificationService.cs:272` |  | GetByIdAsync(sessionId)` (trực tiếp — KHÔNG qua `EnsureSessionOwnedAsync`, tự kiểm tra inline) |
| 12 | :SessionRepository (InterviewAPI) | SQL Server | call | `12. SELECT InterviewSession by SessionId` | — | `ISessionRepository.cs:8` |  | SELECT InterviewSession by SessionId |
| 13 | SQL Server | :SessionRepository (InterviewAPI) | ret | `13. Return session row / null` | — | — |  | Return session row / null |
| 14 | :SessionRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `14. Return InterviewSession?` | — | — |  | Return `InterviewSession? |
| F2.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `throw KeyNotFoundException("Session not found.")` | opt F2 `[session null OR session.UserId != userId]` | `BiometricVerificationService.cs:273-274` | throw | — |
| F2.2..F2.6 | ... | ... | ret | Return 404 chuỗi ngược tới UI | opt F2 | `SessionVerificationController.cs:168-170` |  | — |
| F3.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `throw InvalidOperationException("Session is not in progress.")` | opt F3 `[session.Status != "InProgress"]` | `BiometricVerificationService.cs:275-276` | throw | — |
| F3.2..F3.6 | ... | ... | ret | Return 400 chuỗi ngược tới UI | opt F3 | `SessionVerificationController.cs:172-174` |  | — |
| 15 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationEventRepository (InterviewAPI) | call | `15. TryConsumeQuestionCheckAsync(...)` | — | `BiometricVerificationService.cs:279-280` |  | TryConsumeQuestionCheckAsync(checkId, sessionId, userId, now)` — atomic, tiêu token 1 lần |
| 16 | :SessionVerificationEventRepository (InterviewAPI) | SQL Server | call | `16. UPDATE SessionVerificationEvent SET ConsumedAt, Decision` | — | `SessionVerificationEventRepository.cs:65-79` |  | UPDATE SessionVerificationEvent SET ConsumedAt, Decision="Consumed" WHERE IncidentId AND SessionId AND UserId AND Decision="Pending" AND ConsumedAt IS NULL AND ExpiresAt >= now |
| 17 | SQL Server | :SessionVerificationEventRepository (InterviewAPI) | ret | `17. Return rows affected` | — | — |  | Return rows affected |
| 18 | :SessionVerificationEventRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `18. Return bool (affected == 1)` | — | — |  | Return `bool` (`affected == 1`) |
| F4.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `throw InvalidOperationException("The verification check token is invalid, expired, or already used.")` | opt F4 `[TryConsumeQuestionCheckAsync == false]` | `BiometricVerificationService.cs:281` | throw | — |
| F4.2 | :SessionVerificationController (InterviewAPI) | :API Gateway | ret | Return 400 `ApiResponse.Fail(ex.Message)` | opt F4 | `SessionVerificationController.cs:172-174` |  | — |
| F4.3-F4.5 | ... | ... | ret | Return 400 tới `VerifyInSessionAsync` (WebClient) → `VerificationController.VerifyInSession` | opt F4 | `VerificationService.cs:126-130`, `VerificationController.cs:111-113` |  | — |
| F4.6 | :Interview Room UI | User | ret | `isVerificationTokenError(errorMessage)` → im lặng, KHÔNG hiện cảnh báo (token lỗi coi là vô hại, thường do double-fire) | opt F4 | `interview-room.js:1482-1489` |  | — |
| 19 | :BiometricVerificationService (InterviewAPI) | :ProfileBiometricsRepository (InterviewAPI) | call | `19. GetAsync(userId, "Face") (via GetStoredEmbeddingAsync)` | — | `BiometricVerificationService.cs:291,664-667` |  | GetAsync(userId, "Face")` (qua `GetStoredEmbeddingAsync`) |
| 20 | :ProfileBiometricsRepository (InterviewAPI) | SQL Server | call | `20. SELECT ProfileBiometric WHERE UserId AND Modality="Face"` | — | `ProfileBiometricsRepository.cs:16-19` |  | SELECT ProfileBiometric WHERE UserId AND Modality="Face" |
| 21 | SQL Server | :ProfileBiometricsRepository (InterviewAPI) | ret | `21. Return row / null` | — | — |  | Return row / null |
| 22 | :ProfileBiometricsRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `22. Return ProfileBiometric?` | — | — |  | Return `ProfileBiometric? |
| F5.1..F5.6 | :BiometricVerificationService (InterviewAPI) | ... | ret | `throw InvalidOperationException("No face enrollment found...")` → 400 chuỗi ngược | opt F5 `[row is null]` | `BiometricVerificationService.cs:668-669` | throw | — |
| 23 | :BiometricVerificationService (InterviewAPI) | :AiServiceClient (InterviewAPI) | call | `23. VerifyFaceAsync(frame, embedding)` | — | `BiometricVerificationService.cs:303` |  | VerifyFaceAsync(frame, embedding) |
| 24 | :AiServiceClient (InterviewAPI) | :ai-service /internal/biometrics/verify | call | `24. POST /internal/biometrics/verify (form)` | — | `AiServiceClient.cs:226-241` |  | POST /internal/biometrics/verify` (form: `modality="face"`, `stored_embedding`, `file`) |
| G.1 | :ai-service /internal/biometrics/verify | :AiServiceClient (InterviewAPI) | ret | `G.1. 200 VerifyResponse{match, score, threshold}` | alt G `[frame hợp lệ, đúng 1 khuôn mặt]` | `router.py:112-114` |  | 200 `VerifyResponse{match, score, threshold} |
| G.2 | :AiServiceClient (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `G.2. Return AiFaceVerifyResponse` | alt G | `AiServiceClient.cs:247-249` |  | Return `AiFaceVerifyResponse |
| G.3 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `G.3. isStrike, decision, warningType from Match` | alt G | `BiometricVerificationService.cs:304-315` | self | isStrike = !Match`, `decision = Match?"Pass":"Flag"`, `warningType = Match?"None":"IdentityMismatch" |
| H.1 | :ai-service /internal/biometrics/verify | :AiServiceClient (InterviewAPI) | ret | `H.1. 422 {code:"multiple_faces", face_count, message}` | alt G `[>1 khuôn mặt]` | `router.py:96-101`; `face_engine.py:65` |  | 422 `{code:"multiple_faces", face_count, message} |
| H.2 | :AiServiceClient (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `H.2. throw BiometricQualityException("multiple_faces", ...)` | alt G | `AiServiceClient.cs:242-246` | throw | throw BiometricQualityException(message, "multiple_faces", faceCount) |
| H.3 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `H.3. catch "multiple_faces" -> decision="Flag"` | alt G | `BiometricVerificationService.cs:317-326` | self | catch `QualityCode=="multiple_faces"` → `isStrike=true`, `decision="Flag"`, `warningType="MultiFace" |
| I.1 | :ai-service /internal/biometrics/verify | :AiServiceClient (InterviewAPI) | ret | 422 `{code:"no_face"\|"cannot_decode_image"\|"low_detection_confidence"\|"face_too_small"\|"image_too_blurry", message}` | alt G `[quality khác]` | `router.py:102-108`; `face_engine.py:56,63,84-90` | |
| I.2 | :AiServiceClient (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `I.2. throw BiometricQualityException(message, qualityCode)` | alt G | `AiServiceClient.cs:242-246` | throw | throw BiometricQualityException(message, qualityCode) |
| I.3 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `I.3. catch -> decision="NoFace"/"Quality"; map warningType` | alt G | `BiometricVerificationService.cs:327-350` | self | catch (khác `multiple_faces`) → `isStrike=false`; `no_face`/null→`decision="NoFace"`; khác→`decision="Quality"`, map `warningType` theo code |
| 25 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationEventRepository (InterviewAPI) | call | `25. AddAsync(SessionVerificationEvent)` | — | `BiometricVerificationService.cs:352-355,610-627` |  | AddAsync(SessionVerificationEvent{Phase="InSession", Modality="Face", Matched, Decision, Detail}) |
| 26 | :SessionVerificationEventRepository (InterviewAPI) | SQL Server | call | `26. INSERT SessionVerificationEvent` | — | `SessionVerificationEventRepository.cs:16-20` |  | INSERT SessionVerificationEvent |
| 27 | SQL Server | :SessionVerificationEventRepository (InterviewAPI) | ret | `27. Return` | — | — |  | Return |
| 28 | :SessionVerificationEventRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `28. Return` | — | — |  | Return |
| 29 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationEventRepository (InterviewAPI) | call | `29. ListBySessionAsync(sessionId, "InSession") (count strikes)` | — | `BiometricVerificationService.cs:357` |  | ListBySessionAsync(sessionId, "InSession")` (đếm strike) |
| 30 | :SessionVerificationEventRepository (InterviewAPI) | SQL Server | call | `30. SELECT SessionVerificationEvents WHERE Phase="InSession"` | — | `SessionVerificationEventRepository.cs:47-55` |  | SELECT SessionVerificationEvents WHERE SessionId AND Phase="InSession" |
| 31 | SQL Server | :SessionVerificationEventRepository (InterviewAPI) | ret | `31. Return event list` | — | — |  | Return event list |
| 32 | :SessionVerificationEventRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `32. Return List<SessionVerificationEvent>` | — | — |  | Return `List<SessionVerificationEvent> |
| 33 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `33. Count noFaceCount (Modality=Face AND Decision=NoFace)` | — | `BiometricVerificationService.cs:358` | self | Đếm `noFaceCount` (Modality=Face AND Decision=NoFace) |
| 34 | :BiometricVerificationService (InterviewAPI) | :CheatingStrikeHelper (InterviewAPI) | call | `34. ComputeEffectiveStrikeCount(events, noFaceStrikeThreshold)` | — | `BiometricVerificationService.cs:359-360` |  | ComputeEffectiveStrikeCount(events, noFaceStrikeThreshold) |
| 35 | :CheatingStrikeHelper (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `35. Return directStrikeCount + noFaceCount/noFaceStrikeThreshold` | — | `CheatingStrikeHelper.cs:7-20` |  | Return `directStrikeCount + noFaceCount/noFaceStrikeThreshold |
| 36 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `36. Compute strike counters and shouldEndSession` | — | `BiometricVerificationService.cs:364-395` | self | Tính `noFaceJustBecameStrike`, `strikeJustHappened`, `shouldEndSession`, `displayStrikeCount`, `noFaceGroupProgress`, build `message |
| J.1 | :BiometricVerificationService (InterviewAPI) | :SessionRepository (InterviewAPI) | call | `J.1. GetByIdAsync(sessionId) (via EndSessionForCheatingAsync)` | opt J `[shouldEndSession == true]` | `BiometricVerificationService.cs:379,588-590` |  | GetByIdAsync(sessionId)` (qua `EndSessionForCheatingAsync`) |
| J.2 | :SessionRepository (InterviewAPI) | SQL Server | call | `J.2. SELECT InterviewSession by SessionId` | opt J | — |  | SELECT InterviewSession by SessionId |
| J.3 | SQL Server | :SessionRepository (InterviewAPI) | ret | `J.3. Return session` | opt J | — |  | Return session |
| J.4 | :SessionRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `J.4. Return session` | opt J | — |  | Return session |
| J.5 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `J.5. session.Status="Cancelled", EndedAt=now` | opt J | `BiometricVerificationService.cs:593-595` | self | session.Status="Cancelled"`, `EndedAt=now |
| J.6 | :BiometricVerificationService (InterviewAPI) | :SessionRepository (InterviewAPI) | call | `J.6. GetRoomBySessionAsync(sessionId)` | opt J | `BiometricVerificationService.cs:597` |  | GetRoomBySessionAsync(sessionId) |
| J.7 | :SessionRepository (InterviewAPI) | SQL Server | call | `J.7. SELECT InterviewRoom by SessionId` | opt J | — |  | SELECT InterviewRoom by SessionId |
| J.8 | SQL Server | :SessionRepository (InterviewAPI) | ret | `J.8. Return room / null` | opt J | — |  | Return room / null |
| J.9 | :SessionRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `J.9. Return InterviewRoom?` | opt J | — |  | Return `InterviewRoom? |
| J.10 | :BiometricVerificationService (InterviewAPI) | :SessionRepository (InterviewAPI) | call | `J.10. UpdateRoomAsync(room{Status="Closed", ClosedAt=now})` | opt J `[room khác null]` | `BiometricVerificationService.cs:599-602` |  | UpdateRoomAsync(room{Status="Closed", ClosedAt=now})` (nếu có room) |
| J.11 | :SessionRepository (InterviewAPI) | SQL Server | call | `J.11. UPDATE InterviewSessions SET Status, EndedAt + InterviewRoom` | opt J | `SessionRepository.cs:220-221` |  | UPDATE InterviewSession SET Status,EndedAt + UPDATE InterviewRoom SET Status,ClosedAt` (1 `SaveChangesAsync` lưu cả 2 bảng nhờ EF change-tracking — cả `session` lẫn `room` đều đã tracked từ trước) |
| J.12 | SQL Server | :SessionRepository (InterviewAPI) | ret | `J.12. Return` | opt J | — |  | Return |
| J.13 | :BiometricVerificationService (InterviewAPI) | :SessionRepository (InterviewAPI) | call | `J.13. SaveChangesAsync()` | opt J `[room == null]` | `BiometricVerificationService.cs:604-606` |  | SaveChangesAsync()` (nếu KHÔNG có room) |
| 37 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `37. Return InSessionVerifyResponse` | — | `BiometricVerificationService.cs:397-409` |  | Return `InSessionVerifyResponse{Passed, NoFaceDetected, WarningType, StrikeCount, MaxStrikes, NoFaceCount, NoFaceStrikeThreshold, NoFaceJustBecameStrike, ShouldEndSession, Message} |
| 38 | :SessionVerificationController (InterviewAPI) | :API Gateway | ret | `38. Return 200 ApiResponse.Ok(result)` | — | `SessionVerificationController.cs:166` |  | Return 200 `ApiResponse.Ok(result) |
| 39 | :API Gateway | :VerificationService (WebClient) | ret | `39. Return 200` | — | — |  | Return 200 |
| 40 | :VerificationService (WebClient) | :VerificationController (WebClient) | ret | `40. Return (true, result, null)` | — | `VerificationService.cs:126-133` |  | Return `(true, result, null) |
| 41 | :VerificationController (WebClient) | :Interview Room UI | ret | `41. Return 200 JSON` | — | `VerificationController.cs:116` |  | Return 200 JSON |
| K.1 | :Interview Room UI | User | ret | `K.1. setCheatingWarning(null) - hide warning, nothing shown` | alt K `[data.passed == true]` | `interview-room.js:1493-1495` |  | [passed]` → `setCheatingWarning(null)` (ẩn cảnh báo, không hiện gì) |
| K.2 | :Interview Room UI | User | ret | `K.2. Gentle notice banner, no strike count shown` | alt K `[warningType=="NoFace" AND !noFaceJustBecameStrike]` | `interview-room.js:1498-1502` |  | [NoFace, chưa thành strike]` → banner "notice" nhẹ, không kèm số đếm |
| K.3 | :Interview Room UI | User | ret | `K.3. Notice banner (lighting/distance/connection guidance)` | alt K `[warningType ∈ {ImageTooBlurry,FaceTooSmall,LowDetectionConfidence,CannotDecode}]` | `interview-room.js:1504-1508` |  | [quality khác]` → banner "notice" (hướng dẫn ánh sáng/khoảng cách/kết nối) |
| K.4 | :Interview Room UI | User | ret | `[strike thật]` → banner cảnh báo + `(StrikeCount/MaxStrikes)`, popup nếu chưa kết thúc phiên | alt K `[IdentityMismatch \| MultiFace \| NoFace-vừa-thành-strike]` | `interview-room.js:1510-1522` | |
| L.1 | :Interview Room UI | User | ret | `L.1. Show modal "Interview ended" -> redirect to /Interview` | opt L `[data.shouldEndSession == true]` (lồng trong K.4) | `interview-room.js:1524-1526,1397-1412` |  | Modal "Interview ended" (3s) → `window.location.href="/Interview"` — phiên đã đóng SẴN ở message 37 |

**Cờ:** `[best-effort]` · `self` · `throw` · `boundary` (theo §2)

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong |
|---|---|---|---|---|
| F0 | opt | `[ended OR paused OR không đủ điều kiện client-side]` | 3 → F0.1 | — |
| F1 | opt | `[Biometrics:Enabled == false]` | 10 → F1.6 | — |
| F2 | opt | `[session null OR không thuộc user]` | 11 → F2.6 | — |
| F3 | opt | `[session.Status != "InProgress"]` | (kiểm ngay sau 14) → F3.6 | — |
| F4 | opt | `[checkId không hợp lệ/hết hạn/đã dùng]` | 15 → F4.6 | — |
| F5 | opt | `[chưa enroll face]` | 19 → F5.6 | — |
| G | alt | `[match]` / `[multiple_faces]` / `[quality khác]` | G.1-G.3 / H.1-H.3 / I.1-I.3 | — |
| J | opt | `[shouldEndSession == true]` | J.1 → J.13 | — |
| K | alt | `[passed]` / `[NoFace nhẹ]` / `[quality notice]` / `[strike thật]` | K.1 / K.2 / K.3 / K.4 | — |
| L | opt | `[shouldEndSession == true]` | L.1 | lồng trong K (nhánh K.4) |

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | 3 | This check ALSO fires from `connectCamera()` when the camera reconnects mid-question and a `verificationCheckId` is still pending (`interview-room.js:1220`) — same function, same downstream chain; not drawn as a separate flow since it's literally the same call. |
| N2 | 33-36 | `Biometrics:InSessionMinIntervalSeconds` ("25s" rate limit) is a DEAD config key — not read anywhere in `source/` (KI-04). The real throttle is: exactly 1 pending checkId issued per question (`SessionService.GetInSessionCheckAsync`, boundary §2), one-time-use via `TryConsumeQuestionCheckAsync` (message 15-18), TTL 20s (`Biometrics:InSessionCheckTtlSeconds`, not configured in `appsettings.json` → C# default 20 applies). |
| N3 | J.10-J.13 | `EndSessionForCheatingAsync` deliberately does NOT call any credit-refund client — see comment `BiometricVerificationService.cs:585-587`: refunding here would let a candidate deliberately trigger cheating-detection to end a session for free. |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | File:Line | Liệt kê | Nguồn |
|---|---|---|---|---|---|
| 1 | VerificationController (WebClient) | class | `Controllers/Interview/VerificationController.cs:9` | SUBSET | local |
| 2 | IVerificationService / VerificationService (WebClient) | interface/class | `Services/Interview/VerificationService.cs` | FULL | local *(đã dán ở #12)* |
| 3 | InSessionVerifyResponse | DTO | `DTOs/Biometrics/InSessionVerifyResponse.cs:9-45` | FULL | local |
| 4 | ApiResponse\<T\> (InterviewAPI) | class | `InterviewAPI/DTOs/ApiResponse.cs` | FULL | **SHARED** |
| 5 | SessionVerificationController (InterviewAPI) | class | `Controllers/SessionVerificationController.cs:15` | SUBSET | local |
| 6 | IBiometricVerificationService / BiometricVerificationService | interface/class | `Services/BiometricVerificationService.cs` | SUBSET | local |
| 7 | ISessionRepository / SessionRepository | interface/class | `Repositories/SessionRepository.cs` | SUBSET | local |
| 8 | InterviewSession | entity | `Models/InterviewSession.cs` | FULL | **SHARED** *(đã dán ở #12)* |
| 9 | InterviewRoom | entity | `Models/InterviewRoom.cs:10-33` | FULL | **SHARED** *(lần đầu — dán §8)* |
| 10 | ISessionVerificationEventRepository / SessionVerificationEventRepository | interface/class | `Repositories/SessionVerificationEventRepository.cs` | FULL | local *(đã dán ở #13)* |
| 11 | SessionVerificationEvent | entity | `Models/SessionVerificationEvent.cs` | FULL | **SHARED** *(đã dán ở #13)* |
| 12 | IProfileBiometricsRepository / ProfileBiometricsRepository / ProfileBiometric | interface/class/entity | `Repositories/ProfileBiometricsRepository.cs` | FULL | local *(đã dán ở #12)* |
| 13 | CheatingStrikeHelper | static class | `Helpers/CheatingStrikeHelper.cs:5-21` | FULL | local *(xem §11 — đề xuất SHARED)* |
| 14 | IAiServiceClient / AiServiceClient | interface/class | `Clients/AiServiceClient.cs` | FULL | **SHARED** *(đã dán ở #13)* |
| 15 | BiometricQualityException | exception | `Clients/BiometricQualityException.cs` | FULL | **SHARED** *(đã dán ở #13)* |
| 16 | AiFaceVerifyResponse | DTO | `DTOs/AiService/AiFaceVerifyResponse.cs:8-18` | FULL | local |
| 17 | InterviewDbContext / ControllerBase / DbContext | DbContext/framework | — | FULL | local/**SHARED** *(đã dán ở #12)* |

## 8. CLASS — chi tiết

### `VerificationController` (`WebClient`)
- File: `Controllers/Interview/VerificationController.cs:9` — Liệt kê: SUBSET
- Field: `- verificationService: IVerificationService`
- Method: `+ VerifyInSession(sessionId: Guid, checkId: Guid, frame: IFormFile, ct: CancellationToken): Task<IActionResult>`
- *(SUBSET)* Đã bỏ `Challenge`, `VerifyFace`, `EntryGate`, `VerifyVoice`, `InSessionCheck` — thuộc #12/#13/#14/#16.

### `InSessionVerifyResponse` (`InterviewAPI`)
- File: `DTOs/Biometrics/InSessionVerifyResponse.cs:9-45` — Liệt kê: FULL
- Field: `+ Passed: bool`
- Field: `+ NoFaceDetected: bool`
- Field: `+ WarningType: string`
- Field: `+ StrikeCount: int`
- Field: `+ MaxStrikes: int`
- Field: `+ NoFaceCount: int`
- Field: `+ NoFaceStrikeThreshold: int`
- Field: `+ NoFaceJustBecameStrike: bool`
- Field: `+ ShouldEndSession: bool`
- Field: `+ Message: string`

### `SessionVerificationController` (`InterviewAPI`)
- File: `Controllers/SessionVerificationController.cs:15` — Liệt kê: SUBSET
- Field: `- _service: IBiometricVerificationService`
- Field: `- _sessionService: ISessionService`
- Method: `+ VerifyInSession(sessionId: Guid, checkId: Guid, frame: IFormFile, ct: CancellationToken): Task<IActionResult>`
- *(SUBSET)* Đã bỏ các action khác — thuộc #12/#13/#14/#16.

### `IBiometricVerificationService` / `BiometricVerificationService` (`InterviewAPI`)
- File: `Services/BiometricVerificationService.cs:12` — Liệt kê: SUBSET
- Field: `- _eventRepository: ISessionVerificationEventRepository`
- Field: `- _biometricsRepository: IProfileBiometricsRepository`
- Field: `- _sessionRepository: ISessionRepository`
- Field: `- _aiServiceClient: IAiServiceClient`
- Field: `- _configuration: IConfiguration`
- Method: `+ VerifyInSessionAsync(userId: Guid, sessionId: Guid, checkId: Guid, frame: IFormFile, ct: CancellationToken): Task<InSessionVerifyResponse>`
- Method: `- EndSessionForCheatingAsync(sessionId: Guid, ct: CancellationToken): Task`
- Method: `- RecordEventAsync(sessionId: Guid, userId: Guid, phase: string, modality: string, score: double?, livenessPassed: bool?, matched: bool, decision: string, detail: object, ct: CancellationToken): Task`
- Method: `- GetStoredEmbeddingAsync(userId: Guid, modality: string, ct: CancellationToken): Task<float[]>`
- *(SUBSET)* Đã bỏ `IssueChallengeAsync`, `VerifyFaceAsync`, `VerifyVoiceAsync`, `GetEntryGateStatusAsync`, `VerifyVoiceAnswerAsync`, `BuildVoiceAnswerResponseAsync`, `EnsureSessionOwnedAsync`, `EnsureEnrolledAsync`, `ConsumeChallengeAsync` — thuộc #12/#13/#14/#16.

### `ISessionRepository` / `SessionRepository` (`InterviewAPI`)
- File: `Repositories/SessionRepository.cs` — Liệt kê: SUBSET
- Method: `+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>`
- Method: `+ GetRoomBySessionAsync(sessionId: Guid, ct: CancellationToken): Task<InterviewRoom?>`
- Method: `+ UpdateRoomAsync(room: InterviewRoom, ct: CancellationToken): Task`
- Method: `+ SaveChangesAsync(ct: CancellationToken): Task`

**`InterviewSession` — SHARED, đã dán ở #12.**

**`InterviewRoom` (InterviewAPI) — SHARED, lần đầu xuất hiện trong dự án** → tạo file `_shared/classes/InterviewRoom.yaml`:
```yaml
name: "InterviewRoom"
fields:
  - "+ RoomId: Guid"
  - "+ SessionId: Guid"
  - "+ ChannelName: string"
  - "+ ConnectionId: string?"
  - "+ Status: string"
  - "+ InitializedAt: DateTime"
  - "+ ClosedAt: DateTime?"
  - "+ Session: InterviewSession"
methods: []
```

**`SessionVerificationEvent`+repo, `ProfileBiometric`+repo, `IAiServiceClient`/`AiServiceClient`, `BiometricQualityException` — đã dán đủ ở `VerifyFaceEntryGate.md` §8.**

### `CheatingStrikeHelper` (`InterviewAPI`)
- File: `Helpers/CheatingStrikeHelper.cs:5-21` — Liệt kê: FULL
- Method: `+ ComputeEffectiveStrikeCount(events: IEnumerable<SessionVerificationEvent>, noFaceStrikeThreshold: int): int` *(static)*
- *(Ghi chú: dùng chung với #10 ReportCameraViolation và #16 VerifyVoiceAnswer — chưa nằm trong danh sách SHARED được giao, xem §11.)*

### `AiFaceVerifyResponse` (`InterviewAPI`)
- File: `DTOs/AiService/AiFaceVerifyResponse.cs:8-18` — Liệt kê: FULL
- Field: `+ Match: bool`
- Field: `+ Score: double`
- Field: `+ Threshold: double`

**`InterviewDbContext`, `ControllerBase`, `DbContext`, `Controller` — đã dán đủ ở `GetVerificationChallenge.md` §8.**

## 9. QUAN HỆ CLASS

| Loại | From | To | Label |
|---|---|---|---|
| extends | VerificationController (WebClient) | Controller | — |
| use | VerificationController (WebClient) | IVerificationService | Use |
| extends | SessionVerificationController (InterviewAPI) | ControllerBase | — |
| use | SessionVerificationController (InterviewAPI) | IBiometricVerificationService | Use |
| implements | BiometricVerificationService (InterviewAPI) | IBiometricVerificationService | — |
| use | BiometricVerificationService (InterviewAPI) | ISessionRepository | Use |
| use | BiometricVerificationService (InterviewAPI) | ISessionVerificationEventRepository | Use |
| use | BiometricVerificationService (InterviewAPI) | IProfileBiometricsRepository | Use |
| use | BiometricVerificationService (InterviewAPI) | IAiServiceClient | Use |
| use | BiometricVerificationService (InterviewAPI) | CheatingStrikeHelper | Use |
| use | BiometricVerificationService (InterviewAPI) | BiometricQualityException | throw (catch) |
| use | BiometricVerificationService (InterviewAPI) | InSessionVerifyResponse | Use |
| implements | AiServiceClient (InterviewAPI) | IAiServiceClient | — |
| use | AiServiceClient (InterviewAPI) | AiFaceVerifyResponse | Use |
| use | AiServiceClient (InterviewAPI) | BiometricQualityException | throw |
| composition | SessionRepository (InterviewAPI) | InterviewDbContext | — |
| composition | InterviewDbContext | InterviewRoom | 1..* |
| composition | InterviewSession | InterviewRoom | 1..1 |

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| (KI-04, tham chiếu) | `SessionVerificationController.cs:152`; `interview-room.js:1266`; `InterviewAPI/appsettings.json:24` | Config `InSessionMinIntervalSeconds=25` chết, không đọc ở đâu; cũng phát hiện thêm `InSessionCheckCadenceMinutes=10` (`appsettings.json:25`) cũng KHÔNG được đọc ở bất kỳ đâu trong `source/` — cùng gốc "cơ chế cadence ngẫu nhiên" đã bị bỏ | comment/config lệch code | Vẽ đúng cơ chế thật (1 checkId/câu hỏi + TTL) | Đã có sẵn — **đã bổ sung phát hiện `InSessionCheckCadenceMinutes` vào KI-04** (xem KNOWN_ISSUES.md) |


### Gỡ từ note trên diagram — anomaly

- **(cạnh message `16`)** Biometrics:InSessionMinIntervalSeconds ("25s" rate limit) is a DEAD config key - not read anywhere in source/ (KI-04). The real throttle is: exactly 1 pending checkId issued per question (SessionService.GetInSessionCheckAsync, boundary), one-time-use via TryConsumeQuestionCheckAsync, TTL 20s (Biometrics:InSessionCheckTtlSeconds, C# default applies). *(gỡ từ note trên diagram)*

## 11. QUYẾT ĐỊNH CẦN DUYENTQ DUYỆT

| # | Vấn đề | Phương án | Đề xuất |
|---|---|---|---|
| 1 | Actor `User` kích hoạt GIÁN TIẾP (không bấm nút nào cho chính #15) — giống hoàn cảnh #17 EvaluateInterviewAnswer | Vẽ `User` với note "trigger gián tiếp qua trả lời/skip câu hỏi (#5)" giống cách #17 đã đề xuất | Cần duyệt cách thể hiện actor gián tiếp trước khi vẽ thật (đã có tiền lệ ở #17) |
| 2 | `CheatingStrikeHelper` dùng chung ≥3 chức năng (#10 ReportCameraViolation, #15, #16 VerifyVoiceAnswer) nhưng không nằm trong danh sách SHARED được giao ở đầu lô | Thêm vào `_shared/classes/` | Đề xuất có — class rất nhỏ (1 method static), lợi ích tái dùng rõ ràng |
| 3 | Bảng message có nhiều chuỗi opt lỗi viết tắt bằng `...`/`F#.2..F#.6` để tránh lặp 5-6 dòng gần giống hệt nhau (theo đúng tinh thần đã làm ở #13) | Luồng 2 tự expand đủ số dòng theo `_tools/validate.py` RULE3 khi lên YAML | Xác nhận cách làm này chấp nhận được cho luồng 2 |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `3`)** This check ALSO fires from connectCamera() when the camera reconnects mid-question and a verificationCheckId is still pending (interview-room.js:1220) - same function, same downstream chain; not drawn as a separate flow since it is literally the same call. *(gỡ từ note trên diagram)*
- **(cạnh message `22.1`)** EndSessionForCheatingAsync deliberately does NOT call any credit-refund client - refunding here would let a candidate deliberately trigger cheating-detection to end a session for free. *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| F0.1 chỉ có 1 dòng, target ghi "—" (không xác định) | Bảng §4 mô tả no-op nhưng không có message thứ 2 đóng lại call gốc (message 1, User→UI, boundary) trên nhánh này | Luồng 2 tự thêm `F0.2: UI→User ret "(nothing shown)"` để đóng call 1 cho đủ cặp call/return | luồng 2 |
| J.10-J.12 (nhánh room≠null) và J.13 (nhánh room==null) đều thiếu message đóng cuối (repo trả về BVS); J.13 còn thiếu cả 1 lượt SQL round-trip | Bảng §4 chỉ liệt 1-3 dòng cho mỗi nhánh, không tới lúc trả về BVS | Luồng 2 tự thêm `J.12.1` (đóng J.10) và `J.13.1/J.13.2/J.13.3` (SQL round-trip + đóng J.13), theo đúng mẫu SQL-repo-return dùng xuyên suốt file này | luồng 2 |
| J.10-J.13 model thành `alt [room≠null]/[room==null]` thay vì 2 `opt` độc lập như bảng §4 ghi chữ | 2 `opt` độc lập với guard bù trừ nhau (room≠null / room==null) tạo cấu trúc mơ hồ cho generator; 2 điều kiện này loại trừ nhau và luôn đúng đúng 1 cái → đúng ngữ nghĩa `alt` | Luồng 2 tự đổi thành `alt` lồng trong `opt J`, không đổi nội dung/guard | luồng 2 |
| L.1 (opt L lồng trong K.4) đổi từ `ret` (UI→User) thành `self` (UI→UI) | K.4 đã là `ret` đóng call gốc (message 1, User→UI) — nếu L.1 cũng là `ret` tới cùng participant User thì không còn call nào đang mở để đóng, vi phạm cân bằng call/return của công cụ dựng sơ đồ | Luồng 2 tự đổi kind của L.1 thành self-message trên `:Interview Room UI` (modal + redirect vẫn là hành động phía client, không đổi nội dung) | luồng 2 |
