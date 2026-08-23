# KNOWN ISSUES — lỗi code phát hiện khi vẽ diagram

> Ghi lại bug tìm thấy trong lúc khảo sát code để vẽ sequence diagram.
> **Diagram vẽ theo hành vi ĐÚNG (sau khi fix), không vẽ nguyên trạng bug** — kèm `note`
> trong diagram chỉ rõ điều đó.
> Tài liệu này KHÔNG tự sửa code. Mỗi mục cần được xử lý bằng 1 commit riêng.

---

## KI-01 — WebClient trả HTTP 200 cho lỗi upstream ở endpoint Hint

| | |
|---|---|
| Phát hiện khi vẽ | `GenerateInterviewHint` (UC-09, BL#44) |
| Mức độ | Trung bình — người dùng mất lượt hint mà không thấy báo lỗi |
| Trạng thái | **Chưa fix** |
| Diagram liên quan | `out/GenerateInterviewHint.drawio`, khung `opt-5` |

### Mô tả

Khi InterviewAPI trả **HTTP 200** nhưng thân JSON có `ApiResponse.Success == false`
(ví dụ envelope hỏng, hoặc một handler tương lai trả `Fail(...)` kèm `Ok()`), luồng WebClient
biến lỗi đó thành một phản hồi **200 thành công** gửi về trình duyệt.

Chuỗi sự việc:

1. `InterviewService.GetHintAsync` đọc `statusCode = (int)response.StatusCode` = **200**
   — [InterviewService.cs:383](../../source/WebClient/Services/Interview/InterviewService.cs#L383)
2. Envelope có `Success == false` → trả về tuple `(false, null, message, null, 200)`
   — [InterviewService.cs:391-394](../../source/WebClient/Services/Interview/InterviewService.cs#L391-L394)
3. `InterviewController.Hint` vào nhánh `if (!success)` rồi gọi
   `StatusCode(statusCode == 0 ? 500 : statusCode, ...)`. Vì `statusCode == 200` (không phải 0),
   biểu thức trả về **200** — [InterviewController.cs:513-519](../../source/WebClient/Controllers/Interview/InterviewController.cs#L513-L519)
4. Trình duyệt: `postProxy` thấy `response.ok === true` → **không** ném lỗi
   — [interview-room.js:139](../../source/WebClient/wwwroot/js/pages/interview-room.js#L139)
5. `askHint` chạy tiếp: `data.hint` là `undefined` → `hintText = ""` → hiện bong bóng
   `"💡 "` **rỗng** — [interview-room.js:1012-1014](../../source/WebClient/wwwroot/js/pages/interview-room.js#L1012-L1014)

### Hậu quả

- Người dùng thấy một bong bóng gợi ý trống trơn, không có thông báo lỗi nào.
- Nút Hint **giữ nguyên trạng thái disabled** ([interview-room.js:1021](../../source/WebClient/wwwroot/js/pages/interview-room.js#L1021)),
  trong khi `SessionQuestions.HintUsedAt` đã bị chiếm ở server
  ([SessionRepository.cs:245-254](../../source/InterviewAPI/Repositories/SessionRepository.cs#L245-L254)).
  → **Người dùng mất vĩnh viễn lượt hint duy nhất của câu hỏi đó.**
  Cơ chế hoàn quota `ReleaseHintAsync` không cứu được vì nó chỉ chạy khi *ai-service* ném lỗi
  ([SessionService.cs:1697-1703](../../source/InterviewAPI/Services/SessionService.cs#L1697-L1703)),
  còn ở đây ai-service đã trả 200.

### Cách fix đề xuất

Trong nhánh `!success`, coi mọi status 2xx là lỗi upstream và ánh xạ sang **502 Bad Gateway**
(WebClient đóng vai proxy, upstream trả thân phản hồi không hợp lệ):

```csharp
// InterviewController.cs — Hint()
if (!success)
{
    // A 2xx from upstream that still says Success=false is an upstream contract violation,
    // not a client error — surface it as 502 so the browser takes its error path.
    var httpStatus = statusCode is 0 or (>= 200 and < 300)
        ? StatusCodes.Status502BadGateway
        : statusCode;
    return StatusCode(httpStatus,
        new { error = errorCode, message = error ?? "Could not get a hint." });
}
```

Sau khi fix, luồng trình duyệt đi đúng nhánh `catch` và hiện
`appendBubble("AI", "Could not get a hint: ...")` + bật lại nút Hint
([interview-room.js:1026-1028](../../source/WebClient/wwwroot/js/pages/interview-room.js#L1026-L1028)).
**Đây chính là hành vi được vẽ trong diagram.**

### Cần kiểm tra thêm (cùng khuôn mẫu, CHƯA xác minh hậu quả)

`InterviewController.Message` dùng đúng biểu thức
`statusCode == 0 ? 500 : statusCode` tại
[InterviewController.cs:445](../../source/WebClient/Controllers/Interview/InterviewController.cs#L445).
Nhánh đó còn thêm điều kiện `|| message is null` nên hậu quả có thể khác — cần phân tích riêng
trước khi áp cùng một bản vá. **Không nằm trong phạm vi UC-09 / BL#44.**

**Kết luận sau khi khảo sát `#5 ConductInterviewQA` (lô F, 2026-08-03):** nhánh này **hiện KHÔNG
reachable**. Đã đọc toàn bộ `InterviewSessionController.SendMessage`
(`InterviewSessionController.cs:455-475`): chỉ có 3 đường ra — `Ok(ApiResponse.Ok(result))` (200,
`Success=true`), `StatusCode(ex.StatusCode, ...)` cho `SessionGuardException` (403/409/429), và
`BadRequest(ApiResponse.Fail(...))` (400). Không đường nào tạo ra "2xx kèm `Success=false`", nên
`InterviewService.SendMessageAsync` không bao giờ trả `(false, …, statusCode 2xx)`. Pattern rủi ro
vẫn còn trong code và nên được vá cùng lúc với action `Hint`, nhưng **không gây lỗi hành vi hôm nay**.
Diagram `#5` vì vậy vẽ đúng hành vi thật (không có nhánh "2xx nuốt lỗi").

**Cập nhật (recon 17 file, xem `docs/diagrams/RECON.md` §2.2):** grep lại toàn bộ WebClient xác
nhận pattern này CHỈ tồn tại ở đúng 3 vị trí, tất cả trong `InterviewController.cs`: dòng 316
(action `RecentSessions`, BL#34 — ngoài phạm vi 18 file được vẽ), dòng 445 (action `Message`,
thuộc #5 ConductInterviewQA — hiện KHÔNG reachable thực tế vì `SendMessage` phía API chỉ trả
200/409/429/400), và dòng 518 (action `GetHint`, chính là KI-01 gốc — số dòng đã lệch từ 513-519
sang 518-519 do code thay đổi từ lúc ghi nhận, không phải lỗi trích dẫn). Không phát hiện pattern
tương tự ở bất kỳ controller nào khác (`VerificationController.cs`, `EvaluationController.cs`,
`ProfileController.cs`, ...).

---

## KI-02 — Nhánh lỗi TTS không nhất quán status code (đáng lẽ luôn 502)

| | |
|---|---|
| Phát hiện khi vẽ | `VoiceInteraction` (UC-09, BL#42/43) |
| Mức độ | Thấp — client vẫn nhận được lỗi, nhưng sai contract đã tài liệu hoá |
| Trạng thái | Chưa fix |

### Mô tả
`InterviewSessionController.cs` (action `TextToSpeech`, khoảng dòng 507-522) có 2 nhánh lỗi khác
kiểu: guard audio rỗng trả đúng **502** `{error:"tts_unavailable"}` (dòng ~515-516), nhưng
`catch (Exception ex)` bên dưới (dòng ~519-521 — ví dụ khi `AiServiceClient.SynthesizeSpeechAsync`
ném lỗi do ai-service unreachable/timeout) lại trả **400** `ApiResponse<object>.Fail(...)`. Điều
này trái với ghi chú đã có trong `DIAGRAM_SCOPE.md` ("Nhánh lỗi trả 502 `tts_unavailable`, không
bao giờ 200 body rỗng") — thực tế có 2 status code khác nhau tuỳ loại lỗi.

### Cách fix đề xuất
Gộp `catch (Exception ex)` về cùng 502 `tts_unavailable` như nhánh guard audio rỗng, để giữ đúng
1 contract lỗi duy nhất cho endpoint TTS.

---

## KI-03 — AcceptIntegrityPolicy: response thua race có thể trả giá trị cũ

| | |
|---|---|
| Phát hiện khi vẽ | `AcceptIntegrityPolicy` (UC-09, BL#63/64) |
| Mức độ | Thấp — cửa sổ race hẹp, chưa xác nhận tái diễn thực tế |
| Trạng thái | Chưa fix |

### Mô tả
`SessionService.AcceptIntegrityPolicyAsync` (`SessionService.cs:1648-1669`) dùng atomic
`ExecuteUpdateAsync WHERE !IntegrityPolicyAccepted` (`SessionRepository.cs:234-243`) để tránh ghi
đè khi 2 request đến gần như đồng thời. Tuy nhiên nếu atomic update "thua" (request kia đã accept
trước), hàm build response DTO từ đối tượng `session` đọc **TRƯỚC** atomic update — response cho
request thua có thể báo `Accepted=false`/`AcceptedAt=null` dù DB lúc đó đã `true`.

### Cách fix đề xuất
Sau khi atomic update trả `updated=false`, đọc lại session (hoặc dùng giá trị đã biết là `true`)
trước khi build response, thay vì dùng bản đọc cũ.

---

## KI-04 — Rate limit "25 giây" cho VerifyFaceInSession là config chết

| | |
|---|---|
| Phát hiện khi vẽ | `VerifyFaceInSession` (UC-10, BL#50/51) |
| Mức độ | Trung bình — tài liệu (DIAGRAM_SCOPE.md, comment code) mô tả sai cơ chế thật |
| Trạng thái | Chưa fix (không phải bug hành vi — là tài liệu/comment lệch code) |

### Mô tả
`Biometrics:InSessionMinIntervalSeconds=25` (`InterviewAPI/appsettings.json:24`) KHÔNG được đọc
bằng `_configuration.GetValue` ở bất kỳ đâu trong repo (đã grep toàn bộ `source/`) — chỉ xuất
hiện trong 2 dòng **comment** (`SessionVerificationController.cs:152`, `interview-room.js:1266`)
mô tả nó như cơ chế rate-limit thật. Rate limit thực tế đến từ: mỗi câu hỏi mới chỉ phát đúng 1
`checkId` (`SessionService.cs:217-278`), checkId có TTL = `Biometrics:InSessionCheckTtlSeconds`
(mặc định **20s**, không phải 25s) và one-time-use.

Liên quan: `pollVerificationSchedule()` (`interview-room.js:1545-1587`) là dead code — caller
DUY NHẤT của GET `verify/in-session-check`, nhưng không được gọi từ bất kỳ event/interval nào
khác trong file. checkId luôn đến kèm sẵn trong `nextQuestion.verificationCheckId` do server đính,
không phải do JS chủ động poll.

**Cập nhật (khảo sát lô A, FLOWS/VerifyFaceInSession.md):** phát hiện thêm 1 config key CHẾT thứ
2 cùng gốc — `Biometrics:InSessionCheckCadenceMinutes=10` (`InterviewAPI/appsettings.json:25`)
KHÔNG được đọc ở bất kỳ đâu trong `source/` (đã grep toàn repo, 0 kết quả ngoài chính dòng khai
báo trong appsettings.json). Cùng với `InSessionMinIntervalSeconds`, đây là 2 tàn tích của thiết
kế "cadence ngẫu nhiên" (`InSessionCheckDueResponse.ElapsedMinutes`/`TargetMinutes` cũng thuộc
thiết kế cũ này) đã bị thay bằng cơ chế 1-checkId-cố-định/câu-hỏi đang chạy thật.

### Cách fix đề xuất
Xoá config `InSessionMinIntervalSeconds` VÀ `InSessionCheckCadenceMinutes` (cả 2 đều chết) hoặc
thật sự dùng chúng; cập nhật 2 comment trỏ tới số sai; cân nhắc xoá `pollVerificationSchedule()`/
endpoint `verify/in-session-check` nếu xác nhận không còn caller nào (hoặc audit lại tại sao
endpoint tồn tại mà không ai gọi).

---

## KI-05 — VerifyVoice (BL#67) và GetVerificationChallenge modality=voice unreachable từ UI thật

| | |
|---|---|
| Phát hiện khi vẽ | `VerifyVoiceAnswer` (UC-10, BL#52/67), `GetVerificationChallenge` (BL#66) |
| Mức độ | Thấp — code chết, không gây hành vi sai cho người dùng hiện tại |
| Trạng thái | Chưa fix |

### Mô tả
`biometric-wizard.js` chỉ chuyển sang mode `"verify"` (dòng 267) khi tồn tại 2 phần tử DOM
`#verify-start-btn`/`#verify-session-select` (khai báo dòng 42-43). Trang DUY NHẤT load file JS
này là `Views/Profile/Identity.cshtml` (đã grep xác nhận), và trang này KHÔNG chứa 2 id đó (grep
0 kết quả). `mode` do đó luôn giữ giá trị mặc định `"enroll"` — nhánh "verify" (gồm
`GetChallenge(modality=voice)` và `VerifyVoice`/BL#67, `VerificationController.cs:120`,
`SessionVerificationController.cs:103`) không bao giờ chạy được từ giao diện hiện có trong repo.

### Cách fix đề xuất
Nếu tính năng "verify voice độc lập" (BL#67) không còn cần thiết (đã được thay bằng luồng
verify-voice-answer tích hợp trong Room, BL#52), cân nhắc xoá code chết (JS mode "verify", 2
endpoint .NET, endpoint ai-service `voice/verify-challenge`) hoặc bổ sung lại UI entry point nếu
tính năng vẫn cần dùng độc lập.

---

## KI-06 — PauseResumeSession không dùng atomic UPDATE, có cửa sổ race double-click

| | |
|---|---|
| Phát hiện khi vẽ | `PauseResumeSession` (UC-09, BL#39/40/62/65) |
| Mức độ | Thấp-Trung bình — cần double-click đúng lúc mới kích hoạt |
| Trạng thái | Chưa fix |

### Mô tả
`DIAGRAM_SCOPE.md` mô tả PauseResumeSession có "quota atomic conditional UPDATE", nhưng đọc code
cho thấy chính 2 action `PauseSessionAsync` (`SessionService.cs:1122-1148`) và
`UnpauseSessionAsync` (`SessionService.cs:1150-1177`) dùng check-rồi-set thông thường (đọc
`session`, `if (session.PauseCount >= MaxPauseCount) throw`, rồi `SaveChangesAsync`) — KHÔNG phải
`ExecuteUpdateAsync` có điều kiện. 2 request Pause gần như đồng thời (double-click) có thể vượt
`MaxPauseCount`/`MaxTotalPausedSeconds` trong cửa sổ race hẹp. Atomic UPDATE thật sự chỉ tồn tại
ở nhánh tự hồi phục pause quá hạn (`TryAutoResumeExpiredPauseAsync`, `SessionRepository.cs:315-328`),
là một luồng khác (self-heal), không phải luồng Pause/Unpause chính.

### Cách fix đề xuất
Đổi `PauseSessionAsync`/`UnpauseSessionAsync` sang atomic `ExecuteUpdateAsync` có điều kiện guard
(tương tự pattern đã dùng cho self-heal/`TryConsumeSkipCooldownAsync`/`IncrementAnsweredIfAsync`),
hoặc chấp nhận rủi ro nếu race thực tế hiếm khi xảy ra và ghi chú rõ trong code.

---

## KI-07 — Self-heal auto-resume giả định có poll loop, nhưng WebClient không hề poll `/timer`

| | |
|---|---|
| Phát hiện khi vẽ | `PauseResumeSession` (UC-09, BL#39/40/62/65) |
| Mức độ | Thấp — chỉ ảnh hưởng độ chính xác tài liệu/comment, không phải bug hành vi |
| Trạng thái | Chưa fix (comment lệch code) |

### Mô tả
Comment tại `SessionService.cs:99-100` và `interview-room.js:224-227` khẳng định self-heal auto-
resume chạy "on the poll loop" / "on the next request", nhưng WebClient KHÔNG poll
`api/interview/sessions/{id}/timer` ở bất kỳ đâu (đã grep toàn bộ `InterviewService.cs`).
`GetTimerAsync` (`SessionService.cs:959-980`) hiện là dead endpoint từ góc nhìn Browser — self-
heal chỉ thực sự kích hoạt khi user load lại trang Room hoặc Dashboard (qua `ResumeSessionAsync`/
`GetActiveSessionAsync`), không có polling nền định kỳ.

### Cách fix đề xuất
Sửa comment cho khớp hành vi thật (self-heal chỉ chạy khi chạm session qua request khác, không
phải poll loop định kỳ), hoặc nếu ý định ban đầu là có poll loop thật thì bổ sung lại phần JS gọi
`GetTimerAsync` định kỳ.

---

## KI-08 — Background grading (ScheduleGrading) lỗi âm thầm, không retry, không báo người dùng

| | |
|---|---|
| Phát hiện khi vẽ | `ConductInterviewQA` (BL#56/57/47/59/60), `EvaluateInterviewAnswer` (BL#73/75/70) |
| Mức độ | Trung bình — câu trả lời có thể vĩnh viễn không được chấm điểm mà không ai biết |
| Trạng thái | Chưa fix |

### Mô tả
`SessionService.ScheduleGrading` (`SessionService.cs:1708-1737`) chạy chấm điểm nền qua
`Task.Run` thật sự fire-and-forget: nếu `EvaluationApiClient.EvaluateAnswerAsync` (gọi
EvaluationAPI, sau đó ai-service) ném lỗi, exception chỉ được log (`SessionService.cs:1730-1735`)
— không retry, không có cờ báo cho user hay cơ chế chấm bù sau. Câu trả lời của user vẫn được lưu
bình thường trong `ChatMessages`, nhưng có thể không bao giờ xuất hiện trong `AnswerEvaluations`
mà không có bất kỳ cảnh báo nào trong luồng phỏng vấn hoặc trong báo cáo cuối (`ViewEvaluationReport`
chỉ show `emptyReason` chung chung nếu 0 evaluations, không phân biệt được "chưa chấm" vs "lỗi
chấm").

### Cách fix đề xuất
Thêm cơ chế retry (queue/background job) cho các lần `ScheduleGrading` thất bại, hoặc ít nhất ghi
lại trạng thái "grading failed" ở cấp `SessionQuestion`/`ChatMessage` để `ViewEvaluationReport` có
thể phân biệt và hiển thị cảnh báo phù hợp thay vì im lặng thiếu điểm.

---

## KI-09 — Comment `BiometricEmbeddingResult` nói "Quality is face-only", trái với ai-service

| | |
|---|---|
| Phát hiện khi vẽ | `EnrollVoiceBiometric` (BL#13/12/53) |
| Mức độ | Thấp — comment lệch code, không gây hành vi sai |
| Trạng thái | Chưa fix |

### Mô tả

`InterviewAPI/DTOs/AiService/BiometricEmbeddingResult.cs:6` ghi:

```csharp
// Mirrors ai-service POST /internal/biometrics/{face|voice}/embed JSON
// (snake_case field names). Quality is face-only; null for voice.
```

Thực tế **ai-service LUÔN trả `quality` cho voice**:

- `ai-service/biometrics/voice_engine.py:70` tính `quality = min(1.0, duration / (VOICE_MIN_DURATION_SEC * 2))`
  và `:73` đưa vào dict trả về.
- `ai-service/biometrics/schemas.py:16-21` khai báo `VoiceEmbedResponse.quality: float` — **không**
  nullable, nên Pydantic sẽ lỗi nếu thiếu.

Hệ quả thực tế: `ProfileBiometricsService.EnrollAsync` ghi thẳng `result.Quality` xuống cột
`ProfileBiometrics.Quality` cho cả 2 modality (`ProfileBiometricsService.cs:68, 79`), nên dữ liệu
đúng — chỉ có comment sai, dễ khiến người đọc sau này tưởng cột `Quality` của hàng `Modality="Voice"`
luôn NULL và viết truy vấn/logic sai theo.

### Cách fix đề xuất

Sửa comment thành mô tả đúng, ví dụ:

```csharp
// Quality is populated for BOTH modalities: face = det_score x blur heuristic
// (face_engine.py), voice = duration heuristic (voice_engine.py). Nullable here only
// because the C# record mirrors a shared shape, not because voice omits it.
```

### Ghi chú liên quan (không phải lỗi code)

`docs/diagrams/DIAGRAM_SCOPE.md` kỳ vọng bước embed voice có nhánh 422 "quá ồn". Code KHÔNG có gate
đó — `voice_engine.py:51-58` chỉ có đúng 2 lý do: `"Cannot decode audio"` và `"Audio too short"`.
Đây là lệch **tài liệu kế hoạch**, không phải lỗi mã nguồn, nên không lập KI riêng; diagram
`EnrollVoiceBiometric` vẽ theo code thật (2 lý do).

---

## KI-10 — Comment của `SttResponse` (WebClient) trỏ sai DTO nguồn

| | |
|---|---|
| Phát hiện khi vẽ | `VerifyVoiceAnswer` (BL#52) |
| Mức độ | Thấp — comment lệch code |
| Trạng thái | Chưa fix |

### Mô tả

`WebClient/Models/Interview/SttResponse.cs:3` ghi:

```csharp
// Mirrors InterviewAPI.DTOs.Session.SttResponse (transcribed voice answer).
```

Nhưng `InterviewAPI/DTOs/Session/SttResponse.cs` chỉ có **1 field** (`Text`), trong khi lớp WebClient
có **9 field** (`Text`, `VoiceChecked`, `VoiceMatched`, `Score`, `StrikeCount`, `MaxStrikes`,
`ShouldEndSession`, `Duplicate`, `Message`). Thứ nó thật sự mirror là
`InterviewAPI/DTOs/Biometrics/VoiceAnswerVerifyResponse.cs` — đúng kiểu trả về của endpoint
`POST api/interview/sessions/{sessionId}/verify/voice-answer` mà `InterviewService.TranscribeAudioAsync`
gọi (`InterviewService.cs:359-372`).

### Hậu quả

Người đọc đối chiếu theo comment sẽ thấy 8 field "thừa" và dễ kết luận nhầm rằng phần xử lý
strike gian lận trong `interview-room.js:1111-1121` là code chết — thực tế nó chạy bình thường.
(`InterviewAPI.DTOs.Session.SttResponse` vẫn còn được dùng, nhưng ở endpoint khác:
`InterviewSessionController.cs:544`.)

### Cách fix đề xuất

Sửa comment trỏ đúng nguồn:

```csharp
// Mirrors InterviewAPI.DTOs.Biometrics.VoiceAnswerVerifyResponse — the transcript PLUS the
// speaker-verification outcome returned by .../verify/voice-answer.
```

---

## KI-11 — 4 nhánh "bỏ qua kiểm giọng" đặt `Message` nhưng người dùng không bao giờ thấy

| | |
|---|---|
| Phát hiện khi vẽ | `VerifyVoiceAnswer` (BL#52) |
| Mức độ | Trung bình — người dùng tưởng đã được kiểm giọng trong khi thực tế bước đó bị bỏ qua |
| Trạng thái | Chưa fix |
| Diagram liên quan | `out/VerifyVoiceAnswer.drawio`, khung `opt E` và `opt H` |

### Mô tả

`BiometricVerificationService.VerifyVoiceAnswerAsync` có 4 đường trả về sớm, mỗi đường đặt một
`Message` khác nhau và `VoiceChecked = false`:

| Nhánh | File:Line | `Message` |
|---|---|---|
| `Biometrics:Enabled == false` | `BiometricVerificationService.cs:442-450` | *(không set — chỉ `VoiceChecked=false`)* |
| Chưa enroll voice | `:454-463` | `"Voice verification is unavailable because no voice enrollment was found."` |
| `BiometricQualityException` | `:490-499` | `"Voice verification could not assess this recording."` |
| Exception khác | `:500-509` | `"Voice verification is temporarily unavailable."` |

Phía Room, `interview-room.js:1111-1121` chỉ đọc `data.message` **bên trong** nhánh
`if (data.voiceMatched === false)`. Ở cả 4 đường trên `VoiceMatched` là `null`
(`VoiceAnswerVerifyResponse.VoiceMatched` chỉ được gán trong `BuildVoiceAnswerResponseAsync:572`),
nên `data.voiceMatched === false` là **false** → khối đó không chạy và `data.message` không bao giờ
được hiển thị.

### Hậu quả

- Ứng viên chưa enroll giọng vẫn đọc câu trả lời bằng micro và **không hề biết** bước xác minh
  giọng đã bị bỏ qua hoàn toàn.
- Khi ai-service tạm thời hỏng, hệ thống im lặng bỏ qua kiểm giọng — không có tín hiệu nào cho
  người dùng lẫn cho người vận hành ở phía UI.
- Cùng khuôn với KI-08 (lỗi nền im lặng): dữ liệu vẫn đúng, nhưng thiếu phản hồi.

### Cách fix đề xuất

Ở `transcribe()` (`interview-room.js:1105-1127`), xử lý riêng trường hợp "đã bỏ qua kiểm giọng"
trước khi kiểm tra mismatch:

```js
if (data.voiceChecked === false && data.message) {
    setCheatingWarning(data.message, "notice");   // notice, không phải cảnh báo gian lận
}
```

Đồng thời cân nhắc set `Message` cho cả nhánh `Biometrics:Enabled == false` (hiện đang để trống)
nếu muốn thông điệp nhất quán.

---

## KI-12 — ai-service `POST /evaluate` không kiểm khoá nội bộ và không có caller nào

| | |
|---|---|
| Phát hiện khi vẽ | `EvaluateInterviewAnswer` (BL#73/75/70) |
| Mức độ | Trung bình — endpoint mở, chạy được pipeline chấm điểm Gemini mà không cần khoá |
| Trạng thái | Chưa fix |

### Mô tả

`ai-service/app/api/evaluation.py:16-18`:

```python
@router.post("/evaluate", response_model=EvaluationReport)
def run_evaluation(transcript: Transcript):
    return evaluate(transcript.model_dump())
```

Hai vấn đề song song:

1. **Không có guard khoá nội bộ.** Mọi endpoint `/internal/*` đều gọi `_check_key(x_internal_api_key)`
   (`app/api/internal.py:21-24`) và toàn bộ router `biometrics` gắn
   `dependencies=[Depends(require_internal_api_key)]` (`biometrics/router.py:36-37`). Riêng
   `/evaluate` **không có gì cả**, mà `app/main.py:57` vẫn `include_router(evaluation.router)`
   và app không có middleware xác thực chung (`main.py:42-60` chỉ có CORS).
   Container ai-service map ra host `:8080` (xem `CLAUDE.md`), nên endpoint này gọi được từ máy host.
   Nó chạy `evaluator.evaluate()` → `GeminiJudge` cho **mọi** transcript gửi tới → đốt quota Gemini
   trả phí, không giới hạn.

2. **Không có caller.** Đã grep toàn bộ `source/` (`grep -rn '/evaluate' --include=*.cs`, loại trừ
   `evaluate-answer`): **0 kết quả**. Luồng thật của DevPrep dùng
   `POST /internal/evaluate-answer` (từng câu) + `POST /internal/report` (tổng hợp).
   `app/api/evaluation.py` và `evaluator.evaluate()` là đường chấm điểm **song song, không dùng tới**.

### Cách fix đề xuất

Chọn một trong hai, tuỳ ý định:

- **Nếu không còn cần**: xoá `app/api/evaluation.py`, bỏ dòng `include_router(evaluation.router)`
  (`main.py:57`), và cân nhắc xoá `evaluator.evaluate()` nếu không chỗ nào khác gọi.
- **Nếu vẫn muốn giữ làm công cụ thử/nghiệp vụ tương lai**: thêm guard giống các endpoint khác —

```python
@router.post("/evaluate", response_model=EvaluationReport)
def run_evaluation(transcript: Transcript,
                   x_internal_api_key: str | None = Header(default=None)):
    _check_key(x_internal_api_key)
    return evaluate(transcript.model_dump())
```

hoặc gắn `dependencies=[Depends(require_internal_api_key)]` ngay ở `APIRouter(...)`.

---

## KI-13 — Fetch catalog không kiểm `response.ok`, lỗi server hiện ra thông điệp SAI

| | |
|---|---|
| Phát hiện khi vẽ | `ConfigureInterviewSession` (BL#31/32/33/36) |
| Mức độ | Thấp-Trung bình — người dùng bị báo sai nguyên nhân, và thông điệp dự phòng đã viết sẵn không bao giờ chạy |
| Trạng thái | Chưa fix |
| Diagram liên quan | `out/ConfigureInterviewSession.drawio`, khung `opt A` |

### Mô tả

`source/WebClient/wwwroot/js/pages/dashboard.js:94-113`:

```js
fetch(catalogUrl)
    .then((response) => response.json())          // <-- KHÔNG kiểm response.ok
    .then((catalog) => {
        techByRole = catalog.technologies || {};
        if (Array.isArray(catalog.roles) && catalog.roles.length) { ... }
        renderFocus();
    })
    .catch(() => {
        focusBox.innerHTML =
            '<span class="dash-hint">Could not load technologies. You can still start; the whole role will be covered.</span>';
    });
```

Khi ai-service `/catalog` hỏng, chuỗi lỗi kết thúc ở
`InterviewController.Catalog` → `BadRequest(new { error = ... })`
(`source/WebClient/Controllers/Interview/InterviewController.cs:144-147`).

Đó vẫn là JSON hợp lệ, nên `response.json()` **thành công** → `.catch` KHÔNG chạy. Thay vào đó
`.then` chạy với `catalog = { error: "..." }`:

- `techByRole = catalog.technologies || {}` → `{}`
- `catalog.roles` là `undefined` → không phải mảng → dropdown Job Role giữ nguyên 2 option
  hard-code `Backend`/`Frontend` (`Views/Interview/Index.cshtml:55-57`)
- `renderFocus()` → `techs = techByRole[role] || []` rỗng →
  `focusBox.innerHTML = '<span class="dash-hint">No technologies found for this role.</span>'`
  (`dashboard.js:53-57`)

### Hậu quả

Người dùng đọc **"No technologies found for this role."** — sai sự thật: role đó CÓ technology,
chỉ là lời gọi thất bại. Thông điệp đúng đã được viết sẵn ở `.catch` nhưng không bao giờ hiển thị.
`.catch` chỉ chạy khi fetch bị reject ở tầng mạng hoặc body không parse được JSON.

### Cách fix đề xuất

```js
fetch(catalogUrl)
    .then((response) => {
        if (!response.ok) throw new Error("catalog request failed");
        return response.json();
    })
    .then((catalog) => { ... })
    .catch(() => { ...thông điệp dự phòng... });
```

### Ghi chú liên quan (không phải bug)

Cùng lúc khảo sát, ghi nhận thêm 2 điểm KHÔNG lập KI:

1. Factory mặc định của `ConfigLevelAsync`/`ConfigInteractionAsync`
   (`SessionService.cs:369-381, 403-415`) hard-code `DurationMinutes=30`/`Level="Junior"` — không
   tới được từ UI vì `StartInterviewAsync` luôn gọi duration trước và luôn gửi `SessionId` cho 2
   lệnh sau. Vẫn là code sống nếu gọi thẳng API.
2. Lỗi ở lệnh config thứ 2/3 để lại hàng `InterviewSessions` (`Status="Configured"`) đã INSERT ở
   lệnh đầu, không ai dọn. Không gây lỗi cho user (phiên `Configured` không chặn Start lần sau),
   chỉ là hàng thừa trong DB.
