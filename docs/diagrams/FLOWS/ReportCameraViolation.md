# FLOW — `ReportCameraViolation` · BL#41 · UC-`10`

> Đặt tại `docs/diagrams/FLOWS/ReportCameraViolation.md`
> Trạng thái: `DRAFT` — Khảo sát: lô B, 2026-08-03

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Ghi nhận 1 vi phạm camera (bị từ chối quyền, hoặc rớt kết nối giữa phiên) sau khi hết 15 giây ân hạn kết nối lại; cộng 1 strike gian lận và tự kết thúc phiên khi đạt ngưỡng ở chế độ `Strict` |
| Điểm vào (user thao tác gì) | KHÔNG bấm gì — tự động: camera tắt/bị chặn → sau 15s thử kết nối lại thất bại. User vẫn đang ngồi trước màn hình |
| Kết thúc (user thấy gì) | Cảnh báo đỏ "Integrity violation" + popup *Cheating Warning* (`n/3`), hoặc — khi đủ strike — modal kết thúc rồi chuyển về `/Interview` |
| Quy mô | `11` participant · `90` message · `4` alt / `7` opt · trang Sequence 2310×4713 |
| Đối chiếu ước lượng RECON | RECON đoán `12` · thật `90` · hệ số `7.5×` |
| Service ngoài quyền sở hữu | không |
| **Gọi BYPASS Gateway?** | không |
| Nhà cung cấp AI | **không** — phát hiện mất camera là WebRTC phía client (`navigator.mediaDevices.getUserMedia`), không gọi ai-service |
| Ghi DB | `SessionVerificationEvents` INSERT (`Phase="InSession"`, `Modality="Camera"`, `Decision="Flag"`, `IncidentId` unique); `InterviewSessions.CameraViolationCount` atomic +1; khi `shouldEndSession`: `InterviewSessions` → `Cancelled` + `InterviewRooms` → `Closed` |
| Trừ credit | không — và **không hoàn credit** khi phiên bị kết thúc vì gian lận |
| KI liên quan | không (không phát hiện KI mới) |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| `connectCamera()` thành công (bật preview, gắn listener `track.ended`) | [interview-room.js:1202-1221](../../../source/WebClient/wwwroot/js/pages/interview-room.js#L1202-L1221) | Thuộc `EnterInterviewRoom` (khởi tạo camera). Ở đây chỉ vẽ nhánh **thất bại** của lần thử lại. |
| `captureAndVerifyInSession()` gọi lại khi camera vừa kết nối lại | [interview-room.js:1220](../../../source/WebClient/wwwroot/js/pages/interview-room.js#L1220) | Thuộc `#15 VerifyFaceInSession` (đã ghi chú trong file đó). |
| Vòng lặp thử lại 15s vô hạn (`scheduleCameraRecovery` tự gọi lại) | [interview-room.js:1242](../../../source/WebClient/wwwroot/js/pages/interview-room.js#L1242) | Vẫn chạy nhưng KHÔNG sinh thêm request nào (cờ `cameraIncidentReported`) → gộp vào nhãn message 3, không vẽ lặp. |
| Nhánh `throw` lại của `TryAddIncidentAsync` khi `DbUpdateException` mà bản ghi KHÔNG tồn tại | [SessionVerificationEventRepository.cs:38-39](../../../source/InterviewAPI/Repositories/SessionVerificationEventRepository.cs#L38-L39) | Lỗi hạ tầng (không phải trùng IncidentId) — không có nhánh xử lý riêng ở tầng trên, rơi vào `catch (Exception)` chung của controller giống 4 nhánh lỗi đã vẽ. |
| Điều kiện `CheckSequence` trong nhánh dò trùng | [SessionVerificationEventRepository.cs:35-37](../../../source/InterviewAPI/Repositories/SessionVerificationEventRepository.cs#L35-L37) | `ReportCameraViolationAsync` không bao giờ set `CheckSequence` (luôn `null`) → nhánh đó không chạy được từ chức năng này. |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| *(không có)* | — | — | — |

> Nhánh đóng phiên (`shouldEndSession`) dùng **cùng logic** đóng room như `#9 EndInterviewSession`
> nhưng là code **nội tuyến** trong `ReportCameraViolationAsync` (SessionService.cs:1234-1250),
> không phải lời gọi sang chức năng khác → vẽ đầy đủ tại chỗ, kèm note trỏ sang `#9`.

### Gỡ từ note trên diagram — boundary

- **(cạnh message `F0.2`)** The shouldEndSession branch is the SAME session/room closing logic as #9 EndInterviewSession, but written inline inside ReportCameraViolationAsync - not a call into that flow. Like #9's cancel path it refunds no credit: letting a candidate trigger cheating detection to get a free session back would be an obvious exploit. *(gỡ từ note trên diagram)*


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
| 8 | :SessionVerificationEventRepository (InterviewAPI) | box | `InterviewAPI/Repositories/SessionVerificationEventRepository.cs` | có |
| 9 | :CheatingStrikeHelper (InterviewAPI) | box | `InterviewAPI/Helpers/CheatingStrikeHelper.cs` | có |
| 10 | SQL Server | cylinder | `DevPrep_Interview` | — |

> **ACTOR GIÁN TIẾP:** luồng chạy hoàn toàn tự động, nhưng user **đang ngồi trước màn hình
> phòng phỏng vấn** → theo quy ước vẫn dùng actor `User`, nhãn message 1 ghi rõ `[auto]`.

## 4. BẢNG MESSAGE

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Interview Room UI | call | `1. [auto] Camera stops` | — | interview-room.js:1215-1219, 1247-1252 |  | 1. [auto] Camera stops - permission denied at start, or the video track ends mid-interview |
| 2 | :Interview Room UI | (self) | call | `2. scheduleCameraRecovery(reason); arm the 15s retry timer` | — | interview-room.js:1223-1226, 1192-1200 | self | 2. scheduleCameraRecovery(reason): guard ended / a retry timer is already armed; showCameraUnavailable(reason); arm the 15s retry timer |
| 3 | :Interview Room UI | (self) | call | `3. [after 15s] connectCamera() retry throws` | — | interview-room.js:1226-1243 | self | 3. [after 15s] connectCamera() retry throws -> setCameraStatus("Camera: Recovery failed"); scheduleCameraRecovery(reason) keeps retrying every 15s |
| F0.1 | :Interview Room UI | (self) | call | `F0.1. Skip the report` | F0 | interview-room.js:1233-1238, 1150-1153 | self | F0.1. Skip the report - at most one violation per continuous camera-loss episode |
| F0.2 | :Interview Room UI | User | ret | `F0.2. Camera status stays red, retry loop keeps running` | F0 | interview-room.js:1231-1232, 1242 |  | F0.2. Camera status stays red, retry loop keeps running - no strike, no request sent |
| 4 | :Interview Room UI | (self) | call | `4. cameraIncidentReported = true; reportCameraViolation(reason)` | — | interview-room.js:1235-1237, 1161-1176 | self | 4. cameraIncidentReported = true; reportCameraViolation(reason); incidentId = crypto.randomUUID() |
| F1.1 | :Interview Room UI | (self) | call | `F1.1. return early - no request sent` | F1 | interview-room.js:1171 | self | — |
| F1.2 | :Interview Room UI | User | ret | `F1.2. Nothing shown` | F1 | interview-room.js:1171 |  | F1.2. Nothing shown (the session already ended, or the Room has no camera-violation URL) |
| 5 | :Interview Room UI | :InterviewController (WebClient) | call | `5. POST /Interview/CameraViolation?id={id}` | — | interview-room.js:1173-1176, 141-146 |  | 5. POST /Interview/CameraViolation?id={sessionId} { incidentId, reason } |
| 6 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `6. ReportCameraViolationAsync(id, incidentId, reason, ct)` | — | InterviewController.cs:569-570 |  | 6. ReportCameraViolationAsync(id, request.IncidentId, request.Reason, ct) |
| 7 | :InterviewService (WebClient) | :API Gateway | call | `7. POST api/interview/sessions/{id}/camera-violation` | — | InterviewService.cs:317-320 |  | 7. POST api/interview/sessions/{sessionId}/camera-violation { incidentId, reason } |
| 8 | :API Gateway | :InterviewSessionController | call | `8. Forward (route interview-route, no prefix strip)` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 9 | :InterviewSessionController | (self) | call | `9. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | InterviewSessionController.cs:337 | self | — |
| 10 | :InterviewSessionController | :SessionService | call | `10. ReportCameraViolationAsync(userId, sessionId, request, ct)` | — | InterviewSessionController.cs:338 |  | — |
| 11 | :SessionService | :SessionRepository | call | `11. GetByIdAsync(sessionId, ct)` | — | SessionService.cs:1182 |  | — |
| 12 | :SessionRepository | SQL Server | call | `12. SELECT InterviewSession WHERE SessionId AND !IsDeleted` | — | SessionRepository.cs:21-24 |  | — |
| 13 | SQL Server | :SessionRepository | ret | `13. Return session row / null` | — | SessionRepository.cs:21-24 |  | — |
| 14 | :SessionRepository | :SessionService | ret | `14. Return InterviewSession?` | — | SessionRepository.cs:21-24 |  | — |
| G1.1 | :SessionService | :InterviewSessionController | ret | `G1.1. throw InvalidOperationException("Session not found.")` | G1 | SessionService.cs:1183-1184 | throw | — |
| G1.2 | :InterviewSessionController | :API Gateway | ret | `G1.2. Return 400 ApiResponse.Fail(ex.Message)` | G1 | InterviewSessionController.cs:341-344 |  | — |
| G1.3 | :API Gateway | :InterviewService (WebClient) | ret | `G1.3. Return 400` | G1 | ApiGateway/appsettings.json:49-54 |  | — |
| G1.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `G1.4. Return ApiCallResult.Fail(message, 400)` | G1 | InterviewService.cs:636-639 |  | — |
| G1.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `G1.5. Return 400 { error }` | G1 | InterviewController.cs:572-575 |  | — |
| G1.6 | :Interview Room UI | User | ret | `G1.6. setCheatingWarning("Camera incident could not be reported")` | G1 | interview-room.js:1187-1189 |  | G1.6. setCheatingWarning("Camera incident could not be reported: Session not found.", "warning") |
| G2.* | *(chuỗi 6 message như G1)* |  |  | `G2.1. throw InvalidOperationException("Session is not active.")` … `G2.6. setCheatingWarning(...)` | G2 | SessionService.cs:1185-1186 | throw | — |
| G3.* | *(chuỗi 6 message như G1)* |  |  | `G3.1. throw InvalidOperationException("IncidentId is required.")` … | G3 | SessionService.cs:1187-1188 | throw | — |
| G4.* | *(chuỗi 6 message như G1)* |  |  | `G4.1. throw InvalidOperationException("Reason must be PermissionDenied or Disconnected.")` … | G4 | SessionService.cs:1189-1190 | throw | — |
| 15 | :SessionService | (self) | call | `15. Read maxStrikes, strictness, noFaceStrikeThreshold` | — | SessionService.cs:1192-1198, appsettings.json:22-26 | self | 15. Read config: maxStrikes = Biometrics:InSessionMaxStrikes (3), strictness = Biometrics:InSessionStrictness ("Strict"), noFaceStrikeThreshold = Biometrics:NoFaceStrikeThreshold (3); log the incident |
| 16 | :SessionService | :SessionVerificationEventRepository | call | `16. TryAddIncidentAsync(SessionVerificationEvent)` | — | SessionService.cs:1200-1213 |  | 16. TryAddIncidentAsync(SessionVerificationEvent{Phase="InSession", Modality="Camera", Matched=false, Decision="Flag", IncidentId, Detail={Reason}}) |
| 17 | :SessionVerificationEventRepository | SQL Server | call | `17. INSERT SessionVerificationEvent (SaveChangesAsync)` | — | SessionVerificationEventRepository.cs:24-27 |  | — |
| H-a.1 | SQL Server | :SessionVerificationEventRepository | ret | `H-a.1. Return rows affected` | H-a | SessionVerificationEventRepository.cs:27 |  | — |
| H-a.2 | :SessionVerificationEventRepository | :SessionService | ret | `H-a.2. Return inserted = true` | H-a | SessionVerificationEventRepository.cs:28 |  | — |
| H-b.1 | SQL Server | :SessionVerificationEventRepository | ret | `H-b.1. throw DbUpdateException (unique index on IncidentId)` | H-b | SessionVerificationEventRepository.cs:30 | throw | — |
| H-b.2 | :SessionVerificationEventRepository | (self) | call | `H-b.2. catch DbUpdateException; detach the entity` | H-b | SessionVerificationEventRepository.cs:30-32 | self | H-b.2. catch DbUpdateException; detach the entity from the change tracker |
| H-b.3 | :SessionVerificationEventRepository | SQL Server | call | `H-b.3. SELECT EXISTS SessionVerificationEvents WHERE IncidentId` | H-b | SessionVerificationEventRepository.cs:33-37 |  | H-b.3. SELECT EXISTS SessionVerificationEvents WHERE IncidentId = @incidentId |
| H-b.4 | SQL Server | :SessionVerificationEventRepository | ret | `H-b.4. Return true` | H-b | SessionVerificationEventRepository.cs:33-37 |  | — |
| H-b.5 | :SessionVerificationEventRepository | :SessionService | ret | `H-b.5. Return inserted = false` | H-b | SessionVerificationEventRepository.cs:38 |  | — |
| J-a.1 | :SessionService | :SessionRepository | call | `J-a.1. IncrementCameraViolationAsync(sessionId, ct)` | J-a | SessionService.cs:1216 |  | — |
| J-a.2 | :SessionRepository | SQL Server | call | `J-a.2. UPDATE InterviewSessions SET CameraViolationCount + 1` | J-a | SessionRepository.cs:334-337 |  | J-a.2. UPDATE InterviewSession SET CameraViolationCount = CameraViolationCount + 1, UpdatedAt (atomic ExecuteUpdateAsync) |
| J-a.3 | SQL Server | :SessionRepository | ret | `J-a.3. Return rows affected` | J-a | SessionRepository.cs:334-337 |  | — |
| J-a.4 | :SessionRepository | SQL Server | call | `J-a.4. SELECT CameraViolationCount WHERE SessionId` | J-a | SessionRepository.cs:339-345 |  | J-a.4. SELECT CameraViolationCount WHERE SessionId (re-read - ExecuteUpdateAsync bypasses the change tracker) |
| J-a.5 | SQL Server | :SessionRepository | ret | `J-a.5. Return CameraViolationCount` | J-a | SessionRepository.cs:341-345 |  | — |
| J-a.6 | :SessionRepository | :SessionService | ret | `J-a.6. Return int count` | J-a | SessionRepository.cs:345 |  | — |
| J-b.1 | :SessionService | SQL Server | call | `J-b.1. SELECT CameraViolationCount WHERE SessionId` | J-b | SessionService.cs:1217-1220 |  | J-b.1. SELECT CameraViolationCount FROM InterviewSessions WHERE SessionId AND !IsDeleted (direct EF query on _db, not via SessionRepository) |
| J-b.2 | SQL Server | :SessionService | ret | `J-b.2. Return CameraViolationCount` | J-b | SessionService.cs:1217-1220 |  | — |
| 20 | :SessionService | :SessionVerificationEventRepository | call | `20. ListBySessionAsync(sessionId, "InSession", ct)` | — | SessionService.cs:1222 |  | — |
| 21 | :SessionVerificationEventRepository | SQL Server | call | `21. SELECT SessionVerificationEvents WHERE Phase="InSession"` | — | SessionVerificationEventRepository.cs:47-55 |  | 21. SELECT SessionVerificationEvents WHERE SessionId AND Phase="InSession" ORDER BY Id DESC |
| 22 | SQL Server | :SessionVerificationEventRepository | ret | `22. Return event rows` | — | SessionVerificationEventRepository.cs:54 |  | — |
| 23 | :SessionVerificationEventRepository | :SessionService | ret | `23. Return List<SessionVerificationEvent>` | — | SessionVerificationEventRepository.cs:54 |  | — |
| 24 | :SessionService | :CheatingStrikeHelper | call | `24. ComputeEffectiveStrikeCount(events, noFaceStrikeThreshold)` | — | SessionService.cs:1223-1224 |  | — |
| 25 | :CheatingStrikeHelper | :SessionService | ret | `25. Return directStrikeCount + noFaceCount / threshold` | — | CheatingStrikeHelper.cs:11-18 |  | 25. Return directStrikeCount + noFaceCount / noFaceStrikeThreshold |
| 26 | :SessionService | (self) | call | `26. Compute displayStrikeCount and shouldEndSession` | — | SessionService.cs:1225-1232 | self | 26. displayStrikeCount = Min(effectiveStrikeCount, maxStrikes); shouldEndSession = inserted AND strictness == "Strict" AND effectiveStrikeCount >= maxStrikes; build the reason message |
| K.1 | :SessionService | (self) | call | `K.1. session.Status="Cancelled"; EndedAt=now` | K | SessionService.cs:1236-1238, 1252-1253 | self | K.1. session.Status="Cancelled"; EndedAt=now; UpdatedAt=now; message = "Maximum cheating warnings reached (n/max). This interview has been ended." |
| K.2 | :SessionService | :SessionRepository | call | `K.2. GetRoomBySessionAsync(sessionId, ct)` | K | SessionService.cs:1240 |  | — |
| K.3 | :SessionRepository | SQL Server | call | `K.3. SELECT InterviewRoom WHERE SessionId` | K | SessionRepository.cs:216-217 |  | — |
| K.4 | SQL Server | :SessionRepository | ret | `K.4. Return room row / null` | K | SessionRepository.cs:216-217 |  | — |
| K.5 | :SessionRepository | :SessionService | ret | `K.5. Return InterviewRoom?` | K | SessionRepository.cs:216-217 |  | — |
| L-a.1 | :SessionService | :SessionRepository | call | `L-a.1. UpdateRoomAsync(room{Status="Closed", ClosedAt=now}, ct)` | L-a | SessionService.cs:1241-1246 |  | — |
| L-a.2 | :SessionRepository | SQL Server | call | `L-a.2. UPDATE InterviewSessions SET Status, EndedAt + InterviewRoom` | L-a | SessionRepository.cs:220-221 |  | L-a.2. UPDATE InterviewSession SET Status, EndedAt, UpdatedAt + UPDATE InterviewRoom SET Status, ClosedAt (one SaveChangesAsync) |
| L-a.3 | SQL Server | :SessionRepository | ret | `L-a.3. Return rows affected` | L-a | SessionRepository.cs:220-221 |  | — |
| L-a.4 | :SessionRepository | :SessionService | ret | `L-a.4. Return` | L-a | SessionRepository.cs:220-221 |  | — |
| L-b.1 | :SessionService | :SessionRepository | call | `L-b.1. SaveChangesAsync(ct)` | L-b | SessionService.cs:1249 |  | — |
| L-b.2 | :SessionRepository | SQL Server | call | `L-b.2. UPDATE InterviewSession SET Status, EndedAt, UpdatedAt` | L-b | SessionRepository.cs:348 |  | — |
| L-b.3 | SQL Server | :SessionRepository | ret | `L-b.3. Return rows affected` | L-b | SessionRepository.cs:348 |  | — |
| L-b.4 | :SessionRepository | :SessionService | ret | `L-b.4. Return` | L-b | SessionRepository.cs:348 |  | — |
| 27 | :SessionService | :InterviewSessionController | ret | `27. Return ReportCameraViolationResponse` | — | SessionService.cs:1256-1264 |  | 27. Return ReportCameraViolationResponse{CameraViolationCount, StrikeCount, MaxStrikes, Duplicate, ShouldEndSession, Message} |
| 28 | :InterviewSessionController | :API Gateway | ret | `28. Return 200 ApiResponse.Ok(result)` | — | InterviewSessionController.cs:339 |  | — |
| 29 | :API Gateway | :InterviewService (WebClient) | ret | `29. Return 200` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 30 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `30. Return ApiCallResult.Ok(CameraViolationResultModel, 200)` | — | InterviewService.cs:662, 321 |  | — |
| 31 | :InterviewController (WebClient) | :Interview Room UI | ret | `31. Return 200 JSON (CameraViolationResultModel)` | — | InterviewController.cs:577 |  | — |
| M-a.1 | :Interview Room UI | User | ret | `M-a.1. Nothing shown - the duplicate report is silently ignored` | M-a | interview-room.js:1177 |  | — |
| M-b.1 | :Interview Room UI | (self) | call | `M-b.1. setCheatingWarning(strikeText, "critical")` | M-b | interview-room.js:1179-1183 | self | M-b.1. strikeText = message + " (strikeCount/maxStrikes warnings)"; setCheatingWarning(strikeText, "critical"); showCheatingBanner(...) |
| M-b.2 | :Interview Room UI | User | ret | `M-b.2. Show "Integrity violation" alert + modal` | M-b | interview-room.js:1369-1395 |  | M-b.2. Red "Integrity violation" alert + modal popup "Cheating Warning" (or "Final Warning" when strikeCount == maxStrikes - 1), dismissed by OK |
| M-c.1 | :Interview Room UI | (self) | call | `M-c.1. handleForcedCheatingEnd(message): ended=true` | M-c | interview-room.js:1180, 1186, 1397-1409 | self | M-c.1. setCheatingWarning(strikeText, "critical"); handleForcedCheatingEnd(message): ended=true; clear camera/skip timers; clearDraft(); disableLifecycleControls(); appendBubble("AI", message) |
| M-c.2 | :Interview Room UI | User | ret | `M-c.2. Show termination modal -> redirect to /Interview` | M-c | interview-room.js:1410-1411 |  | M-c.2. Termination modal, then redirect to /Interview (the session was already closed inside message 27) |

**Cờ:** `self` · `throw`

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| F0 | opt | `[cameraIncidentReported == true - already reported for this camera-loss episode]` | F0.1 → F0.2 | — | — |
| F1 | opt | `[!cameraViolationUrl OR ended]` | F1.1 → F1.2 | — | — |
| G1 | opt | `[session is null OR session.UserId != userId]` | G1.1 → G1.6 | — | — |
| G2 | opt | `[session.Status not in ("InProgress","Paused")]` | G2.1 → G2.6 | — | — |
| G3 | opt | `[req.IncidentId == Guid.Empty]` | G3.1 → G3.6 | — | — |
| G4 | opt | `[req.Reason not in ("PermissionDenied","Disconnected")]` | G4.1 → G4.6 | — | — |
| H | alt | H-a: `[INSERT succeeded]` · H-b: `[unique index on IncidentId rejects it - the same incident was already reported]` | H-a.1 → H-b.5 | — | — |
| J | alt | J-a: `[inserted == true]` · J-b: `[inserted == false - duplicate report]` | J-a.1 → J-b.2 | — | — |
| K | opt | `[shouldEndSession == true]` | K.1 → L-b.4 | — | — |
| L | alt | L-a: `[room is not null]` · L-b: `[room is null]` | L-a.1 → L-b.4 | K | — |
| M | alt | M-a: `[data.duplicate == true]` · M-b: `[!duplicate AND !shouldEndSession]` · M-c: `[!duplicate AND shouldEndSession]` | M-a.1 → M-c.2 | — | — |

> `K` là `opt` **tự khép kín** (chỉ mở và đóng đúng các call của chính nó) → không phải điểm kết
> thúc, luồng chạy tiếp vào message 27 dù guard đúng hay sai — giống `opt J` trong
> `VerifyFaceInSession`.

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | 3-4 | `Two independent idempotency layers: (1) the client reports at most ONE violation per continuous camera-loss episode (cameraIncidentReported, reset only when the camera actually reconnects), and (2) the server has a unique index on IncidentId, so a retried request is stored once and comes back Duplicate=true.` |
| N2 | K.1 | `This is the SAME session/room closing logic as #9 EndInterviewSession, but written inline inside ReportCameraViolationAsync - not a call into that flow. Like #9's cancel path, it refunds no credit: letting a candidate trigger cheating detection to get a free session back would be an obvious exploit.` |
| N3 | 26 | `shouldEndSession requires inserted == true, so a duplicate report can never end the session on its own. Strictness="Flag" (config) turns the whole feature into audit-only: strikes are still recorded, the session is never ended.` |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | File:Line | Liệt kê | Nguồn |
|---|---|---|---|---|---|
| 1 | InterviewController (WebClient) | class | WebClient/Controllers/Interview/InterviewController.cs:17, 564-578 | SUBSET | local |
| 2 | Controller | framework-base | ASP.NET Core MVC | FULL (rỗng) | SHARED |
| 3 | IInterviewService (WebClient) | interface | WebClient/Services/Interview/InterviewService.cs:45-47 | SUBSET | local |
| 4 | InterviewService (WebClient) | class | WebClient/Services/Interview/InterviewService.cs:89, 314-322, 621-700 | SUBSET | local |
| 5 | IApiClient | interface | WebClient/Services/Shared/IApiClient.cs:3-6 | FULL | SHARED |
| 6 | ApiClient | class | WebClient/Services/Shared/ApiClient.cs:3-6 | FULL | SHARED |
| 7 | CameraViolationProxyRequest | DTO | WebClient/Models/Interview/CameraViolationProxyRequest.cs:4-8 | FULL | local |
| 8 | CameraViolationResultModel | DTO | WebClient/Models/Interview/CameraViolationProxyRequest.cs:10-18 | FULL | local |
| 9 | ApiCallResult<T> | DTO | WebClient/Models/Shared/ApiCallResult.cs:3-23 | FULL | local |
| 10 | ApiResponse<T> (WebClient) | DTO | WebClient/Models/Shared/ApiResponse.cs:5-12 | FULL | local |
| 11 | ControllerBase | framework-base | ASP.NET Core | FULL (rỗng) | SHARED |
| 12 | InterviewSessionController (InterviewAPI) | class | InterviewAPI/Controllers/InterviewSessionController.cs:15, 328-345 | SUBSET | local |
| 13 | ISessionService | interface | InterviewAPI/Services/Interfaces/ISessionService.cs:33 | SUBSET | local |
| 14 | SessionService (InterviewAPI) | class | InterviewAPI/Services/SessionService.cs:45, 1179-1265 | SUBSET | local |
| 15 | ApiResponse<T> (InterviewAPI) | DTO | InterviewAPI/DTOs/ApiResponse.cs | FULL | SHARED |
| 16 | ReportCameraViolationRequest | DTO | InterviewAPI/DTOs/Session/ReportCameraViolationRequest.cs:3-7 | FULL | local |
| 17 | ReportCameraViolationResponse | DTO | InterviewAPI/DTOs/Session/ReportCameraViolationResponse.cs:3-11 | FULL | local |
| 18 | CheatingStrikeHelper | class | InterviewAPI/Helpers/CheatingStrikeHelper.cs:5-20 | FULL | SHARED |
| 19 | ISessionRepository | interface | InterviewAPI/Repositories/Interfaces/ISessionRepository.cs | SUBSET | local |
| 20 | SessionRepository (InterviewAPI) | class | InterviewAPI/Repositories/SessionRepository.cs | SUBSET | local |
| 21 | ISessionVerificationEventRepository | interface | InterviewAPI/Repositories/Interfaces/ISessionVerificationEventRepository.cs | FULL | local |
| 22 | SessionVerificationEventRepository (InterviewAPI) | class | InterviewAPI/Repositories/SessionVerificationEventRepository.cs:7-98 | FULL | local |
| 23 | InterviewSession | entity | InterviewAPI/Models/InterviewSession.cs | FULL | SHARED |
| 24 | InterviewRoom | entity | InterviewAPI/Models/InterviewRoom.cs | FULL | SHARED |
| 25 | SessionVerificationEvent | entity | InterviewAPI/Models/SessionVerificationEvent.cs | FULL | SHARED |
| 26 | InterviewDbContext | class | InterviewAPI/Models/InterviewDbContext.cs + .Biometrics.cs | FULL | SHARED |
| 27 | DbContext | framework-base | EF Core | FULL (rỗng) | SHARED |

## 8. CLASS — chi tiết

### `InterviewController (WebClient)` — `SUBSET`
- Field `- interviewService: IInterviewService`; method `+ CameraViolation(id, request: CameraViolationProxyRequest, ct)`

### `IInterviewService` / `InterviewService (WebClient)` — `SUBSET`
- `+ ReportCameraViolationAsync(sessionId, incidentId, reason, ct)`; class thêm `- SendAsync<T>(...)`,
  `- ReadErrorMessageAsync(...)`, field `- JsonOptions`, `- apiClient`.

### `InterviewSessionController (InterviewAPI)` — `SUBSET`
- 3 field DI; method `+ ReportCameraViolation(id, request, ct)`

### `SessionService (InterviewAPI)` — `SUBSET`
- 10 field DI; method `+ ReportCameraViolationAsync(userId, sessionId, req, ct)`

### `ISessionRepository` / `SessionRepository (InterviewAPI)` — `SUBSET`
- `+ GetByIdAsync`, `+ IncrementCameraViolationAsync`, `+ GetRoomBySessionAsync`,
  `+ UpdateRoomAsync`, `+ SaveChangesAsync`

### `ISessionVerificationEventRepository` / `SessionVerificationEventRepository` — `FULL` (8 method)

Không có class SHARED mới trong chức năng này.

## 9. QUAN HỆ CLASS

| Loại | From | To | Label |
|---|---|---|---|
| extends | InterviewController (WebClient) | Controller | — |
| use | InterviewController (WebClient) | IInterviewService (WebClient) | Use |
| use | InterviewController (WebClient) | CameraViolationProxyRequest | Use |
| implements | InterviewService (WebClient) | IInterviewService (WebClient) | — |
| use | InterviewService (WebClient) | IApiClient | Use |
| implements | ApiClient | IApiClient | — |
| use | InterviewService (WebClient) | CameraViolationResultModel | Use |
| use | InterviewService (WebClient) | ApiCallResult<T> | Use |
| use | InterviewService (WebClient) | ApiResponse<T> (WebClient) | Use |
| extends | InterviewSessionController (InterviewAPI) | ControllerBase | — |
| use | InterviewSessionController (InterviewAPI) | ISessionService | Use |
| use | InterviewSessionController (InterviewAPI) | ApiResponse<T> (InterviewAPI) | Use |
| implements | SessionService (InterviewAPI) | ISessionService | — |
| use | SessionService (InterviewAPI) | ISessionRepository | Use |
| use | SessionService (InterviewAPI) | ISessionVerificationEventRepository | Use |
| use | SessionService (InterviewAPI) | CheatingStrikeHelper | Use |
| use | SessionService (InterviewAPI) | ReportCameraViolationRequest | Use |
| use | SessionService (InterviewAPI) | ReportCameraViolationResponse | Use |
| implements | SessionRepository (InterviewAPI) | ISessionRepository | — |
| implements | SessionVerificationEventRepository (InterviewAPI) | ISessionVerificationEventRepository | — |
| composition | SessionRepository (InterviewAPI) | InterviewDbContext | — |
| composition | SessionVerificationEventRepository (InterviewAPI) | InterviewDbContext | — |
| extends | InterviewDbContext | DbContext | — |
| composition | InterviewDbContext | InterviewSession | `1..*` |
| composition | InterviewDbContext | InterviewRoom | `1..*` |
| composition | InterviewDbContext | SessionVerificationEvent | `1..*` |
| composition | InterviewSession | InterviewRoom | `1..1` |

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| — | — | Không phát hiện anomaly mới | — | — | — |

> Đã kiểm 2 điểm nghi ngờ: (a) `SessionService` truy vấn `_db.InterviewSessions` **trực tiếp** ở
> nhánh duplicate ([SessionService.cs:1217-1220](../../../source/InterviewAPI/Services/SessionService.cs#L1217-L1220))
> — bỏ qua `SessionRepository`, lệch pattern nhưng **không sai hành vi**, đã vẽ đúng như code
> (message J-b.1 nối thẳng từ `SessionService` sang `SQL Server`);
> (b) `TryAddIncidentAsync` có nhánh `CheckSequence` không bao giờ chạy từ chức năng này → đưa vào §1.

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | `SessionService` gọi thẳng `_db` (không qua repository) ở nhánh duplicate | **Vẽ mũi tên `SessionService → SQL Server`**, nhãn ghi rõ "direct EF query on _db, not via SessionRepository" | Vẽ đúng code thật; thêm participant `:InterviewDbContext` chỉ cho 1 message sẽ làm phình trang mà không thêm thông tin. |
| 2 | 4 nhánh lỗi guard (G1-G4) cùng 400, cùng khuôn `Camera incident could not be reported: …` | **Tách riêng, expand đủ** | Cùng lý do như #8 quyết định 1: chữ user thấy khác nhau. G3/G4 không tới được từ UI hiện tại nhưng vẫn là guard sống với caller gọi thẳng API. |
| 3 | `TryAddIncidentAsync` có 2 roundtrip DB ở nhánh trùng (INSERT lỗi → SELECT EXISTS) | **Vẽ đủ bằng `alt H`** | Đây là 2 lần chạm DB thật, bỏ đi sẽ che mất chi phí thật của nhánh duplicate. |
| 4 | Vòng lặp thử lại camera mỗi 15s | **Không vẽ lặp**, gộp vào nhãn message 3 + `opt F0` | Vòng lặp không sinh thêm request; vẽ lặp sẽ gợi ý sai là mỗi 15s cộng 1 strike. |
| 5 | Nhánh đóng phiên trùng logic với #9 | **Vẽ đầy đủ tại chỗ + note N2 trỏ sang #9** | Đây là code nội tuyến, không phải lời gọi sang chức năng khác → không áp dụng quy tắc boundary 1 cặp call+return. |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `3`)** Two independent idempotency layers: (1) the client reports at most ONE violation per continuous camera-loss episode (cameraIncidentReported, reset only when the camera actually reconnects), and (2) the server has a unique index on IncidentId, so a retried request is stored once and comes back Duplicate=true. *(gỡ từ note trên diagram)*
- **(cạnh message `4`)** shouldEndSession requires inserted == true, so a duplicate report can never end the session on its own. Biometrics:InSessionStrictness = "Flag" turns the whole feature into audit-only: strikes are still recorded, the session is never ended. *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| *(không có)* | — | — | — |
