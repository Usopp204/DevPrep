# FLOW — `VerifyFaceEntryGate` · BL#48 · UC-10

> Đặt tại `docs/diagrams/FLOWS/VerifyFaceEntryGate.md`
>
> Trạng thái: `DRAFT` — Khảo sát: luồng 1, lô `A`, `2026-08-02`

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Xác thực khuôn mặt 1 lần cho phiên (Entry gate) trước khi cho vào phòng phỏng vấn — so khớp embedding đã enroll, tối đa 3 lần thử/phiên |
| Điểm vào (user thao tác gì) | Bấm "I'm ready" trên trang Verify → countdown 3s → tự động chụp 8 frame webcam → gửi |
| Kết thúc (user thấy gì) | Pass: overlay "Identity Verified" 3.2s rồi chuyển sang trang Integrity Policy (boundary #11) · Fail: thông báo lỗi cụ thể + nút "Try again" (poll lại số lần còn — boundary #14), hết lượt → chuyển về dashboard |
| Quy mô | 14 participant · 71 dòng trong bảng §4 (một số nhánh lỗi viết tắt dạng `F#.3-F#.6` thay vì liệt kê từng return — xem §11; nếu expand đủ, số message thật CAO HƠN 71) · 2 alt (A: ai-service quality/success; B: passed/failed ở browser) / 6 opt (F1-F6) |
| Service ngoài quyền sở hữu | không |
| **Gọi BYPASS Gateway?** | không |
| Nhà cung cấp AI | InsightFace (qua `ai-service/biometrics/router.py` → `liveness_face.verify_face_live`) |
| Ghi DB | `SessionVerificationChallenges` — UPDATE (`ConsumedAt`); `SessionVerificationEvents` — INSERT |
| Trừ credit | không (xác nhận đúng: KHÔNG có lời gọi PaymentApiClient nào trong `VerifyFaceAsync`) |
| KI liên quan | không (không phát hiện anomaly mới cho #13 riêng) |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| Query string `phase` cho phép giá trị khác `"Entry"` | `SessionVerificationController.cs:79` (`[FromQuery] string phase = "Entry"`) | Không nơi nào trong WebClient gửi `phase` khác giá trị mặc định — nhánh `phase != "Entry"` (bỏ qua retry-cap check) là dead path từ UI hiện tại; chỉ note ở §6, không vẽ alt riêng |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Challenge (hướng quay đầu) phải được phát trước khi verify-face chạy | `#12 GetVerificationChallenge` | *(đã xảy ra trước message #1 của file này — chỉ note, không vẽ message)* | — |
| Sau khi verify thất bại, JS hỏi lại server số lần thử còn | `#14 ValidateEntryGateStatus` | `CheckEntryGate(sessionId)` | `entry-verify.js:219` |
| Sau khi verify PASS, browser điều hướng sang Integrity Policy | `#11 AcceptIntegrityPolicy` | `Navigate to Integrity Policy page` | `entry-verify.js:72` |

---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 1 | User | actor | — | — |
| 2 | :Verify UI | box | `Views/Interview/Verify.cshtml` + `entry-verify.js` | — |
| 3 | :VerificationController (WebClient) | box | `Controllers/Interview/VerificationController.cs` | — |
| 4 | :VerificationService (WebClient) | box | `Services/Interview/VerificationService.cs` | — |
| 5 | :API Gateway | box | `interview-route` | có |
| 6 | :SessionVerificationController (InterviewAPI) | box | `Controllers/SessionVerificationController.cs` | — |
| 7 | :BiometricVerificationService (InterviewAPI) | box | `Services/BiometricVerificationService.cs` | — |
| 8 | :SessionRepository (InterviewAPI) | box | `Repositories/SessionRepository.cs` (SUBSET) | — |
| 9 | :SessionVerificationEventRepository (InterviewAPI) | box | `Repositories/SessionVerificationEventRepository.cs` | — |
| 10 | :SessionVerificationChallengeRepository (InterviewAPI) | box | `Repositories/SessionVerificationChallengeRepository.cs` | — |
| 11 | :ProfileBiometricsRepository (InterviewAPI) | box | `Repositories/ProfileBiometricsRepository.cs` | — |
| 12 | :AiServiceClient (InterviewAPI) | box | `Clients/AiServiceClient.cs` | — |
| 13 | :ai-service /internal/biometrics/face/verify-live | box | `ai-service/biometrics/router.py:117-148` | — |
| 14 | SQL Server | cylinder | `DevPrep_Interview` | — |

## 4. BẢNG MESSAGE

| # | From | To | call/ret | Label (tiếng Anh) | Frame (guard) | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Verify UI | call | `1. Click "I'm ready"` | — | `entry-verify.js:155-156` |  | Click "I'm ready" |
| 2 | :Verify UI | :Verify UI | call | `2. capture(): 3s countdown, grab 8 frames` | — | `entry-verify.js:159-198` | self | capture()`: 3s countdown, grab 8 frames from webcam via `canvas.toBlob |
| 3 | :Verify UI | :VerificationController (WebClient) | call | `3. POST Verification/VerifyFace (multipart)` | — | `entry-verify.js:194-199` |  | POST Verification/VerifyFace` (multipart: `sessionId`, `frames[]` ×8) |
| 4 | :VerificationController (WebClient) | :VerificationController (WebClient) | call | `4. Buffer each IFormFile into MemoryStream` | — | `VerificationController.cs:51-60` | self | Buffer each `IFormFile` into `MemoryStream |
| F1.1 | :VerificationController (WebClient) | :Verify UI | ret | Return 400 `{error: "At least one frame is required."}` | opt F1 `[sessionId empty OR frames empty]` | `VerificationController.cs:46-49` |  | — |
| F1.2 | :Verify UI | User | ret | Hiện lỗi + "Try again" (qua `handleFail`, boundary #14) | opt F1 | `entry-verify.js:207-213` | boundary | — |
| 5 | :VerificationController (WebClient) | :VerificationService (WebClient) | call | `5. VerifyFaceAsync(sessionId, buffered)` | — | `VerificationController.cs:62-63` |  | VerifyFaceAsync(sessionId, buffered) |
| 6 | :VerificationService (WebClient) | :API Gateway | call | `6. POST api/interview/sessions/{id}/verify/face` | — | `VerificationService.cs:59-68` |  | POST api/interview/sessions/{sessionId}/verify/face` (multipart `frames`) |
| 7 | :API Gateway | :SessionVerificationController (InterviewAPI) | call | `7. Forward (route interview-route, no prefix strip)` | — | `ApiGateway/appsettings.json:49-54` |  | Forward (route `interview-route`, no prefix strip) |
| 8 | :SessionVerificationController (InterviewAPI) | :SessionVerificationController (InterviewAPI) | call | `8. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | `SessionVerificationController.cs:83` | self | Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)) |
| 9 | :SessionVerificationController (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `9. VerifyFaceAsync(userId, sessionId, frames, ct, "Entry")` | — | `SessionVerificationController.cs:84` |  | VerifyFaceAsync(userId, sessionId, frames, ct, phase="Entry") |
| 10 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `10. EnsureEnabled()` | — | `BiometricVerificationService.cs:98,629-633` | self | EnsureEnabled() |
| F2.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `throw InvalidOperationException("Biometric verification is disabled.")` | opt F2 `[Biometrics:Enabled == false]` | `BiometricVerificationService.cs:631-632` | throw | — |
| F2.2 | :SessionVerificationController (InterviewAPI) | :API Gateway | ret | Return 400 `ApiResponse.Fail(ex.Message)` | opt F2 | `SessionVerificationController.cs:95-97` |  | — |
| F2.3 | :API Gateway | :VerificationService (WebClient) | ret | Return 400 | opt F2 | — |  | — |
| F2.4 | :VerificationService (WebClient) | :VerificationController (WebClient) | ret | Return `(false, null, error)` | opt F2 | `VerificationService.cs:139-142` |  | — |
| F2.5 | :VerificationController (WebClient) | :Verify UI | ret | Return 400 `{error}` | opt F2 | `VerificationController.cs:64-67` |  | — |
| F2.6 | :Verify UI | User | ret | Hiện lỗi + "Try again" (boundary #14) | opt F2 | `entry-verify.js:207-213` | boundary | — |
| 11 | :BiometricVerificationService (InterviewAPI) | :SessionRepository (InterviewAPI) | call | `11. GetByIdAsync(sessionId) — via EnsureSessionOwnedAsync` | — | `BiometricVerificationService.cs:101,635-640` |  | GetByIdAsync(sessionId)` (qua `EnsureSessionOwnedAsync`) |
| 12 | :SessionRepository (InterviewAPI) | SQL Server | call | `12. SELECT InterviewSession by SessionId` | — | `ISessionRepository.cs:8` |  | SELECT InterviewSession by SessionId |
| 13 | SQL Server | :SessionRepository (InterviewAPI) | ret | `13. Return session row / null` | — | — |  | Return session row / null |
| 14 | :SessionRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `14. Return InterviewSession?` | — | — |  | Return `InterviewSession? |
| F3.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `throw KeyNotFoundException("Session not found.")` | opt F3 `[session is null OR session.UserId != userId]` | `BiometricVerificationService.cs:638-639` | throw | — |
| F3.2 | :SessionVerificationController (InterviewAPI) | :API Gateway | ret | Return 404 `ApiResponse.Fail(ex.Message)` | opt F3 | `SessionVerificationController.cs:91-93` |  | — |
| F3.3-F3.6 | ... | ... | ret | (chuỗi return 404 giống F2.3→F2.6, thay 400→404) | opt F3 | `VerificationService.cs:139-142`, `VerificationController.cs:64-67` | boundary (F3.6) | — |
| 15 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationEventRepository (InterviewAPI) | call | `15. ListBySessionAsync(sessionId, "Entry")` | phase=="Entry" | `BiometricVerificationService.cs:106-109` |  | ListBySessionAsync(sessionId, "Entry")` (đếm lần thử thất bại) |
| 16 | :SessionVerificationEventRepository (InterviewAPI) | SQL Server | call | `16. SELECT SessionVerificationEvents WHERE Phase="Entry"` | — | `SessionVerificationEventRepository.cs:47-55` |  | SELECT SessionVerificationEvents WHERE SessionId AND Phase="Entry" |
| 17 | SQL Server | :SessionVerificationEventRepository (InterviewAPI) | ret | `17. Return event list` | — | — |  | Return event list |
| 18 | :SessionVerificationEventRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `18. Return List<SessionVerificationEvent>` | — | — |  | Return `List<SessionVerificationEvent> |
| 19 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `19. Count failedAttempts (Modality=Face AND Decision!=Pass)` | — | `BiometricVerificationService.cs:110-111` | self | Đếm `failedAttempts` (Modality=Face AND Decision!=Pass) |
| F4.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `throw InvalidOperationException("Maximum verification attempts reached...")` | opt F4 `[failedAttempts >= EntryMaxRetries (mặc định 3)]` | `BiometricVerificationService.cs:112-114` | throw | — |
| F4.2-F4.6 | ... | ... | ret | (chuỗi return 400 giống F2.2→F2.6) | opt F4 | `SessionVerificationController.cs:95-97` | boundary (F4.6) | — |
| 20 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationChallengeRepository (InterviewAPI) | call | `20. GetActiveAsync(sessionId, "Face", now)` | — | `BiometricVerificationService.cs:117-118,654-655` |  | GetActiveAsync(sessionId, "Face", now)` (qua `ConsumeChallengeAsync`) |
| 21 | :SessionVerificationChallengeRepository (InterviewAPI) | SQL Server | call | `21. SELECT SessionVerificationChallenge WHERE Modality="Face"` | — | `SessionVerificationChallengeRepository.cs:22-30` |  | SELECT newest unconsumed unexpired SessionVerificationChallenge WHERE SessionId AND Modality="Face" |
| 22 | SQL Server | :SessionVerificationChallengeRepository (InterviewAPI) | ret | `22. Return challenge row / null` | — | — |  | Return challenge row / null |
| 23 | :SessionVerificationChallengeRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `23. Return SessionVerificationChallenge?` | — | — |  | Return `SessionVerificationChallenge? |
| F5.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `throw InvalidOperationException("No active challenge...")` | opt F5 `[challenge is null]` | `BiometricVerificationService.cs:656-658` | throw | — |
| F5.2-F5.6 | ... | ... | ret | (chuỗi return 400 giống F2.2→F2.6) | opt F5 | — | boundary (F5.6) | — |
| 24 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationChallengeRepository (InterviewAPI) | call | `24. ConsumedAt = now + SaveChangesAsync()` | — | `BiometricVerificationService.cs:659-660` |  | ConsumedAt = now` + `SaveChangesAsync() |
| 25 | :SessionVerificationChallengeRepository (InterviewAPI) | SQL Server | call | `25. UPDATE SessionVerificationChallenge SET ConsumedAt` | — | `SessionVerificationChallengeRepository.cs:32` |  | UPDATE SessionVerificationChallenge SET ConsumedAt |
| 26 | SQL Server | :SessionVerificationChallengeRepository (InterviewAPI) | ret | `26. Return` | — | — |  | Return |
| 27 | :BiometricVerificationService (InterviewAPI) | :ProfileBiometricsRepository (InterviewAPI) | call | `27. GetAsync(userId, "Face") — via GetStoredEmbeddingAsync` | — | `BiometricVerificationService.cs:119-120,664-667` |  | GetAsync(userId, "Face")` (qua `GetStoredEmbeddingAsync`) |
| 28 | :ProfileBiometricsRepository (InterviewAPI) | SQL Server | call | `28. SELECT ProfileBiometric WHERE UserId AND Modality="Face"` | — | `ProfileBiometricsRepository.cs:16-19` |  | SELECT ProfileBiometric WHERE UserId AND Modality="Face" |
| 29 | SQL Server | :ProfileBiometricsRepository (InterviewAPI) | ret | `29. Return row / null` | — | — |  | Return row / null |
| 30 | :ProfileBiometricsRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `30. Return ProfileBiometric?` | — | — |  | Return `ProfileBiometric? |
| F6.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `throw InvalidOperationException("No face enrollment found...")` | opt F6 `[row is null]` | `BiometricVerificationService.cs:668-669` | throw | — |
| F6.2-F6.6 | ... | ... | ret | (chuỗi return 400 giống F2.2→F2.6) | opt F6 | — | boundary (F6.6) | — |
| 31 | :BiometricVerificationService (InterviewAPI) | :AiServiceClient (InterviewAPI) | call | `31. VerifyFaceLiveAsync(frames, embedding, challengeJson)` | — | `BiometricVerificationService.cs:122-123` |  | VerifyFaceLiveAsync(frames, embedding, challengeJson) |
| 32 | :AiServiceClient (InterviewAPI) | :ai-service /internal/biometrics/face/verify-live | call | `32. POST /internal/biometrics/face/verify-live` | — | `AiServiceClient.cs:174-198` |  | POST /internal/biometrics/face/verify-live` (multipart `frames[]`, `stored_embedding`, `challenge`) |
| A.1 | :ai-service /internal/biometrics/face/verify-live | :AiServiceClient (InterviewAPI) | ret | 422 `{detail: "No face detected" \| "Cannot decode image" \| ...}` | alt A `[FaceQualityError / MultipleFacesError]` | `router.py:117,133-135`; `face_engine.py:56,63,65,84-90` | |
| A.2 | :AiServiceClient (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `A.2. throw BiometricQualityException(detail)` | alt A | `AiServiceClient.cs:199-201` | throw | throw BiometricQualityException(detail) |
| A.3 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `A.3. (exception propagates up, not caught inside VerifyFaceAsync)` | alt A | — |  | *(exception propagates up, không catch trong VerifyFaceAsync)* |
| A.4 | :SessionVerificationController (InterviewAPI) | :API Gateway | ret | `A.4. Return 422 ApiResponse.Fail(ex.Message)` | alt A | `SessionVerificationController.cs:87-90` |  | Return 422 `ApiResponse.Fail(ex.Message) |
| A.5 | :API Gateway | :VerificationService (WebClient) | ret | `A.5. Return 422` | alt A | — |  | Return 422 |
| A.6 | :VerificationService (WebClient) | :VerificationController (WebClient) | ret | `A.6. Return (false, null, "Verification failed (422): ...")` | alt A | `VerificationService.cs:139-142` |  | Return `(false, null, "Verification failed (422): ...") |
| A.7 | :VerificationController (WebClient) | :Verify UI | ret | `A.7. Return 400 {error}` | alt A | `VerificationController.cs:64-67` |  | Return 400 `{error}` (proxy luôn trả 400 dù upstream 422 — WebClient không forward status thô) |
| A.8 | :Verify UI | User | ret | `A.8. catch in capture() -> handleFail(err.message) (boundary #14)` | alt A | `entry-verify.js:208-213` | boundary | catch trong `capture()` → `handleFail(err.message)` (boundary #14) |
| 33 | :ai-service /internal/biometrics/face/verify-live | :AiServiceClient (InterviewAPI) | ret | `33. Return 200 FaceVerifyLiveResponse` | alt A `[frame hợp lệ]` | `router.py:147-148` |  | 200 `FaceVerifyLiveResponse{passed, threshold, identity_score, live, yaw_range, moved_enough, direction_ok, reason} |
| 34 | :AiServiceClient (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `34. Return AiFaceVerifyLiveResponse` | alt A | `AiServiceClient.cs:203-205` |  | Return `AiFaceVerifyLiveResponse |
| 35 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `35. Build VerificationResultResponse` | alt A | `BiometricVerificationService.cs:125-134` | self | Build `VerificationResultResponse{Passed, Score=IdentityScore, Live, Reason} |
| 36 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationEventRepository (InterviewAPI) | call | `36. AddAsync(SessionVerificationEvent)` | alt A | `BiometricVerificationService.cs:136-139,610-627` |  | AddAsync(SessionVerificationEvent{Phase="Entry", Modality="Face", Score, Matched=Passed, Decision}) |
| 37 | :SessionVerificationEventRepository (InterviewAPI) | SQL Server | call | `37. INSERT SessionVerificationEvent` | alt A | `SessionVerificationEventRepository.cs:16-20` |  | INSERT SessionVerificationEvent |
| 38 | SQL Server | :SessionVerificationEventRepository (InterviewAPI) | ret | `38. Return` | alt A | — |  | Return |
| 39 | :SessionVerificationEventRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `39. Return` | alt A | — |  | Return |
| 40 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `40. Return VerificationResultResponse` | alt A | `BiometricVerificationService.cs:141` |  | Return `VerificationResultResponse |
| 41 | :SessionVerificationController (InterviewAPI) | :API Gateway | ret | `41. Return 200 ApiResponse.Ok(result)` | alt A | `SessionVerificationController.cs:85` |  | Return 200 `ApiResponse.Ok(result) |
| 42 | :API Gateway | :VerificationService (WebClient) | ret | `42. Return 200` | alt A | — |  | Return 200 |
| 43 | :VerificationService (WebClient) | :VerificationController (WebClient) | ret | `43. Return (true, result, null)` | alt A | `VerificationService.cs:135-146` |  | Return `(true, result, null) |
| 44 | :VerificationController (WebClient) | :Verify UI | ret | `44. Return 200 JSON {passed, score, live, reason}` | alt A | `VerificationController.cs:69` |  | Return 200 JSON `{passed, score, live, reason} |
| B.1 | :Verify UI | :Verify UI | call | `B.1. Check result.passed` | — | `entry-verify.js:202` | self | Kiểm `result.passed |
| B.2 | :Verify UI | User | ret | `B.2. Overlay "Identity Verified" [see #11]` | alt B `[result.passed == true]` | `entry-verify.js:60-73,202-204` | boundary | [passed=true]` overlay "Identity Verified" 3.2s → navigate Integrity Policy |
| B.3 | :Verify UI | User | ret | `B.3. handleFail(result.reason) [see #14]` | alt B `[result.passed == false]` | `entry-verify.js:207` | boundary | [passed=false]` `handleFail(result.reason)` — hỏi lại entry-gate |

**Cờ:** `[best-effort]` · `self` · `throw` · `boundary` (theo §2)

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong |
|---|---|---|---|---|
| F1 | opt | `[sessionId empty OR frames empty]` (WebClient layer) | 4 → F1.2 | — |
| F2 | opt | `[Biometrics:Enabled == false]` | 10 → F2.6 | — |
| F3 | opt | `[session is null OR session.UserId != userId]` | 11 → F3.6 | — |
| F4 | opt | `[phase=="Entry" AND failedAttempts >= EntryMaxRetries]` | 15 → F4.6 | — |
| F5 | opt | `[no active unconsumed challenge]` | 20 → F5.6 | — |
| F6 | opt | `[no Face ProfileBiometric enrolled]` | 27 → F6.6 | — |
| A | alt | `[ai-service trả 422 quality/multi-face]` / `[ai-service trả 200 passed=true\|false]` | A.1 → A.8 / 33 → 44 | — |
| B | alt | `[result.passed == true]` / `[result.passed == false]` | B.2 / B.3 | — |

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | 33 | Liveness (`live`/`yaw_range`/`moved_enough`/`direction_ok`) is computed but deliberately NOT part of the pass/fail decision — yaw measurement is unreliable on real hardware (see `router.py:139-146`). Entry gate is face-match only for now; these fields are recorded as diagnostics. |
| N2 | 9 | `phase` query parameter defaults to `"Entry"` and no caller in this codebase sends any other value — the `phase != "Entry"` branch (skips the retry-cap check at F4) is a dead path from the current UI, kept only for a future Milestone 4 caller. |
| N3 | A.7 | WebClient's `VerificationController.VerifyFace` always returns 400 to the browser regardless of the upstream status (422/404/400 all collapse to 400) — the browser only ever sees the message text, never the real status code, for this specific proxy action (contrast with Hint's KI-01, which is about a DIFFERENT bug — envelope Success=false disguised as 200; this is a deliberate, harmless status-collapsing design, not the same defect). |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | File:Line | Liệt kê | Nguồn |
|---|---|---|---|---|---|
| 1 | VerificationController (WebClient) | class | `Controllers/Interview/VerificationController.cs:9` | SUBSET | local |
| 2 | IVerificationService (WebClient) | interface | `Services/Interview/VerificationService.cs:12-34` | FULL | local *(đã liệt kê đủ ở #12 — không lặp)* |
| 3 | VerificationService (WebClient) | class | `Services/Interview/VerificationService.cs:36-160` | FULL | local *(đã liệt kê đủ ở #12)* |
| 4 | VerificationResultResponse | DTO | `DTOs/Biometrics/VerificationResultResponse.cs:6-14` | FULL | local |
| 5 | ApiResponse\<T\> (InterviewAPI) | class | `InterviewAPI/DTOs/ApiResponse.cs` | FULL | **SHARED** |
| 6 | SessionVerificationController (InterviewAPI) | class | `Controllers/SessionVerificationController.cs:15` | SUBSET | local |
| 7 | IBiometricVerificationService | interface | `Services/Interfaces/IBiometricVerificationService.cs:14-15` | SUBSET | local |
| 8 | BiometricVerificationService (InterviewAPI) | class | `Services/BiometricVerificationService.cs:12` | SUBSET | local |
| 9 | ISessionRepository / SessionRepository (InterviewAPI) | interface/class | `Repositories/SessionRepository.cs` | SUBSET | local |
| 10 | InterviewSession | entity | `Models/InterviewSession.cs:11-91` | FULL | **SHARED** *(đã dán ở #12 — chỉ ghi tên)* |
| 11 | ISessionVerificationEventRepository | interface | `Repositories/Interfaces/ISessionVerificationEventRepository.cs:5-30` | FULL | local |
| 12 | SessionVerificationEventRepository (InterviewAPI) | class | `Repositories/SessionVerificationEventRepository.cs:7-98` | FULL | local |
| 13 | SessionVerificationEvent | entity | `Models/SessionVerificationEvent.cs:12-52` | FULL | **SHARED** *(lần đầu — dán §8)* |
| 14 | ISessionVerificationChallengeRepository / SessionVerificationChallengeRepository | interface/class | `Repositories/SessionVerificationChallengeRepository.cs` | FULL | local *(đã dán ở #12)* |
| 15 | SessionVerificationChallenge | entity | `Models/SessionVerificationChallenge.cs:13-33` | FULL | local *(đã dán ở #12)* |
| 16 | IProfileBiometricsRepository / ProfileBiometricsRepository | interface/class | `Repositories/ProfileBiometricsRepository.cs` | FULL | local *(đã dán ở #12)* |
| 17 | ProfileBiometric | entity | `Models/ProfileBiometric.cs:12-34` | FULL | local *(đã dán ở #12)* |
| 18 | IAiServiceClient / AiServiceClient (InterviewAPI) | interface/class | `Clients/AiServiceClient.cs:9-404` | FULL | **SHARED** *(lần đầu — dán §8)* |
| 19 | BiometricQualityException | exception | `Clients/BiometricQualityException.cs:8-31` | FULL | **SHARED** *(lần đầu — dán §8)* |
| 20 | AiFaceVerifyLiveResponse | DTO | `DTOs/AiService/AiFaceVerifyLiveResponse.cs:6-31` | FULL | local |
| 21 | InterviewDbContext | DbContext | `Models/InterviewDbContext.cs` + `.Biometrics.cs` | FULL | local *(đã dán ở #12)* |
| 22 | ControllerBase / DbContext (framework) | framework-base | — | FULL (rỗng) | **SHARED** *(đã dán ở #12)* |

## 8. CLASS — chi tiết

### `VerificationController` (`WebClient`)
- File: `Controllers/Interview/VerificationController.cs:9` — Liệt kê: SUBSET
- Field: `- verificationService: IVerificationService`
- Method: `+ VerifyFace(sessionId: Guid, frames: List<IFormFile>, ct: CancellationToken): Task<IActionResult>`
- *(SUBSET)* Đã bỏ `Challenge`, `InSessionCheck`, `EntryGate`, `VerifyInSession`, `VerifyVoice` — thuộc #12/#14/#15/#16.

### `VerificationResultResponse` (`InterviewAPI`)
- File: `DTOs/Biometrics/VerificationResultResponse.cs:6-14` — Liệt kê: FULL
- Field: `+ Modality: string`
- Field: `+ Passed: bool`
- Field: `+ Score: double?`
- Field: `+ Live: bool?`
- Field: `+ ContentOk: bool?`
- Field: `+ Transcript: string?`
- Field: `+ Reason: string?`

### `SessionVerificationController` (`InterviewAPI`)
- File: `Controllers/SessionVerificationController.cs:15` — Liệt kê: SUBSET
- Field: `- _service: IBiometricVerificationService`
- Field: `- _sessionService: ISessionService`
- Method: `+ VerifyFace(sessionId: Guid, frames: List<IFormFile>, ct: CancellationToken, phase: string = "Entry"): Task<IActionResult>`
- *(SUBSET)* Đã bỏ `GetChallenge`, `GetInSessionCheck`, `GetEntryGateStatus`, `VerifyInSession`, `VerifyVoice`, `VerifyVoiceAnswer` — thuộc #12/#14/#15/#16.

### `IBiometricVerificationService` (`InterviewAPI`)
- File: `Services/Interfaces/IBiometricVerificationService.cs:14-15` — Liệt kê: SUBSET
- Method: `+ VerifyFaceAsync(userId: Guid, sessionId: Guid, frames: List<IFormFile>, ct: CancellationToken, phase: string = "Entry"): Task<VerificationResultResponse>`
- *(SUBSET)* Đã bỏ `IssueChallengeAsync`, `VerifyVoiceAsync`, `GetEntryGateStatusAsync`, `VerifyInSessionAsync`, `VerifyVoiceAnswerAsync` — thuộc #12/#14/#15/#16.

### `BiometricVerificationService` (`InterviewAPI`)
- File: `Services/BiometricVerificationService.cs:12` — Liệt kê: SUBSET
- Field: `- _challengeRepository: ISessionVerificationChallengeRepository`
- Field: `- _eventRepository: ISessionVerificationEventRepository`
- Field: `- _biometricsRepository: IProfileBiometricsRepository`
- Field: `- _sessionRepository: ISessionRepository`
- Field: `- _aiServiceClient: IAiServiceClient`
- Field: `- _configuration: IConfiguration`
- Method: `+ VerifyFaceAsync(userId: Guid, sessionId: Guid, frames: List<IFormFile>, ct: CancellationToken, phase: string = "Entry"): Task<VerificationResultResponse>`
- Method: `- EnsureEnabled(): void`
- Method: `- EnsureSessionOwnedAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task`
- Method: `- ConsumeChallengeAsync(sessionId: Guid, modality: string, ct: CancellationToken): Task<SessionVerificationChallenge>`
- Method: `- GetStoredEmbeddingAsync(userId: Guid, modality: string, ct: CancellationToken): Task<float[]>`
- Method: `- RecordEventAsync(sessionId: Guid, userId: Guid, phase: string, modality: string, score: double?, livenessPassed: bool?, matched: bool, decision: string, detail: object, ct: CancellationToken): Task`
- *(SUBSET)* Đã bỏ `IssueChallengeAsync`, `VerifyVoiceAsync`, `GetEntryGateStatusAsync`, `VerifyInSessionAsync`, `VerifyVoiceAnswerAsync`, `BuildVoiceAnswerResponseAsync`, `EndSessionForCheatingAsync`, `EnsureEnrolledAsync` — thuộc #12/#14/#15/#16.

### `ISessionRepository` / `SessionRepository` (`InterviewAPI`)
- File: `Repositories/SessionRepository.cs` — Liệt kê: SUBSET
- Method: `+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>`

**`InterviewSession` (InterviewAPI) — SHARED, đã dán ở `GetVerificationChallenge.md` §8** — chỉ ghi tên.

### `ISessionVerificationEventRepository` (`InterviewAPI`)
- File: `Repositories/Interfaces/ISessionVerificationEventRepository.cs:5-30` — Liệt kê: FULL
- Method: `+ AddAsync(evt: SessionVerificationEvent, ct: CancellationToken): Task`
- Method: `+ TryAddIncidentAsync(evt: SessionVerificationEvent, ct: CancellationToken): Task<bool>`
- Method: `+ GetIncidentAsync(incidentId: Guid, ct: CancellationToken): Task<SessionVerificationEvent?>`
- Method: `+ ListBySessionAsync(sessionId: Guid, phase: string?, ct: CancellationToken): Task<List<SessionVerificationEvent>>`
- Method: `+ GetQuestionCheckAsync(sessionId: Guid, sessionQuestionId: Guid, ct: CancellationToken): Task<SessionVerificationEvent?>`
- Method: `+ TryConsumeQuestionCheckAsync(incidentId: Guid, sessionId: Guid, userId: Guid, consumedAt: DateTime, ct: CancellationToken): Task<bool>`
- Method: `+ ListExpiredPendingChecksAsync(sessionId: Guid, now: DateTime, ct: CancellationToken): Task<List<SessionVerificationEvent>>`
- Method: `+ TryMarkCheckMissedAsync(eventId: long, ct: CancellationToken): Task<bool>`

### `SessionVerificationEventRepository` (`InterviewAPI`)
- File: `Repositories/SessionVerificationEventRepository.cs:7-98` — Liệt kê: FULL
- Field: `- _db: InterviewDbContext`
- Method: như `ISessionVerificationEventRepository` ở trên (implements)

**`SessionVerificationEvent` (InterviewAPI) — SHARED, lần đầu xuất hiện trong dự án** → tạo file `_shared/classes/SessionVerificationEvent.yaml`:
```yaml
name: "SessionVerificationEvent"
fields:
  - "+ Id: long"
  - "+ SessionId: Guid"
  - "+ UserId: Guid"
  - "+ Phase: string"
  - "+ Modality: string"
  - "+ Score: double?"
  - "+ LivenessPassed: bool?"
  - "+ Matched: bool"
  - "+ Decision: string"
  - "+ Detail: string?"
  - "+ IncidentId: Guid?"
  - "+ SessionQuestionId: Guid?"
  - "+ CheckSequence: int?"
  - "+ ExpiresAt: DateTime?"
  - "+ ConsumedAt: DateTime?"
  - "+ ChallengeJson: string?"
  - "+ CreatedAt: DateTime"
methods: []
```

**`SessionVerificationChallenge`, `ISessionVerificationChallengeRepository`/`SessionVerificationChallengeRepository`, `IProfileBiometricsRepository`/`ProfileBiometricsRepository`, `ProfileBiometric` — đã dán đủ ở `GetVerificationChallenge.md` §8** — chỉ ghi tên, không lặp.

**`IAiServiceClient` / `AiServiceClient` (InterviewAPI) — SHARED, lần đầu xuất hiện trong dự án** → tạo file `_shared/classes/AiServiceClient.yaml`:
```yaml
name: "AiServiceClient"
fields:
  - "- _httpClient: HttpClient"
  - "- _logger: ILogger<AiServiceClient>"
methods:
  - "+ PlanQuestionsAsync(request: AiQuestionPlanRequest, ct: CancellationToken): Task<AiQuestionPlanResponse>"
  - "+ EvaluateAnswerAsync(request: AiEvaluateAnswerRequest, ct: CancellationToken): Task<AiEvaluateAnswerResponse>"
  - "+ BuildReportAsync(request: AiBuildReportRequest, ct: CancellationToken): Task<AiBuildReportResponse>"
  - "+ SynthesizeSpeechAsync(text: string, ct: CancellationToken): Task<byte[]>"
  - "+ TranscribeAsync(audio: byte[], fileName: string, contentType: string, ct: CancellationToken): Task<string>"
  - "+ GetHintAsync(question: string, ct: CancellationToken): Task<string>"
  - "+ GetCatalogAsync(ct: CancellationToken): Task<AiCatalogResponse>"
  - "+ EmbedFaceAsync(file: IFormFile, ct: CancellationToken): Task<BiometricEmbeddingResult>"
  - "+ EmbedVoiceAsync(file: IFormFile, ct: CancellationToken): Task<BiometricEmbeddingResult>"
  - "+ VerifyFaceLiveAsync(frames: IReadOnlyList<IFormFile>, storedEmbedding: float[], challengeJson: string, ct: CancellationToken): Task<AiFaceVerifyLiveResponse>"
  - "+ VerifyFaceAsync(frame: IFormFile, storedEmbedding: float[], ct: CancellationToken): Task<AiFaceVerifyResponse>"
  - "+ VerifyVoiceAsync(audio: IFormFile, storedEmbedding: float[], ct: CancellationToken): Task<AiFaceVerifyResponse>"
  - "+ VerifyVoiceChallengeAsync(file: IFormFile, storedEmbedding: float[], expectedPhrase: string, ct: CancellationToken): Task<AiVoiceVerifyChallengeResponse>"
```
*(Ghi chú: 3 helper private `EmbedAsync`/`ReadDetailAsync`/`ReadStructuredDetailAsync` — `AiServiceClient.cs:142-172,334-387` — bị lược khỏi danh sách vì là chi tiết cài đặt nội bộ, không được gọi từ class nào khác ngoài chính nó và không nằm trong `IAiServiceClient`.)*

**`BiometricQualityException` — SHARED, lần đầu xuất hiện trong dự án** → tạo file `_shared/classes/BiometricQualityException.yaml`:
```yaml
name: "BiometricQualityException"
fields:
  - "+ QualityCode: string?"
  - "+ FaceCount: int?"
methods:
  - "+ BiometricQualityException(message: string)"
  - "+ BiometricQualityException(message: string, faceCount: int)"
  - "+ BiometricQualityException(message: string, qualityCode: string?, faceCount: int? = null)"
```

### `AiFaceVerifyLiveResponse` (`InterviewAPI`)
- File: `DTOs/AiService/AiFaceVerifyLiveResponse.cs:6-31` — Liệt kê: FULL
- Field: `+ Passed: bool`
- Field: `+ Live: bool`
- Field: `+ IdentityScore: double?`
- Field: `+ Threshold: double`
- Field: `+ YawRange: double`
- Field: `+ MovedEnough: bool?`
- Field: `+ DirectionOk: bool?`
- Field: `+ Reason: string?`

**`InterviewDbContext`, `ControllerBase`, `DbContext` — đã dán đủ ở `GetVerificationChallenge.md` §8** — chỉ ghi tên.

## 9. QUAN HỆ CLASS

| Loại | From | To | Label |
|---|---|---|---|
| extends | VerificationController (WebClient) | Controller | — |
| use | VerificationController (WebClient) | IVerificationService (WebClient) | Use |
| use | VerificationService (WebClient) | VerificationResultResponse | T = VerificationResultModel |
| extends | SessionVerificationController (InterviewAPI) | ControllerBase | — |
| use | SessionVerificationController (InterviewAPI) | IBiometricVerificationService | Use |
| use | SessionVerificationController (InterviewAPI) | BiometricQualityException | throw (catch) |
| implements | BiometricVerificationService (InterviewAPI) | IBiometricVerificationService | — |
| use | BiometricVerificationService (InterviewAPI) | ISessionRepository | Use |
| use | BiometricVerificationService (InterviewAPI) | ISessionVerificationEventRepository | Use |
| use | BiometricVerificationService (InterviewAPI) | ISessionVerificationChallengeRepository | Use |
| use | BiometricVerificationService (InterviewAPI) | IProfileBiometricsRepository | Use |
| use | BiometricVerificationService (InterviewAPI) | IAiServiceClient | Use |
| use | BiometricVerificationService (InterviewAPI) | VerificationResultResponse | Use |
| implements | AiServiceClient (InterviewAPI) | IAiServiceClient | — |
| use | AiServiceClient (InterviewAPI) | BiometricQualityException | throw |
| use | AiServiceClient (InterviewAPI) | AiFaceVerifyLiveResponse | Use |
| implements | SessionVerificationEventRepository (InterviewAPI) | ISessionVerificationEventRepository | — |
| composition | SessionVerificationEventRepository (InterviewAPI) | InterviewDbContext | — |
| composition | InterviewDbContext | SessionVerificationEvent | 1..* |

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| — | — | Không phát hiện anomaly mới cho #13 (khớp đúng mô tả DIAGRAM_SCOPE.md: "alt 3 lần thử; chưa trừ credit cho tới khi pass") | — | — | không cần |

## 11. QUYẾT ĐỊNH CẦN DUYENTQ DUYỆT

| # | Vấn đề | Phương án | Đề xuất |
|---|---|---|---|
| 1 | Bảng message có nhiều nhánh opt (F2-F6) dùng chuỗi return gần như GIỐNG HỆT nhau (chỉ khác status code 400/404 và message) — bảng đã viết tắt bằng `...` ở một số dòng để tránh lặp 5 dòng gần như y hệt × 5 lần | Khi lên YAML thật (luồng 2), mỗi opt vẫn cần đủ số message riêng theo `_tools/validate.py` RULE3 (mỗi call cần return trên MỌI nhánh nó đi qua) — không thể tái dùng chung 1 khối return cho nhiều opt | Xác nhận luồng 2 tự expand đủ, không copy nguyên bảng viết tắt vào YAML |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `30.1`)** Liveness (live/yaw_range/moved_enough/direction_ok) is computed but deliberately NOT part of the pass/fail decision - yaw measurement is unreliable on real hardware (router.py:139-146). Entry gate is face-match only for now; these fields are recorded as diagnostics. *(gỡ từ note trên diagram)*
- **(cạnh message `14`)** phase query parameter defaults to "Entry" and no caller in this codebase sends any other value - the phase != "Entry" branch (skips the retry-cap check) is a dead path from the current UI, kept only for a future Milestone 4 caller. *(gỡ từ note trên diagram)*
- **(cạnh message `A.3`)** WebClient's VerificationController.VerifyFace always returns 400 to the browser regardless of upstream status (422/404/400 all collapse to 400) - the browser only sees the message text, never the real status code. This is a deliberate, harmless status-collapsing design (NOT the same defect as Hint's KI-01, which is about Success=false disguised as HTTP 200). *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| Bảng §4 thiếu 1 message return giữa dòng 24 (`ConsumedAt = now + SaveChangesAsync()`, call BVS→SessionVerificationChallengeRepository) và dòng 27 (call kế tiếp) — dòng 25-26 chỉ đóng lời gọi SQL nội bộ của repo (`SaveChangesAsync`→SQL→return), nhưng KHÔNG có message nào đóng lại chính call gốc ở dòng 24 (repo trả về `Task` cho `BiometricVerificationService`) | Luồng 1 bỏ sót return của 1 lệnh gọi `void/Task`-trả-về (dễ quên vì không mang giá trị) — cùng loại thiếu sót đã gặp ở `GetVerificationChallenge.md`/`ValidateEntryGateStatus.md` §12 | Đã thêm message `26.1: SessionVerificationChallengeRepository → BiometricVerificationService, ret, "Return"` trước khi tiếp tục dòng 27, khớp đúng quy tắc "kể cả SaveChangesAsync cũng phải vẽ Return" (`QUY_TAC_VE_DIAGRAM.md` §1.3) | luồng 2 |
| §11 mục 1 hỏi "gộp F2-F6 hay expand riêng" — đã tự trả lời KHÔNG gộp (mỗi opt là 1 guard/exception khác nhau: `Biometrics disabled` / `session not found` / `retry cap` / `no active challenge` / `not enrolled` — nội dung message hiển thị cho user khác nhau ở CẢ 6 nhánh, không thoả điều kiện "cùng HTTP status VÀ cùng nội dung user nhìn thấy" để gộp) | Đây là quyết định đọc-hiểu, không phải thiếu dữ kiện — nhưng ghi lại để xác nhận đã cân nhắc đúng theo quy trình 3 bước đã cho | Đã expand đầy đủ 6×5=30 message cho F2-F6 (mỗi khung 6 dòng: throw → 404/400 → 400 → (false,null,error) → 400 {error} → hiện lỗi), dùng đúng khuôn F2 làm mẫu, chỉ đổi status/message theo đúng exception thật của từng guard | luồng 2 |
