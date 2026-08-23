# FLOW — `EnrollVoiceBiometric` · BL#13, 12, 53 · UC-`chưa có`

> Đặt tại `docs/diagrams/FLOWS/EnrollVoiceBiometric.md`
> Trạng thái: `DRAFT` — Khảo sát: lô C, 2026-08-03

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Đăng ký (hoặc đăng ký lại) mẫu giọng nói: thu 4-20 giây audio → ai-service (SpeechBrain `EncoderClassifier`) trích embedding → lưu vào `ProfileBiometrics` (`Modality="Voice"`). Kèm BL#53 (thưởng 100 credit khi đủ cả Face + Voice) và màn hình "You're all set" |
| Điểm vào (user thao tác gì) | **Tự động** chuyển tiếp từ bước Face của wizard (`#1 EnrollFaceBiometric`), user đang ngồi trước màn hình → bấm **Start recording**, nói ≥ 4s, bấm **Stop & submit** |
| Kết thúc (user thấy gì) | Màn hình "You're all set" + thông điệp thưởng tuỳ `bonusStatus`. Lỗi → dòng đỏ dưới card + nút **Record again** |
| Quy mô | `13` participant · `92` message · `32` class · `5` alt / `6` opt · trang Sequence 2860×5023 · trang Class 5400×1436 |
| Đối chiếu ước lượng RECON | RECON đoán `26` · thật `92` · hệ số `3.5×` |
| Service ngoài quyền sở hữu | **PaymentAPI** (:7008) — chỉ nhận `AwardCreditAsync`, không sửa |
| **Gọi BYPASS Gateway?** | **CÓ — PaymentAPI :7008 gọi THẲNG** (`InterviewAPI/Program.cs:49, 80-87`), giống `#1` |
| Nhà cung cấp AI | **SpeechBrain** `EncoderClassifier` (`ai-service/biometrics/voice_engine.py:16-28, 51-73`) |
| Ghi DB | `ProfileBiometrics` — INSERT (lần đầu) / UPDATE `Embedding`/`Dim`/`ModelVersion`/`Quality`/`UpdatedAt` (`Modality="Voice"`); đọc `GetAsync` ×2 + `ListByModalityAsync` (dedup thưởng) |
| Trừ credit | Không trừ — **cộng** 100 credit (BL#53) khi đủ cả 2 modality |
| KI liên quan | **KI-09 (mới)** — comment `BiometricEmbeddingResult.cs:6` nói "Quality is face-only; null for voice", sai so với code ai-service |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| BL#12 — `GET api/profiles/me/biometrics` lúc load trang Identity | [ProfileController.cs:140](../../../source/WebClient/Controllers/Profile/ProfileController.cs#L140) | Cùng **một** request với `#1`; đã vẽ ĐẦY ĐỦ ở `FLOWS/EnrollFaceBiometric.md` pha 0. Vẽ lại ở đây sẽ lặp 23 message. Xem §11 quyết định 1. |
| Nhánh `mode === "verify"` của bước Voice (challenge phrase, `VerifyVoice`) | [biometric-wizard.js:466-480, 573-582](../../../source/WebClient/wwwroot/js/pages/biometric-wizard.js#L466-L582) | Thuộc `#16 VerifyVoiceAnswer` (BL#67) và là code CHẾT trên UI hiện tại — xem **KI-05**. |
| Nhánh `mode === "verify"` của màn hình Done (transcript "We heard: …") | [biometric-wizard.js:618-628](../../../source/WebClient/wwwroot/js/pages/biometric-wizard.js#L618-L628) | Cùng lý do. |
| Nhánh lỗi hạ tầng của `AiServiceClient.EmbedAsync` (ai-service unreachable / timeout / non-2xx khác 422) | [AiServiceClient.cs:159, 167-171](../../../source/InterviewAPI/Clients/AiServiceClient.cs#L159-L171) | Cùng tiền lệ lô A / `#1` §11 quyết định 3. |
| `renderBars()` / `AnalyserNode` (thanh mức âm thanh) | [biometric-wizard.js:504-521](../../../source/WebClient/wwwroot/js/pages/biometric-wizard.js#L504-L521) | Hiệu ứng hiển thị, không đổi luồng — gộp 1 câu vào nhãn message 5. |
| `warm_up()` nạp model SpeechBrain lúc khởi động ai-service | [biometrics/router.py:172-181](../../../ai-service/biometrics/router.py#L172-L181) | Chạy 1 lần lúc container start, không thuộc request nào. |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Bước Face vừa xong → wizard tự chuyển sang màn hình Voice | `#1 EnrollFaceBiometric` | message 1 `[auto] Face step passed -> the wizard advances to the Voice screen (entry from #1 EnrollFaceBiometric)` | biometric-wizard.js:409-418 |
| BL#12 — đọc trạng thái enroll lúc load trang | `#1 EnrollFaceBiometric` (pha 0) | *(không vẽ message — xem §1)* | ProfileController.cs:140 |

---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 0 | User | actor | — | — |
| 1 | :Biometric Wizard UI | box | `WebClient/wwwroot/js/pages/biometric-wizard.js` | — |
| 2 | :ProfileController (WebClient) | box | `WebClient/Controllers/Profile/ProfileController.cs:190-212` | — |
| 3 | :BiometricsService (WebClient) | box | `WebClient/Services/Profile/BiometricsService.cs` | — |
| 4 | :API Gateway | box | `ApiGateway` (YARP :7000) | — |
| 5 | :ProfileBiometricsController (InterviewAPI) | box | `InterviewAPI/Controllers/ProfileBiometricsController.cs` | có |
| 6 | :ProfileBiometricsService (InterviewAPI) | box | `InterviewAPI/Services/ProfileBiometricsService.cs` | có |
| 7 | :ProfileBiometricsRepository (InterviewAPI) | box | `InterviewAPI/Repositories/ProfileBiometricsRepository.cs` | có |
| 8 | :AiServiceClient (InterviewAPI) | box | `InterviewAPI/Clients/AiServiceClient.cs:139-172` | có |
| 9 | :ai-service /internal/biometrics/voice/embed | box | `ai-service/biometrics/router.py:59-66` → `voice_engine.py:51-73` | có |
| 10 | :PaymentApiClient (InterviewAPI) | box | `InterviewAPI/Clients/PaymentApiClient.cs` | — |
| 11 | :PaymentAPI (:7008) | box | service ngoài quyền sở hữu | **không (bypass)** |
| 12 | SQL Server | cylinder | `DevPrep_Interview` | — |

> **ACTOR GIÁN TIẾP:** message 1 là chuyển màn hình tự động, nhưng user đang ngồi trước wizard →
> vẫn giữ actor `User`, nhãn ghi `[auto]`.

## 4. BẢNG MESSAGE

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Biometric Wizard UI | call | `1. [auto] Face step passed [see #1]` | — | biometric-wizard.js:409-418 | boundary | 1. [auto] Face step passed -> the wizard advances to the Voice screen (entry from #1 EnrollFaceBiometric) |
| 2 | :Biometric Wizard UI | (self) | call | `2. enterVoiceStep()` | — | biometric-wizard.js:451-488 | self | 2. enterVoiceStep(): guard facePassed; prompt = "Introduce yourself naturally for a few seconds - your name and your role." |
| 3 | :Biometric Wizard UI | User | ret | `3. Show voice screen; "Start recording" enabled` | — | biometric-wizard.js:457-487 |  | 3. Voice screen: prompt, level bars, timer 0:00, Start recording enabled |
| 4 | User | :Biometric Wizard UI | call | `4. Click Start recording` | — | biometric-wizard.js:488 |  | — |
| 5 | :Biometric Wizard UI | (self) | call | `5. startRecording(): MediaRecorder.start()` | — | biometric-wizard.js:491-531 | self | 5. startRecording(): check MediaRecorder supports audio/webm; getUserMedia({audio:true}); AnalyserNode level bars; MediaRecorder.start() |
| A.1 | :Biometric Wizard UI | (self) | call | `A.1. setVoiceError("Could not access the microphone...")` | A | biometric-wizard.js:493-502 | self | A.1. setVoiceError("Your browser does not support audio recording." / "Could not access the microphone...") |
| A.2 | :Biometric Wizard UI | User | ret | `A.2. Show the error` | A | biometric-wizard.js:494, 500 |  | A.2. Voice screen shows the error; nothing recorded, no request sent |
| 6 | :Biometric Wizard UI | (self) | call | `6. 1s timer: enable "Stop & submit" at 4s, auto-stop at 20s` | — | biometric-wizard.js:539-549 | self | 6. 1s timer: Stop & submit stays disabled until 4s; MediaRecorder auto-stops at 20s |
| 7 | :Biometric Wizard UI | User | ret | `7. Show live level bars + elapsed timer` | — | biometric-wizard.js:531-544 |  | 7. Recording: live level bars + elapsed timer; Stop & submit enabled from 4s |
| 8 | User | :Biometric Wizard UI | call | `8. Click "Stop & submit"` | — | biometric-wizard.js:546-556 |  | 8. Click Stop & submit (or the 20s cap stops the recorder automatically) |
| 9 | :Biometric Wizard UI | (self) | call | `9. onRecordingStopped(): stopMic(); blob = new Blob(chunks)` | — | biometric-wizard.js:558-571 | self | 9. onRecordingStopped(): stopMic(); blob = new Blob(chunks, {type:"audio/webm"}); show processing |
| 10 | :Biometric Wizard UI | :ProfileController (WebClient) | call | `10. POST /Profile/EnrollVoice (multipart: voice=voice.webm)` | — | biometric-wizard.js:584-586 |  | — |
| B.1 | :ProfileController (WebClient) | :Biometric Wizard UI | ret | `B.1. Return 400 { error: "A voice recording is required." }` | B | ProfileController.cs:193-196 |  | — |
| B.2 | :Biometric Wizard UI | User | ret | `B.2. failVoiceStep(error)` | B | biometric-wizard.js:591-596, 605-612 |  | B.2. failVoiceStep(error), plus "(recording was Ns - aim for at least 4s)" when the clip was shorter than 4s |
| 11 | :ProfileController (WebClient) | :BiometricsService (WebClient) | call | `11. EnrollAsync("voice", bytes, fileName, contentType, ct)` | — | ProfileController.cs:198-204 |  | 11. EnrollAsync("voice", bytes copied from the upload, fileName ?? voice.webm, contentType ?? audio/webm, ct) |
| 12 | :BiometricsService (WebClient) | :API Gateway | call | `12. POST api/profiles/me/biometrics/voice (multipart file)` | — | BiometricsService.cs:43-49 |  | — |
| 13 | :API Gateway | :ProfileBiometricsController | call | `13. Forward (route profiles-biometrics-route, no prefix strip)` | — | ApiGateway/appsettings.json:61-66 |  | — |
| 14 | :ProfileBiometricsController | (self) | call | `14. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | ProfileBiometricsController.cs:54 | self | — |
| 15 | :ProfileBiometricsController | :ProfileBiometricsService | call | `15. EnrollAsync(userId, "Voice", file, ct)` | — | ProfileBiometricsController.cs:46-48, 55 |  | — |
| C.1 | :ProfileBiometricsService | :ProfileBiometricsController | ret | `C.1. throw ArgumentException("A sample file is required.")` | C | ProfileBiometricsService.cs:47-48 | throw | — |
| C.2 | :ProfileBiometricsController | :API Gateway | ret | `C.2. Return 400 ApiResponse.Fail(ex.Message)` | C | ProfileBiometricsController.cs:67-70 |  | — |
| C.3 | :API Gateway | :BiometricsService (WebClient) | ret | `C.3. Return 400` | C | ApiGateway/appsettings.json:61-66 |  | — |
| C.4 | :BiometricsService (WebClient) | :ProfileController (WebClient) | ret | `C.4. Return (false, null, envelope.Message)` | C | BiometricsService.cs:51-55 |  | — |
| C.5 | :ProfileController (WebClient) | :Biometric Wizard UI | ret | `C.5. Return 400 { error }` | C | ProfileController.cs:206-209 |  | — |
| C.6 | :Biometric Wizard UI | User | ret | `C.6. failVoiceStep(error) - red note + Record again` | C | biometric-wizard.js:591-596 |  | — |
| 16 | :ProfileBiometricsService | :AiServiceClient | call | `16. EmbedVoiceAsync(file, ct)` | — | ProfileBiometricsService.cs:50-55 |  | — |
| 17 | :AiServiceClient | :ai-service | call | `17. POST /internal/biometrics/voice/embed (multipart)` | — | AiServiceClient.cs:139-140, 142-153 |  | 17. POST /internal/biometrics/voice/embed (multipart file, header X-Internal-Api-Key) |
| D.1 | :ai-service | :AiServiceClient | ret | `D.1. Return 422 {detail}` | D | router.py:62-65, voice_engine.py:54-58 |  | D.1. 422 { detail } - only two reasons exist: "Cannot decode audio" or "Audio too short (Xs, need >= 4.0s)" |
| D.2 | :AiServiceClient | :ProfileBiometricsService | ret | `D.2. throw BiometricQualityException(detail)` | D | AiServiceClient.cs:154-158 | throw | — |
| D.3 | :ProfileBiometricsService | :ProfileBiometricsController | ret | `D.3. propagate BiometricQualityException` | D | ProfileBiometricsService.cs:53 | throw | — |
| D.4 | :ProfileBiometricsController | :API Gateway | ret | `D.4. Return 422 ApiResponse.Fail(ex.Message)` | D | ProfileBiometricsController.cs:58-62 |  | — |
| D.5 | :API Gateway | :BiometricsService (WebClient) | ret | `D.5. Return 422` | D | ApiGateway/appsettings.json:61-66 |  | — |
| D.6 | :BiometricsService (WebClient) | :ProfileController (WebClient) | ret | `D.6. Return (false, null, envelope.Message)` | D | BiometricsService.cs:51-55 |  | D.6. Return (false, null, envelope.Message) - the 422 body carries the ai-service reason |
| D.7 | :ProfileController (WebClient) | :Biometric Wizard UI | ret | `D.7. Return 400 { error }` | D | ProfileController.cs:206-209 |  | — |
| D.8 | :Biometric Wizard UI | User | ret | `D.8. failVoiceStep(reason)` | D | biometric-wizard.js:591-596 |  | D.8. failVoiceStep(reason) + the "aim for at least 4s" hint when the clip was short |
| 18 | :ai-service | :AiServiceClient | ret | `18. Return 200 VoiceEmbedResponse` | — | router.py:66, voice_engine.py:70-73, schemas.py:16-21 |  | 18. 200 VoiceEmbedResponse{embedding (L2-normalized), dim, duration_sec, quality, model_version} |
| 19 | :AiServiceClient | :ProfileBiometricsService | ret | `19. Return BiometricEmbeddingResult` | — | AiServiceClient.cs:159-161 |  | 19. Return BiometricEmbeddingResult{Embedding, Dim, Quality, ModelVersion} |
| 20 | :ProfileBiometricsService | :ProfileBiometricsRepository | call | `20. GetAsync(userId, "Voice", ct)` | — | ProfileBiometricsService.cs:58 |  | — |
| 21 | :ProfileBiometricsRepository | SQL Server | call | `21. SELECT ProfileBiometric WHERE UserId AND Modality="Voice"` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| 22 | SQL Server | :ProfileBiometricsRepository | ret | `22. Return row / null` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| 23 | :ProfileBiometricsRepository | :ProfileBiometricsService | ret | `23. Return ProfileBiometric?` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| E-a.1 | :ProfileBiometricsService | :ProfileBiometricsRepository | call | `E-a.1. AddAsync(ProfileBiometric{Modality="Voice"})` | E-a | ProfileBiometricsService.cs:59-72 |  | E-a.1. AddAsync(ProfileBiometric{UserId, Modality="Voice", Embedding=EmbeddingCodec.ToBytes(embedding), Dim, ModelVersion, Quality, CreatedAt, UpdatedAt}) |
| E-a.2 | :ProfileBiometricsRepository | SQL Server | call | `E-a.2. INSERT ProfileBiometric (SaveChangesAsync)` | E-a | ProfileBiometricsRepository.cs:31-35 |  | — |
| E-a.3 | SQL Server | :ProfileBiometricsRepository | ret | `E-a.3. Return rows affected` | E-a | ProfileBiometricsRepository.cs:34 |  | — |
| E-a.4 | :ProfileBiometricsRepository | :ProfileBiometricsService | ret | `E-a.4. Return` | E-a | ProfileBiometricsRepository.cs:34 |  | — |
| E-b.1 | :ProfileBiometricsService | (self) | call | `E-b.1. EnsureReEnrollmentMatches(): Cosine vs threshold 0.25` | E-b | ProfileBiometricsService.cs:95-116 | self | E-b.1. EnsureReEnrollmentMatches(): score = BiometricSimilarity.Cosine(stored, candidate); threshold = Biometrics:VoiceReEnrollmentMatchThreshold (default 0.25) |
| F.1 | :ProfileBiometricsService | :ProfileBiometricsController | ret | `F.1. throw InvalidOperationException` | F | ProfileBiometricsService.cs:100-104 | throw | F.1. throw InvalidOperationException("The enrolled voice template uses an incompatible biometric model.") |
| F.2 | :ProfileBiometricsController | :API Gateway | ret | `F.2. Return 400 ApiResponse.Fail(ex.Message)` | F | ProfileBiometricsController.cs:67-70 |  | — |
| F.3 | :API Gateway | :BiometricsService (WebClient) | ret | `F.3. Return 400` | F | ApiGateway/appsettings.json:61-66 |  | — |
| F.4 | :BiometricsService (WebClient) | :ProfileController (WebClient) | ret | `F.4. Return (false, null, envelope.Message)` | F | BiometricsService.cs:51-55 |  | — |
| F.5 | :ProfileController (WebClient) | :Biometric Wizard UI | ret | `F.5. Return 400 { error }` | F | ProfileController.cs:206-209 |  | — |
| F.6 | :Biometric Wizard UI | User | ret | `F.6. failVoiceStep(error)` | F | biometric-wizard.js:591-596 |  | — |
| G.1 | :ProfileBiometricsService | :ProfileBiometricsController | ret | `G.1. throw BiometricIdentityMismatchException("Voice", ...)` | G | ProfileBiometricsService.cs:115-116 | throw | G.1. throw BiometricIdentityMismatchException("Voice", score, threshold) |
| G.2 | :ProfileBiometricsController | :API Gateway | ret | `G.2. Return 409 ApiResponse.Fail(ex.Message)` | G | ProfileBiometricsController.cs:63-66 |  | — |
| G.3 | :API Gateway | :BiometricsService (WebClient) | ret | `G.3. Return 409` | G | ApiGateway/appsettings.json:61-66 |  | — |
| G.4 | :BiometricsService (WebClient) | :ProfileController (WebClient) | ret | `G.4. Return (false, null, envelope.Message)` | G | BiometricsService.cs:51-55 |  | — |
| G.5 | :ProfileController (WebClient) | :Biometric Wizard UI | ret | `G.5. Return 400 { error }` | G | ProfileController.cs:206-209 |  | — |
| G.6 | :Biometric Wizard UI | User | ret | `G.6. failVoiceStep("The new voice sample does not match...")` | G | biometric-wizard.js:591-596 |  | G.6. failVoiceStep("The new voice sample does not match the currently enrolled identity.") |
| E-b.2 | :ProfileBiometricsService | :ProfileBiometricsRepository | call | `E-b.2. SaveChangesAsync(ct)` | E-b | ProfileBiometricsService.cs:76-81 |  | E-b.2. SaveChangesAsync(ct) after copying Embedding / Dim / ModelVersion / Quality / UpdatedAt onto the tracked entity |
| E-b.3 | :ProfileBiometricsRepository | SQL Server | call | `E-b.3. UPDATE ProfileBiometrics SET Embedding, Quality, UpdatedAt` | E-b | ProfileBiometricsRepository.cs:37 |  | E-b.3. UPDATE ProfileBiometric SET Embedding, Dim, ModelVersion, Quality, UpdatedAt |
| E-b.4 | SQL Server | :ProfileBiometricsRepository | ret | `E-b.4. Return rows affected` | E-b | ProfileBiometricsRepository.cs:37 |  | — |
| E-b.5 | :ProfileBiometricsRepository | :ProfileBiometricsService | ret | `E-b.5. Return` | E-b | ProfileBiometricsRepository.cs:37 |  | — |
| 24 | :ProfileBiometricsService | :ProfileBiometricsRepository | call | `24. GetAsync(userId, "Face", ct)` | — | ProfileBiometricsService.cs:84, 125-128 |  | 24. GetAsync(userId, "Face", ct) - TryAwardEnrollmentBonusAsync: is the OTHER modality enrolled? |
| 25 | :ProfileBiometricsRepository | SQL Server | call | `25. SELECT ProfileBiometric WHERE UserId AND Modality="Face"` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| 26 | SQL Server | :ProfileBiometricsRepository | ret | `26. Return row / null` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| 27 | :ProfileBiometricsRepository | :ProfileBiometricsService | ret | `27. Return ProfileBiometric?` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| H-a.1 | :ProfileBiometricsService | (self) | call | `H-a.1. bonusStatus = "NotYetEligible" - no PaymentAPI call` | H-a | ProfileBiometricsService.cs:129-130 | self | — |
| H-b.1 | :ProfileBiometricsService | :ProfileBiometricsRepository | call | `H-b.1. ListByModalityAsync("Face", ct)` | H-b | ProfileBiometricsService.cs:132, 170 |  | H-b.1. ListByModalityAsync("Face", ct) - IsCanonicalBonusAccountAsync |
| H-b.2 | :ProfileBiometricsRepository | SQL Server | call | `H-b.2. SELECT ProfileBiometrics WHERE Modality="Face"` | H-b | ProfileBiometricsRepository.cs:26-29 |  | H-b.2. SELECT ProfileBiometrics WHERE Modality="Face" (every account) |
| H-b.3 | SQL Server | :ProfileBiometricsRepository | ret | `H-b.3. Return rows` | H-b | ProfileBiometricsRepository.cs:26-29 |  | — |
| H-b.4 | :ProfileBiometricsRepository | :ProfileBiometricsService | ret | `H-b.4. Return List<ProfileBiometric>` | H-b | ProfileBiometricsRepository.cs:26-29 |  | — |
| H-b.5 | :ProfileBiometricsService | (self) | call | `H-b.5. canonicalAccount = oldest FACE template with Cosine >= 0.5` | H-b | ProfileBiometricsService.cs:168-195 | self | H-b.5. canonicalAccount = oldest FACE template with the same Dim + ModelVersion AND Cosine >= Biometrics:BonusDedupMatchThreshold (0.5) |
| L-a.1 | :ProfileBiometricsService | (self) | call | `L-a.1. bonusStatus = "IdentityAlreadyClaimed"` | L-a | ProfileBiometricsService.cs:132-138 | self | L-a.1. bonusStatus = "IdentityAlreadyClaimed" (logged) - no PaymentAPI call |
| L-b.1 | :ProfileBiometricsService | :PaymentApiClient | call | `L-b.1. AwardCreditAsync(userId, 100, userId, ...) [best-effort]` | L-b | ProfileBiometricsService.cs:144-146, CreditConfig.cs:14 | best-effort | L-b.1. AwardCreditAsync(userId, 100, referenceId = userId, "Bonus: enrolled both Face and Voice biometrics") |
| L-b.2 | :PaymentApiClient | :PaymentAPI (:7008) | call | `L-b.2. POST /api/internal/credit/award` | L-b | PaymentApiClient.cs:128-129, Program.cs:49, 80-87 |  | L-b.2. POST /api/internal/credit/award - direct to PaymentAPI :7008, NOT through the Gateway |
| M-a.1 | :PaymentAPI (:7008) | :PaymentApiClient | ret | `M-a.1. Return 200 CreditAwardResponse{Awarded=true}` | M-a | PaymentApiClient.cs:143-146 |  | M-a.1. 200 CreditAwardResponse{Success=true, Awarded=true, BalanceAfter} |
| M-a.2 | :PaymentApiClient | :ProfileBiometricsService | ret | `M-a.2. Return CreditAwardResponse -> bonusStatus = "Awarded"` | M-a | ProfileBiometricsService.cs:156-159 |  | — |
| M-b.1 | :PaymentAPI (:7008) | :PaymentApiClient | ret | `M-b.1. 200 CreditAwardResponse{Success=true, Awarded=false}` | M-b | PaymentApiClient.cs:143-146 |  | — |
| M-b.2 | :PaymentApiClient | :ProfileBiometricsService | ret | `M-b.2. Return CreditAwardResponse -> bonusStatus = "AlreadyAwarded"` | M-b | ProfileBiometricsService.cs:159 |  | — |
| M-c.1 | :PaymentAPI (:7008) | :PaymentApiClient | ret | `M-c.1. Non-2xx / no response` | M-c | PaymentApiClient.cs:131-141 |  | — |
| M-c.2 | :PaymentApiClient | :ProfileBiometricsService | ret | `M-c.2. Return CreditAwardResponse{Success=false} [best-effort]` | M-c | PaymentApiClient.cs:153-161 | best-effort | M-c.2. Return CreditAwardResponse{Success=false} -> bonusStatus = "NotAwarded"; enrollment still succeeds |
| 28 | :ProfileBiometricsService | :ProfileBiometricsController | ret | `28. Return BiometricEnrollResponse{Modality="Voice"}` | — | ProfileBiometricsService.cs:86-92 |  | 28. Return BiometricEnrollResponse{Modality="Voice", Dim, Quality, BonusStatus} |
| 29 | :ProfileBiometricsController | :API Gateway | ret | `29. Return 200 ApiResponse.Ok(result, "Enrolled.")` | — | ProfileBiometricsController.cs:56 |  | — |
| 30 | :API Gateway | :BiometricsService (WebClient) | ret | `30. Return 200` | — | ApiGateway/appsettings.json:61-66 |  | — |
| 31 | :BiometricsService (WebClient) | :ProfileController (WebClient) | ret | `31. Return (true, BiometricEnrollModel, null)` | — | BiometricsService.cs:57 |  | — |
| 32 | :ProfileController (WebClient) | :Biometric Wizard UI | ret | `32. Return 200 { enrolled: true, bonusStatus }` | — | ProfileController.cs:211 |  | — |
| 33 | :Biometric Wizard UI | (self) | call | `33. mergeBonusStatus(); showScreen("done"); enterDoneStep()` | — | biometric-wizard.js:587, 599-602, 617-651 | self | 33. mergeBonusStatus(); "Voice captured"; after 900ms showScreen("done"); enterDoneStep() |
| K-a.1 | :Biometric Wizard UI | User | ret | `K-a.1. Show "You are all set" + "awarded 100 free credits"` | K-a | biometric-wizard.js:633-635 |  | K-a.1. "You're all set" + "You've been awarded 100 free credits for completing both Face and Voice enrollment!" |
| K-b.1 | :Biometric Wizard UI | User | ret | `K-b.1. Show "You are all set" + "bonus already credited"` | K-b | biometric-wizard.js:642-644 |  | K-b.1. "You're all set" + "Your one-time 100-credit enrollment bonus was already credited to this account." |
| K-c.1 | :Biometric Wizard UI | User | ret | `K-c.1. Show "You are all set" + "bonus claimed on another account"` | K-c | biometric-wizard.js:636-638 |  | K-c.1. "You're all set" + "This identity has already received the one-time enrollment bonus on another account..." |
| K-d.1 | :Biometric Wizard UI | User | ret | `K-d.1. Show "You are all set" + "bonus could not be credited"` | K-d | biometric-wizard.js:639-641 |  | K-d.1. "You're all set" + "Your enrollment bonus couldn't be credited right now - please check back later." |
| K-e.1 | :Biometric Wizard UI | User | ret | `K-e.1. Show "You are all set" - no bonus line` | K-e | biometric-wizard.js:630-631, 645-646 |  | K-e.1. "You're all set. Identity verification is set up. You can re-enroll any time." - no bonus line |

**Cờ:** `self` · `throw` · `best-effort` · `boundary`

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| A | opt | `[MediaRecorder does not support audio/webm OR getUserMedia for the microphone throws]` | A.1 → A.2 | — | — |
| B | opt | `[voice is null OR voice.Length == 0]` | B.1 → B.2 | — | — |
| C | opt | `[file is null OR file.Length == 0]` | C.1 → C.6 | — | — |
| D | opt | `[ai-service returns 422 - cannot decode audio, or audio too short]` | D.1 → D.8 | — | — |
| E | alt | E-a: `[first enrollment - no Voice template yet]` · E-b: `[re-enrollment - a Voice template already exists]` | E-a.1 → E-b.5 | — | — |
| F | opt | `[existing.Dim != candidate.Dim OR existing.ModelVersion != candidate.ModelVersion]` | F.1 → F.6 | E-b | — |
| G | opt | `[score < threshold]` | G.1 → G.6 | E-b | — |
| H | alt | H-a: `[Face is not enrolled yet]` · H-b: `[both Face and Voice are now enrolled]` | H-a.1 → M-c.2 | — | — |
| L | alt | L-a: `[canonicalAccount is null OR belongs to an earlier account]` · L-b: `[this account owns the face identity]` | L-a.1 → M-c.2 | H-b | — |
| M | alt | M-a: `[200, Awarded == true]` · M-b: `[200, Awarded == false]` · M-c: `[non-2xx, unreachable, or the client throws]` | M-a.1 → M-c.2 | L-b | — |
| K | alt | K-a `[bonusStatus == "Awarded"]` · K-b `["AlreadyAwarded"]` · K-c `["IdentityAlreadyClaimed"]` · K-d `["NotAwarded"]` · K-e `["NotYetEligible" or no meaningful status]` | K-a.1 → K-e.1 | — | — |

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | D.1 | `The voice embed endpoint has EXACTLY TWO 422 reasons: "Cannot decode audio" (librosa/ffmpeg cannot read the WebM/Opus blob) and "Audio too short". There is NO "too noisy" gate at embed time - DIAGRAM_SCOPE.md expected one, the code does not have it (voice_engine.py:51-58).` |
| N2 | 18 | `KI-09: BiometricEmbeddingResult.cs:6 comments "Quality is face-only; null for voice", but VoiceEmbedResponse ALWAYS carries quality (a duration heuristic, voice_engine.py:70). The diagram shows the real behaviour - Quality is populated for voice too.` |
| N3 | E-b.1 | `The voice re-enrollment threshold (0.25) is deliberately looser than the face one (0.35): speaker embeddings vary far more with microphone, room and phrasing than face embeddings do with lighting and pose.` |
| N4 | K | `The done screen is the ONLY place the bonus outcome is surfaced. mergeBonusStatus keeps the most important status across the face and voice calls (Awarded > IdentityAlreadyClaimed > NotAwarded > AlreadyAwarded > NotYetEligible), so an "Awarded" from the face call is never overwritten by the voice call's "AlreadyAwarded".` |

---

## 7. CLASS — danh sách

Danh sách class **giống hệt `#1 EnrollFaceBiometric`** (cùng code), chỉ khác method hiển thị ở
`ProfileController (WebClient)` (`EnrollVoice` thay cho `Identity`/`EnrollFace`) và bỏ
`BiometricsViewModel` (view model của trang Identity, chỉ dùng ở pha 0 của `#1`).

| # | Tên hiển thị | Loại | Liệt kê | Nguồn |
|---|---|---|---|---|
| 1 | ProfileController (WebClient) | class | SUBSET (`EnrollVoice`) | local |
| 2 | Controller | framework-base | FULL (rỗng) | SHARED |
| 3 | IBiometricsService (WebClient) | interface | FULL | local |
| 4 | BiometricsService (WebClient) | class | FULL | local |
| 5 | IApiClient / 6 ApiClient | interface/class | FULL | SHARED |
| 7 | BiometricStatusModel | DTO | FULL | local |
| 8 | BiometricEnrollModel | DTO | FULL | local |
| 9 | ApiResponse<T> (WebClient) | DTO | FULL | local |
| 10 | ControllerBase | framework-base | FULL (rỗng) | SHARED |
| 11 | ProfileBiometricsController (InterviewAPI) | class | FULL | local |
| 12 | IProfileBiometricsService | interface | FULL | local |
| 13 | ProfileBiometricsService (InterviewAPI) | class | FULL | local |
| 14 | ApiResponse<T> (InterviewAPI) | DTO | FULL | SHARED |
| 15 | BiometricEnrollResponse / 16 BiometricStatusResponse / 17 BiometricEmbeddingResult | DTO | FULL | local |
| 18 | BiometricQualityException / 19 BiometricIdentityMismatchException | exception | FULL | SHARED |
| 20 | EmbeddingCodec / 21 BiometricSimilarity / 22 CreditConfig | helper | FULL | local |
| 23 | IAiServiceClient | interface | FULL | local |
| 24 | AiServiceClient (InterviewAPI) | class | FULL | SHARED |
| 25 | IPaymentApiClient / 26 PaymentApiClient / 27 CreditAwardResponse | interface/class/DTO | FULL | local |
| 28 | IProfileBiometricsRepository / 29 ProfileBiometricsRepository | interface/class | FULL | local |
| 30 | ProfileBiometric | entity | FULL | local |
| 31 | InterviewDbContext / 32 DbContext | class/framework-base | FULL | SHARED |

## 8. CLASS — chi tiết

Xem `FLOWS/EnrollFaceBiometric.md` §8 — cùng bộ class, cùng nội dung. Khác biệt duy nhất:
`ProfileController (WebClient)` ở đây liệt kê `+ EnrollVoice(voice: IFormFile, cancellationToken)`.
Không có class SHARED mới trong chức năng này.

## 9. QUAN HỆ CLASS

Giống `#1` (xem §9 của file đó), bỏ 1 quan hệ `ProfileController → BiometricsViewModel`.

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| **KI-09** | `InterviewAPI/DTOs/AiService/BiometricEmbeddingResult.cs:6` | Comment "Quality is face-only; null for voice" **sai**: `voice_engine.py:70,73` LUÔN trả `quality` (heuristic theo duration) và `schemas.py:16-21` khai báo `quality: float` (không nullable) cho `VoiceEmbedResponse` | comment/tài liệu lệch code | **Vẽ hành vi THẬT** — message 18 ghi rõ `VoiceEmbedResponse{… quality …}`, message 19 giữ `Quality` có giá trị + note N2 | **Có — thêm mới KI-09** |
| — | `docs/diagrams/DIAGRAM_SCOPE.md` | Kỳ vọng có nhánh 422 "quá ồn" ở bước embed voice — code KHÔNG có gate đó (`voice_engine.py:51-58` chỉ có "Cannot decode audio" và "Audio too short") | tài liệu (không phải code) lệch code | **Vẽ hành vi THẬT** — `opt D` chỉ ghi 2 lý do + note N1. Không thêm KI vì đây là tài liệu kế hoạch, không phải mã nguồn | không (đã ghi ở RECON) |


### Gỡ từ note trên diagram — anomaly

- **(cạnh message `A.1`)** KI-09: BiometricEmbeddingResult.cs:6 comments "Quality is face-only; null for voice", but VoiceEmbedResponse ALWAYS carries quality (a duration heuristic, voice_engine.py:70). The diagram shows the real behaviour - Quality is populated for voice too. *(gỡ từ note trên diagram)*

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | BL#12 (GET trạng thái enroll) nằm trong backlog của cả #1 lẫn #2 | **Không vẽ lại ở #2 (§1 + §2 trỏ sang #1)** | Là 1 request duy nhất, đã vẽ đầy đủ 23 message ở `#1` pha 0. |
| 2 | Điểm vào là chuyển màn hình tự động, không phải user bấm | **Vẫn giữ actor `User`**, message 1 gắn nhãn `[auto]` + cờ `boundary` | Đúng quy ước ACTOR GIÁN TIẾP: user đang ngồi trước wizard. |
| 3 | Màn hình Done có 5 thông điệp khác nhau theo `bonusStatus` | **Vẽ `alt K` 5 nhánh, mỗi nhánh 1 return về Actor** | Đây là 5 nội dung user THẬT SỰ nhìn thấy khác nhau → không đủ điều kiện gộp. |
| 4 | Nhánh lỗi hạ tầng ai-service | **Không vẽ (§1)** | Cùng lý do như `#1` §11 quyết định 3. |
| 5 | Comment sai ở `BiometricEmbeddingResult.cs:6` | **Vẽ hành vi THẬT + ghi KI-09** | Đúng quy tắc "comment/tài liệu lệch code → vẽ hành vi THẬT". Không sửa code. |
| 6 | Bố cục trang Class | **Dùng lại đúng bố cục 8 cột đã kiểm của `#1`** | Tránh lặp lại `cls.RoutingError` (xem `#1` §11 quyết định 7). |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `3`)** The voice embed endpoint has EXACTLY TWO 422 reasons: "Cannot decode audio" (librosa/ffmpeg cannot read the WebM/Opus blob) and "Audio too short". There is NO "too noisy" gate at embed time - DIAGRAM_SCOPE.md expected one, the code does not have it (voice_engine.py:51-58). *(gỡ từ note trên diagram)*
- **(cạnh message `6`)** The voice re-enrollment threshold (0.25) is deliberately looser than the face one (0.35): speaker embeddings vary far more with microphone, room and phrasing than face embeddings do with lighting and pose. *(gỡ từ note trên diagram)*
- **(cạnh message `8`)** The done screen is the ONLY place the bonus outcome is surfaced. mergeBonusStatus keeps the most important status across the face and voice calls (Awarded > IdentityAlreadyClaimed > NotAwarded > AlreadyAwarded > NotYetEligible), so an "Awarded" from the face call is never overwritten by the voice call's "AlreadyAwarded". *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| `uc_id` của chức năng | RECON không ghi UC-id cho #2 | Nhóm xác nhận ê-líp use-case rồi điền `uc_id` vào `specs/enrollvoicebiometric.yaml` | luồng 1 |
