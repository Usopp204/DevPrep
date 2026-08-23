# PHẠM VI VẼ DIAGRAM — Tran Quang Duyen

> Bản tổng hợp khớp giữa **backlog** (`SEP490_Backlog__2_.xlsx`) và **bảng Use Case của nhóm**.
> Đặt tại `docs/diagrams/DIAGRAM_SCOPE.md`.
>
> **Nguyên tắc:** bảng UC của nhóm là chuẩn. Sequence diagram bám theo UC, không cắt ngang
> ranh giới UC. Mỗi file = 1 chức năng, 2 trang (Sequence + Class).

---

## 0. Con số

| | |
|---|---|
| Dòng backlog gán `Tran Quang Duyen` | 49 |
| Dòng loại (chức năng của người khác) | 5 → BL#34, 35, 58, 71, 72 |
| Dòng vẽ nhưng KHÔNG tính phủ backlog | 1 → BL#67 (endpoint tồn tại, không có UI entry point thật — xem KI-05 trong `KNOWN_ISSUES.md`; vẫn vẽ trong file #16 kèm note, chỉ không tính là "đã phủ") |
| **Dòng phải vẽ (tính phủ backlog)** | **43** |
| **Số file** | **18** |
| **Số diagram** | **36** (18 × 2 trang) |
| Use Case liên quan | UC-07, UC-09, UC-10, UC-11 |

---

## 0-bis. BẢNG SỐ FILE — nguồn duy nhất cho nhãn `[see #N]`

> **Bắt buộc tra bảng này trước khi viết `[see #N]` vào bất kỳ nhãn message nào.**
> Lý do bảng tồn tại: `EnterInterviewRoom` từng không có số (nó tách ra từ #4 SAU khi danh
> sách 18 file bên dưới được chốt), nên một nhãn `[see #7]` đã được viết và trỏ nhầm sang
> `GenerateInterviewHint`. Mọi file mới PHẢI được cấp số ở đây ngay khi tạo.

| # | File | # | File |
|---|---|---|---|
| 1 | `EnrollFaceBiometric` | 12 | `GetVerificationChallenge` |
| 2 | `EnrollVoiceBiometric` | 13 | `VerifyFaceEntryGate` |
| 3 | `ConfigureInterviewSession` | 14 | `ValidateEntryGateStatus` |
| 4 | `StartInterviewSession` | 15 | `VerifyFaceInSession` |
| 5 | `ConductInterviewQA` | 16 | `VerifyVoiceAnswer` |
| 6 | `VoiceInteraction` | 17 | `EvaluateInterviewAnswer` |
| 7 | `GenerateInterviewHint` | 18 | `ViewEvaluationReport` |
| 8 | `PauseResumeSession` | **19** | **`EnterInterviewRoom`** |
| 9 | `EndInterviewSession` | **20** | **`ChargeAndActivateSession`** |
| 10 | `ReportCameraViolation` | **21** | **`ResumeActiveSession`** |
| 11 | `AcceptIntegrityPolicy` | **22** | **`CheckBiometricEnrollment`** |

`#1`–`#18` là danh sách gốc ở mục 1 dưới đây. `#19`–`#22` bổ sung 2026-08-14: `#19` vốn là
phần tách khỏi `#4`; `#20` tách khỏi `#19` (xem mục 3.5); `#21`/`#22` là chức năng có thật
trong code nhưng trước đó chưa có diagram.

---

## 1. Danh sách 18 file

### UC-07 — Manage Profile (phần sinh trắc học)

| # | File | BL# | Ghi chú vẽ |
|---|---|---|---|
| 1 | `EnrollFaceBiometric` | 11, 12, 53 | alt 422 quality code (no face / multi face / blurry); opt trao bonus 100 credit |
| 2 | `EnrollVoiceBiometric` | 13, 12, 53 | alt 422 (quá ngắn / quá ồn); bonus chỉ kích hoạt khi đã đủ CẢ face lẫn voice |

> BL#12 (`GET /api/profiles/me/biometrics`) xuất hiện ở cả 2 file — là bước đọc trạng thái
> mở đầu, luồng ~4 message, tách riêng sẽ thành diagram rỗng. Ghi BL#12 ở cả hai để tra chéo.

### UC-09 — Manage Interview

| # | File | BL# | Ghi chú vẽ |
|---|---|---|---|
| 3 | `ConfigureInterviewSession` | 31, 32, 33, 36 | alt theo loại config (duration / level / interaction) + đọc catalog |
| 4 | `StartInterviewSession` | 55, 45, 46, 54, 38 | trừ credit 2 credit/phút; opt refund khi init lỗi; opt room tour lần đầu |
| 5 | `ConductInterviewQA` | 56, 57, 47, 59, 60 | RAG → ChromaDB → Gemini; alt content filter (profanity / gibberish); timer seed |
| 6 | `VoiceInteraction` | 42, 43 | **TTS + STT chung 1 file** (khớp bubble "Voice Interaction (TTS/STT)") |
| 7 | `GenerateInterviewHint` | 44 | **PILOT — vẽ đầu tiên** |
| 8 | `PauseResumeSession` | 39, 40, 62, 65 | ⚠️ Pause/Unpause chính KHÔNG atomic (check-rồi-set thường, `SessionService.cs:1122-1177`) — xem KI-06; atomic `ExecuteUpdateAsync` chỉ có ở nhánh self-heal tự hồi phục pause quá hạn (`SessionRepository.cs:315-328`, luồng khác); giữ draft qua `sessionStorage` |
| 9 | `EndInterviewSession` | 37, 61 | phân biệt complete (hết giờ/hết câu) vs end (chủ động) |
| 10 | `ReportCameraViolation` | 41 | tăng `CameraViolationCount` |
| 11 | `AcceptIntegrityPolicy` | 63, 64 | ⚠️ **chưa có ê-líp trong bảng UC — xem mục 3.1** |

**Ghi chú file #6:** BL#43 (`POST /sessions/stt`) hiện web client KHÔNG gọi trực tiếp —
transcript trả kèm trong endpoint verify voice-answer (BL#52). Vẽ như nhánh `opt` kèm note
giải thích, KHÔNG vẽ như luồng người dùng chủ động gọi.

**Ghi chú file #6 (TTS):** có 2 lớp alt lồng nhau —
`alt TTS_PROVIDER` (Piper local / ElevenLabs legacy) × `alt cache hit / cache miss`.
⚠️ Nhánh lỗi hiện KHÔNG nhất quán: guard audio rỗng trả đúng **502 `tts_unavailable`**
(`InterviewSessionController.cs` ~515-516), nhưng `catch (Exception ex)` bên dưới (~519-521 —
ví dụ ai-service unreachable/timeout) lại trả **400** `ApiResponse<object>.Fail(...)` — xem KI-02
trong `KNOWN_ISSUES.md`. **Diagram vẽ theo hành vi ĐÚNG** (luôn 502 `tts_unavailable` cho mọi lỗi
synth) kèm 1 `note` chỉ rõ đây là hành vi mong muốn, khác code hiện tại — đúng quy ước "vẽ theo
hành vi đúng sau khi fix" ở đầu `KNOWN_ISSUES.md`.
Class diagram vẫn phải vẽ client ElevenLabs vì code còn giữ sau cờ cấu hình.

### UC-10 — Verify Identity (Face & Voice) — nhóm đã chốt "Tách 1 → 5"

| # | File | BL# | Bubble UC tương ứng |
|---|---|---|---|
| 12 | `GetVerificationChallenge` | 66 | Get Verification Challenge |
| 13 | `VerifyFaceEntryGate` | 48 | Verify Face (Entry Gate) |
| 14 | `ValidateEntryGateStatus` | 49 | Validate Entry Gate Status |
| 15 | `VerifyFaceInSession` | 50, 51 | Verify Face (In-Session) |
| 16 | `VerifyVoiceAnswer` | 52, 67 | Verify Voice Answer |

**Ghi chú #13:** alt 3 lần thử; **chưa trừ credit cho tới khi pass**.
**Ghi chú #15:** ⚠️ KHÔNG có rate limit "25 giây/lần" thật trong code —
`Biometrics:InSessionMinIntervalSeconds=25` là config CHẾT, không được đọc ở đâu trong `source/`
(xem KI-04). Cơ chế thật: mỗi câu hỏi mới phát đúng 1 `checkId` một-lần-dùng
(`SessionService.cs:217-278`), TTL = `Biometrics:InSessionCheckTtlSeconds`, mặc định **20 giây**.
Vẽ theo cơ chế checkId+TTL thật, KHÔNG vẽ một guard "cách nhau 25s" không tồn tại. Còn có: strike
accumulation; alt multi-face / no-face.
**Ghi chú #16:** BL#67 (`POST verify/voice`) và BL#52 (`POST verify/voice-answer`) là 2
endpoint khác nhau — vẽ chung 1 file, phân bằng khung alt. ⚠️ BL#67 **vẫn phải vẽ** (nhánh alt
riêng, có khung rõ ràng) nhưng **không tính vào số dòng backlog đã phủ** (xem bảng ở mục 0) —
recon xác nhận nhánh "verify" của `biometric-wizard.js` (nơi duy nhất có thể kích hoạt BL#67)
không bao giờ chạy được vì trang `Identity.cshtml` (trang duy nhất load file JS đó) không có 2
phần tử DOM mà nó cần (`#verify-start-btn`/`#verify-session-select`) — xem KI-05 trong
`KNOWN_ISSUES.md`. **Bắt buộc thêm 1 `note` trên diagram tại nhánh BL#67** ghi rõ: "endpoint tồn
tại và chạy đúng, nhưng hiện không có lối vào từ UI — xem KI-05".

### UC-11 — Manage Evaluation & Reporting (phần của Duyen)

| # | File | BL# | Ghi chú vẽ |
|---|---|---|---|
| 17 | `EvaluateInterviewAnswer` | 73, 75, 70 | background grading; IDOR check qua `/owns`; guard chống chấm trùng |
| 18 | `ViewEvaluationReport` | 74, 76, 68, 69 | alt report đã tồn tại / phải sinh mới; check eligibility |

> ⚠️ UC-11 là UC **dùng chung với thành viên khác** — xem mục 3.3.

---

## 2. Dòng backlog KHÔNG vẽ

| BL# | Task | Endpoint | Lý do |
|---|---|---|---|
| 34 | List User Interview Sessions | `GET /api/interview/sessions` | chức năng của thành viên khác |
| 35 | Get Interview Transcript | `GET /api/interview/sessions/{id}/transcript` | nt |
| 58 | Delete Interview Session | `DELETE /api/interview/sessions/{id}` | nt |
| 71 | Get User Sessions (internal) | `GET /api/internal/interviews/sessions` | nt |
| 72 | Get Last Interview Activity (internal) | `POST /api/internal/interviews/last-activity` | nt |

**Việc cần làm với nhóm:** cột `Assigned to` của 5 dòng này trong backlog đang ghi
`Tran Quang Duyen`. Nếu chức năng thuộc người khác thì phải sửa lại tên trong backlog
TRƯỚC KHI NỘP — nếu không, khi chấm sẽ bị hỏi "5 chức năng này của bạn sao không có diagram?".

---

## 3. Điểm vênh cần nhóm xử lý

### 3.1. `AcceptIntegrityPolicy` không có ê-líp trong bảng UC

BL#63 (`GET .../integrity-policy`) và BL#64 (`POST .../integrity-policy/accept`) đã chạy
thật trong code nhưng bảng UC không có bubble nào. Đây là **thiếu sót của bảng UC**.

→ Đề nghị nhóm bổ sung 1 bubble vào **UC-09** (hoặc UC-10), tên `Accept Integrity Policy`.
Đây là bước người dùng phải bấm đồng ý → đủ tiêu chuẩn use case.

### 3.2. Ghi chú UC-10 nói "rows #48-52" nhưng thực tế là 7 dòng

Bảng UC ghi *"5 endpoint riêng biệt trong `SessionVerificationController` (rows #48-52)"*.
Thực tế phần verify gồm **7 dòng backlog**: #48, 49, 50, 51, 52, **66**, **67**.
Hai dòng #66 (`GET verify/challenge`) và #67 (`POST verify/voice`) bị bỏ sót khỏi ghi chú.

→ Đề nghị sửa ghi chú UC-10 thành "rows #48-52, #66-67".

### 3.3. UC-11 do 2 người cùng làm

UC-11 chứa cả `View Session History`, `View Chat Log`, các bubble Feedback — thuộc người khác;
và `View Performance Report` — thuộc Duyen.

→ Duyen chỉ vẽ file #17 và #18. Cần ghi rõ trong báo cáo để không bị hiểu là bỏ sót.
Cần thống nhất với người làm phần còn lại của UC-11 để không vẽ trùng.

### 3.4. Ghi chú UC-09 tự nhận sơ đồ đang thiếu

Bảng UC-09 ghi *"Sơ đồ hiện chỉ vẽ 4 nhánh — thực tế phòng phỏng vấn có rất nhiều hành động
khác đã chạy thật trong backlog + code (rows #32, #36, #39-46, #57, #59, #61)"*.

→ Use case diagram cần cập nhật cho khớp. 18 file trong tài liệu này đã phủ đủ các rows đó.

---

## 4. Quy ước đặt tên trang

Tên **file** không chứa mã UC (giữ ổn định vĩnh viễn):

```
docs/diagrams/out/GenerateInterviewHint.drawio
```

Mã UC nằm trong **tên trang**, sửa bằng 1 dòng YAML:

```yaml
feature: GenerateInterviewHint
title: Generate Interview Hint
uc_id: "UC-09"
backlog: [44]
```

| Trạng thái | Tên trang |
|---|---|
| Chưa gán UC | `Sequence - Generate Interview Hint [BL#44]` |
| Đã gán UC | `Sequence - UC-09 Generate Interview Hint [BL#44]` |

`BL#` là traceability về backlog — chứng minh 18 file phủ đủ 43 dòng tính phủ backlog (44 dòng
"phải vẽ" trừ đi BL#67 — vẫn vẽ trong file #16 nhưng không tính phủ, xem mục 0 và ghi chú #16).

---

## 5. Thứ tự thực hiện

| Đợt | File | Lý do |
|---|---|---|
| **Pilot** | #7 `GenerateInterviewHint` | luồng đủ dạng (WebClient → proxy → InterviewAPI → ai-service → Gemini) nhưng ít nhánh — dùng để hiệu chỉnh generator |
| Đợt 1 | #12, 13, 14 | UC-10 phần ngắn, kiểm khung alt cơ bản |
| Đợt 2 | #3, 8, 9, 10, 11 | UC-09 phần CRUD/lifecycle |
| Đợt 3 | #1, 2, 15, 16 | sinh trắc học — nhiều nhánh 422 / strike |
| Đợt 4 | #4, 5, 6 | phức tạp nhất: credit + refund, RAG, TTS 2 lớp alt |
| Đợt 5 | #17, 18 | UC-11, có actor hệ thống (background job) |

**Ngoại lệ spec ở đợt 5:** file #17 chạy nền (background grading), không có actor người trực
tiếp. Spec mục 1.1 bắt buộc participant đầu là actor hình người → vẽ `User` kích hoạt gián
tiếp (bấm End Session), luồng sau đó chạy async, kèm `note` giải thích. Cần duyệt trước khi vẽ.
