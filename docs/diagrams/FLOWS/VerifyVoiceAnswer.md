# FLOW — `VerifyVoiceAnswer` · BL#52, 67 · UC-`10`

> Đặt tại `docs/diagrams/FLOWS/VerifyVoiceAnswer.md`
> Trạng thái: `DRAFT` — Khảo sát: lô C, 2026-08-03

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Khi ứng viên đọc câu trả lời bằng micro trong phòng phỏng vấn: **một** request vừa chuyển giọng thành văn bản (Deepgram STT) vừa đối chiếu giọng với mẫu đã enroll (SpeechBrain). Lệch giọng = 1 strike gian lận trực tiếp; lỗi chất lượng KHÔNG tính strike |
| Điểm vào (user thao tác gì) | Bấm nút **micro** trong Room để thu, bấm lại để dừng |
| Kết thúc (user thấy gì) | Bản chép lời được đổ vào ô trả lời để tự sửa rồi bấm Send. Nếu lệch giọng: banner cảnh báo cam + popup; đủ strike → modal kết thúc + chuyển về `/Interview` |
| Quy mô | `14` participant · `107` message · `32` class · `3` alt / `7` opt · trang Sequence 3120×5153 · trang Class 6260×2148 |
| Đối chiếu ước lượng RECON | RECON đoán `14` · thật `105` · hệ số `7.5×` |
| Service ngoài quyền sở hữu | ApiGateway (chỉ định tuyến) |
| **Gọi BYPASS Gateway?** | không |
| Nhà cung cấp AI | **Deepgram** (STT, `/internal/stt`) **+ SpeechBrain** (speaker verify, `/internal/biometrics/verify` modality=voice) — 2 lời gọi **chạy song song** |
| Ghi DB | `SessionVerificationEvents` INSERT (`Phase="InSession"`, `Modality="Voice"`, `IncidentId=checkId` unique); khi `shouldEndSession`: `InterviewSessions` → `Cancelled` + `InterviewRooms` → `Closed` |
| Trừ credit | không (và không hoàn credit khi kết thúc vì gian lận) |
| KI liên quan | **KI-05** (BL#67 chết trên UI) · **KI-10 (mới)** comment `SttResponse.cs:3` trỏ sai DTO · **KI-11 (mới)** 4 thông điệp `Message` khi bỏ qua kiểm giọng không bao giờ hiển thị |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| **BL#67 — toàn bộ luồng `verify/voice` (challenge phrase độc lập)** | chuỗi đầy đủ ghi ở bảng dưới | **Code chết trên UI thật (KI-05)** — `biometric-wizard.js:267` chỉ đặt `mode="verify"` khi tồn tại `#verify-start-btn` **và** `#verify-session-select`; trang DUY NHẤT nạp file JS này là `Views/Profile/Identity.cshtml` và trang đó KHÔNG có 2 id đó. Theo `QUY_TAC_VE_DIAGRAM.md` (§10 "code chết → §1 hoặc note"), đưa vào §1 + note trên diagram thay vì vẽ, để người đọc không tưởng nhầm BL#67 chạy trong Room. Xem §11 quyết định 1. |
| `stopRecording()` / trạng thái nút micro | [interview-room.js:1090-1092, 1135-1138](../../../source/WebClient/wwwroot/js/pages/interview-room.js#L1090-L1138) | Chi tiết UI, gộp vào nhãn message 2/4. |
| Nhánh lỗi hạ tầng của `AiServiceClient.TranscribeAsync` (ai-service unreachable) | [AiServiceClient.cs:97-118](../../../source/InterviewAPI/Clients/AiServiceClient.cs#L97-L118) | Ném ra ngoài, rơi vào `catch (Exception)` chung của controller → cùng chuỗi 400 đã vẽ ở `opt C`. Không có nhánh xử lý riêng. |
| Endpoint `POST api/interview/sessions/stt` (`InterviewSessionController.cs:544`) | [InterviewSessionController.cs:530-546](../../../source/InterviewAPI/Controllers/InterviewSessionController.cs#L530-L546) | Endpoint STT thuần, **không** có caller nào từ Browser (đã grep `InterviewService.cs`) — thuộc `#6 VoiceInteraction`, và là code chết theo RECON. |

### Chuỗi gọi BL#67 (ghi lại đầy đủ để tra cứu, KHÔNG vẽ)

| Bước | File:Line |
|---|---|
| Browser (wizard, mode="verify") | `biometric-wizard.js:558, 577` |
| WebClient proxy | `WebClient/Controllers/Profile/VerificationController.cs:120` |
| WebClient service | `WebClient/Services/Profile/VerificationService.cs:72` |
| Gateway | `ApiGateway/appsettings.json:49-54` (`interview-route`, không strip) |
| API controller | `InterviewAPI/Controllers/SessionVerificationController.cs:103-126` (`VerifyVoice`) |
| API service | `InterviewAPI/Services/BiometricVerificationService.cs:144-185` (`VerifyVoiceAsync`) — `ConsumeChallengeAsync` + `GetStoredEmbeddingAsync` + `RecordEventAsync` |
| ai-service client | `InterviewAPI/Clients/AiServiceClient.cs:297-331` (`VerifyVoiceChallengeAsync`) |
| ai-service | `ai-service/biometrics/router.py:151-169` → `liveness_voice.py` (Deepgram `language="vi"` + SpeechBrain) |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Sau khi có bản chép lời, user tự sửa rồi bấm **Send** | `#5 ConductInterviewQA` | *(không vẽ — luồng này kết thúc ở "transcript đổ vào ô trả lời")* | interview-room.js:1124-1127 |
| Đóng phiên khi đủ strike (`EndSessionForCheatingAsync`) | dùng chung với `#15 VerifyFaceInSession` | vẽ đầy đủ tại chỗ (`opt P`) — code nội tuyến trong `BiometricVerificationService`, không phải lời gọi sang chức năng khác | BiometricVerificationService.cs:588-608 |

---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 0 | User | actor | — | — |
| 1 | :Interview Room UI | box | `wwwroot/js/pages/interview-room.js` | — |
| 2 | :InterviewController (WebClient) | box | `WebClient/Controllers/Interview/InterviewController.cs:470-500` | — |
| 3 | :InterviewService (WebClient) | box | `WebClient/Services/Interview/InterviewService.cs:349-373` | — |
| 4 | :API Gateway | box | `ApiGateway` (YARP :7000) | — |
| 5 | :SessionVerificationController (InterviewAPI) | box | `InterviewAPI/Controllers/SessionVerificationController.cs:178-203` | có |
| 6 | :BiometricVerificationService (InterviewAPI) | box | `InterviewAPI/Services/BiometricVerificationService.cs:419-608` | có |
| 7 | :SessionRepository (InterviewAPI) | box | `InterviewAPI/Repositories/SessionRepository.cs` | có |
| 8 | :ProfileBiometricsRepository (InterviewAPI) | box | `InterviewAPI/Repositories/ProfileBiometricsRepository.cs` | có |
| 9 | :SessionVerificationEventRepository (InterviewAPI) | box | `InterviewAPI/Repositories/SessionVerificationEventRepository.cs` | có |
| 10 | :AiServiceClient (InterviewAPI) | box | `InterviewAPI/Clients/AiServiceClient.cs:97-118, 262-295` | có |
| 11 | :ai-service (Deepgram STT + SpeechBrain) | box | `ai-service/app/api/internal.py:133-142` + `biometrics/router.py:79-114` | có |
| 12 | :CheatingStrikeHelper (InterviewAPI) | box | `InterviewAPI/Helpers/CheatingStrikeHelper.cs` | có |
| 13 | SQL Server | cylinder | `DevPrep_Interview` | — |

> Actor trực tiếp — user tự bấm micro.

## 4. BẢNG MESSAGE

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Interview Room UI | call | `1. Click the microphone button to start recording the answer` | — | interview-room.js:1135-1138 |  | — |
| 2 | :Interview Room UI | (self) | call | `2. startRecording(): MediaRecorder.start()` | — | interview-room.js:1066-1087 | self | 2. startRecording(): check MediaRecorder supports audio/webm; getUserMedia({audio:true}); MediaRecorder.start(); mic button turns red |
| A.1 | :Interview Room UI | (self) | call | `A.1. appendBubble("AI", "Microphone unavailable...")` | A | interview-room.js:1069-1072, 1085-1087 | self | A.1. appendBubble("AI", "Voice input isn't supported in this browser..." / "Microphone unavailable or permission denied.") |
| A.2 | :Interview Room UI | User | ret | `A.2. Show the reason` | A | interview-room.js:1070, 1086 |  | A.2. Chat bubble with the reason; nothing recorded, no request sent |
| 3 | :Interview Room UI | User | ret | `3. Mic button shows the recording state` | — | interview-room.js:1084 |  | — |
| 4 | User | :Interview Room UI | call | `4. Click the microphone button again to stop` | — | interview-room.js:1090-1092, 1136 |  | — |
| 5 | :Interview Room UI | (self) | call | `5. onstop -> transcribe(): blob = new Blob(chunks)` | — | interview-room.js:1078-1082, 1094-1103 | self | 5. onstop -> transcribe(): blob = new Blob(chunks, {type:"audio/webm"}); voiceCheckId = crypto.randomUUID(); placeholder = "Transcribing..." |
| 6 | :Interview Room UI | :InterviewController (WebClient) | call | `6. POST /Interview/Stt (multipart)` | — | interview-room.js:1099-1104 |  | 6. POST /Interview/Stt (multipart: sessionId, voiceCheckId, audio=answer.webm) |
| B.1 | :InterviewController (WebClient) | :Interview Room UI | ret | `B.1. Return 400 {error}` | B | InterviewController.cs:477-484 |  | B.1. Return 400 { error: "Session id and voice check id are required." / "Audio is required." } |
| B.2 | :Interview Room UI | User | ret | `B.2. appendBubble("AI", "Transcription failed: <error>.")` | B | interview-room.js:1106-1108 |  | — |
| 7 | :InterviewController (WebClient) | :InterviewService (WebClient) | call | `7. TranscribeAudioAsync(sessionId, voiceCheckId, bytes, ...)` | — | InterviewController.cs:486-492 |  | 7. TranscribeAudioAsync(sessionId, voiceCheckId, bytes, fileName ?? answer.webm, contentType ?? audio/webm, ct) |
| 8 | :InterviewService (WebClient) | :API Gateway | call | `8. POST api/interview/sessions/{id}/verify/voice-answer` | — | InterviewService.cs:353-360 |  | 8. POST api/interview/sessions/{sessionId}/verify/voice-answer (multipart: audio, checkId) |
| 9 | :API Gateway | :SessionVerificationController | call | `9. Forward (route interview-route, no prefix strip)` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 10 | :SessionVerificationController | (self) | call | `10. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | SessionVerificationController.cs:190 | self | — |
| 11 | :SessionVerificationController | :BiometricVerificationService | call | `11. VerifyVoiceAnswerAsync(userId, sessionId, checkId, ...)` | — | SessionVerificationController.cs:191-192 |  | 11. VerifyVoiceAnswerAsync(userId, sessionId, checkId, audio, ct) |
| C.1 | :BiometricVerificationService | :SessionVerificationController | ret | `C.1. throw ArgumentException / InvalidOperationException` | C | BiometricVerificationService.cs:422-425, 430-431, 468-471 | throw | — |
| C.2 | :SessionVerificationController | :API Gateway | ret | `C.2. Return 400 ApiResponse.Fail(ex.Message)` | C | SessionVerificationController.cs:199-202 |  | — |
| C.3 | :API Gateway | :InterviewService (WebClient) | ret | `C.3. Return 400` | C | ApiGateway/appsettings.json:49-54 |  | — |
| C.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `C.4. Return (false, null, "Transcription failed (400).")` | C | InterviewService.cs:361-364 |  | C.4. Return (false, null, "Transcription failed (400).") - the server message is discarded here |
| C.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `C.5. Return 400 { error }` | C | InterviewController.cs:494-497 |  | — |
| C.6 | :Interview Room UI | User | ret | `C.6. appendBubble("AI", "Transcription failed (400).")` | C | interview-room.js:1106-1108 |  | C.6. appendBubble("AI", "Transcription failed: Transcription failed (400).") |
| 12 | :BiometricVerificationService | :SessionRepository | call | `12. GetByIdAsync(sessionId, ct)` | — | BiometricVerificationService.cs:427 |  | — |
| 13 | :SessionRepository | SQL Server | call | `13. SELECT InterviewSession WHERE SessionId AND !IsDeleted` | — | SessionRepository.cs:21-24 |  | — |
| 14 | SQL Server | :SessionRepository | ret | `14. Return session row / null` | — | SessionRepository.cs:21-24 |  | — |
| 15 | :SessionRepository | :BiometricVerificationService | ret | `15. Return InterviewSession?` | — | SessionRepository.cs:21-24 |  | — |
| D.1 | :BiometricVerificationService | :SessionVerificationController | ret | `D.1. throw KeyNotFoundException("Session not found.")` | D | BiometricVerificationService.cs:428-429 | throw | — |
| D.2 | :SessionVerificationController | :API Gateway | ret | `D.2. Return 404 ApiResponse.Fail(ex.Message)` | D | SessionVerificationController.cs:195-198 |  | — |
| D.3 | :API Gateway | :InterviewService (WebClient) | ret | `D.3. Return 404` | D | ApiGateway/appsettings.json:49-54 |  | — |
| D.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `D.4. Return (false, null, "Transcription failed (404).")` | D | InterviewService.cs:361-364 |  | — |
| D.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `D.5. Return 400 { error }` | D | InterviewController.cs:494-497 |  | — |
| D.6 | :Interview Room UI | User | ret | `D.6. appendBubble("AI", "Transcription failed (404).")` | D | interview-room.js:1106-1108 |  | D.6. appendBubble("AI", "Transcription failed: Transcription failed (404).") |
| 16 | :BiometricVerificationService | (self) | call | `16. Copy the upload into a byte[]; read maxStrikes from config` | — | BiometricVerificationService.cs:433-440 | self | 16. Copy the upload into a byte[]; contentType ?? audio/webm; fileName ?? answer.webm; maxStrikes = Biometrics:InSessionMaxStrikes (3) |
| 17 | :BiometricVerificationService | :AiServiceClient | call | `17. TranscribeAsync(audioBytes, fileName, contentType, ct)` | — | BiometricVerificationService.cs:439 |  | 17. TranscribeAsync(audioBytes, fileName, contentType, ct) - started WITHOUT await; runs concurrently with the speaker check below |
| 18 | :AiServiceClient | :ai-service | call | `18. POST /internal/stt (multipart audio) - Deepgram` | — | AiServiceClient.cs:97-108, internal.py:133-142 |  | — |
| 19 | :ai-service | :AiServiceClient | ret | `19. 200 { text }` | — | internal.py:141 |  | — |
| 20 | :AiServiceClient | :BiometricVerificationService | ret | `20. Return transcript (awaited at whichever return path runs)` | — | AiServiceClient.cs:109-111 |  | — |
| 21 | :BiometricVerificationService | :ProfileBiometricsRepository | call | `21. GetAsync(userId, "Voice", ct)` | — | BiometricVerificationService.cs:452-453 |  | — |
| 22 | :ProfileBiometricsRepository | SQL Server | call | `22. SELECT ProfileBiometric WHERE UserId AND Modality="Voice"` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| 23 | SQL Server | :ProfileBiometricsRepository | ret | `23. Return row / null` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| 24 | :ProfileBiometricsRepository | :BiometricVerificationService | ret | `24. Return ProfileBiometric?` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| E.1 | :BiometricVerificationService | :SessionVerificationController | ret | `E.1. Return VoiceAnswerVerifyResponse{VoiceChecked=false}` | E | BiometricVerificationService.cs:442-463 |  | E.1. Return VoiceAnswerVerifyResponse{Text, VoiceChecked=false, MaxStrikes, Message} |
| E.2 | :SessionVerificationController | :API Gateway | ret | `E.2. Return 200 ApiResponse.Ok(result)` | E | SessionVerificationController.cs:193 |  | — |
| E.3 | :API Gateway | :InterviewService (WebClient) | ret | `E.3. Return 200` | E | ApiGateway/appsettings.json:49-54 |  | — |
| E.4 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `E.4. Return (true, SttResponse{VoiceChecked=false}, null)` | E | InterviewService.cs:366-372 |  | E.4. Return (true, SttResponse{Text, VoiceChecked=false, ...}, null) |
| E.5 | :InterviewController (WebClient) | :Interview Room UI | ret | `E.5. Return 200 JSON` | E | InterviewController.cs:499 |  | — |
| E.6 | :Interview Room UI | User | ret | `E.6. Transcript dropped into the answer box` | E | interview-room.js:1111, 1124-1127 |  | E.6. Transcript dropped into the answer box; no strike and NO warning - Message is never displayed (KI-11) |
| 25 | :BiometricVerificationService | :SessionVerificationEventRepository | call | `25. GetIncidentAsync(checkId, ct)` | — | BiometricVerificationService.cs:465 |  | — |
| 26 | :SessionVerificationEventRepository | SQL Server | call | `26. SELECT SessionVerificationEvent WHERE IncidentId = @checkId` | — | SessionVerificationEventRepository.cs:43-45 |  | — |
| 27 | SQL Server | :SessionVerificationEventRepository | ret | `27. Return row / null` | — | SessionVerificationEventRepository.cs:43-45 |  | — |
| 28 | :SessionVerificationEventRepository | :BiometricVerificationService | ret | `28. Return SessionVerificationEvent?` | — | SessionVerificationEventRepository.cs:43-45 |  | — |
| F-a.1 | :BiometricVerificationService | (self) | call | `F-a.1. duplicate = true; reuse the stored event` | F-a | BiometricVerificationService.cs:466-476 | self | F-a.1. duplicate = true; reuse the stored event; skip the speaker check entirely |
| F-b.1 | :BiometricVerificationService | :AiServiceClient | call | `F-b.1. VerifyVoiceAsync(probe, storedEmbedding, ct)` | F-b | BiometricVerificationService.cs:481-488 |  | F-b.1. VerifyVoiceAsync(probe, EmbeddingCodec.ToFloats(voiceRow.Embedding), ct) |
| F-b.2 | :AiServiceClient | :ai-service | call | `F-b.2. POST /internal/biometrics/verify (form)` | F-b | AiServiceClient.cs:262-277, router.py:79-114 |  | F-b.2. POST /internal/biometrics/verify (form: modality="voice", stored_embedding, file) - SpeechBrain |
| H.1 | :ai-service | :AiServiceClient | ret | `H.1. Return 422 {detail}` | H | router.py:109-110, AiServiceClient.cs:278-281 |  | H.1. 422 { detail } - or no response at all (unreachable / timeout / other non-2xx) |
| H.2 | :AiServiceClient | :BiometricVerificationService | ret | `H.2. throw BiometricQualityException(detail) / other Exception` | H | AiServiceClient.cs:280, 290-294 | throw | — |
| H.3 | :BiometricVerificationService | :SessionVerificationController | ret | `H.3. Return VoiceAnswerVerifyResponse{VoiceChecked=false}` | H | BiometricVerificationService.cs:490-509 |  | H.3. Return VoiceAnswerVerifyResponse{Text, VoiceChecked=false, Message="Voice verification could not assess this recording." / "...temporarily unavailable."} |
| H.4 | :SessionVerificationController | :API Gateway | ret | `H.4. Return 200 ApiResponse.Ok(result)` | H | SessionVerificationController.cs:193 |  | — |
| H.5 | :API Gateway | :InterviewService (WebClient) | ret | `H.5. Return 200` | H | ApiGateway/appsettings.json:49-54 |  | — |
| H.6 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `H.6. Return (true, SttResponse{...}, null)` | H | InterviewService.cs:366-372 |  | — |
| H.7 | :InterviewController (WebClient) | :Interview Room UI | ret | `H.7. Return 200 JSON` | H | InterviewController.cs:499 |  | — |
| H.8 | :Interview Room UI | User | ret | `H.8. Transcript in the answer box, no warning` | H | interview-room.js:1111, 1124-1127 |  | H.8. Transcript in the answer box, no strike, no warning shown (KI-11) |
| F-b.3 | :ai-service | :AiServiceClient | ret | `F-b.3. 200 VerifyResponse{match, score, threshold}` | F-b | router.py:112-114 |  | — |
| F-b.4 | :AiServiceClient | :BiometricVerificationService | ret | `F-b.4. Return AiFaceVerifyResponse{Match, Score, Threshold}` | F-b | AiServiceClient.cs:282-284 |  | — |
| F-b.5 | :BiometricVerificationService | (self) | call | `F-b.5. Build SessionVerificationEvent{Modality="Voice"}` | F-b | BiometricVerificationService.cs:511-527 | self | F-b.5. Build SessionVerificationEvent{Phase="InSession", Modality="Voice", Score, Matched, Decision = Match ? "Pass" : "Flag", IncidentId = checkId, Detail={Reason, Threshold}} |
| F-b.6 | :BiometricVerificationService | :SessionVerificationEventRepository | call | `F-b.6. TryAddIncidentAsync(event)` | F-b | BiometricVerificationService.cs:529 |  | — |
| F-b.7 | :SessionVerificationEventRepository | SQL Server | call | `F-b.7. INSERT SessionVerificationEvent` | F-b | SessionVerificationEventRepository.cs:24-27 |  | — |
| F-b.8 | SQL Server | :SessionVerificationEventRepository | ret | `F-b.8. Return rows affected / unique-index violation on IncidentId` | F-b | SessionVerificationEventRepository.cs:27-30 |  | — |
| F-b.9 | :SessionVerificationEventRepository | :BiometricVerificationService | ret | `F-b.9. Return inserted` | F-b | SessionVerificationEventRepository.cs:28, 38 |  | — |
| G.1 | :BiometricVerificationService | :SessionVerificationEventRepository | call | `G.1. GetIncidentAsync(checkId, ct)` | G | BiometricVerificationService.cs:530-534 |  | G.1. GetIncidentAsync(checkId, ct) - reload the row the concurrent request stored |
| G.2 | :SessionVerificationEventRepository | SQL Server | call | `G.2. SELECT SessionVerificationEvent WHERE IncidentId` | G | SessionVerificationEventRepository.cs:43-45 |  | — |
| G.3 | SQL Server | :SessionVerificationEventRepository | ret | `G.3. Return row` | G | SessionVerificationEventRepository.cs:43-45 |  | — |
| G.4 | :SessionVerificationEventRepository | :BiometricVerificationService | ret | `G.4. Return the stored event (duplicate = true)` | G | BiometricVerificationService.cs:532-537 |  | — |
| 29 | :BiometricVerificationService | :SessionVerificationEventRepository | call | `29. ListBySessionAsync(sessionId, "InSession", ct)` | — | BiometricVerificationService.cs:550 |  | 29. ListBySessionAsync(sessionId, "InSession", ct) - BuildVoiceAnswerResponseAsync |
| 30 | :SessionVerificationEventRepository | SQL Server | call | `30. SELECT SessionVerificationEvents WHERE Phase="InSession"` | — | SessionVerificationEventRepository.cs:47-55 |  | 30. SELECT SessionVerificationEvents WHERE SessionId AND Phase="InSession" ORDER BY Id DESC |
| 31 | SQL Server | :SessionVerificationEventRepository | ret | `31. Return event rows` | — | SessionVerificationEventRepository.cs:54 |  | — |
| 32 | :SessionVerificationEventRepository | :BiometricVerificationService | ret | `32. Return List<SessionVerificationEvent>` | — | SessionVerificationEventRepository.cs:54 |  | — |
| 33 | :BiometricVerificationService | :CheatingStrikeHelper | call | `33. ComputeEffectiveStrikeCount(events, noFaceStrikeThreshold)` | — | BiometricVerificationService.cs:551-552 |  | 33. ComputeEffectiveStrikeCount(events, Biometrics:NoFaceStrikeThreshold (3)) |
| 34 | :CheatingStrikeHelper | :BiometricVerificationService | ret | `34. Return directStrikeCount + noFaceCount / threshold` | — | CheatingStrikeHelper.cs:11-18 |  | 34. Return directStrikeCount + noFaceCount / noFaceStrikeThreshold |
| 35 | :BiometricVerificationService | (self) | call | `35. Compute mismatch, displayStrikeCount, shouldEndSession` | — | BiometricVerificationService.cs:553-561 | self | 35. mismatch = event.Decision == "Flag"; displayStrikeCount = Min(effective, maxStrikes); shouldEndSession = mismatch AND Biometrics:InSessionStrictness == "Strict" AND effective >= maxStrikes |
| P.1 | :BiometricVerificationService | :SessionRepository | call | `P.1. GetByIdAsync(sessionId, ct) - EndSessionForCheatingAsync` | P | BiometricVerificationService.cs:564, 590 |  | — |
| P.2 | :SessionRepository | SQL Server | call | `P.2. SELECT InterviewSession by SessionId` | P | SessionRepository.cs:21-24 |  | — |
| P.3 | SQL Server | :SessionRepository | ret | `P.3. Return session row / null` | P | SessionRepository.cs:21-24 |  | — |
| P.4 | :SessionRepository | :BiometricVerificationService | ret | `P.4. Return InterviewSession?` | P | SessionRepository.cs:21-24 |  | — |
| P.5 | :BiometricVerificationService | (self) | call | `P.5. session.Status="Cancelled"; EndedAt=now` | P | BiometricVerificationService.cs:593-595, 565 | self | P.5. session.Status="Cancelled"; EndedAt=now; message = "Maximum cheating warnings reached (n/max). This interview has been ended." |
| P.6 | :BiometricVerificationService | :SessionRepository | call | `P.6. GetRoomBySessionAsync(sessionId, ct)` | P | BiometricVerificationService.cs:597 |  | — |
| P.7 | :SessionRepository | SQL Server | call | `P.7. SELECT InterviewRoom by SessionId` | P | SessionRepository.cs:216-217 |  | — |
| P.8 | SQL Server | :SessionRepository | ret | `P.8. Return room row / null` | P | SessionRepository.cs:216-217 |  | — |
| P.9 | :SessionRepository | :BiometricVerificationService | ret | `P.9. Return InterviewRoom?` | P | SessionRepository.cs:216-217 |  | — |
| Q-a.1 | :BiometricVerificationService | :SessionRepository | call | `Q-a.1. UpdateRoomAsync(room{Status="Closed", ClosedAt=now}, ct)` | Q-a | BiometricVerificationService.cs:598-603 |  | — |
| Q-a.2 | :SessionRepository | SQL Server | call | `Q-a.2. UPDATE InterviewSessions SET Status, EndedAt + InterviewRoom` | Q-a | SessionRepository.cs:220-221 |  | Q-a.2. UPDATE InterviewSession SET Status, EndedAt, UpdatedAt + UPDATE InterviewRoom SET Status, ClosedAt (one SaveChangesAsync) |
| Q-a.3 | SQL Server | :SessionRepository | ret | `Q-a.3. Return rows affected` | Q-a | SessionRepository.cs:220-221 |  | — |
| Q-a.4 | :SessionRepository | :BiometricVerificationService | ret | `Q-a.4. Return` | Q-a | SessionRepository.cs:220-221 |  | — |
| Q-b.1 | :BiometricVerificationService | :SessionRepository | call | `Q-b.1. SaveChangesAsync(ct)` | Q-b | BiometricVerificationService.cs:606 |  | — |
| Q-b.2 | :SessionRepository | SQL Server | call | `Q-b.2. UPDATE InterviewSession SET Status, EndedAt, UpdatedAt` | Q-b | SessionRepository.cs:348 |  | — |
| Q-b.3 | SQL Server | :SessionRepository | ret | `Q-b.3. Return rows affected` | Q-b | SessionRepository.cs:348 |  | — |
| Q-b.4 | :SessionRepository | :BiometricVerificationService | ret | `Q-b.4. Return` | Q-b | SessionRepository.cs:348 |  | — |
| 36 | :BiometricVerificationService | :SessionVerificationController | ret | `36. Return VoiceAnswerVerifyResponse{VoiceChecked=true}` | — | BiometricVerificationService.cs:568-579 |  | 36. Return VoiceAnswerVerifyResponse{Text, VoiceChecked=true, VoiceMatched, Score, StrikeCount, MaxStrikes, ShouldEndSession, Duplicate, Message} |
| 37 | :SessionVerificationController | :API Gateway | ret | `37. Return 200 ApiResponse.Ok(result)` | — | SessionVerificationController.cs:193 |  | — |
| 38 | :API Gateway | :InterviewService (WebClient) | ret | `38. Return 200` | — | ApiGateway/appsettings.json:49-54 |  | — |
| 39 | :InterviewService (WebClient) | :InterviewController (WebClient) | ret | `39. Return (true, SttResponse, null)` | — | InterviewService.cs:366-372 |  | 39. Return (true, SttResponse{Text, VoiceChecked, VoiceMatched, Score, StrikeCount, MaxStrikes, ShouldEndSession, Duplicate, Message}, null) |
| 40 | :InterviewController (WebClient) | :Interview Room UI | ret | `40. Return 200 JSON (the full SttResponse)` | — | InterviewController.cs:499 |  | — |
| R-a.1 | :Interview Room UI | (self) | call | `R-a.1. inputEl.value = data.text; resetIdle()` | R-a | interview-room.js:1124-1127 | self | R-a.1. inputEl.value = data.text; resetIdle(); autoResizeInput(); focus |
| R-a.2 | :Interview Room UI | User | ret | `R-a.2. Transcript in the answer box, no warning` | R-a | interview-room.js:1124-1127 |  | R-a.2. Transcript in the answer box, ready to review and Send; no warning |
| R-b.1 | :Interview Room UI | (self) | call | `R-b.1. setCheatingWarning(strikeText, "warning")` | R-b | interview-room.js:1111-1127 | self | R-b.1. strikeText = message + " (strikeCount/maxStrikes warnings)"; setCheatingWarning(strikeText, "warning", 10000); showCheatingBanner(...); then fill the answer box |
| R-b.2 | :Interview Room UI | User | ret | `R-b.2. Show "Verification warning" alert + popup` | R-b | interview-room.js:1112-1120, 1369-1395 |  | R-b.2. Orange "Verification warning" alert + popup ("Final Warning" when strikeCount == maxStrikes - 1); transcript still filled in |
| R-c.1 | :Interview Room UI | (self) | call | `R-c.1. handleForcedCheatingEnd(message): ended=true` | R-c | interview-room.js:1113-1117, 1397-1409 | self | R-c.1. setCheatingWarning(strikeText, "warning", 10000); handleForcedCheatingEnd(message): ended=true; clear timers; disableLifecycleControls(); appendBubble("AI", message) |
| R-c.2 | :Interview Room UI | User | ret | `R-c.2. Show termination modal -> redirect to /Interview` | R-c | interview-room.js:1410-1411 |  | R-c.2. Termination modal, then redirect to /Interview (the session was already closed in message 36) |

**Cờ:** `self` · `throw`

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| A | opt | `[MediaRecorder does not support audio/webm OR getUserMedia throws]` | A.1 → A.2 | — | 2 (cùng bong bóng chat, cùng "không gửi request") |
| B | opt | `[sessionId or voiceCheckId is Guid.Empty OR audio is null/empty]` | B.1 → B.2 | — | 2 (cùng 400, cùng chữ) |
| C | opt | `[checkId == Guid.Empty OR audio is null/empty OR session.Status != "InProgress" OR the checkId belongs to a different session/user/modality]` | C.1 → C.6 | — | **4** (cùng HTTP 400 **và** cùng chữ `Transcription failed (400).` — `InterviewService` bỏ hẳn thân lỗi của server, xem §11 quyết định 2) |
| D | opt | `[session is null OR session.UserId != userId]` | D.1 → D.6 | — | — (404 → chữ khác `Transcription failed (404).`) |
| E | opt | `[Biometrics:Enabled == false OR no Voice enrollment found for this user]` | E.1 → E.6 | — | **2** (cùng 200, `VoiceChecked=false`, và user thấy y hệt) |
| F | alt | F-a: `[a voice check for this checkId already exists in THIS session - replayed request]` · F-b: `[no event for this checkId yet - run the speaker check]` | F-a.1 → G.4 | — | — |
| H | opt | `[ai-service returns 422 (quality) OR any other ai-service failure]` | H.1 → H.8 | F-b | **2** (cùng 200, `VoiceChecked=false`, user thấy y hệt) |
| G | opt | `[inserted == false - a concurrent request already stored this checkId]` | G.1 → G.4 | F-b | — |
| P | opt | `[shouldEndSession == true]` | P.1 → Q-b.4 | — | — |
| Q | alt | Q-a: `[room is not null]` · Q-b: `[room is null]` | Q-a.1 → Q-b.4 | P | — |
| R | alt | R-a: `[data.voiceMatched !== false]` · R-b: `[voiceMatched === false AND !shouldEndSession]` · R-c: `[voiceMatched === false AND shouldEndSession]` | R-a.1 → R-c.2 | — | — |

> `P` là `opt` **tự khép kín** → luồng vẫn chạy tiếp vào message 36 dù guard đúng hay sai.

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | 17-20 | `TranscribeAsync is started WITHOUT await (BiometricVerificationService.cs:439) and only awaited on whichever return path runs, so the Deepgram transcription and the SpeechBrain speaker check overlap in time. The generator has no "par" frame, so the STT call is drawn in the order it is STARTED; its result is consumed later.` |
| N2 | — (BL#67) | `BL#67 (POST .../verify/voice, standalone challenge phrase) is NOT drawn: it has no UI entry point today (KI-05). biometric-wizard.js only switches to mode="verify" when #verify-start-btn AND #verify-session-select exist, and Identity.cshtml - the only page that loads that script - has neither. The full call chain is listed in FLOWS/VerifyVoiceAnswer.md section 1.` |
| N3 | E.6 / H.6 | `KI-11: all four "check skipped" paths set a Message (biometrics disabled / no voice enrollment / could not assess / temporarily unavailable), but the Room only reads data.message inside the voiceMatched === false branch. VoiceMatched is null on these paths, so the user is never told the voice check did not run.` |
| N4 | 11 | `Voice checking is deliberately opt-in: it only runs when the candidate dictates an answer. Typing an answer skips it entirely - Face already provides the mandatory per-question identity check (#15 VerifyFaceInSession).` |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | Liệt kê | Nguồn |
|---|---|---|---|---|
| 1 | InterviewController (WebClient) | class | SUBSET (`Stt`) | local |
| 2 | Controller | framework-base | FULL (rỗng) | SHARED |
| 3 | IInterviewService (WebClient) | interface | SUBSET | local |
| 4 | InterviewService (WebClient) | class | SUBSET | local |
| 5 | IApiClient / 6 ApiClient | interface/class | FULL | SHARED |
| 7 | SttResponse (WebClient) | DTO | FULL | local |
| 8 | ApiResponse<T> (WebClient) | DTO | FULL | local |
| 9 | ControllerBase | framework-base | FULL (rỗng) | SHARED |
| 10 | SessionVerificationController (InterviewAPI) | class | SUBSET (`VerifyVoiceAnswer`) | local |
| 11 | IBiometricVerificationService | interface | SUBSET | local |
| 12 | BiometricVerificationService (InterviewAPI) | class | SUBSET | local |
| 13 | ApiResponse<T> (InterviewAPI) | DTO | FULL | SHARED |
| 14 | VoiceAnswerVerifyResponse | DTO | FULL | local |
| 15 | AiFaceVerifyResponse | DTO | FULL | local |
| 16 | BiometricQualityException | exception | FULL | SHARED |
| 17 | EmbeddingCodec | helper | FULL | local |
| 18 | CheatingStrikeHelper | helper | FULL | SHARED |
| 19 | IAiServiceClient | interface | FULL | local |
| 20 | AiServiceClient (InterviewAPI) | class | FULL | SHARED |
| 21 | ISessionRepository / 22 SessionRepository (InterviewAPI) | interface/class | SUBSET | local |
| 23 | IProfileBiometricsRepository / 24 ProfileBiometricsRepository (InterviewAPI) | interface/class | FULL | local |
| 25 | ISessionVerificationEventRepository / 26 SessionVerificationEventRepository (InterviewAPI) | interface/class | FULL | local |
| 27 | InterviewSession / 28 InterviewRoom / 29 SessionVerificationEvent / 30 ProfileBiometric | entity | FULL | SHARED (ProfileBiometric: local) |
| 31 | InterviewDbContext / 32 DbContext | class/framework-base | FULL | SHARED |

## 8. CLASS — chi tiết

### `BiometricVerificationService (InterviewAPI)` — `SUBSET`
- 5 field DI; method `+ VerifyVoiceAnswerAsync(...)`, `- BuildVoiceAnswerResponseAsync(...)`,
  `- EndSessionForCheatingAsync(...)`, `- GetStoredEmbeddingAsync(...)`
- *(SUBSET)* Đã bỏ các method thuộc `#12`/`#13`/`#14`/`#15` (`IssueChallengeAsync`,
  `VerifyFaceLiveAsync`, `VerifyVoiceAsync`, `GetEntryGateStatusAsync`, `VerifyInSessionAsync`…).

### `InterviewController (WebClient)` / `InterviewService (WebClient)` — `SUBSET`
- `+ Stt(sessionId, voiceCheckId, audio, ct)` / `+ TranscribeAudioAsync(...)`

Không có class SHARED mới trong chức năng này.

## 9. QUAN HỆ CLASS

Xem file spec `specs/verifyvoiceanswer_classes.yaml` (khai báo `relations:` đầy đủ, cùng khuôn với
`#15 VerifyFaceInSession`).

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| KI-05 | `biometric-wizard.js:42-43, 267` + `Views/Profile/Identity.cshtml` | BL#67 (`verify/voice`) không có lối vào UI | code chết | **Không vẽ**, đưa vào §1 + note N2 | Có (đã tồn tại) |
| **KI-10** | `WebClient/Models/Interview/SttResponse.cs:3` | Comment ghi "Mirrors InterviewAPI.DTOs.Session.SttResponse" nhưng class đó chỉ có `Text`; thực tế nó mirror `InterviewAPI.DTOs.Biometrics.VoiceAnswerVerifyResponse` (9 field). Người đọc dễ tưởng 8 field còn lại là code chết | comment lệch code | **Vẽ hành vi THẬT** — message 39/40 ghi đủ 9 field | **Có — thêm mới KI-10** |
| **KI-11** | `BiometricVerificationService.cs:442-509` + `interview-room.js:1111-1121` | 4 nhánh "bỏ qua kiểm giọng" đều set `Message` (biometrics disabled / chưa enroll voice / không đánh giá được / tạm thời không khả dụng) nhưng Room chỉ đọc `data.message` **bên trong** nhánh `voiceMatched === false`. Ở 4 nhánh này `VoiceMatched` là `null` → **người dùng không bao giờ được báo là bước kiểm giọng đã bị bỏ qua** | bug hành vi có hậu quả thật (im lặng) | **Vẽ hành vi THẬT** (E.6 / H.6 ghi rõ "no warning shown") + note N3 | **Có — thêm mới KI-11** |


### Gỡ từ note trên diagram — anomaly

- **(cạnh message `6`)** KI-11: all four "check skipped" paths set a Message (biometrics disabled / no voice enrollment / could not assess / temporarily unavailable), but the Room only reads data.message inside the voiceMatched === false branch. VoiceMatched is null on these paths, so the user is never told the voice check did not run. *(gỡ từ note trên diagram)*

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | RECON yêu cầu vẽ CẢ 2 endpoint (BL#52 sống + BL#67 chết) trong 1 file | **Chỉ vẽ BL#52; BL#67 đưa vào §1 kèm bảng chuỗi gọi đầy đủ + note N2 nổi bật trên diagram** | (a) `QUY_TAC_VE_DIAGRAM.md` §10 cho phép "code chết → §1 hoặc note"; (b) vẽ thêm BL#67 cần 4 participant nữa và ~29 message, đẩy trang lên ~6055px, **vượt ngưỡng 5500px**; (c) đúng tinh thần cảnh báo của RECON là không để người đọc tưởng BL#67 chạy trong Room. Nếu hội đồng muốn có hình BL#67, đề xuất tách thành file riêng — ghi ở §12. |
| 2 | 4 guard lỗi 400 nằm rải rác trong `VerifyVoiceAnswerAsync` | **Gộp thành 1 `opt C`, guard ghép bằng OR** | Thoả **cả hai** điều kiện gộp: cùng HTTP 400 **và** cùng chữ user thấy — `InterviewService.TranscribeAudioAsync:361-364` vứt bỏ thân lỗi của server và luôn trả `"Transcription failed (400)."`. Cột File:Line liệt kê đủ 3 vị trí code. |
| 3 | 4 nhánh trả 200 với `VoiceChecked=false` | **Gộp thành 2 `opt` (E và H) theo đúng vị trí trong code** | Cùng 200 + user thấy y hệt (transcript, không cảnh báo). Không gộp cả 4 thành 1 vì 2 nhánh đầu xảy ra TRƯỚC lời gọi ai-service còn 2 nhánh sau xảy ra SAU — gộp hết sẽ sai thứ tự. |
| 4 | 2 lời gọi ai-service chạy song song (STT + speaker verify) | **Vẽ theo thứ tự KHỞI PHÁT, kèm note N1 giải thích** | Generator không có khung `par`; sửa generator là ràng buộc cuối cùng, không cần thiết ở đây. |
| 5 | Nhánh lỗi hạ tầng ai-service ở `VerifyVoiceAsync` | **CÓ vẽ (gộp vào `opt H`)** — khác với `#1`/`#2` | Ở đây `catch (Exception)` **không** ném tiếp mà trả 200 với thông điệp riêng → là kết cục user-visible thật, không phải lỗi hạ tầng xuyên suốt. |
| 6 | Gán `room.Status="Closed"` | **Gộp vào nhãn `UpdateRoomAsync(room{...})`** | Tiền lệ lô A (`verifyfaceinsession.yaml` J.10) và lô B. |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `2`)** TranscribeAsync is started WITHOUT await (BiometricVerificationService.cs:439) and only awaited on whichever return path runs, so the Deepgram transcription and the SpeechBrain speaker check overlap in time. The generator has no "par" frame, so the STT call is drawn in the order it is STARTED; its result is consumed later. *(gỡ từ note trên diagram)*
- **(cạnh message `A.2`)** BL#67 (POST .../verify/voice, standalone challenge phrase) is NOT drawn here: it has no UI entry point today (KI-05). biometric-wizard.js only switches to mode="verify" when #verify-start-btn AND #verify-session-select exist, and Identity.cshtml - the only page that loads that script - has neither. The full call chain is listed in FLOWS/VerifyVoiceAnswer.md section 1. *(gỡ từ note trên diagram)*
- **(cạnh message `7`)** Voice checking is deliberately opt-in: it only runs when the candidate dictates an answer. Typing an answer skips it entirely - Face already provides the mandatory per-question identity check (#15 VerifyFaceInSession). *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| Hình sequence cho BL#67 | Không thiếu dữ liệu — chuỗi gọi đã tra đủ (§1). Vấn đề là **ngân sách kích thước trang** (thêm BL#67 → ~6055px > ngưỡng 5500px) | DuyenTQ quyết: (a) giữ nguyên như hiện tại — BL#67 chỉ ghi ở §1 + note, hoặc (b) tách file riêng `VerifyVoiceChallenge.drawio` cho BL#67. Dữ liệu để vẽ đã sẵn ở §1 | luồng 2 |
