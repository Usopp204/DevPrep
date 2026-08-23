# FLOW — `EnrollFaceBiometric` · BL#11, 12, 53 · UC-`chưa có`

> Đặt tại `docs/diagrams/FLOWS/EnrollFaceBiometric.md`
> Trạng thái: `DRAFT` — Khảo sát: lô C, 2026-08-03

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Đăng ký (hoặc đăng ký lại) mẫu khuôn mặt của user: chụp 1 ảnh từ camera → ai-service (InsightFace) trích embedding 512 chiều → lưu vào `ProfileBiometrics`. Kèm BL#12 (đọc trạng thái enroll lúc load trang) và BL#53 (thưởng 100 credit khi đủ cả Face + Voice) |
| Điểm vào (user thao tác gì) | Mở **Profile › Identity verification**, cấp quyền camera/mic, tick đồng ý, bấm **Start** → màn hình Face → bấm **I'm ready** |
| Kết thúc (user thấy gì) | Oval chuyển xanh "Face captured ✓", sau 1.1s wizard tự chuyển sang bước Voice (`#2 EnrollVoiceBiometric`). Lỗi → oval đỏ "Check failed" + lý do + nút Try again |
| Quy mô | `13` participant · `109` message · `33` class · `4` alt / `5` opt · trang Sequence 2850×5293 · trang Class 5400×1436 |
| Đối chiếu ước lượng RECON | RECON đoán `26` · thật `109` · hệ số `4.2×` |
| Service ngoài quyền sở hữu | **PaymentAPI** (:7008) — chỉ nhận `AwardCreditAsync` ở nhánh thưởng BL#53, không sửa |
| **Gọi BYPASS Gateway?** | **CÓ — PaymentAPI :7008 gọi THẲNG.** Đã tự xác minh: `InterviewAPI/Program.cs:49, 80-87` đặt `BaseAddress = PAYMENT_API_BASEURL` (mặc định `http://localhost:7008`), không phải URL Gateway → KHÔNG vẽ hop ApiGateway cho message L-b.2 |
| Nhà cung cấp AI | **InsightFace** (`ai-service/biometrics/face_engine.py:22-27`, model pack cấu hình trong `biometrics/config.py`) |
| Ghi DB | `ProfileBiometrics` — INSERT (lần đầu) hoặc UPDATE `Embedding`/`Dim`/`ModelVersion`/`Quality`/`UpdatedAt` (enroll lại). Đọc: `ListByUserAsync` (BL#12), `GetAsync` ×2, `ListByModalityAsync` (dedup thưởng) |
| Trừ credit | Không trừ — **cộng** 100 credit (BL#53, `CreditConfig.BiometricEnrollmentBonusCredits`) khi đủ cả 2 modality |
| KI liên quan | không (không phát hiện KI mới) |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| `interviewService.GetDashboardAsync()` trong action `Identity` | [ProfileController.cs:144-153](../../../source/WebClient/Controllers/Profile/ProfileController.cs#L144-L153) | Chỉ nạp danh sách phiên cho khu "Test verification" **tạm thời của Milestone 2** (comment code ghi rõ "Removed in Milestone 4"), bọc `try/catch` nuốt lỗi, không liên quan enrollment. Thuộc `#4 ViewInterviewHistory`. |
| Guard `access_token` cookie ở đầu action `Identity` | [ProfileController.cs:136-137](../../../source/WebClient/Controllers/Profile/ProfileController.cs#L136-L137) | Guard đăng nhập dùng chung mọi action Profile — thuộc `Login`, không phải logic enrollment. |
| Nhánh `mode === "verify"` của wizard (8 frame, challenge direction) | [biometric-wizard.js:357-368, 384-395](../../../source/WebClient/wwwroot/js/pages/biometric-wizard.js#L357-L395) | Thuộc `#13 VerifyFaceEntryGate` / `#12 GetVerificationChallenge`. Chức năng này chỉ đi nhánh `enroll` (1 frame). |
| Nhánh lỗi hạ tầng của `AiServiceClient.EmbedAsync` (ai-service unreachable / timeout / non-2xx khác 422) | [AiServiceClient.cs:159, 167-171](../../../source/InterviewAPI/Clients/AiServiceClient.cs#L159-L171) | Theo tiền lệ lô A (`specs/verifyfaceinsession.yaml` cũng không vẽ nhánh này): lỗi hạ tầng chung cho MỌI lời gọi ai-service trong bộ diagram, vẽ ở từng file sẽ lặp vô ích. Xem §11 quyết định 3. |
| `MultipleFacesError.face_count` (mã lỗi có cấu trúc) | [face_engine.py:36-44](../../../ai-service/biometrics/face_engine.py#L36-L44) | Endpoint `/face/embed` trả `detail` dạng **chuỗi** (`router.py:55`), không phải JSON có `code` — chỉ endpoint `/verify` mới trả cấu trúc (`#15 VerifyFaceInSession`). Ở đây multi-face chỉ là 1 trong các lý do 422 văn bản. |
| `face_frame_features()` (liveness) | [face_engine.py:98-107](../../../ai-service/biometrics/face_engine.py#L98-L107) | Chỉ dùng cho `/face/verify-live` (`#13`). |
| `stopAllMedia()` / hiệu ứng chuyển màn hình | [biometric-wizard.js:135-174](../../../source/WebClient/wwwroot/js/pages/biometric-wizard.js#L135-L174) | Dọn tài nguyên UI, không đổi luồng nghiệp vụ. |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Wizard tự chuyển sang bước Voice sau khi face xong | `#2 EnrollVoiceBiometric` | message 49 `... after 1.1s the wizard auto-advances to the Voice step (#2 EnrollVoiceBiometric)` | biometric-wizard.js:414-418 |

---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 0 | User | actor | — | — |
| 1 | :Biometric Wizard UI | box | `WebClient/wwwroot/js/pages/biometric-wizard.js` + `Views/Profile/Identity.cshtml` | — |
| 2 | :ProfileController (WebClient) | box | `WebClient/Controllers/Profile/ProfileController.cs` | — |
| 3 | :BiometricsService (WebClient) | box | `WebClient/Services/Profile/BiometricsService.cs` | — |
| 4 | :API Gateway | box | `ApiGateway` (YARP :7000) | — |
| 5 | :ProfileBiometricsController (InterviewAPI) | box | `InterviewAPI/Controllers/ProfileBiometricsController.cs` | có |
| 6 | :ProfileBiometricsService (InterviewAPI) | box | `InterviewAPI/Services/ProfileBiometricsService.cs` | có |
| 7 | :ProfileBiometricsRepository (InterviewAPI) | box | `InterviewAPI/Repositories/ProfileBiometricsRepository.cs` | có |
| 8 | :AiServiceClient (InterviewAPI) | box | `InterviewAPI/Clients/AiServiceClient.cs` | có |
| 9 | :ai-service /internal/biometrics/face/embed | box | `ai-service/biometrics/router.py:49-56` → `face_engine.py:74-95` | có |
| 10 | :PaymentApiClient (InterviewAPI) | box | `InterviewAPI/Clients/PaymentApiClient.cs` | — |
| 11 | :PaymentAPI (:7008) | box | service ngoài quyền sở hữu | **không (bypass)** |
| 12 | SQL Server | cylinder | `DevPrep_Interview` | — |

> Actor trực tiếp — user tự bấm từng bước của wizard.

## 4. BẢNG MESSAGE

### Pha 0 — load trang Identity, đọc trạng thái enroll (BL#12)

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Biometric Wizard UI | call | `1. Open Profile > Identity verification` | — | Views/Profile/Identity.cshtml:35-36 |  | — |
| 2 | :Biometric Wizard UI | :ProfileController (WebClient) | call | `2. GET /Profile/Identity` | — | ProfileController.cs:133-134 |  | — |
| 3 | :ProfileController (WebClient) | :BiometricsService (WebClient) | call | `3. GetStatusAsync(ct)` | — | ProfileController.cs:140 |  | — |
| 4 | :BiometricsService (WebClient) | :API Gateway | call | `4. GET api/profiles/me/biometrics` | — | BiometricsService.cs:28-29 |  | — |
| 5 | :API Gateway | :ProfileBiometricsController | call | `5. Forward` | — | ApiGateway/appsettings.json:55-60 |  | 5. Forward (route profiles-biometrics-root-route, no prefix strip) |
| 6 | :ProfileBiometricsController | (self) | call | `6. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | ProfileBiometricsController.cs:30 | self | — |
| 7 | :ProfileBiometricsController | :ProfileBiometricsService | call | `7. GetStatusAsync(userId, ct)` | — | ProfileBiometricsController.cs:31 |  | — |
| 8 | :ProfileBiometricsService | :ProfileBiometricsRepository | call | `8. ListByUserAsync(userId, ct)` | — | ProfileBiometricsService.cs:200 |  | — |
| 9 | :ProfileBiometricsRepository | SQL Server | call | `9. SELECT ProfileBiometrics WHERE UserId` | — | ProfileBiometricsRepository.cs:21-24 |  | — |
| 10 | SQL Server | :ProfileBiometricsRepository | ret | `10. Return rows` | — | ProfileBiometricsRepository.cs:21-24 |  | — |
| 11 | :ProfileBiometricsRepository | :ProfileBiometricsService | ret | `11. Return List<ProfileBiometric>` | — | ProfileBiometricsRepository.cs:21-24 |  | — |
| A-a.1 | :ProfileBiometricsService | :ProfileBiometricsController | ret | `A-a.1. Return BiometricStatusResponse` | A-a | ProfileBiometricsService.cs:203-209 |  | A-a.1. Return BiometricStatusResponse{FaceEnrolled, FaceUpdatedAt, VoiceEnrolled, VoiceUpdatedAt} |
| A-a.2 | :ProfileBiometricsController | :API Gateway | ret | `A-a.2. Return 200 ApiResponse.Ok(result)` | A-a | ProfileBiometricsController.cs:32 |  | — |
| A-a.3 | :API Gateway | :BiometricsService (WebClient) | ret | `A-a.3. Return 200` | A-a | ApiGateway/appsettings.json:55-60 |  | — |
| A-a.4 | :BiometricsService (WebClient) | :ProfileController (WebClient) | ret | `A-a.4. Return (true, BiometricStatusModel, null)` | A-a | BiometricsService.cs:30-36 |  | — |
| A-a.5 | :ProfileController (WebClient) | :Biometric Wizard UI | ret | `A-a.5. Return View Identity (BiometricsViewModel{Status})` | A-a | ProfileController.cs:155-160 |  | — |
| A-a.6 | :Biometric Wizard UI | User | ret | `A-a.6. Show Identity page` | A-a | Views/Profile/Identity.cshtml:35-36 |  | A-a.6. Identity page: Face / Voice enrollment state + the wizard guide screen |
| A-b.1 | :ProfileBiometricsService | :ProfileBiometricsController | ret | `A-b.1. throw Exception (e.g. database unavailable)` | A-b | ProfileBiometricsController.cs:34-37 | throw | — |
| A-b.2 | :ProfileBiometricsController | :API Gateway | ret | `A-b.2. Return 400 ApiResponse.Fail(ex.Message)` | A-b | ProfileBiometricsController.cs:36 |  | — |
| A-b.3 | :API Gateway | :BiometricsService (WebClient) | ret | `A-b.3. Return 400` | A-b | ApiGateway/appsettings.json:55-60 |  | — |
| A-b.4 | :BiometricsService (WebClient) | :ProfileController (WebClient) | ret | `A-b.4. Return (false, null, envelope.Message)` | A-b | BiometricsService.cs:31-34 |  | — |
| A-b.5 | :ProfileController (WebClient) | :Biometric Wizard UI | ret | `A-b.5. Return View Identity (BiometricsViewModel)` | A-b | ProfileController.cs:155-160 |  | A-b.5. Return View Identity (BiometricsViewModel{ErrorMessage}) - the page still renders |
| A-b.6 | :Biometric Wizard UI | User | ret | `A-b.6. Identity page with an error banner; enrollment state unknown` | A-b | ProfileController.cs:158 |  | — |

### Pha 1 — wizard: cấp quyền → chụp mặt → enroll (BL#11) → thưởng (BL#53)

| # | From | To | call/ret | Label (tiếng Anh) | Frame | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 18 | User | :Biometric Wizard UI | call | `18. Click Grant camera & microphone access` | — | biometric-wizard.js:242 |  | — |
| 19 | :Biometric Wizard UI | (self) | call | `19. getUserMedia({video, audio}) probe; applyPermState()` | — | biometric-wizard.js:244-253 | self | 19. getUserMedia({video:true, audio:true}) probe, tracks stopped immediately; applyPermState() |
| 20 | :Biometric Wizard UI | User | ret | `20. Update permission pills; Start stays disabled` | — | biometric-wizard.js:196-216 |  | 20. Permission pills updated; Start stays disabled until consent is ticked AND both permissions are granted |
| 21 | User | :Biometric Wizard UI | call | `21. Tick the consent checkbox, then click Start` | — | biometric-wizard.js:256-265 |  | — |
| 22 | :Biometric Wizard UI | (self) | call | `22. showScreen("face"); enterFaceStep(): getUserMedia({video})` | — | biometric-wizard.js:258-264, 291-320 | self | 22. mode="enroll"; showScreen("face"); enterFaceStep(): getUserMedia({video:true}) -> live preview |
| B.1 | :Biometric Wizard UI | (self) | call | `B.1. setFaceError("Could not access the camera...")` | B | biometric-wizard.js:321-327 | self | B.1. setFaceError("Could not access the camera..."); action button becomes Try again |
| B.2 | :Biometric Wizard UI | User | ret | `B.2. Show "Camera unavailable"` | B | biometric-wizard.js:322-326 |  | B.2. Face screen shows "Camera unavailable" and a Try again button |
| 23 | :Biometric Wizard UI | User | ret | `23. Show face screen; "I am ready" enabled` | — | biometric-wizard.js:330-334 |  | 23. Face screen: oval preview, "Center your face in the oval", I'm ready enabled |
| 24 | User | :Biometric Wizard UI | call | `24. Click I am ready` | — | biometric-wizard.js:334 |  | 24. Click I'm ready |
| 25 | :Biometric Wizard UI | (self) | call | `25. runFaceCapture(): countdown -> canvas.toBlob("image/jpeg")` | — | biometric-wizard.js:337-381 | self | 25. runFaceCapture(): 3-2-1 countdown, hold 350ms, canvas.drawImage(video) -> canvas.toBlob("image/jpeg", 0.92); stopCamera(); show processing |
| 26 | :Biometric Wizard UI | :ProfileController (WebClient) | call | `26. POST /Profile/EnrollFace (multipart: face=face.jpg)` | — | biometric-wizard.js:397-399 |  | — |
| C.1 | :ProfileController (WebClient) | :Biometric Wizard UI | ret | `C.1. Return 400 { error: "A face image is required." }` | C | ProfileController.cs:168-171 |  | — |
| C.2 | :Biometric Wizard UI | User | ret | `C.2. failFaceStep(error) - red oval, "Check failed", Try again` | C | biometric-wizard.js:403-406, 421-428 |  | — |
| 27 | :ProfileController (WebClient) | :BiometricsService (WebClient) | call | `27. EnrollAsync("face", bytes, fileName, contentType, ct)` | — | ProfileController.cs:173-179 |  | 27. EnrollAsync("face", bytes copied from the upload, fileName ?? face.jpg, contentType ?? image/jpeg, ct) |
| 28 | :BiometricsService (WebClient) | :API Gateway | call | `28. POST api/profiles/me/biometrics/face (multipart file)` | — | BiometricsService.cs:43-49 |  | — |
| 29 | :API Gateway | :ProfileBiometricsController | call | `29. Forward (route profiles-biometrics-route, no prefix strip)` | — | ApiGateway/appsettings.json:61-66 |  | — |
| 30 | :ProfileBiometricsController | (self) | call | `30. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | ProfileBiometricsController.cs:54 | self | — |
| 31 | :ProfileBiometricsController | :ProfileBiometricsService | call | `31. EnrollAsync(userId, "Face", file, ct)` | — | ProfileBiometricsController.cs:41-43, 55 |  | — |
| D.1 | :ProfileBiometricsService | :ProfileBiometricsController | ret | `D.1. throw ArgumentException("A sample file is required.")` | D | ProfileBiometricsService.cs:47-48 | throw | — |
| D.2 | :ProfileBiometricsController | :API Gateway | ret | `D.2. Return 400 ApiResponse.Fail(ex.Message)` | D | ProfileBiometricsController.cs:67-70 |  | — |
| D.3 | :API Gateway | :BiometricsService (WebClient) | ret | `D.3. Return 400` | D | ApiGateway/appsettings.json:61-66 |  | — |
| D.4 | :BiometricsService (WebClient) | :ProfileController (WebClient) | ret | `D.4. Return (false, null, envelope.Message)` | D | BiometricsService.cs:51-55 |  | — |
| D.5 | :ProfileController (WebClient) | :Biometric Wizard UI | ret | `D.5. Return 400 { error }` | D | ProfileController.cs:181-184 |  | — |
| D.6 | :Biometric Wizard UI | User | ret | `D.6. failFaceStep(error) - red oval, "Check failed", Try again` | D | biometric-wizard.js:403-406 |  | — |
| 32 | :ProfileBiometricsService | :AiServiceClient | call | `32. EmbedFaceAsync(file, ct)` | — | ProfileBiometricsService.cs:50-55 |  | — |
| 33 | :AiServiceClient | :ai-service | call | `33. POST /internal/biometrics/face/embed (multipart)` | — | AiServiceClient.cs:136-137, 142-153 |  | 33. POST /internal/biometrics/face/embed (multipart file, header X-Internal-Api-Key) |
| E.1 | :ai-service | :AiServiceClient | ret | `E.1. Return 422 {detail}` | E | router.py:52-55, face_engine.py:56, 63, 65, 83-90 |  | E.1. 422 { detail: "<reason>" } - cannot decode image / no face / multiple faces / low detection confidence / face too small / image too blurry |
| E.2 | :AiServiceClient | :ProfileBiometricsService | ret | `E.2. throw BiometricQualityException(detail)` | E | AiServiceClient.cs:154-158 | throw | — |
| E.3 | :ProfileBiometricsService | :ProfileBiometricsController | ret | `E.3. propagate BiometricQualityException` | E | ProfileBiometricsService.cs:52 | throw | — |
| E.4 | :ProfileBiometricsController | :API Gateway | ret | `E.4. Return 422 ApiResponse.Fail(ex.Message)` | E | ProfileBiometricsController.cs:58-62 |  | — |
| E.5 | :API Gateway | :BiometricsService (WebClient) | ret | `E.5. Return 422` | E | ApiGateway/appsettings.json:61-66 |  | — |
| E.6 | :BiometricsService (WebClient) | :ProfileController (WebClient) | ret | `E.6. Return (false, null, envelope.Message)` | E | BiometricsService.cs:51-55 |  | E.6. Return (false, null, envelope.Message) - the 422 body carries the ai-service reason |
| E.7 | :ProfileController (WebClient) | :Biometric Wizard UI | ret | `E.7. Return 400 { error }` | E | ProfileController.cs:181-184 |  | — |
| E.8 | :Biometric Wizard UI | User | ret | `E.8. failFaceStep(reason)` | E | biometric-wizard.js:403-406, 421-428 |  | E.8. failFaceStep(reason) - e.g. "No face detected - please try again" |
| 34 | :ai-service | :AiServiceClient | ret | `34. Return 200 FaceEmbedResponse` | — | router.py:56, face_engine.py:92-95, schemas.py:8-13 |  | 34. 200 FaceEmbedResponse{embedding (512-d, L2-normalized), dim, quality, det_score, model_version} |
| 35 | :AiServiceClient | :ProfileBiometricsService | ret | `35. Return BiometricEmbeddingResult` | — | AiServiceClient.cs:159-161 |  | 35. Return BiometricEmbeddingResult{Embedding, Dim, Quality, ModelVersion} |
| 36 | :ProfileBiometricsService | :ProfileBiometricsRepository | call | `36. GetAsync(userId, "Face", ct)` | — | ProfileBiometricsService.cs:58 |  | — |
| 37 | :ProfileBiometricsRepository | SQL Server | call | `37. SELECT ProfileBiometric WHERE UserId AND Modality="Face"` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| 38 | SQL Server | :ProfileBiometricsRepository | ret | `38. Return row / null` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| 39 | :ProfileBiometricsRepository | :ProfileBiometricsService | ret | `39. Return ProfileBiometric?` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| G-a.1 | :ProfileBiometricsService | :ProfileBiometricsRepository | call | `G-a.1. AddAsync(ProfileBiometric{Modality="Face"})` | G-a | ProfileBiometricsService.cs:59-72 |  | G-a.1. AddAsync(ProfileBiometric{UserId, Modality="Face", Embedding=EmbeddingCodec.ToBytes(embedding), Dim, ModelVersion, Quality, CreatedAt, UpdatedAt}) |
| G-a.2 | :ProfileBiometricsRepository | SQL Server | call | `G-a.2. INSERT ProfileBiometric (SaveChangesAsync)` | G-a | ProfileBiometricsRepository.cs:31-35 |  | — |
| G-a.3 | SQL Server | :ProfileBiometricsRepository | ret | `G-a.3. Return rows affected` | G-a | ProfileBiometricsRepository.cs:34 |  | — |
| G-a.4 | :ProfileBiometricsRepository | :ProfileBiometricsService | ret | `G-a.4. Return` | G-a | ProfileBiometricsRepository.cs:34 |  | — |
| G-b.1 | :ProfileBiometricsService | (self) | call | `G-b.1. EnsureReEnrollmentMatches(): Cosine vs threshold 0.35` | G-b | ProfileBiometricsService.cs:95-116 | self | G-b.1. EnsureReEnrollmentMatches(): score = BiometricSimilarity.Cosine(stored, candidate); threshold = Biometrics:FaceReEnrollmentMatchThreshold (default 0.35) |
| H.1 | :ProfileBiometricsService | :ProfileBiometricsController | ret | `H.1. throw InvalidOperationException` | H | ProfileBiometricsService.cs:100-104 | throw | H.1. throw InvalidOperationException("The enrolled face template uses an incompatible biometric model.") |
| H.2 | :ProfileBiometricsController | :API Gateway | ret | `H.2. Return 400 ApiResponse.Fail(ex.Message)` | H | ProfileBiometricsController.cs:67-70 |  | — |
| H.3 | :API Gateway | :BiometricsService (WebClient) | ret | `H.3. Return 400` | H | ApiGateway/appsettings.json:61-66 |  | — |
| H.4 | :BiometricsService (WebClient) | :ProfileController (WebClient) | ret | `H.4. Return (false, null, envelope.Message)` | H | BiometricsService.cs:51-55 |  | — |
| H.5 | :ProfileController (WebClient) | :Biometric Wizard UI | ret | `H.5. Return 400 { error }` | H | ProfileController.cs:181-184 |  | — |
| H.6 | :Biometric Wizard UI | User | ret | `H.6. failFaceStep(error)` | H | biometric-wizard.js:403-406 |  | — |
| J.1 | :ProfileBiometricsService | :ProfileBiometricsController | ret | `J.1. throw BiometricIdentityMismatchException("Face", ...)` | J | ProfileBiometricsService.cs:115-116 | throw | J.1. throw BiometricIdentityMismatchException("Face", score, threshold) |
| J.2 | :ProfileBiometricsController | :API Gateway | ret | `J.2. Return 409 ApiResponse.Fail(ex.Message)` | J | ProfileBiometricsController.cs:63-66 |  | — |
| J.3 | :API Gateway | :BiometricsService (WebClient) | ret | `J.3. Return 409` | J | ApiGateway/appsettings.json:61-66 |  | — |
| J.4 | :BiometricsService (WebClient) | :ProfileController (WebClient) | ret | `J.4. Return (false, null, envelope.Message)` | J | BiometricsService.cs:51-55 |  | — |
| J.5 | :ProfileController (WebClient) | :Biometric Wizard UI | ret | `J.5. Return 400 { error }` | J | ProfileController.cs:181-184 |  | — |
| J.6 | :Biometric Wizard UI | User | ret | `J.6. failFaceStep("The new face sample does not match...")` | J | biometric-wizard.js:403-406 |  | J.6. failFaceStep("The new face sample does not match the currently enrolled identity.") |
| G-b.2 | :ProfileBiometricsService | :ProfileBiometricsRepository | call | `G-b.2. SaveChangesAsync(ct)` | G-b | ProfileBiometricsService.cs:76-81 |  | G-b.2. SaveChangesAsync(ct) after copying Embedding / Dim / ModelVersion / Quality / UpdatedAt onto the tracked entity |
| G-b.3 | :ProfileBiometricsRepository | SQL Server | call | `G-b.3. UPDATE ProfileBiometrics SET Embedding, Quality, UpdatedAt` | G-b | ProfileBiometricsRepository.cs:37 |  | G-b.3. UPDATE ProfileBiometric SET Embedding, Dim, ModelVersion, Quality, UpdatedAt |
| G-b.4 | SQL Server | :ProfileBiometricsRepository | ret | `G-b.4. Return rows affected` | G-b | ProfileBiometricsRepository.cs:37 |  | — |
| G-b.5 | :ProfileBiometricsRepository | :ProfileBiometricsService | ret | `G-b.5. Return` | G-b | ProfileBiometricsRepository.cs:37 |  | — |
| 40 | :ProfileBiometricsService | :ProfileBiometricsRepository | call | `40. GetAsync(userId, "Voice", ct)` | — | ProfileBiometricsService.cs:84, 125-128 |  | 40. GetAsync(userId, "Voice", ct) - TryAwardEnrollmentBonusAsync: is the OTHER modality enrolled? |
| 41 | :ProfileBiometricsRepository | SQL Server | call | `41. SELECT ProfileBiometric WHERE UserId AND Modality="Voice"` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| 42 | SQL Server | :ProfileBiometricsRepository | ret | `42. Return row / null` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| 43 | :ProfileBiometricsRepository | :ProfileBiometricsService | ret | `43. Return ProfileBiometric?` | — | ProfileBiometricsRepository.cs:16-19 |  | — |
| K-a.1 | :ProfileBiometricsService | (self) | call | `K-a.1. bonusStatus = "NotYetEligible" - no PaymentAPI call` | K-a | ProfileBiometricsService.cs:129-130 | self | — |
| K-b.1 | :ProfileBiometricsService | :ProfileBiometricsRepository | call | `K-b.1. ListByModalityAsync("Face", ct)` | K-b | ProfileBiometricsService.cs:132, 170 |  | K-b.1. ListByModalityAsync("Face", ct) - IsCanonicalBonusAccountAsync |
| K-b.2 | :ProfileBiometricsRepository | SQL Server | call | `K-b.2. SELECT ProfileBiometrics WHERE Modality="Face"` | K-b | ProfileBiometricsRepository.cs:26-29 |  | K-b.2. SELECT ProfileBiometrics WHERE Modality="Face" (every account) |
| K-b.3 | SQL Server | :ProfileBiometricsRepository | ret | `K-b.3. Return rows` | K-b | ProfileBiometricsRepository.cs:26-29 |  | — |
| K-b.4 | :ProfileBiometricsRepository | :ProfileBiometricsService | ret | `K-b.4. Return List<ProfileBiometric>` | K-b | ProfileBiometricsRepository.cs:26-29 |  | — |
| K-b.5 | :ProfileBiometricsService | (self) | call | `K-b.5. canonicalAccount = oldest template with Cosine >= 0.5` | K-b | ProfileBiometricsService.cs:175-195 | self | K-b.5. canonicalAccount = oldest template with the same Dim + ModelVersion AND Cosine >= Biometrics:BonusDedupMatchThreshold (default 0.5) |
| L-a.1 | :ProfileBiometricsService | (self) | call | `L-a.1. bonusStatus = "IdentityAlreadyClaimed"` | L-a | ProfileBiometricsService.cs:132-138 | self | L-a.1. bonusStatus = "IdentityAlreadyClaimed" (logged) - no PaymentAPI call |
| L-b.1 | :ProfileBiometricsService | :PaymentApiClient | call | `L-b.1. AwardCreditAsync(userId, 100, userId, ...) [best-effort]` | L-b | ProfileBiometricsService.cs:144-146, CreditConfig.cs:14 | best-effort | L-b.1. AwardCreditAsync(userId, 100, referenceId = userId, "Bonus: enrolled both Face and Voice biometrics") |
| L-b.2 | :PaymentApiClient | :PaymentAPI (:7008) | call | `L-b.2. POST /api/internal/credit/award` | L-b | PaymentApiClient.cs:128-129, Program.cs:49, 80-87 |  | L-b.2. POST /api/internal/credit/award { UserId, Amount=100, ReferenceId, Description } - direct to PaymentAPI :7008, NOT through the Gateway |
| M-a.1 | :PaymentAPI (:7008) | :PaymentApiClient | ret | `M-a.1. Return 200 CreditAwardResponse{Awarded=true}` | M-a | PaymentApiClient.cs:143-146 |  | M-a.1. 200 CreditAwardResponse{Success=true, Awarded=true, BalanceAfter} |
| M-a.2 | :PaymentApiClient | :ProfileBiometricsService | ret | `M-a.2. Return CreditAwardResponse -> bonusStatus = "Awarded"` | M-a | ProfileBiometricsService.cs:156-159 |  | — |
| M-b.1 | :PaymentAPI (:7008) | :PaymentApiClient | ret | `M-b.1. 200 CreditAwardResponse{Success=true, Awarded=false}` | M-b | PaymentApiClient.cs:143-146 |  | M-b.1. 200 CreditAwardResponse{Success=true, Awarded=false} - idempotent no-op, the account already had the bonus |
| M-b.2 | :PaymentApiClient | :ProfileBiometricsService | ret | `M-b.2. Return CreditAwardResponse -> bonusStatus = "AlreadyAwarded"` | M-b | ProfileBiometricsService.cs:159 |  | — |
| M-c.1 | :PaymentAPI (:7008) | :PaymentApiClient | ret | `M-c.1. Non-2xx / no response` | M-c | PaymentApiClient.cs:131-141 |  | — |
| M-c.2 | :PaymentApiClient | :ProfileBiometricsService | ret | `M-c.2. Return CreditAwardResponse{Success=false} [best-effort]` | M-c | PaymentApiClient.cs:153-161, ProfileBiometricsService.cs:148-165 | best-effort | M-c.2. Return CreditAwardResponse{Success=false, Message} - exception swallowed and logged -> bonusStatus = "NotAwarded"; enrollment still succeeds |
| 44 | :ProfileBiometricsService | :ProfileBiometricsController | ret | `44. Return BiometricEnrollResponse{Modality="Face"}` | — | ProfileBiometricsService.cs:86-92 |  | 44. Return BiometricEnrollResponse{Modality="Face", Dim, Quality, BonusStatus} |
| 45 | :ProfileBiometricsController | :API Gateway | ret | `45. Return 200 ApiResponse.Ok(result, "Enrolled.")` | — | ProfileBiometricsController.cs:56 |  | — |
| 46 | :API Gateway | :BiometricsService (WebClient) | ret | `46. Return 200` | — | ApiGateway/appsettings.json:61-66 |  | — |
| 47 | :BiometricsService (WebClient) | :ProfileController (WebClient) | ret | `47. Return (true, BiometricEnrollModel, null)` | — | BiometricsService.cs:57 |  | — |
| 48 | :ProfileController (WebClient) | :Biometric Wizard UI | ret | `48. Return 200 { enrolled: true, bonusStatus }` | — | ProfileController.cs:186 |  | — |
| 49 | :Biometric Wizard UI | User | ret | `49. mergeBonusStatus(); facePassed = true [see #2]` | — | biometric-wizard.js:400, 409-418 | boundary | 49. mergeBonusStatus(); facePassed = true; oval -> success, "Face captured ✓"; after 1.1s the wizard auto-advances to the Voice step (#2 EnrollVoiceBiometric) |

**Cờ:** `self` · `throw` · `best-effort` · `boundary`

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| A | alt | A-a: `[status read OK]` · A-b: `[GetStatusAsync throws - caught by the controller and returned as 400]` | A-a.1 → A-b.6 | — | — |
| B | opt | `[getUserMedia for the camera throws]` | B.1 → B.2 | — | — |
| C | opt | `[face is null OR face.Length == 0]` | C.1 → C.2 | — | — |
| D | opt | `[file is null OR file.Length == 0]` | D.1 → D.6 | — | — |
| E | opt | `[ai-service returns 422 - quality rejection]` | E.1 → E.8 | — | — |
| G | alt | G-a: `[first enrollment - no Face template yet]` · G-b: `[re-enrollment - a Face template already exists]` | G-a.1 → G-b.5 | — | — |
| H | opt | `[existing.Dim != candidate.Dim OR existing.ModelVersion != candidate.ModelVersion]` | H.1 → H.6 | G-b | — |
| J | opt | `[score < threshold]` | J.1 → J.6 | G-b | — |
| K | alt | K-a: `[Voice is not enrolled yet]` · K-b: `[both Face and Voice are now enrolled]` | K-a.1 → M-c.2 | — | — |
| L | alt | L-a: `[canonicalAccount is null OR belongs to an earlier account]` · L-b: `[this account owns the face identity]` | L-a.1 → M-c.2 | K-b | — |
| M | alt | M-a: `[200, Awarded == true]` · M-b: `[200, Awarded == false]` · M-c: `[non-2xx, unreachable, or the client throws]` | M-a.1 → M-c.2 | L-b | — |

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | L-b.2 | `PaymentApiClient talks to PaymentAPI :7008 DIRECTLY - its HttpClient BaseAddress is PAYMENT_API_BASEURL (InterviewAPI/Program.cs:49, 80-87), not the Gateway. That is why no ApiGateway hop is drawn for this message.` |
| N2 | 32-35 | `The embedding never reaches the browser: ai-service returns it to InterviewAPI, which stores it as VARBINARY (EmbeddingCodec, little-endian float32). BiometricEnrollResponse deliberately carries only Modality / Dim / Quality / BonusStatus.` |
| N3 | G-b.1 | `First-time enrollment does NOT run an identity check - there is nothing to compare against yet. The 0.35 cosine gate only exists to stop someone REPLACING an existing template with a different person's face. This is by design, not a missing guard.` |
| N4 | K-b.5 | `The bonus dedup threshold (0.5) is deliberately stricter than the re-enrollment threshold (0.35): re-enrollment tolerates the same person across lighting/pose, while this decides whether two DIFFERENT accounts are the same real person. Provisional pending Milestone 3 calibration.` |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | File:Line | Liệt kê | Nguồn |
|---|---|---|---|---|---|
| 1 | ProfileController (WebClient) | class | WebClient/Controllers/Profile/ProfileController.cs:133-187 | SUBSET | local |
| 2 | Controller | framework-base | ASP.NET Core MVC | FULL (rỗng) | SHARED |
| 3 | IBiometricsService (WebClient) | interface | WebClient/Services/Profile/BiometricsService.cs:10-19 | FULL | local |
| 4 | BiometricsService (WebClient) | class | WebClient/Services/Profile/BiometricsService.cs:21-72 | FULL | local |
| 5 | IApiClient | interface | WebClient/Services/Shared/IApiClient.cs:3-6 | FULL | SHARED |
| 6 | ApiClient | class | WebClient/Services/Shared/ApiClient.cs:3-6 | FULL | SHARED |
| 7 | BiometricStatusModel | DTO | WebClient/Models/Profile/BiometricsViewModel.cs:6-12 | FULL | local |
| 8 | BiometricEnrollModel | DTO | WebClient/Models/Profile/BiometricsViewModel.cs:15-24 | FULL | local |
| 9 | BiometricsViewModel | DTO | WebClient/Models/Profile/BiometricsViewModel.cs:27-35 | FULL | local |
| 10 | ApiResponse<T> (WebClient) | DTO | WebClient/Models/Shared/ApiResponse.cs:5-12 | FULL | local |
| 11 | ControllerBase | framework-base | ASP.NET Core | FULL (rỗng) | SHARED |
| 12 | ProfileBiometricsController (InterviewAPI) | class | InterviewAPI/Controllers/ProfileBiometricsController.cs:15-72 | FULL | local |
| 13 | IProfileBiometricsService | interface | InterviewAPI/Services/Interfaces/IProfileBiometricsService.cs | FULL | local |
| 14 | ProfileBiometricsService (InterviewAPI) | class | InterviewAPI/Services/ProfileBiometricsService.cs:11-211 | FULL | local |
| 15 | ApiResponse<T> (InterviewAPI) | DTO | InterviewAPI/DTOs/ApiResponse.cs | FULL | SHARED |
| 16 | BiometricEnrollResponse | DTO | InterviewAPI/DTOs/Biometrics/BiometricEnrollResponse.cs:4-19 | FULL | local |
| 17 | BiometricStatusResponse | DTO | InterviewAPI/DTOs/Biometrics/BiometricStatusResponse.cs:4-10 | FULL | local |
| 18 | BiometricEmbeddingResult | DTO (record) | InterviewAPI/DTOs/AiService/BiometricEmbeddingResult.cs:7-11 | FULL | local |
| 19 | BiometricQualityException | exception | InterviewAPI/Clients/BiometricQualityException.cs | FULL | SHARED |
| 20 | BiometricIdentityMismatchException | exception | InterviewAPI/Clients/BiometricIdentityMismatchException.cs:3-16 | FULL | SHARED (**tạo mới**) |
| 21 | EmbeddingCodec | helper | InterviewAPI/Helpers/EmbeddingCodec.cs:7-22 | FULL | local |
| 22 | BiometricSimilarity | helper | InterviewAPI/Helpers/BiometricSimilarity.cs:3-25 | FULL | local |
| 23 | CreditConfig | helper | InterviewAPI/Helpers/CreditConfig.cs:7-30 | FULL | local |
| 24 | IAiServiceClient | interface | InterviewAPI/Clients/IAiServiceClient.cs | FULL | local (KHÔNG include `AiServiceClient.yaml`) |
| 25 | AiServiceClient (InterviewAPI) | class | InterviewAPI/Clients/AiServiceClient.cs | FULL | SHARED |
| 26 | IPaymentApiClient | interface | InterviewAPI/Clients/IPaymentApiClient.cs:3-20 | FULL | local |
| 27 | PaymentApiClient (InterviewAPI) | class | InterviewAPI/Clients/PaymentApiClient.cs:6-188 | FULL | local |
| 28 | CreditAwardResponse | DTO | InterviewAPI/Clients/IPaymentApiClient.cs:30-41 | FULL | local |
| 29 | IProfileBiometricsRepository | interface | InterviewAPI/Repositories/Interfaces/IProfileBiometricsRepository.cs | FULL | local |
| 30 | ProfileBiometricsRepository (InterviewAPI) | class | InterviewAPI/Repositories/ProfileBiometricsRepository.cs:7-38 | FULL | local |
| 31 | ProfileBiometric | entity | InterviewAPI/Models/ProfileBiometric.cs | FULL | local |
| 32 | InterviewDbContext | class | InterviewAPI/Models/InterviewDbContext.cs + .Biometrics.cs | FULL | SHARED |
| 33 | DbContext | framework-base | EF Core | FULL (rỗng) | SHARED |

## 8. CLASS — chi tiết

### `ProfileController (WebClient)` — `SUBSET`
- Field: `- biometricsService: IBiometricsService`
- Method: `+ Identity(cancellationToken): Task<IActionResult>`, `+ EnrollFace(face: IFormFile, cancellationToken): Task<IActionResult>`
- *(SUBSET)* Đã bỏ các field DI khác (`userService`, `interviewService`, `paymentService`…) và ~15 action ngoài chức năng này (`EnrollVoice` được vẽ ở `#2`).

### `ProfileBiometricsService (InterviewAPI)` — `FULL`
- 5 field DI + 5 method (`EnrollAsync`, `EnsureReEnrollmentMatches`, `TryAwardEnrollmentBonusAsync`,
  `IsCanonicalBonusAccountAsync`, `GetStatusAsync`) + 5 hằng `BonusStatus*` + 2 hằng `Modality*`.

**Class SHARED lần đầu xuất hiện trong toàn dự án** (đã tạo file):
`BiometricIdentityMismatchException.yaml` — exception FULL (3 property + 1 constructor).

## 9. QUAN HỆ CLASS

| Loại | From | To | Label |
|---|---|---|---|
| extends | ProfileController (WebClient) | Controller | — |
| use | ProfileController (WebClient) | IBiometricsService (WebClient) | Use |
| use | ProfileController (WebClient) | BiometricsViewModel | Use |
| implements | BiometricsService (WebClient) | IBiometricsService (WebClient) | — |
| use | BiometricsService (WebClient) | IApiClient | Use |
| implements | ApiClient | IApiClient | — |
| use | BiometricsService (WebClient) | BiometricStatusModel | Use |
| use | BiometricsService (WebClient) | BiometricEnrollModel | Use |
| use | BiometricsService (WebClient) | ApiResponse<T> (WebClient) | Use |
| extends | ProfileBiometricsController (InterviewAPI) | ControllerBase | — |
| use | ProfileBiometricsController (InterviewAPI) | IProfileBiometricsService | Use |
| use | ProfileBiometricsController (InterviewAPI) | ApiResponse<T> (InterviewAPI) | Use |
| use | ProfileBiometricsController (InterviewAPI) | BiometricQualityException | catch |
| use | ProfileBiometricsController (InterviewAPI) | BiometricIdentityMismatchException | catch |
| implements | ProfileBiometricsService (InterviewAPI) | IProfileBiometricsService | — |
| use | ProfileBiometricsService (InterviewAPI) | IProfileBiometricsRepository | Use |
| use | ProfileBiometricsService (InterviewAPI) | IAiServiceClient | Use |
| use | ProfileBiometricsService (InterviewAPI) | IPaymentApiClient | Use |
| use | ProfileBiometricsService (InterviewAPI) | BiometricEnrollResponse | Use |
| use | ProfileBiometricsService (InterviewAPI) | BiometricStatusResponse | Use |
| use | ProfileBiometricsService (InterviewAPI) | EmbeddingCodec | Use |
| use | ProfileBiometricsService (InterviewAPI) | BiometricSimilarity | Use |
| use | ProfileBiometricsService (InterviewAPI) | CreditConfig | Use |
| use | ProfileBiometricsService (InterviewAPI) | BiometricIdentityMismatchException | throw |
| implements | AiServiceClient (InterviewAPI) | IAiServiceClient | — |
| use | AiServiceClient (InterviewAPI) | BiometricEmbeddingResult | Use |
| use | AiServiceClient (InterviewAPI) | BiometricQualityException | throw |
| implements | PaymentApiClient (InterviewAPI) | IPaymentApiClient | — |
| use | PaymentApiClient (InterviewAPI) | CreditAwardResponse | Use |
| implements | ProfileBiometricsRepository (InterviewAPI) | IProfileBiometricsRepository | — |
| composition | ProfileBiometricsRepository (InterviewAPI) | InterviewDbContext | — |
| extends | InterviewDbContext | DbContext | — |
| composition | InterviewDbContext | ProfileBiometric | `1..*` |

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| — | — | Không phát hiện anomaly mới ở nhánh Face | — | — | — |

> Đã kiểm 2 điểm nghi ngờ và **bác cả hai**:
> (a) enroll lần đầu không kiểm tra danh tính — đúng chủ đích (không có gì để so sánh), xem note N3;
> (b) `TryAwardEnrollmentBonusAsync` được gọi sau **mọi** lần enroll kể cả enroll lại — cố ý, vì
> PaymentAPI tự idempotent theo `ReferenceId = userId`
> ([ProfileBiometricsService.cs:119-124](../../../source/InterviewAPI/Services/ProfileBiometricsService.cs#L119-L124),
> [IPaymentApiClient.cs:11-14](../../../source/InterviewAPI/Clients/IPaymentApiClient.cs#L11-L14)).
>
> Anomaly comment `BiometricEmbeddingResult.cs:6` ("Quality is face-only; null for voice") thuộc
> nhánh **Voice** → ghi ở `#2 EnrollVoiceBiometric` §10.

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | BL#12 (GET trạng thái enroll lúc load trang) — vẽ trong #1, #2, hay cả hai? | **Vẽ ĐẦY ĐỦ ở #1 (pha 0); ở #2 chỉ vẽ 1 message boundary trỏ về file này** | Là 1 request duy nhất dùng chung; vẽ 2 lần sẽ lặp 23 message. RECON cảnh báo không được gộp GET này vào cùng request POST enroll — đã tách thành pha riêng. |
| 2 | `GetDashboardAsync()` trong action `Identity` | **Không vẽ (§1)** | Nạp dữ liệu cho khu "Test verification" tạm thời của Milestone 2 (code ghi rõ sẽ xoá ở Milestone 4), bọc try/catch nuốt lỗi, không thuộc luồng enrollment. |
| 3 | Nhánh lỗi hạ tầng ai-service (unreachable / timeout / non-2xx khác 422) | **Không vẽ (§1)** | Theo tiền lệ lô A: `specs/verifyfaceinsession.yaml` chỉ vẽ 200 và 422, không vẽ nhánh hạ tầng. Vẽ ở mọi file sẽ lặp 8 message × 8 chức năng có gọi ai-service. |
| 4 | 4 nhánh lỗi enroll (D 400, E 422, H 400, J 409) | **Tách riêng, expand đủ** | H và D cùng 400 nhưng khác chữ user thấy; E/J khác cả status. Không đủ điều kiện gộp. |
| 5 | Guard `access_token` cookie | **Không vẽ (§1)** | Guard đăng nhập dùng chung, thuộc `Login`. |
| 6 | Gán `bonusStatus` sau khi PaymentAPI trả về | **Gộp vào nhãn message return (`M-a.2`, `M-b.2`, `M-c.2`)** thay vì self-message riêng | Tiền lệ lô B (gộp gán state vào nhãn call/ret). Tiết kiệm 180px, giữ nguyên thông tin. |
| 7 | Trang Class ban đầu build FAIL (`cls.RoutingError`: hết hành lang dọc cho các edge ra từ `ProfileBiometricsService`) | **Tách 33 class thành 8 cột thay vì 7** (helper `EmbeddingCodec`/`BiometricSimilarity`/`CreditConfig` + 2 exception tách sang cột riêng ngay cạnh service; DTO/client/repository/entity mỗi nhóm 1 cột) **và chuyển 2 edge `use → BiometricEnrollResponse` / `BiometricStatusResponse` từ class hiện thực sang interface `IProfileBiometricsService`** (nơi 2 kiểu này thật sự xuất hiện trong chữ ký) | Mỗi khe giữa 2 cột chỉ chứa được ~5 đường dọc (COL_GAP=100, LANE=18); `ProfileBiometricsService` có 9 edge đi ra nên vượt hạn mức. **KHÔNG sửa generator** — chỉ đổi bố cục trong file spec, đúng ràng buộc "chỉ sửa generator khi không còn cách khác". |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `4`)** PaymentApiClient talks to PaymentAPI :7008 DIRECTLY - its HttpClient BaseAddress is PAYMENT_API_BASEURL (InterviewAPI/Program.cs:49, 80-87), not the Gateway. That is why no ApiGateway hop is drawn for message L-b.2. *(gỡ từ note trên diagram)*
- **(cạnh message `7`)** The embedding never reaches the browser: ai-service returns it to InterviewAPI, which stores it as VARBINARY (EmbeddingCodec, little-endian float32). BiometricEnrollResponse deliberately carries only Modality / Dim / Quality / BonusStatus. *(gỡ từ note trên diagram)*
- **(cạnh message `11`)** First-time enrollment does NOT run an identity check - there is nothing to compare against yet. The 0.35 cosine gate only exists to stop someone REPLACING an existing template with a different person's face. This is by design, not a missing guard. *(gỡ từ note trên diagram)*
- **(cạnh message `A-a.3`)** The bonus dedup threshold (0.5) is deliberately stricter than the re-enrollment threshold (0.35): re-enrollment tolerates the same person across lighting/pose, while this decides whether two DIFFERENT accounts are the same real person. Provisional pending Milestone 3 calibration. *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| `uc_id` của chức năng | RECON không ghi UC-id cho #1; bảng use-case của nhóm chưa đối chiếu | Nhóm xác nhận ê-líp use-case rồi điền `uc_id` vào `specs/enrollfacebiometric.yaml` | luồng 1 |
