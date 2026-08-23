# FLOW — `GetVerificationChallenge` · BL#66 · UC-10

> Đặt tại `docs/diagrams/FLOWS/GetVerificationChallenge.md`
>
> Trạng thái: `DRAFT` — Khảo sát: luồng 1, lô `A`, `2026-08-02`

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Phát 1 challenge chống-replay (hướng quay đầu cho face / câu thoại cho voice) trước khi cho verify — TTL ngắn, one-time-use |
| Điểm vào (user thao tác gì) | Tự động, ngay khi trang Verify tải xong — KHÔNG phải hành động bấm nút; `entry-verify.js` gọi challenge trong `prepare()` trước khi bật nút "I'm ready" |
| Kết thúc (user thấy gì) | Thành công: camera sẵn sàng, nút "I'm ready" bật, KHÔNG hiện chi tiết challenge (hướng/câu thoại không hiển thị — xem NOTE N1) · Lỗi: "Challenge unavailable" + nút "Try again" |
| Quy mô | 11 participant · 49 dòng trong bảng §4 (chuỗi return cuối bảng chưa đánh số `#`, luồng 2 tự đánh số tiếp) · 1 alt / 4 opt (F1-F4) |
| Service ngoài quyền sở hữu | không (ApiGateway chỉ routing) |
| **Gọi BYPASS Gateway?** | không |
| Nhà cung cấp AI | không (challenge sinh nội bộ .NET — danh sách phrase tĩnh + Random) |
| Ghi DB | `SessionVerificationChallenges` — INSERT |
| Trừ credit | không |
| KI liên quan | KI-05 (nhánh `modality=voice` không có lối vào UI thật — vẫn vẽ đủ 2 nhánh vì là hành vi ĐÚNG của endpoint, chỉ note UI hiện tại chỉ gọi `face`) |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| (không có mục nào bị loại trừ hoàn toàn — nhánh `modality=voice` vẫn vẽ, xem §6 NOTE thay vì loại trừ) | — | — |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Sau khi verify-face thành công, browser điều hướng sang trang Integrity Policy | `#11 AcceptIntegrityPolicy` | `Navigate to Integrity Policy page` | `entry-verify.js:72` |

---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 1 | User | actor | — | — |
| 2 | :Verify UI | box | `Views/Interview/Verify.cshtml` + `wwwroot/js/pages/entry-verify.js` | — |
| 3 | :VerificationController (WebClient) | box | `Controllers/Interview/VerificationController.cs` | — |
| 4 | :VerificationService (WebClient) | box | `Services/Interview/VerificationService.cs` | — |
| 5 | :API Gateway | box | `ApiGateway/appsettings.json` route `interview-route` | có |
| 6 | :SessionVerificationController (InterviewAPI) | box | `Controllers/SessionVerificationController.cs` | — |
| 7 | :BiometricVerificationService (InterviewAPI) | box | `Services/BiometricVerificationService.cs` | — |
| 8 | :SessionRepository (InterviewAPI) | box | `Repositories/SessionRepository.cs` (SUBSET) | — |
| 9 | :ProfileBiometricsRepository (InterviewAPI) | box | `Repositories/ProfileBiometricsRepository.cs` | — |
| 10 | :SessionVerificationChallengeRepository (InterviewAPI) | box | `Repositories/SessionVerificationChallengeRepository.cs` | — |
| 11 | SQL Server | cylinder | `DevPrep_Interview` | — |

## 4. BẢNG MESSAGE

| # | From | To | call/ret | Label (tiếng Anh) | Frame (guard) | File:Line | Cờ | Chi tiết (gỡ khỏi nhãn) |
|---|---|---|---|---|---|---|---|---|
| 1 | User | :Verify UI | call | `1. Open Verify page (redirect from Start, or page reload)` | — | `Views/Interview/Verify.cshtml:23` |  | Open Verify page (redirect from Start, or page reload) |
| 2 | :Verify UI | :Verify UI | call | `2. prepare(): auto-fetch challenge before enabling camera` | — | `entry-verify.js:110,131-132` | self | prepare()`: auto-fetch challenge before enabling camera |
| 3 | :Verify UI | :VerificationController (WebClient) | call | `3. GET Verification/Challenge?sessionId&modality=face` | — | `entry-verify.js:131-132` |  | GET Verification/Challenge?sessionId&modality=face |
| 4 | :VerificationController (WebClient) | :VerificationController (WebClient) | call | `4. validate sessionId != Guid.Empty AND modality not blank` | — | `VerificationController.cs:28-31` | self | validate `sessionId != Guid.Empty && modality not blank |
| F1.1 | :VerificationController (WebClient) | :Verify UI | ret | Return 400 `{error: "sessionId and modality are required."}` | opt F1 `[sessionId empty OR modality blank]` | `VerificationController.cs:29-31` |  | — |
| F1.2 | :Verify UI | User | ret | Hiện "Challenge unavailable" + nút "Try again" | opt F1 | `entry-verify.js:134-138` |  | — |
| 5 | :VerificationController (WebClient) | :VerificationService (WebClient) | call | `5. GetChallengeAsync(sessionId, modality)` | — | `VerificationController.cs:33-34` |  | GetChallengeAsync(sessionId, modality) |
| 6 | :VerificationService (WebClient) | :API Gateway | call | `6. GET api/interview/sessions/{id}/verify/challenge` | — | `VerificationService.cs:43-45` |  | GET api/interview/sessions/{sessionId}/verify/challenge?modality=face |
| 7 | :API Gateway | :SessionVerificationController (InterviewAPI) | call | `7. Forward request to InterviewAPI (no prefix strip)` | — | `ApiGateway/appsettings.json:49-54` |  | Forward request to InterviewAPI (route `interview-route`, no prefix strip) |
| 8 | :SessionVerificationController (InterviewAPI) | :SessionVerificationController (InterviewAPI) | call | `NormalizeModality(modality)` → "face"\|"voice"\|null | — | `SessionVerificationController.cs:51,205-211` | self |
| F2.1 | :SessionVerificationController (InterviewAPI) | :API Gateway | ret | Return 400 `ApiResponse.Fail("modality must be 'face' or 'voice'.")` | opt F2 `[normalized is null]` | `SessionVerificationController.cs:52-55` |  | — |
| F2.2 | :API Gateway | :VerificationService (WebClient) | ret | Return 400 | opt F2 | — |  | — |
| F2.3 | :VerificationService (WebClient) | :VerificationController (WebClient) | ret | Return `(false, null, error)` | opt F2 | `VerificationService.cs:47-50` |  | — |
| F2.4 | :VerificationController (WebClient) | :Verify UI | ret | Return 400 `{error}` | opt F2 | `VerificationController.cs:36-38` |  | — |
| F2.5 | :Verify UI | User | ret | Hiện "Challenge unavailable" + nút "Try again" | opt F2 | `entry-verify.js:134-138` |  | — |
| 9 | :SessionVerificationController (InterviewAPI) | :SessionVerificationController (InterviewAPI) | call | `9. Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))` | — | `SessionVerificationController.cs:59` | self | Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)) |
| 10 | :SessionVerificationController (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `10. IssueChallengeAsync(userId, sessionId, normalized)` | — | `SessionVerificationController.cs:60` |  | IssueChallengeAsync(userId, sessionId, normalized) |
| 11 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `11. EnsureEnabled()` | — | `BiometricVerificationService.cs:56,629-633` | self | EnsureEnabled()` — throw nếu `Biometrics:Enabled=false |
| 12 | :BiometricVerificationService (InterviewAPI) | :SessionRepository (InterviewAPI) | call | `12. GetByIdAsync(sessionId) — via EnsureSessionOwnedAsync` | — | `BiometricVerificationService.cs:57,635-640` |  | GetByIdAsync(sessionId)` (qua `EnsureSessionOwnedAsync`) |
| 13 | :SessionRepository (InterviewAPI) | SQL Server | call | `13. SELECT InterviewSession by SessionId` | — | `ISessionRepository.cs:8` |  | SELECT InterviewSession by SessionId |
| 14 | SQL Server | :SessionRepository (InterviewAPI) | ret | `14. Return session row / null` | — | — |  | Return session row / null |
| 15 | :SessionRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `15. Return InterviewSession?` | — | — |  | Return `InterviewSession? |
| F3.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `throw KeyNotFoundException("Session not found.")` | opt F3 `[session is null OR session.UserId != userId]` | `BiometricVerificationService.cs:638-639` | throw | — |
| F3.2 | :SessionVerificationController (InterviewAPI) | :API Gateway | ret | Return 404 `ApiResponse.Fail(ex.Message)` | opt F3 | `SessionVerificationController.cs:63-65` |  | — |
| F3.3 | :API Gateway | :VerificationService (WebClient) | ret | Return 404 | opt F3 | — |  | — |
| F3.4 | :VerificationService (WebClient) | :VerificationController (WebClient) | ret | Return `(false, null, error)` | opt F3 | `VerificationService.cs:47-50` |  | — |
| F3.5 | :VerificationController (WebClient) | :Verify UI | ret | Return 400 `{error}` | opt F3 | `VerificationController.cs:36-38` |  | — |
| F3.6 | :Verify UI | User | ret | Hiện "Challenge unavailable" + nút "Try again" | opt F3 | `entry-verify.js:134-138` |  | — |
| 16 | :BiometricVerificationService (InterviewAPI) | :ProfileBiometricsRepository (InterviewAPI) | call | `16. GetAsync(userId, modality) — via EnsureEnrolledAsync` | — | `BiometricVerificationService.cs:58,642-648` |  | GetAsync(userId, modality)` (qua `EnsureEnrolledAsync`) |
| 17 | :ProfileBiometricsRepository (InterviewAPI) | SQL Server | call | `17. SELECT ProfileBiometric WHERE UserId AND Modality` | — | `ProfileBiometricsRepository.cs:16-19` |  | SELECT ProfileBiometric WHERE UserId AND Modality |
| 18 | SQL Server | :ProfileBiometricsRepository (InterviewAPI) | ret | `18. Return row / null` | — | — |  | Return row / null |
| 19 | :ProfileBiometricsRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | `19. Return ProfileBiometric?` | — | — |  | Return `ProfileBiometric? |
| F4.1 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | `throw InvalidOperationException("No {modality} enrollment found...")` | opt F4 `[row is null]` | `BiometricVerificationService.cs:645-647` | throw | — |
| F4.2 | :SessionVerificationController (InterviewAPI) | :API Gateway | ret | Return 400 `ApiResponse.Fail(ex.Message)` | opt F4 | `SessionVerificationController.cs:67-69` |  | — |
| F4.3 | :API Gateway | :VerificationService (WebClient) | ret | Return 400 | opt F4 | — |  | — |
| F4.4 | :VerificationService (WebClient) | :VerificationController (WebClient) | ret | Return `(false, null, error)` | opt F4 | `VerificationService.cs:47-50` |  | — |
| F4.5 | :VerificationController (WebClient) | :Verify UI | ret | Return 400 `{error}` | opt F4 | `VerificationController.cs:36-38` |  | — |
| F4.6 | :Verify UI | User | ret | Hiện "Enroll your face first" (message chứa gợi ý enroll) + "Try again" | opt F4 | `entry-verify.js:134-138` |  | — |
| A.1 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `direction = "left"\|"right"` (Random), build `{type:"turn_head", direction}` | alt A `[modality == "Face"]` | `BiometricVerificationService.cs:65-69` | self |
| A.2 | :BiometricVerificationService (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | call | `phrase = ChallengePhrases[Random]`, build `{phrase}` | alt A `[modality == "Voice"]` | `BiometricVerificationService.cs:71-73` | self | — |
| 20 | :BiometricVerificationService (InterviewAPI) | :SessionVerificationChallengeRepository (InterviewAPI) | call | `AddAsync(SessionVerificationChallenge{SessionId, UserId, Modality, ChallengeJson, ExpiresAt=now+120s})` | — | `BiometricVerificationService.cs:76-84` |  | — |
| 21 | :SessionVerificationChallengeRepository (InterviewAPI) | SQL Server | call | `21. AddAsync(SessionVerificationChallenge)` | — | `SessionVerificationChallengeRepository.cs:16-20` |  | INSERT SessionVerificationChallenge |
| 22 | SQL Server | :SessionVerificationChallengeRepository (InterviewAPI) | ret | `22. INSERT SessionVerificationChallenge` | — | — |  | Return |
| — | :SessionVerificationChallengeRepository (InterviewAPI) | :BiometricVerificationService (InterviewAPI) | ret | Return | — | — |  | — |
| — | :BiometricVerificationService (InterviewAPI) | :SessionVerificationController (InterviewAPI) | ret | Return `VerificationChallengeResponse{Modality, Direction?, Phrase?, ExpiresInSeconds=120}` | — | `BiometricVerificationService.cs:86-92` |  | — |
| — | :SessionVerificationController (InterviewAPI) | :API Gateway | ret | Return 200 `ApiResponse.Ok(result)` | — | `SessionVerificationController.cs:61` |  | — |
| — | :API Gateway | :VerificationService (WebClient) | ret | Return 200 | — | — |  | — |
| — | :VerificationService (WebClient) | :VerificationController (WebClient) | ret | Return `(true, challenge, null)` | — | `VerificationService.cs:52` |  | — |
| — | :VerificationController (WebClient) | :Verify UI | ret | Return 200 JSON challenge | — | `VerificationController.cs:40` |  | — |
| — | :Verify UI | User | ret | Bật camera + nút "I'm ready" (KHÔNG hiện direction/phrase — xem NOTE N1) | — | `entry-verify.js:142-156` |  | — |

> Ghi chú đánh số: các dòng cuối cùng (return chuỗi thành công) tiếp tục đếm `23-30` khi lên YAML thật — bảng trên để trống `#` cho các return thuần tuý không cần tra cứu chéo, luồng 2 tự đánh số tiếp khi vẽ.

**Cờ:** `[best-effort]` · `self` · `throw` · `boundary` (theo §2)

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong |
|---|---|---|---|---|
| F1 | opt | `[sessionId == Guid.Empty OR modality is blank]` | 4 → F1.2 | — |
| F2 | opt | `[normalized is null]` (modality không phải "face"/"voice") | 8 → F2.5 | — |
| F3 | opt | `[session is null OR session.UserId != userId]` | 12 → F3.6 | — |
| F4 | opt | `[không tìm thấy ProfileBiometric cho modality này]` | 16 → F4.6 | — |
| A | alt | `[modality == "Face"]` / `[modality == "Voice"]` | A.1 / A.2 | — |

## 6. NOTE

| Note# | Cạnh message # | Nội dung (tiếng Anh) |
|---|---|---|
| N1 | cuối bảng (return to User) | `entry-verify.js` no longer displays the requested head-turn direction to the user — liveness/direction scoring is disabled (see `ai-service/biometrics/router.py:139-146`); the challenge is still issued and stored for anti-replay, just not surfaced in the UI. |
| N2 | 8, A.1/A.2 | `modality=voice` branch is real, working code, but no live UI currently calls it with `modality=voice` for THIS endpoint — the only caller that would (`biometric-wizard.js` "verify" mode) is itself unreachable (KI-05). Drawn per "vẽ hành vi đúng của code", not UI reachability. |

---

## 7. CLASS — danh sách

| # | Tên hiển thị | Loại | File:Line | Liệt kê | Nguồn |
|---|---|---|---|---|---|
| 1 | VerificationController (WebClient) | class | `Controllers/Interview/VerificationController.cs:9` | SUBSET | local |
| 2 | Controller | framework-base | (Microsoft.AspNetCore.Mvc) | FULL (rỗng) | local |
| 3 | IVerificationService (WebClient) | interface | `Services/Interview/VerificationService.cs:12-34` | FULL | local |
| 4 | VerificationService (WebClient) | class | `Services/Interview/VerificationService.cs:36-160` | FULL | local |
| 5 | IApiClient | interface | `Services/Shared/IApiClient.cs` | FULL | local |
| 6 | ApiClient | class | `Services/Shared/ApiClient.cs` | FULL | local |
| 7 | ApiResponse\<T\> (InterviewAPI) | class | `InterviewAPI/DTOs/ApiResponse.cs` | FULL | **SHARED** |
| 8 | VerificationChallengeResponse | DTO | `DTOs/Biometrics/VerificationChallengeResponse.cs:6-12` | FULL | local |
| 9 | SessionVerificationController (InterviewAPI) | class | `Controllers/SessionVerificationController.cs:15` | SUBSET | local |
| 10 | ControllerBase | framework-base | (Microsoft.AspNetCore.Mvc) | FULL (rỗng) | **SHARED** |
| 11 | IBiometricVerificationService | interface | `Services/Interfaces/IBiometricVerificationService.cs:5-36` | SUBSET | local |
| 12 | BiometricVerificationService (InterviewAPI) | class | `Services/BiometricVerificationService.cs:12` | SUBSET | local |
| 13 | ISessionRepository | interface | `Repositories/Interfaces/ISessionRepository.cs` | SUBSET | local |
| 14 | SessionRepository (InterviewAPI) | class | `Repositories/SessionRepository.cs` | SUBSET | local |
| 15 | InterviewSession | entity | `Models/InterviewSession.cs:11-91` | FULL | **SHARED** |
| 16 | IProfileBiometricsRepository | interface | `Repositories/Interfaces/IProfileBiometricsRepository.cs:5-21` | FULL | local |
| 17 | ProfileBiometricsRepository (InterviewAPI) | class | `Repositories/ProfileBiometricsRepository.cs:7-38` | FULL | local |
| 18 | ProfileBiometric | entity | `Models/ProfileBiometric.cs:12-34` | FULL | local |
| 19 | ISessionVerificationChallengeRepository | interface | `Repositories/Interfaces/ISessionVerificationChallengeRepository.cs:5-15` | FULL | local |
| 20 | SessionVerificationChallengeRepository (InterviewAPI) | class | `Repositories/SessionVerificationChallengeRepository.cs:7-33` | FULL | local |
| 21 | SessionVerificationChallenge | entity | `Models/SessionVerificationChallenge.cs:13-33` | FULL | local |
| 22 | InterviewDbContext | DbContext | `Models/InterviewDbContext.cs` + `InterviewDbContext.Biometrics.cs` | FULL | local |
| 23 | DbContext | framework-base | (Microsoft.EntityFrameworkCore) | FULL (rỗng) | local |

## 8. CLASS — chi tiết

### `VerificationController` (`WebClient`)
- File: `Controllers/Interview/VerificationController.cs:9` — Liệt kê: SUBSET
- Field: `- verificationService: IVerificationService`
- Method: `+ Challenge(sessionId: Guid, modality: string, ct: CancellationToken): Task<IActionResult>`
- *(SUBSET)* Đã bỏ `InSessionCheck`, `VerifyFace`, `EntryGate`, `VerifyInSession`, `VerifyVoice` — thuộc #13/#14/#15/#16.

### `Controller` (`Microsoft.AspNetCore.Mvc`)
- File: framework base — Liệt kê: FULL (rỗng)
- (không field/method — hộp chỉ có tên, dùng để nối `extends`)

### `IVerificationService` (`WebClient`)
- File: `Services/Interview/VerificationService.cs:12-34` — Liệt kê: FULL
- Method: `+ GetChallengeAsync(sessionId: Guid, modality: string, ct: CancellationToken): Task<(bool Success, VerificationChallengeModel? Challenge, string? Error)>`
- Method: `+ VerifyFaceAsync(sessionId: Guid, frames: IReadOnlyList<(byte[] Data, string FileName, string ContentType)>, ct: CancellationToken): Task<(bool Success, VerificationResultModel? Result, string? Error)>`
- Method: `+ VerifyVoiceAsync(sessionId: Guid, audio: byte[], fileName: string, contentType: string, ct: CancellationToken): Task<(bool Success, VerificationResultModel? Result, string? Error)>`
- Method: `+ GetEntryGateAsync(sessionId: Guid, ct: CancellationToken): Task<(bool Success, EntryGateStatusModel? Status, string? Error)>`
- Method: `+ VerifyInSessionAsync(sessionId: Guid, checkId: Guid, frame: byte[], fileName: string, contentType: string, ct: CancellationToken): Task<(bool Success, InSessionVerifyResultModel? Result, string? Error)>`
- Method: `+ GetInSessionCheckAsync(sessionId: Guid, sessionQuestionId: Guid, ct: CancellationToken): Task<(bool Success, InSessionCheckDueModel? Check, string? Error)>`

### `VerificationService` (`WebClient`)
- File: `Services/Interview/VerificationService.cs:36-160` — Liệt kê: FULL
- Field: `- JsonOptions: JsonSerializerOptions` *(static readonly)*
- Method: `+ GetChallengeAsync(...)`: như trên
- Method: `+ VerifyFaceAsync(...)`: như trên
- Method: `+ VerifyVoiceAsync(...)`: như trên
- Method: `+ GetEntryGateAsync(...)`: như trên
- Method: `+ GetInSessionCheckAsync(...)`: như trên
- Method: `+ VerifyInSessionAsync(...)`: như trên
- Method: `- ReadResultAsync(response: HttpResponseMessage, ct: CancellationToken): Task<(bool, VerificationResultModel?, string?)>` *(static)*
- Method: `- ReadEnvelopeAsync<T>(response: HttpResponseMessage, ct: CancellationToken): Task<ApiResponse<T>?>` *(static)*

### `IApiClient` (`WebClient`)
- File: `Services/Shared/IApiClient.cs` — Liệt kê: FULL
- Field: `+ HttpClient: HttpClient`

### `ApiClient` (`WebClient`)
- File: `Services/Shared/ApiClient.cs:3-6` — Liệt kê: FULL
- Field: `+ HttpClient: HttpClient`

**`ApiResponse<T>` (InterviewAPI) — SHARED, đã có file `_shared/classes/ApiResponse.yaml`** — không dán lại nội dung.

### `VerificationChallengeResponse` (`InterviewAPI`)
- File: `DTOs/Biometrics/VerificationChallengeResponse.cs:6-12` — Liệt kê: FULL
- Field: `+ Modality: string`
- Field: `+ Direction: string?`
- Field: `+ Phrase: string?`
- Field: `+ ExpiresInSeconds: int`

### `SessionVerificationController` (`InterviewAPI`)
- File: `Controllers/SessionVerificationController.cs:15` — Liệt kê: SUBSET
- Field: `- _service: IBiometricVerificationService`
- Field: `- _sessionService: ISessionService`
- Method: `+ GetChallenge(sessionId: Guid, modality: string, ct: CancellationToken): Task<IActionResult>`
- Method: `- NormalizeModality(modality: string?): string?` *(static)*
- *(SUBSET)* Đã bỏ `GetInSessionCheck`, `VerifyFace`, `VerifyVoice`, `GetEntryGateStatus`, `VerifyInSession`, `VerifyVoiceAnswer` — thuộc #13/#14/#15/#16.

### `ControllerBase` (`Microsoft.AspNetCore.Mvc`) — SHARED, framework rỗng
→ tạo file `_shared/classes/ControllerBase.yaml`:
```yaml
name: ControllerBase
fields: []
methods: []
```

### `IBiometricVerificationService` (`InterviewAPI`)
- File: `Services/Interfaces/IBiometricVerificationService.cs:5-36` — Liệt kê: SUBSET
- Method: `+ IssueChallengeAsync(userId: Guid, sessionId: Guid, modality: string, ct: CancellationToken): Task<VerificationChallengeResponse>`
- *(SUBSET)* Đã bỏ `VerifyFaceAsync`, `VerifyVoiceAsync`, `GetEntryGateStatusAsync`, `VerifyInSessionAsync`, `VerifyVoiceAnswerAsync` — thuộc #13/#14/#15/#16.

### `BiometricVerificationService` (`InterviewAPI`)
- File: `Services/BiometricVerificationService.cs:12` — Liệt kê: SUBSET
- Field: `- ChallengePhrases: string[]` *(static readonly)*
- Field: `- _challengeRepository: ISessionVerificationChallengeRepository`
- Field: `- _eventRepository: ISessionVerificationEventRepository`
- Field: `- _biometricsRepository: IProfileBiometricsRepository`
- Field: `- _sessionRepository: ISessionRepository`
- Field: `- _aiServiceClient: IAiServiceClient`
- Field: `- _configuration: IConfiguration`
- Method: `+ IssueChallengeAsync(userId: Guid, sessionId: Guid, modality: string, ct: CancellationToken): Task<VerificationChallengeResponse>`
- Method: `- EnsureEnabled(): void`
- Method: `- EnsureSessionOwnedAsync(userId: Guid, sessionId: Guid, ct: CancellationToken): Task`
- Method: `- EnsureEnrolledAsync(userId: Guid, modality: string, ct: CancellationToken): Task`
- *(SUBSET)* Đã bỏ `VerifyFaceAsync`, `VerifyVoiceAsync`, `GetEntryGateStatusAsync`, `VerifyInSessionAsync`, `VerifyVoiceAnswerAsync`, `BuildVoiceAnswerResponseAsync`, `EndSessionForCheatingAsync`, `RecordEventAsync`, `ConsumeChallengeAsync`, `GetStoredEmbeddingAsync` — thuộc #13/#14/#15/#16 (một số helper dùng chung sẽ liệt kê lại khi #13/#15 cần).

### `ISessionRepository` (`InterviewAPI`)
- File: `Repositories/Interfaces/ISessionRepository.cs:8` — Liệt kê: SUBSET
- Method: `+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>`

### `SessionRepository` (`InterviewAPI`)
- File: `Repositories/SessionRepository.cs` — Liệt kê: SUBSET
- Method: `+ GetByIdAsync(id: Guid, ct: CancellationToken): Task<InterviewSession?>`

**`InterviewSession` (InterviewAPI) — SHARED, lần đầu xuất hiện trong dự án** → tạo file `_shared/classes/InterviewSession.yaml`:
```yaml
name: "InterviewSession"
fields:
  - "+ SessionId: Guid"
  - "+ UserId: Guid"
  - "+ CvId: Guid?"
  - "+ JdId: Guid?"
  - "+ Position: string?"
  - "+ DurationMinutes: int"
  - "+ Level: string"
  - "+ InteractionMode: string"
  - "+ QuestionType: string"
  - "+ Status: string"
  - "+ StartedAt: DateTime?"
  - "+ EndedAt: DateTime?"
  - "+ TotalPausedSeconds: int"
  - "+ PausedAt: DateTime?"
  - "+ PauseCount: int"
  - "+ CameraViolationCount: int"
  - "+ TotalQuestions: int"
  - "+ AnsweredQuestions: int"
  - "+ AvgResponseTimeMs: int?"
  - "+ CreditCost: int"
  - "+ IntegrityPolicyAccepted: bool"
  - "+ IntegrityPolicyAcceptedAt: DateTime?"
  - "+ LastSkipAtUtc: DateTime?"
  - "+ IsDeleted: bool"
  - "+ DeletedAt: DateTime?"
  - "+ CreatedAt: DateTime"
  - "+ UpdatedAt: DateTime"
  - "+ InterviewRoom: InterviewRoom?"
  - "+ SessionQuestions: ICollection<SessionQuestion>"
methods: []
```

### `IProfileBiometricsRepository` (`InterviewAPI`)
- File: `Repositories/Interfaces/IProfileBiometricsRepository.cs:5-21` — Liệt kê: FULL
- Method: `+ GetAsync(userId: Guid, modality: string, ct: CancellationToken): Task<ProfileBiometric?>`
- Method: `+ ListByUserAsync(userId: Guid, ct: CancellationToken): Task<List<ProfileBiometric>>`
- Method: `+ ListByModalityAsync(modality: string, ct: CancellationToken): Task<List<ProfileBiometric>>`
- Method: `+ AddAsync(entity: ProfileBiometric, ct: CancellationToken): Task`
- Method: `+ SaveChangesAsync(ct: CancellationToken): Task`

### `ProfileBiometricsRepository` (`InterviewAPI`)
- File: `Repositories/ProfileBiometricsRepository.cs:7-38` — Liệt kê: FULL
- Field: `- _db: InterviewDbContext`
- Method: như `IProfileBiometricsRepository` ở trên (implements)

### `ProfileBiometric` (`InterviewAPI`)
- File: `Models/ProfileBiometric.cs:12-34` — Liệt kê: FULL
- Field: `+ Id: long`
- Field: `+ UserId: Guid`
- Field: `+ Modality: string`
- Field: `+ Embedding: byte[]`
- Field: `+ Dim: int`
- Field: `+ ModelVersion: string`
- Field: `+ Quality: double?`
- Field: `+ CreatedAt: DateTime`
- Field: `+ UpdatedAt: DateTime`

### `ISessionVerificationChallengeRepository` (`InterviewAPI`)
- File: `Repositories/Interfaces/ISessionVerificationChallengeRepository.cs:5-15` — Liệt kê: FULL
- Method: `+ AddAsync(challenge: SessionVerificationChallenge, ct: CancellationToken): Task`
- Method: `+ GetActiveAsync(sessionId: Guid, modality: string, now: DateTime, ct: CancellationToken): Task<SessionVerificationChallenge?>`
- Method: `+ SaveChangesAsync(ct: CancellationToken): Task`

### `SessionVerificationChallengeRepository` (`InterviewAPI`)
- File: `Repositories/SessionVerificationChallengeRepository.cs:7-33` — Liệt kê: FULL
- Field: `- _db: InterviewDbContext`
- Method: như `ISessionVerificationChallengeRepository` ở trên (implements)

### `SessionVerificationChallenge` (`InterviewAPI`)
- File: `Models/SessionVerificationChallenge.cs:13-33` — Liệt kê: FULL
- Field: `+ Id: long`
- Field: `+ SessionId: Guid`
- Field: `+ UserId: Guid`
- Field: `+ Modality: string`
- Field: `+ ChallengeJson: string`
- Field: `+ ExpiresAt: DateTime`
- Field: `+ ConsumedAt: DateTime?`
- Field: `+ CreatedAt: DateTime`

### `InterviewDbContext` (`InterviewAPI`)
- File: `Models/InterviewDbContext.cs:9,13` + `Models/InterviewDbContext.Biometrics.cs:9-13` — Liệt kê: FULL
- Field: `+ ChatMessages: DbSet<ChatMessage>`
- Field: `+ InterviewRooms: DbSet<InterviewRoom>`
- Field: `+ InterviewSessions: DbSet<InterviewSession>`
- Field: `+ SessionQuestions: DbSet<SessionQuestion>`
- Field: `+ UserRoomTourStatuses: DbSet<UserRoomTourStatus>`
- Field: `+ ProfileBiometrics: DbSet<ProfileBiometric>`
- Field: `+ SessionVerificationChallenges: DbSet<SessionVerificationChallenge>`
- Field: `+ SessionVerificationEvents: DbSet<SessionVerificationEvent>`
- Method: `+ InterviewDbContext()`
- Method: `+ InterviewDbContext(options: DbContextOptions<InterviewDbContext>)`

### `DbContext` (`Microsoft.EntityFrameworkCore`)
- File: framework base — Liệt kê: FULL (rỗng)

## 9. QUAN HỆ CLASS

| Loại | From | To | Label |
|---|---|---|---|
| extends | VerificationController (WebClient) | Controller | — |
| use | VerificationController (WebClient) | IVerificationService (WebClient) | Use |
| implements | VerificationService (WebClient) | IVerificationService (WebClient) | — |
| use | VerificationService (WebClient) | IApiClient | Use |
| implements | ApiClient | IApiClient | — |
| use | VerificationService (WebClient) | VerificationChallengeResponse | T = VerificationChallengeModel (đọc qua ApiResponse\<T\>) |
| extends | SessionVerificationController (InterviewAPI) | ControllerBase | — |
| use | SessionVerificationController (InterviewAPI) | IBiometricVerificationService | Use |
| use | SessionVerificationController (InterviewAPI) | ApiResponse\<T\> (InterviewAPI) | Use |
| implements | BiometricVerificationService (InterviewAPI) | IBiometricVerificationService | — |
| use | BiometricVerificationService (InterviewAPI) | ISessionRepository | Use |
| use | BiometricVerificationService (InterviewAPI) | IProfileBiometricsRepository | Use |
| use | BiometricVerificationService (InterviewAPI) | ISessionVerificationChallengeRepository | Use |
| use | BiometricVerificationService (InterviewAPI) | VerificationChallengeResponse | Use |
| implements | SessionRepository (InterviewAPI) | ISessionRepository | — |
| implements | ProfileBiometricsRepository (InterviewAPI) | IProfileBiometricsRepository | — |
| implements | SessionVerificationChallengeRepository (InterviewAPI) | ISessionVerificationChallengeRepository | — |
| composition | ProfileBiometricsRepository (InterviewAPI) | InterviewDbContext | — |
| composition | SessionVerificationChallengeRepository (InterviewAPI) | InterviewDbContext | — |
| composition | SessionRepository (InterviewAPI) | InterviewDbContext | — |
| extends | InterviewDbContext | DbContext | — |
| composition | InterviewDbContext | InterviewSession | 1..* |
| composition | InterviewDbContext | ProfileBiometric | 1..* |
| composition | InterviewDbContext | SessionVerificationChallenge | 1..* |

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| (KI-05, tham chiếu) | `biometric-wizard.js:267,42-43` | Nhánh `modality=voice` của endpoint Challenge không có lối vào UI thật (mode "verify" của wizard không bao giờ kích hoạt) | code chết (do UI, không phải do API) | Vẽ đủ alt face/voice ở tầng API (đúng code), kèm NOTE N2 nói rõ UI hiện chỉ dùng face | Đã có sẵn — KI-05 |

Không phát hiện anomaly MỚI riêng cho #12 (endpoint tự thân hoạt động đúng như tài liệu).


### Gỡ từ note trên diagram — anomaly

- **(cạnh message `8.1`)** modality=voice branch is real, working code, but no live UI currently calls this endpoint with modality=voice - the only caller that would (biometric-wizard.js verify mode) is itself unreachable (see KI-05). *(gỡ từ note trên diagram)*

## 11. QUYẾT ĐỊNH CẦN DUYENTQ DUYỆT

| # | Vấn đề | Phương án | Đề xuất |
|---|---|---|---|
| 1 | `InterviewDbContext` (8 DbSet) không nằm trong danh sách SHARED được giao nhưng sẽ lặp lại y hệt ở cả #12/#13/#14/#15 (và mọi lô khác dùng InterviewAPI) | (a) Giữ local, dán lại mỗi file (đã làm ở #12) — đúng nghĩa đen chỉ thị · (b) Thêm vào `_shared/classes/InterviewDbContext.yaml` từ lô sau | Đề xuất (b) cho lô kế tiếp — giảm trùng lặp, nội dung không đổi giữa các file |
| 2 | `IApiClient`/`ApiClient` (WebClient) cũng lặp lại giống hệt ở mọi file có gọi Gateway (tương tự Login đã có) | Thêm vào SHARED? | Đề xuất có, cùng đợt với #1 |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `20.1`)** entry-verify.js no longer displays the requested head-turn direction/phrase to the user - liveness/direction scoring is disabled (ai-service/biometrics/router.py:139-146). The challenge is still issued and stored for anti-replay, just not surfaced in the UI. *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
| §7/§8 thiếu class `VerificationChallengeModel` (DTO phía WebClient, mirror của `VerificationChallengeResponse`) — `IVerificationService.GetChallengeAsync` trả về kiểu này nhưng luồng 1 chỉ liệt kê bản InterviewAPI, bỏ sót bản WebClient | Luồng 1 không mở `WebClient/Models/Interview/VerificationModels.cs`, chỉ theo dấu tới chữ ký hàm mà không tra định nghĩa class | Đã mở `Models/Interview/VerificationModels.cs:4-10` khi lên YAML, xác nhận field `Modality/Direction/Phrase/ExpiresInSeconds` khớp 100% với `VerificationChallengeResponse` — đã thêm class này vào `getverificationchallenge_classes.yaml` (cột 1) | luồng 2 |
| §4 bảng message thiếu hẳn nhánh lỗi cho message #11 (`EnsureEnabled()` — throw nếu `Biometrics:Enabled=false`) — dòng 11 có cột Frame để trống `—`, không có opt bao quanh, trong khi 3 file cùng lô (#13/#14/#15) đều có nhánh này | Luồng 1 bỏ sót khi soạn bảng cho #12 dù đã đọc đúng code (`EnsureEnabled()` tại `BiometricVerificationService.cs:629-633` ném `InvalidOperationException` giống hệt các file khác) | Đã tự bù bằng cách copy đúng pattern nhánh "disabled" từ #13 (opt F2 trong `VerifyFaceEntryGate.md`), dựng thành opt riêng (message 11.1→11.6) trong `getverificationchallenge.yaml` trước khi build | luồng 2 |
