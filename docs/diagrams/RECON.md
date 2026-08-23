# RECON — Khảo sát diện rộng 17 chức năng còn lại

> Bước lập kế hoạch. KHÔNG chứa YAML, KHÔNG build, KHÔNG vẽ. Mọi File:Line trong tài liệu này
> đã được đọc thật (bởi tôi hoặc 5 subagent song song do tôi giao việc và tự đối chiếu kết quả).
> Nơi không tra ra được ghi thẳng "CHƯA TRA ĐƯỢC".

---

## BƯỚC 0 — Xác minh môi trường

### 0.0 Sai lệch so với yêu cầu đọc trước

Yêu cầu đọc 3 file `HANDOFF.md`, `QUY_TAC_VE_DIAGRAM.md`, `SPEC_SCHEMA.md` trước khi bắt đầu.
**`docs/diagrams/HANDOFF.md` KHÔNG TỒN TẠI** trong repo (đã tìm toàn bộ `Q:\DevPrep` bằng
`find -iname "*handoff*"`, không có kết quả). `docs/diagrams/` hiện có các file thay thế nội
dung tương đương: `DIAGRAM_SCOPE.md`, `LAYOUT_CONSTANTS.md`, `KNOWN_ISSUES.md`. Đã đọc cả 3
file này thay cho HANDOFF.md — nội dung KI-01 trích dẫn trong đề bài (`InterviewController.cs:513-519`)
khớp đúng với `KNOWN_ISSUES.md` hiện có, xác nhận đây là nguồn thay thế hợp lệ. Các tham chiếu
"HANDOFF §12/§13" trong đề bài đã được trả lời độc lập ở mục 0.3 bên dưới bằng cách đọc trực
tiếp `SPEC_SCHEMA.md` + mã nguồn generator (`_tools/*.py`), không phụ thuộc HANDOFF.md.

### 0.1 Kiểm kê `docs/diagrams/_tools/`

```
build.py   cls.py   emit.py   seq.py   validate.py
```
Đủ 5 file theo kỳ vọng (không có file thừa/thiếu). Có thêm thư mục `__pycache__` (không tính).

### 0.2 Regression `python build.py login`

Output nguyên văn:
```
-> Q:\DevPrep\docs\diagrams/out/Login.drawio  | 98 message | 35 class
VALIDATE: OK
```
**Khớp 100% kỳ vọng** (98 message | 35 class | VALIDATE: OK). Generator còn nguyên vẹn.

### 0.3 Ba câu hỏi từ SPEC_SCHEMA.md

**(a) YAML có cơ chế include/import/anchor để tái dùng khối class giữa nhiều file spec không?**
**KHÔNG CÓ.** Xác nhận bằng cách đọc `_tools/build.py:9-22`: mỗi feature load đúng 1 file
`specs/<feature>.yaml` + 1 file `specs/<feature>_classes.yaml` qua `yaml.safe_load(open(...))`
độc lập, không có bước merge/include nào. `cls.py`/`seq.py`/`emit.py` cũng không xử lý khóa nào
kiểu `include:`/`<<:`/YAML anchor (`&`/`*`). Muốn dùng lại 1 class (vd `ApiResponse<T>`,
`SessionService`) ở nhiều file class-spec, hiện tại PHẢI copy-paste định nghĩa đó vào từng
`<feature>_classes.yaml` riêng — không có cơ chế chia sẻ.

**(b) Có trường activate/deactivate cho activation bar chưa?**
**CHƯA CÓ.** Trường hợp lệ của 1 message trong `flow` chỉ có `d, t, k, l, self` (SPEC_SCHEMA.md
dòng 40-48, xác nhận lại trong code `seq.py:32-54` hàm `_walk`, không đọc khóa nào khác).
Activation bar được **tự động suy ra** từ span (touch đầu tiên → touch cuối cùng) của mỗi
participant trong toàn bộ message list — xem `seq.py:57-64` hàm `_bars`. Không có cách khai báo
thủ công điểm activate/deactivate trong YAML.

**(c) Chiều cao/rộng trang cấu hình ở đâu, có tự giãn khi message nhiều không?**
**Tự tính, tự giãn — không khai báo trong YAML.** Trang Sequence: `seq.py:76-77`
`page_h = int(end_y + FOOT_GAP + 60)` (phụ thuộc `end_y` = kết quả đệ quy `_walk` cộng dồn theo
số message + độ sâu frame), `page_w = int(parts[-1]["x"] + parts[-1]["w"] + MARGIN_R)` (phụ thuộc
số participant). Trang Class: `cls.py:104-105` `pw`/`ph` = max(x/y + width/height của mọi hộp) + 100.
→ Càng nhiều message/participant/class thì trang càng giãn tự động, KHÔNG có giới hạn cứng nào
chặn lại trong code — file mẫu Login (2350×3800) chỉ là kích thước thực tế đo được, không phải
hằng số cấu hình.

### 0.4 Kích thước file mẫu (mốc ước lượng công cho 17 file)

```
specs/login.yaml            482 dòng
specs/login_classes.yaml    512 dòng
Tổng                        994 dòng cho 1 chức năng (98 message | 35 class)
```
→ Ước lượng thô: 17 file còn lại × ~994 dòng/file ≈ **~16.900 dòng YAML** nếu độ phức tạp trung
bình tương đương Login. Thực tế sẽ lệch nhiều — xem xếp hạng quy mô ở mục 2.3 (một số chức năng
nhỏ hơn Login nhiều lần, một số như `StartInterviewSession` ước tính VƯỢT Login).

---

## BƯỚC 1 — Recon 17 chức năng

> 17 card dưới đây do 5 subagent chỉ-đọc khảo sát song song (nhóm theo UC/độ liên quan), tôi đã
> tự grep đối chiếu độc lập phần KI-01 (mục 2.2) và spot-check các route Gateway trước khi giao
> việc. Định dạng giữ nguyên mẫu đề bài.

### #1 EnrollFaceBiometric — BL#11, 12, 53
- Entry point: `source/WebClient/Views/Profile/Identity.cshtml:35` (`data-enroll-face-url`), kích hoạt từ `source/WebClient/wwwroot/js/pages/biometric-wizard.js:397-399` (`runFaceCapture`)
- Chuỗi gọi:
    Browser `biometric-wizard.js:397-399`
     -> WebClient Controller `Controllers/Profile/ProfileController.cs:166-187` (`EnrollFace`)
     -> WebClient Service `Services/Profile/BiometricsService.cs:39-58` (`EnrollAsync`, POST `api/profiles/me/biometrics/face`)
     -> Gateway route `ApiGateway/appsettings.json:61-66` (`profiles-biometrics-route`, KHÔNG strip prefix)
     -> API Controller `InterviewAPI/Controllers/ProfileBiometricsController.cs:41-43` (`EnrollFace`) → helper `:50-71`
     -> API Service `InterviewAPI/Services/ProfileBiometricsService.cs:44-93` (`EnrollAsync`)
     -> AiServiceClient `InterviewAPI/Clients/AiServiceClient.cs:136-137,142-172` (`EmbedFaceAsync`, POST `/internal/biometrics/face/embed`)
     -> Repository `InterviewAPI/Repositories/ProfileBiometricsRepository.cs:16-19,31-35,37` (`GetAsync`/`AddAsync`/`SaveChangesAsync`)
     -> ai-service `ai-service/biometrics/router.py:49-56` (`embed_face`) → `ai-service/biometrics/face_engine.py:74-95` (InsightFace)
    (opt BL#53) `ProfileBiometricsService.cs:125-166` (`TryAwardEnrollmentBonusAsync`) -> `PaymentApiClient.cs:115-162` (`AwardCreditAsync`, POST `/api/internal/credit/award`, gọi THẲNG PaymentAPI :7008, KHÔNG qua Gateway — `InterviewAPI/Program.cs:49,80-83`)
    (BL#12, dùng chung với #2) `ProfileController.cs:134-161` -> `BiometricsService.cs:25-37` (GET `api/profiles/me/biometrics`) -> Gateway `appsettings.json:55-60` -> `ProfileBiometricsController.cs:25-38` -> `ProfileBiometricsService.cs:198-210` -> Repository `:21-24`
- Endpoint HTTP: `POST /Profile/EnrollFace` (WebClient proxy) → `POST /api/profiles/me/biometrics/face` (Gateway/InterviewAPI) → ai-service `POST /internal/biometrics/face/embed`; bonus `POST /api/internal/credit/award` (PaymentAPI trực tiếp)
- Participant dự kiến: 10 — Browser, ProfileController, BiometricsService (WebClient), ApiGateway, ProfileBiometricsController, ProfileBiometricsService, ProfileBiometricsRepository+SQL Server, AiServiceClient, ai-service (InsightFace), PaymentApiClient/PaymentAPI
- Số message ƯỚC LƯỢNG: ~26
- Khung rẽ nhánh: 2 alt (422 quality: no-face/multi-face/blurry/… — `ai-service/biometrics/face_engine.py:56,63,65,84-90`; 409 re-enroll mismatch — `ProfileBiometricsService.cs:95-117`, threshold 0.35) / 1 opt (bonus BL#53, chỉ khi voice đã enroll — tự rẽ Awarded/AlreadyAwarded/IdentityAlreadyClaimed/NotAwarded)
- Service CẤM SỬA có tham gia?: PaymentAPI (nhận `AwardCreditAsync` khi bonus kích hoạt, chỉ gọi qua client, không sửa)
- Class dùng chung với chức năng khác: `ApiResponse<T>`, `ProfileBiometric` entity + `IProfileBiometricsRepository` (chung #2, #14), `BiometricQualityException`/`BiometricIdentityMismatchException` (chung #2, #13, #16), `IAiServiceClient` (chung hầu hết chức năng có AI), `IPaymentApiClient`+`CreditConfig` (chung #2, #4)
- Ghi DB?: `ProfileBiometrics` — INSERT/UPDATE (`Embedding`,`Dim`,`ModelVersion`,`Quality`,`UpdatedAt`), unique `(UserId, Modality)`
- Nhà cung cấp AI: InsightFace
- Anomaly: không có bug; enroll lần đầu không check identity-mismatch (đúng chủ đích, không phải bug)
- Rủi ro khi vẽ: opt bonus lồng nhiều alt con dễ tràn trang, nên tách fragment riêng; BL#12 là GET riêng lúc load trang, KHÔNG cùng request với POST enroll — dễ vẽ nhầm gộp chung.

### #2 EnrollVoiceBiometric — BL#13, 12, 53
- Entry point: `Views/Profile/Identity.cshtml:36`, kích hoạt từ `biometric-wizard.js:584-586` (`onRecordingStopped`)
- Chuỗi gọi: cấu trúc giống hệt #1, chỉ khác modality —
    Browser `biometric-wizard.js:584-586`
     -> `ProfileController.cs:191-212` (`EnrollVoice`)
     -> `BiometricsService.cs:39-58` (POST `api/profiles/me/biometrics/voice`)
     -> Gateway `appsettings.json:61-66` (không strip)
     -> `ProfileBiometricsController.cs:46-48` → helper `:50-71`
     -> `ProfileBiometricsService.cs:44-93`
     -> `AiServiceClient.cs:139-140,142-172` (`EmbedVoiceAsync`, POST `/internal/biometrics/voice/embed`)
     -> Repository `ProfileBiometricsRepository.cs:16-19,31-35,37`
     -> ai-service `biometrics/router.py:59-66` → `biometrics/voice_engine.py:51-73` (SpeechBrain `EncoderClassifier`)
    (opt BL#53) `ProfileBiometricsService.cs:125-166` -> `PaymentApiClient.cs:115-162` (THẲNG PaymentAPI :7008)
    (BL#12, dùng chung với #1) — như card #1
- Endpoint HTTP: `POST /Profile/EnrollVoice` → `POST /api/profiles/me/biometrics/voice` → ai-service `POST /internal/biometrics/voice/embed`; bonus `POST /api/internal/credit/award`
- Participant dự kiến: 10 (giống #1, khác modality)
- Số message ƯỚC LƯỢNG: ~26
- Khung rẽ nhánh: 2 alt / 1 opt — QUAN TRỌNG: ghi chú `DIAGRAM_SCOPE.md` kỳ vọng "quá ngắn / quá ồn", nhưng code (`ai-service/biometrics/voice_engine.py:51-73`) chỉ có 2 lý do 422 thật: "Cannot decode audio" và "Audio too short" — KHÔNG có gate "quá ồn" ở bước embed; 409 re-enroll mismatch threshold 0.25
- Service CẤM SỬA có tham gia?: PaymentAPI (như #1)
- Class dùng chung với chức năng khác: giống #1 — `ApiResponse<T>`, `ProfileBiometric`, `IProfileBiometricsRepository`, `BiometricQualityException`/`BiometricIdentityMismatchException`, `IAiServiceClient`, `IPaymentApiClient`+`CreditConfig`
- Ghi DB?: `ProfileBiometrics` INSERT/UPDATE (`Modality="Voice"`)
- Nhà cung cấp AI: SpeechBrain
- Anomaly: **(1)** Alt "quá ồn" trong `DIAGRAM_SCOPE.md` không khớp code thật — chỉ nên vẽ 2 lý do 422 thật, xem ghi chú trên. **(2)** `InterviewAPI/DTOs/AiService/BiometricEmbeddingResult.cs:6` — comment "Quality is face-only; null for voice" SAI: `ai-service/biometrics/schemas.py:16-21` và `voice_engine.py:70,73` LUÔN trả `quality` cho voice (heuristic theo duration). Comment lỗi thời, không sửa (ngoài phạm vi).
- Rủi ro khi vẽ: giống #1.

### #3 ConfigureInterviewSession — BL#31, 32, 33, 36
- Entry point: `Views/Interview/Index.cshtml:43-48` (form POST → `Start`) + `wwwroot/js/pages/dashboard.js:94` (fetch catalog độc lập, GET lúc load trang)
- Chuỗi gọi (catalog):
    Browser `dashboard.js:94`
     -> `InterviewController.cs:141-150` (`Catalog`)
     -> `InterviewService.cs:112-118` (`GetCatalogAsync`)
     -> Gateway `appsettings.json:49-54` (`interview-route`, không strip)
     -> `InterviewSessionController.cs:32-44` (`GetCatalog`)
     -> `AiServiceClient.cs:389-397` (GET `/catalog`) -> ai-service `app/api/catalog.py:20-33`
    Chuỗi gọi (config, 3 lệnh tuần tự trong `StartInterviewAsync`):
    Browser (form submit) -> `InterviewController.cs:56-100` (`Start`) -> `InterviewService.cs:120-194`
     -> PUT `config/duration` -> `InterviewSessionController.cs:386-405` -> `SessionService.cs:330-362` (`ConfigDurationAsync`) -> `ResolveSessionAsync` `SessionService.cs:1602-1627` (alt 409 `active_session_exists` `:1605-1618`) -> `SessionRepository.cs:48` (`CreateAsync`)
     -> PUT `config/level` -> `InterviewSessionController.cs:407-427` -> `SessionService.cs:364-396`
     -> PUT `config/interaction` -> `InterviewSessionController.cs:429-449` -> `SessionService.cs:398-430`
     (POST `init` kế tiếp ngay sau — thuộc #4, không mở rộng ở đây)
- Endpoint HTTP: GET `/api/interview/catalog`; PUT `.../config/duration`, `.../config/level`, `.../config/interaction`; ai-service GET `/catalog`
- Participant dự kiến: 9 — Browser, InterviewController, InterviewService (WebClient), ApiGateway, InterviewSessionController, SessionService, SessionRepository+SQL Server, ai-service (catalog.py)
- Số message ƯỚC LƯỢNG: ~34
- Khung rẽ nhánh: 3 alt (mỗi config: tạo mới vs cập nhật phiên có sẵn) / 2 opt (409 active_session_exists; 400 giá trị không hợp lệ)
- Service CẤM SỬA có tham gia?: không (PaymentAPI chỉ ở nhánh lỗi fallback dashboard, ngoài luồng chính)
- Class dùng chung với chức năng khác: `SessionService`, `SessionRepository`, `ApiResponse<T>`, `AiServiceClient` (chung #4/#5/#6), `InterviewSession` entity (chung #4/#5/#8/#9/#10/#11)
- Ghi DB?: `InterviewSessions` INSERT (lần đầu) / UPDATE `DurationMinutes`/`Level`/`InteractionMode`
- Nhà cung cấp AI: không (catalog chỉ đọc Chroma nội bộ, không gọi Gemini)
- Anomaly: Factory mặc định `ConfigLevelAsync`/`ConfigInteractionAsync` (`SessionService.cs:369-381,403-415`) hard-code `DurationMinutes=30`/`Level="Junior"` — dead path trong UI hiện tại (luôn gọi Duration trước), nhưng API cho phép gọi bất kỳ thứ tự nào. KI-01 KHÔNG lan tới `Catalog`/`Start` (đã đọc toàn bộ, dùng `BadRequest`/DTO nội bộ, không forward status code thô).
- Rủi ro khi vẽ: catalog fetch là async riêng biệt lúc trang load, KHÔNG đồng bộ với form submit — dễ vẽ nhầm gộp tuyến tính; Init (thuộc #4) dễ bị lẫn vào vì cùng hàm `StartInterviewAsync`.

### #4 StartInterviewSession — BL#55, 45, 46, 54, 38
- Entry point: `Views/Interview/Index.cshtml:45` (form POST `Start`, tiếp nối #3)
- Chuỗi gọi (tóm tắt — chi tiết đủ 4 tầng, xem ghi chú rủi ro để tránh tràn trang khi vẽ thật):
    Browser (form submit, tiếp #3) -> `InterviewController.cs:56` (`Start`) -> `InterviewController.cs:61` biometrics gate (boundary, ngoài phạm vi) -> `InterviewService.cs:183` POST `.../init`
     -> `InterviewSessionController.cs:587` (`Init`) -> `SessionService.cs:432-529` (`InitAsync`)
        -> `SessionService.cs:446` biometrics check (boundary)
        -> `SessionService.cs:476` `AiServiceClient.PlanQuestionsAsync` (`AiServiceClient.cs:20-35`) -> ai-service POST `/internal/questions/plan` (`internal.py:49-63`) -> `interviewer.build_interview_plan` (`interviewer.py:366-422`) -> `chroma_store.build_question_pool` ×3 (RAG retrieval, **KHÔNG gọi Gemini ở bước plan**)
        -> `SessionRepository.cs:55` `AddSessionQuestionsAsync` -> DB; `SessionRepository.cs:61` `CreateRoomAsync` -> DB
     -> `InterviewController.cs:99` redirect Verify (boundary biometrics, ngoài phạm vi)
     -> `InterviewController.cs:193` (`Room`) -> `InterviewService.cs:206` GET `.../resume` -> `InterviewSessionController.cs:349` (`ResumeSession`) -> `SessionService.cs:1267` (`ResumeSessionAsync`)
     -> [opt] Integrity Policy chưa accept -> redirect (thuộc #11, chỉ tham chiếu)
     -> `InterviewController.cs:267` -> `InterviewService.cs:196-204` POST `.../questions/generate` -> `InterviewSessionController.cs:482` -> `SessionService.cs:555` (`GenerateQuestionAsync`)
        -> `SessionRepository.cs:223` `UpdateStatusIfAsync` (Initialized→InProgress)
        -> [opt, isFirstEntry] `SessionService.cs:621-623` `PaymentApiClient.DeductCreditAsync` (`CreditConfig` — 2 credit/phút, `Helpers/CreditConfig.cs:10,24`) -> **PaymentAPI** POST `/api/internal/credit/deduct` (THẲNG :7008, không qua Gateway)
        -> [opt lỗi] `SessionService.cs:625-631` rollback status + throw
        -> `SessionRepository.cs:68` `AddChatMessageAsync` (AI Question)
        -> [opt refund] `SessionService.cs:673-679`: nếu insert ChatMessage lỗi SAU KHI đã trừ credit -> `PaymentApiClient.cs:66-113` POST `/api/internal/credit/refund`
     -> re-fetch `GetRoomAsync` (gọi lại `ResumeSessionAsync`)
     -> [opt, lần đầu] `interview-room.js:1672-1681` room tour -> `markRoomTourSeen()` -> `InterviewController.cs:595` (`TourSeen`) -> `SessionService.cs:1389` -> DB `UserRoomTourStatuses` upsert
- Endpoint HTTP: POST `.../init`, GET `.../resume`, POST `.../questions/generate`, POST `/api/interview/room-tour/seen`; ai-service POST `/internal/questions/plan`; PaymentAPI (trực tiếp) POST `/credit/deduct`, `/credit/refund`
- Participant dự kiến: 12 — Browser, InterviewController, InterviewService (WebClient), ApiGateway, InterviewSessionController, SessionService, SessionRepository, AiServiceClient, ai-service (interview.py+internal.py+interviewer.py+chroma_store.py), PaymentApiClient, PaymentAPI, SQL Server
- Số message ƯỚC LƯỢNG: **~110-130** — VƯỢT mốc Login (98)
- Khung rẽ nhánh: ~3 alt (409 active_session_exists ở #3; entry-gate allowed/not — boundary; Completed vs Initialized/InProgress) / ~4 opt (biometrics gate; credit-deduct fail→rollback; chat-insert-fail→refund; room-tour lần đầu)
- Service CẤM SỬA có tham gia?: **PaymentAPI** (deduct/refund, bypass Gateway) và **ApiGateway** (routing)
- Class dùng chung với chức năng khác: `SessionService`, `SessionRepository`, `ApiResponse<T>`, `InterviewSession`/`SessionQuestion`/`InterviewRoom` entities, `CreditConfig`, `AiServiceClient`, `UserRoomTourStatus`, `AttachVerificationCheckAsync`/`GetInSessionCheckAsync` (chung #5, #15)
- Ghi DB?: `InterviewSessions`, `SessionQuestions`, `InterviewRooms`, `ChatMessages` (insert AI Question), `UserRoomTourStatuses`
- Nhà cung cấp AI: **ChromaDB** (RAG retrieval câu hỏi, KHÔNG gọi Gemini ở bước plan — khác kỳ vọng ban đầu "RAG→Gemini" đầy đủ)
- Anomaly: KI-01 KHÔNG xuất hiện trong `Start`/`Room`/`GenerateQuestionAsync`/`TourSeen` (đã đọc toàn bộ InterviewController.cs). "Opt refund khi init lỗi" hẹp hơn tên gọi trong `DIAGRAM_SCOPE.md`: chỉ kích hoạt khi `AddChatMessageAsync` lỗi NGAY SAU trừ credit trong `GenerateQuestionAsync` — KHÔNG bao gồm lỗi ở `InitAsync` (chưa trừ credit) lẫn lỗi re-fetch `GetRoomAsync` sau đó.
- Rủi ro khi vẽ: **CAO NHẤT trong 17 file** — ước tính vượt mốc Login đáng kể do 3 config lặp cấu trúc (#3) + Init + Resume gọi 2 lần + credit deduct/refund + room-tour. Đề xuất tách trang hoặc gộp block lặp/ref-box, cần bàn với người dùng trước khi vẽ thật.

### #5 ConductInterviewQA — BL#56, 57, 47, 59, 60
- Entry point: `wwwroot/js/pages/interview-room.js:833` (`sendAnswer`, cũng dùng chung bởi skip:918, autoAdvance:971)
- Chuỗi gọi:
    Browser `interview-room.js:833` -> [alt content-filter client] `content-filter.js:117-119` -> `interview-room.js:858` `postProxy(messageUrl)`
     -> `InterviewController.cs:433` (`Message`) -> `InterviewService.cs:241` (`SendMessageAsync`)
     -> Gateway `appsettings.json:49-54` (không strip)
     -> `InterviewSessionController.cs:456` (`SendMessage`) -> `SessionService.cs:699` (`SendMessageAsync`)
        -> `SessionRepository.cs:21` GetByIdAsync
        -> `SessionService.cs:706` `SettleExpiredVerificationChecksAsync` (boundary biometrics)
        -> [alt 409 session_time_expired] `SessionService.cs:717-719`
        -> `SessionService.cs:724` `RequireIntegrityPolicyAccepted` (alt 403)
        -> [opt, Reason=="Skipped"] `SessionRepository.cs:264` `TryConsumeSkipCooldownAsync` (alt 429)
        -> `SessionRepository.cs:76` GetQuestionByIdAsync
        -> [alt content-filter server] `Helpers/ContentModerationHelper.cs:48`
        -> [alt idempotency] `SessionRepository.cs:87` `HasUserAnswerAsync` + `:295` `IncrementAnsweredIfAsync`
        -> `SessionRepository.cs:68` `AddChatMessageAsync` (User Answer)
        -> [opt, fire-and-forget async] `SessionService.cs:827` `ScheduleGrading` (`:1708-1737`, `Task.Run`) -> `EvaluationApiClient.cs:17-39` POST `http://localhost:7006/api/evaluation/sessions/{id}/answers/{qid}` (THẲNG, không qua Gateway) -> EvaluationAPI (xem #17)
        -> `SessionRepository.cs:93` GetNextUnansweredQuestionAsync
        -> [alt còn câu] AddChatMessageAsync (AI Question) + `SessionService.cs:870` `AttachVerificationCheckAsync` (boundary #15)
        -> [alt hết câu] Status=Completed/Expired + `UpdateRoomAsync`
- Endpoint HTTP: POST `/api/interview/sessions/{id}/message`; POST `/api/evaluation/sessions/{sessionId}/answers/{sessionQuestionId}` (EvaluationAPI trực tiếp)
- Participant dự kiến: 10-11 — Browser, InterviewController, InterviewService, ApiGateway, InterviewSessionController, SessionService, SessionRepository, EvaluationApiClient, EvaluationAPI (boundary), ai-service+Gemini (nền, boundary)
- Số message ƯỚC LƯỢNG: ~45-55 (có thể ~65-70 nếu mở rộng đầy đủ nhánh EvaluationAPI→ai-service→Gemini)
- Khung rẽ nhánh: ~5 alt (session hết giờ; content-filter accept/reject; idempotency×2; còn câu/hết câu) / ~3 opt (skip cooldown; attach verification check; background grading)
- Service CẤM SỬA có tham gia?: ApiGateway (routing hop chính). EvaluationAPI KHÔNG thuộc danh sách cấm sửa nhưng ngoài phạm vi khảo sát chi tiết ở đây (xem #17).
- Class dùng chung với chức năng khác: `SessionService`, `SessionRepository`, `ApiResponse<T>`, `ChatMessage`/`InterviewSession`/`SessionQuestion` entities, `AttachVerificationCheckAsync`/`GetInSessionCheckAsync` (chung #4, #15), `EvaluationApiClient` (chung #17)
- Ghi DB?: `InterviewSessions` (update Status/EndedAt), `SessionQuestions` (update AskedAt), `ChatMessages` (insert User Answer + AI Question), `InterviewRooms` (update Closed)
- Nhà cung cấp AI: Gemini + ChromaDB — **chỉ trong nhánh grading nền (qua EvaluationAPI), không nằm trên critical path trả lời**
- Anomaly: **KI-01 CONFIRMED tại `InterviewController.cs:445-446`** (action `Message`, hiện tại là dòng 445, KNOWN_ISSUES.md ghi 445 khớp) — cùng pattern với Hint. `SendMessage` phía API hiện chỉ trả 200/409/429/400 nên nhánh "2xx nuốt lỗi" hiện KHÔNG reachable thực tế, nhưng code vẫn giữ pattern rủi ro. `ScheduleGrading` là fire-and-forget thật: lỗi chỉ log, không retry, không báo user — câu trả lời được lưu nhưng có thể không bao giờ được chấm mà không ai biết (xem KI-08 trong KNOWN_ISSUES.md).
- Rủi ro khi vẽ: sát mốc rủi ro nếu vẽ đầy đủ nhánh grading nền — khuyến nghị giữ dạng 1 opt/ref box gọn.

### #6 VoiceInteraction — BL#42, 43
- Entry point: `interview-room.js:1595` (`speak`, TTS) / `interview-room.js:1094` (`transcribe`, STT)
- Chuỗi gọi (TTS):
    Browser `interview-room.js:1598` fetch(ttsUrl)
     -> `InterviewController.cs:453` (`Tts`) -> `InterviewService.cs:333` (`GetTtsAudioAsync`)
     -> Gateway (không strip) -> `InterviewSessionController.cs:507` (`TextToSpeech`, stateless)
     -> `AiServiceClient.cs:71-96` (`SynthesizeSpeechAsync`) -> ai-service POST `/interview/tts` (`interview.py:161-207`, stateless)
        -> `tts_cache.get` (`tts_cache.py:43-54`) -> [alt cache HIT] trả ngay -> [alt cache MISS]:
           [alt TTS_PROVIDER=piper, mặc định] `voice.py:103-117` (PiperTTS) / [alt TTS_PROVIDER=elevenlabs, legacy] `voice.py:62-75` (ElevenLabsTTS, XÁC NHẬN còn tồn tại)
           -> `tts_cache.put` -> [opt lỗi] `interview.py:184-196` -> 502 `tts_unavailable`
    Chuỗi gọi (STT, opt — KHÔNG phải luồng chủ động gọi `/sessions/stt`):
    Browser `interview-room.js:1104` fetch(sttUrl) -> `InterviewController.cs:470` (`Stt`) -> `InterviewService.cs:349` (`TranscribeAudioAsync`) -> **thực tế POST `.../verify/voice-answer`** (`InterviewService.cs:359-360`) -> `SessionVerificationController.cs:182` (`VerifyVoiceAnswer`, xem #16) -> `AiServiceClient.cs:98-118` (`TranscribeAsync`) -> ai-service POST `/internal/stt` (`internal.py:134-143`) -> Deepgram (`voice.py:38-47`)
    Ghi chú: endpoint `POST /api/interview/sessions/stt` (`InterviewSessionController.cs:529-550`) tồn tại và hoạt động, nhưng KHÔNG có nơi nào trong WebClient gọi tới nó.
- Endpoint HTTP: POST `/api/interview/sessions/tts`; ai-service POST `/interview/tts`; [opt] POST `.../verify/voice-answer` -> ai-service POST `/internal/stt`; (tồn tại nhưng không được gọi) POST `/api/interview/sessions/stt`
- Participant dự kiến: 9 — Browser, InterviewController, InterviewService, ApiGateway, InterviewSessionController+SessionVerificationController, AiServiceClient, BiometricVerificationService (opt), ai-service (interview.py+internal.py+voice.py+tts_cache.py)
- Số message ƯỚC LƯỢNG: ~35 (TTS) + ~13 (STT opt, vẽ gọn) ≈ **~45-55**
- Khung rẽ nhánh: 2 alt lồng nhau (TTS_PROVIDER piper/elevenlabs × cache hit/miss) / 2 opt (lỗi synth → 502; STT-qua-voice-answer)
- Service CẤM SỬA có tham gia?: ApiGateway (routing)
- Class dùng chung với chức năng khác: `AiServiceClient.TranscribeAsync` (chung endpoint chết `/sessions/stt` và `BiometricVerificationService.VerifyVoiceAnswerAsync` — #16), `PiperTTS`/`ElevenLabsTTS` singleton, `tts_cache`, `ApiResponse<T>`
- Ghi DB?: TTS — không (chỉ cache đĩa); STT-qua-voice-answer — có ghi `SessionVerificationEvents` (thuộc #16)
- Nhà cung cấp AI: Piper TTS (mặc định) / ElevenLabs TTS (legacy, còn sống sau cờ config) / Deepgram STT
- Anomaly: xem **KI-02** trong KNOWN_ISSUES.md (nhánh lỗi TTS không nhất quán 502/400). Endpoint `/sessions/stt` là code sống về kỹ thuật nhưng chết từ góc nhìn Browser (không JS nào gọi). KI-01 KHÔNG xuất hiện trong action `Tts`/`Stt`.
- Rủi ro khi vẽ: rủi ro thấp nếu giữ nhánh STT ở dạng opt gọn (không mở rộng toàn bộ logic speaker-verify của #16).

### #8 PauseResumeSession — BL#39, 40, 62, 65
- Entry point: `interview-room.js:299-300` (pauseBtn/resumeBtn click)
- Chuỗi gọi:
    Browser `interview-room.js:264-278` (`pauseInterview`) -> `InterviewController.cs:552-562` (`Pause`) -> `InterviewService.cs:294-302` -> Gateway (không strip) -> `InterviewSessionController.cs:297-310` -> `SessionService.cs:1122-1148` (`PauseSessionAsync`) -> SessionRepository GetByIdAsync+SaveChangesAsync (KHÔNG atomic)
    Browser `interview-room.js:280-297` (`resumeInterview`) -> `InterviewController.cs:582-592` (`Unpause`) -> `InterviewService.cs:304-312` -> `InterviewSessionController.cs:313-326` -> `SessionService.cs:1150-1177` (`UnpauseSessionAsync`)
    (self-heal, KHÔNG nằm trong Pause/Unpause) `SessionService.cs:113-128` (`AutoResumeIfPauseExpiredAsync`) -> `SessionRepository.cs:315-328` (`TryAutoResumeExpiredPauseAsync`, atomic `ExecuteUpdateAsync`) — gọi từ `ResumeSessionAsync`(:1273) và `GetActiveSessionAsync`(:992); `GetTimerAsync`(:959) cũng gọi nhưng KHÔNG được WebClient gọi (xem KI-07)
- Endpoint HTTP: POST `.../pause`; POST `.../unpause`
- Participant dự kiến: 7 — Browser, InterviewController, InterviewService, ApiGateway, InterviewSessionController, SessionService, SessionRepository+SQL Server
- Số message ƯỚC LƯỢNG: ~16
- Khung rẽ nhánh: 2 alt (Pause: `PauseCount>=3`/`TotalPausedSeconds>=300`; Unpause: `Status!="Paused"`) / 1 opt (self-heal, chạy ở request KHÁC, không trong chính Pause/Unpause)
- Service CẤM SỬA có tham gia?: không
- Class dùng chung với chức năng khác: `SessionService`, `SessionRepository`, `AutoResumeIfPauseExpiredAsync`/`TryAutoResumeExpiredPauseAsync` (chung `ResumeSessionAsync`, `GetActiveSessionAsync`, `GetTimerAsync`)
- Ghi DB?: `InterviewSessions` UPDATE `Status`/`PausedAt`/`PauseCount`/`TotalPausedSeconds` (không atomic ở chính Pause/Unpause; atomic chỉ ở self-heal)
- Nhà cung cấp AI: không
- Anomaly: xem **KI-06** (Pause/Unpause không atomic — race double-click) và **KI-07** (poll loop `/timer` không tồn tại thực tế, self-heal chỉ chạy khi load lại trang) trong KNOWN_ISSUES.md. Draft `sessionStorage` (`interview-room.js:788-820`) chỉ giữ Ô NHẬP câu trả lời khi F5, KHÔNG lưu trạng thái pause — dễ vẽ nhầm.
- Rủi ro khi vẽ: không vẽ atomic UPDATE như đang nằm trong luồng Pause/Unpause chính — nó thuộc luồng Resume/GetActiveSession khác; không vẽ poll loop `/timer` vì không tồn tại thật.

### #9 EndInterviewSession — BL#37, 61
- Entry point: `interview-room.js:606-608` (countdown hết giờ → `completeInterview()`, tự động) và `interview-room.js:664-667` (nút Cancel → `cancelInterview()`, chủ động)
- Chuỗi gọi (auto-complete, BL37):
    Browser `interview-room.js:677-721` -> `postProxy(completeUrl)` -> `InterviewController.cs:526-536` (`CompleteSession`) -> `InterviewService.cs:274-282` -> Gateway -> `InterviewSessionController.cs:263-277` -> `SessionService.cs:1052-1090` (`CompleteSessionAsync`)
    Chuỗi gọi (end chủ động, BL61):
    Browser `interview-room.js:723-751` (`cancelInterview`) -> `InterviewController.cs:539-549` (`EndSession`) -> `InterviewService.cs:284-292` -> `InterviewSessionController.cs:280-294` -> `SessionService.cs:1092-1120` (`EndSessionAsync`)
- Endpoint HTTP: POST `.../complete` (auto); POST `.../end` (chủ động)
- Participant dự kiến: 7 — Browser, InterviewController, InterviewService, ApiGateway, InterviewSessionController, SessionService, SessionRepository+SQL Server
- Số message ƯỚC LƯỢNG: ~16
- Khung rẽ nhánh: 2 alt (CompleteSession: `candidateWasActiveAtDeadline` → Completed vs Expired, `SessionService.cs:1070-1074`) / 1 opt (EndSession: cho phép huỷ từ Initialized/InProgress/Paused, đã terminal thì no-op)
- Service CẤM SỬA có tham gia?: không
- Class dùng chung với chức năng khác: `SessionService`, `SessionRepository` (`GetRoomBySessionAsync`, `UpdateRoomAsync`, `HasSubstantiveAnswerAsync`), `IsTerminalStatus` helper (chung #10)
- Ghi DB?: `InterviewSessions` UPDATE Status/EndedAt; `InterviewRooms` UPDATE Closed; CompleteSession còn ĐỌC `ChatMessages` qua `HasSubstantiveAnswerAsync`
- Nhà cung cấp AI: không
- Anomaly: `completeInterview()` (interview-room.js:677) được gọi từ NHIỀU nơi (countdown hết giờ + các nhánh "hết câu hỏi"/lỗi trong sendAnswer/skip/hint) — BL37 gộp chung 1 hàm JS cho 2 lý do khác nhau, server phân biệt lại nội bộ (chi tiết "hết câu hỏi" CHƯA TRA ĐƯỢC — có thể nằm trong `SendMessageAsync`, ngoài phạm vi card này). KI-01 KHÔNG lan tới đây.
- Rủi ro khi vẽ: dễ conflate 2 lý do kết thúc vào 1 alt; tên biến `cancelUrl` (JS) vs action `EndSession` (API) dễ gây nhầm khi đặt tên lifeline.

### #10 ReportCameraViolation — BL#41
- Entry point: `interview-room.js:1223-1244` (`scheduleCameraRecovery`, hết grace period mất camera) -> `reportCameraViolation()` `:1237`
- Chuỗi gọi:
    Browser `interview-room.js:1170-1190` -> `postProxy(cameraViolationUrl)` -> `InterviewController.cs:566-578` (`CameraViolation`) -> `InterviewService.cs:314-322` -> Gateway -> `InterviewSessionController.cs:331-345` -> `SessionService.cs:1179-1265` (`ReportCameraViolationAsync`)
     -> `SessionVerificationEventRepository.cs:22-41` (`TryAddIncidentAsync`, INSERT idempotent theo `IncidentId`)
     -> `SessionRepository.cs:332-346` (`IncrementCameraViolationAsync`, atomic `ExecuteUpdateAsync`)
- Endpoint HTTP: POST `.../camera-violation`
- Participant dự kiến: 8 — Browser, InterviewController, InterviewService, ApiGateway, InterviewSessionController, SessionService, SessionRepository, SessionVerificationEventRepository+SQL Server
- Số message ƯỚC LƯỢNG: ~12
- Khung rẽ nhánh: 2 alt (`inserted` mới vs `Duplicate` trùng IncidentId; `shouldEndSession` đạt maxStrikes với Strictness=Strict → Cancelled vs chỉ Flag) / 1 opt (session không InProgress/Paused → InvalidOperationException)
- Service CẤM SỬA có tham gia?: không
- Class dùng chung với chức năng khác: `SessionService`, `CheatingStrikeHelper.ComputeEffectiveStrikeCount` (chung #15, #16), `SessionVerificationEvent` entity (chung #13/#14/#15/#16)
- Ghi DB?: `SessionVerificationEvents` INSERT (Phase=InSession, Modality=Camera); `InterviewSessions.CameraViolationCount` atomic increment; nếu shouldEndSession: `InterviewSessions`/`InterviewRooms` đóng session
- Nhà cung cấp AI: không (phát hiện mất camera là client-side WebRTC, không qua ai-service)
- Anomaly: khi `shouldEndSession=true`, Browser (`handleForcedCheatingEnd`, interview-room.js:1397-1412) chỉ hiện modal + redirect, KHÔNG gọi thêm Complete/EndSession — server đã tự đóng session bên trong response camera-violation. Dễ vẽ nhầm thành 2 request tách biệt. KI-01 KHÔNG lan tới đây.
- Rủi ro khi vẽ: 2 tầng idempotency (unique constraint DB + cờ `Duplicate`) nên gộp 1 opt; nhánh `shouldEndSession` tái dùng đúng logic đóng room như #9 — nếu vẽ độc lập cần ghi chú tham chiếu, tránh lặp chi tiết.

### #11 AcceptIntegrityPolicy — BL#63, 64
- Entry point: `Views/Interview/IntegrityPolicy.cshtml:106-109` (form POST, nút chỉ enable sau khi tick checkbox)
- Chuỗi gọi (GET status):
    Browser (redirect từ `InterviewController.cs:254-257`) -> `InterviewController.cs:152-173` (`IntegrityPolicy` GET) -> `InterviewService.cs:556-559` -> Gateway -> `InterviewSessionController.cs:195-208` (`GetIntegrityPolicyStatus`) -> `SessionService.cs:1635-1646`
    Chuỗi gọi (POST accept):
    Browser (form submit) -> `InterviewController.cs:177-187` (`AcceptIntegrityPolicy`) -> `InterviewService.cs:561-564` -> `InterviewSessionController.cs:210-227` -> `SessionService.cs:1648-1669` (`AcceptIntegrityPolicyAsync`) -> `SessionRepository.cs:234-243` (`AcceptIntegrityPolicyIfAsync`, atomic `ExecuteUpdateAsync WHERE !IntegrityPolicyAccepted`)
- Endpoint HTTP: GET `.../integrity-policy`; POST `.../integrity-policy/accept`
- Participant dự kiến: 7 — Browser, InterviewController, InterviewService, ApiGateway, InterviewSessionController, SessionService, SessionRepository+SQL Server
- Số message ƯỚC LƯỢNG: ~14
- Khung rẽ nhánh: 2 alt (GET: đã Accepted → redirect thẳng Room vs chưa accept → hiện trang; POST: `Status != "Initialized"` → lỗi) / 1 opt (atomic update thua race — xem KI-03)
- Service CẤM SỬA có tham gia?: không
- Class dùng chung với chức năng khác: `SessionService`, `SessionRepository`, `IntegrityPolicyStatusResponse` DTO
- Ghi DB?: `InterviewSessions` — GET đọc `IntegrityPolicyAccepted`/`AcceptedAt`; POST atomic UPDATE 2 cột đó
- Nhà cung cấp AI: không
- Anomaly: xem **KI-03** trong KNOWN_ISSUES.md (response cho request thua race dựng từ đối tượng session đọc TRƯỚC atomic update, có thể báo sai giá trị). GET được gọi từ CẢ guard trong action `Room` (InterviewController.cs:247-256) LẪN action `IntegrityPolicy` riêng (:152-173) — cùng 1 `GetIntegrityPolicyStatusAsync`, dễ bỏ sót 1 trong 2 nguồn khi vẽ. KI-01 không áp dụng (action trả View/Redirect, không phải JSON proxy).
- Rủi ro khi vẽ: **bảng UC của nhóm CHƯA có ê-líp cho chức năng này** (đã biết, xem DIAGRAM_SCOPE.md §3.1) — cần nhóm bổ sung trước khi chốt UC-id.

### #12 GetVerificationChallenge — BL#66
- Entry point: `entry-verify.js:131` (modality=face, nhánh SỐNG); `biometric-wizard.js:471` (modality=voice, nhánh CHẾT — xem #16 Anomaly)
- Chuỗi gọi:
    Browser `entry-verify.js:131` -> `VerificationController.cs:26` (`Challenge`) -> `VerificationService.cs:40` (`GetChallengeAsync`) -> Gateway (`interview-route`, không strip) -> `SessionVerificationController.cs:48` (`GetChallenge`) -> `BiometricVerificationService.cs:53` (`IssueChallengeAsync`) -> `SessionVerificationChallengeRepository.cs:16` (`AddAsync`)
    ai-service: không gọi (phrase/direction sinh nội bộ .NET, danh sách tĩnh `BiometricVerificationService.cs:16-28`)
- Endpoint HTTP: GET `api/interview/sessions/{sessionId}/verify/challenge?modality=face|voice`
- Participant dự kiến: 6 — Browser, VerificationController, VerificationService, ApiGateway, SessionVerificationController+BiometricVerificationService, SQL Server (SessionVerificationChallenges)
- Số message ƯỚC LƯỢNG: 8
- Khung rẽ nhánh: 2 alt (modality face/voice; enrollment thiếu → throw) / 1 opt (session không thuộc user → KeyNotFoundException)
- Service CẤM SỬA có tham gia?: ApiGateway (routing)
- Class dùng chung với chức năng khác: `ApiResponse<T>`, `SessionVerificationChallenge` entity (chung #13, #16)
- Ghi DB?: `SessionVerificationChallenges` INSERT
- Nhà cung cấp AI: không
- Anomaly: nhánh modality=voice không có lối vào UI thật (xem KI-05). KI-01 không xuất hiện.
- Rủi ro khi vẽ: cần ghi chú rõ nhánh voice "hiện không có lối vào UI" để tránh vẽ như đang hoạt động song song với face.

### #13 VerifyFaceEntryGate — BL#48
- Entry point: `entry-verify.js:159` (`capture()`), fetch tại `:199`
- Chuỗi gọi:
    Browser `entry-verify.js:199` -> `VerificationController.cs:44` (`VerifyFace`) -> `VerificationService.cs:55` (`VerifyFaceAsync`) -> Gateway (không strip) -> `SessionVerificationController.cs:77` (`VerifyFace`, phase mặc định "Entry") -> `BiometricVerificationService.cs:95` (`VerifyFaceAsync`)
     -> Repository: `SessionVerificationEventRepository.cs:47` (đếm retry), `SessionVerificationChallengeRepository.cs:22` (qua `ConsumeChallengeAsync` :651), `ProfileBiometricsRepository.cs:16` (qua `GetStoredEmbeddingAsync` :664)
     -> `AiServiceClient.cs:174` (`VerifyFaceLiveAsync`, POST `/internal/biometrics/face/verify-live`) -> ai-service `biometrics/router.py:117`
     -> `SessionVerificationEventRepository.cs:16` (`AddAsync`, qua `RecordEventAsync` :610)
- Endpoint HTTP: POST `api/interview/sessions/{sessionId}/verify/face`; ai-service POST `/internal/biometrics/face/verify-live`
- Participant dự kiến: 7 — Browser, VerificationController/Service, ApiGateway, InterviewAPI (Controller+Service+Repos), ai-service (InsightFace), SQL Server
- Số message ƯỚC LƯỢNG: 14
- Khung rẽ nhánh: 3 alt (retry cap `EntryMaxRetries=3` đạt → throw trước khi tiêu challenge — đúng BL#48 "3 lần thử"; ai-service Passed true/false; `BiometricQualityException` → 422) / 1 opt (phase khác "Entry" — không có caller nào gửi hiện tại, dead path)
- Service CẤM SỬA có tham gia?: ApiGateway (routing)
- Class dùng chung với chức năng khác: `ApiResponse<T>`, `SessionVerificationEvent` entity (chung #14 đọc lại, #15/#16 cùng ghi), `BiometricQualityException` (chung mọi lời gọi ai-service biometrics)
- Ghi DB?: `SessionVerificationChallenges` UPDATE (ConsumedAt); `SessionVerificationEvents` INSERT (Phase=Entry, Modality=Face)
- Nhà cung cấp AI: InsightFace
- Anomaly: xác nhận đúng BL#48 "chưa trừ credit cho tới khi pass" — `VerifyFaceAsync` không gọi PaymentApiClient (credit trừ ở request câu hỏi đầu tiên của Room, thuộc #4). Query `phase` không hề được WebClient gửi ở bất kỳ đâu (đã grep) → nhánh `phase="InSession"` trong hàm này là dead path. KI-01 không xuất hiện.
- Rủi ro khi vẽ: nhiều bước tuần tự dễ tràn trang — nên gộp "upload nhiều frame" thành 1 message thay vì vẽ từng frame.

### #14 ValidateEntryGateStatus — BL#49
- Entry point: `entry-verify.js:219` (`handleFail()`, sau khi verify-face thất bại) — cũng được gọi server-side lúc load trang (`InterviewController.cs:105`, boundary #4), chọn JS làm chính vì đúng "endpoint GET" theo BL#49
- Chuỗi gọi:
    Browser `entry-verify.js:219` -> `VerificationController.cs:74` (`EntryGate`) -> `VerificationService.cs:86` (`GetEntryGateAsync`) -> Gateway (không strip) -> `SessionVerificationController.cs:131` (`GetEntryGateStatus`) -> `BiometricVerificationService.cs:191` (`GetEntryGateStatusAsync`) -> `ProfileBiometricsRepository.cs:16` + `SessionVerificationEventRepository.cs:47`
    ai-service: không gọi
- Endpoint HTTP: GET `api/interview/sessions/{sessionId}/verify/entry-gate`
- Participant dự kiến: 5 — Browser, VerificationController/Service, ApiGateway, InterviewAPI (Controller+Service+Repos), SQL Server
- Số message ƯỚC LƯỢNG: 8
- Khung rẽ nhánh: 3 alt (`Biometrics:Enabled=false` → allowed short-circuit; chưa enroll face → not-eligible; `facePassed` true/false) / 0 opt
- Service CẤM SỬA có tham gia?: ApiGateway (routing)
- Class dùng chung với chức năng khác: `ApiResponse<T>`, `EntryGateStatusResponse` DTO, `SessionVerificationEvent` entity (chung #13/#15/#16)
- Ghi DB?: không (chỉ đọc)
- Nhà cung cấp AI: không
- Anomaly: comment `SessionVerificationController.cs:129-130` ("not called from any Room UI yet") gây hiểu nhầm — thực tế `entry-verify.js:219` ĐÃ gọi nó (ở màn Verify, trước Room); comment chỉ đúng nghĩa "chưa gọi từ Room". KI-01 không xuất hiện.
- Rủi ro khi vẽ: có 2 lối vào (Razor server-side lúc load trang + JS poll lúc fail) — nên vẽ JS làm chính, note cho lối server-side để tránh trùng lặp logic.

### #15 VerifyFaceInSession — BL#50, 51
- Entry point: `interview-room.js:582` (`applyNextQuestion` → `captureAndVerifyInSession()`) và `:1220` (camera reconnect)
- Chuỗi gọi:
    Browser `interview-room.js:1480` (fetch trong `captureAndVerifyInSession`, def `:1453-1538`) -> `VerificationController.cs:93` (`VerifyInSession`) -> `VerificationService.cs:114` -> Gateway (không strip) -> `SessionVerificationController.cs:155` -> `BiometricVerificationService.cs:265` (`VerifyInSessionAsync`)
     -> `SessionVerificationEventRepository.cs:65` (`TryConsumeQuestionCheckAsync`, tiêu checkId) + `:16` AddAsync + `:47` ListBySessionAsync (đếm strike)
     -> `AiServiceClient.cs:226` (`VerifyFaceAsync`, POST `/internal/biometrics/verify`, modality=face) -> ai-service `biometrics/router.py:79`
    Nguồn phát checkId: KHÔNG do browser chủ động gọi — đính kèm sẵn trong `NextQuestionDto` qua `SessionService.cs:217` (`GetInSessionCheckAsync`) trong luồng #4/#5 (`AttachVerificationCheckAsync`, `:211-214`)
- Endpoint HTTP: POST `api/interview/sessions/{sessionId}/verify/in-session`; ai-service POST `/internal/biometrics/verify`
- Participant dự kiến: 8 — Browser, VerificationController/Service, ApiGateway, InterviewAPI (Controller+Service+Repos), ai-service (InsightFace), SQL Server
- Số message ƯỚC LƯỢNG: 12 (phức tạp NHẤT về nhánh rẽ trong 5 file UC-10 dù message thô không cao nhất)
- Khung rẽ nhánh: nhiều alt lồng (checkId hợp lệ/hết hạn/đã dùng; ai-service match/mismatch; `multiple_faces` → `BiometricQualityException`; quality khác/no-face; strike accumulation cho no-face; kết thúc phiên khi `shouldEndSession` với Strictness=Strict) / 1 opt (rate-limit — xem KI-04, KHÔNG phải interval cứng 25s như tài liệu cũ ghi)
- Service CẤM SỬA có tham gia?: ApiGateway (routing)
- Class dùng chung với chức năng khác: `ApiResponse<T>`, `SessionVerificationEvent` entity (chung #13/#14/#16), `CheatingStrikeHelper.ComputeEffectiveStrikeCount` (chung #10, #16), `BiometricQualityException`
- Ghi DB?: `SessionVerificationEvents` UPDATE (consume token) + INSERT (kết quả); nếu shouldEndSession: `InterviewSessions`/`InterviewRooms` đóng session
- Nhà cung cấp AI: InsightFace
- Anomaly: xem **KI-04** trong KNOWN_ISSUES.md (rate-limit "25 giây" là config chết, cơ chế thật là 1-checkId/câu-hỏi + TTL 20s) và ghi chú `pollVerificationSchedule()` (interview-room.js:1545-1587) là dead code — checkId luôn đến kèm `nextQuestion.verificationCheckId`, không phải do JS chủ động poll. KI-01 không xuất hiện.
- Rủi ro khi vẽ: card phức tạp nhất trong nhóm UC-10 — nhiều alt lồng nhau dễ tràn trang, nên gộp response ai-service thành 1 alt lớn 5 nhánh con; PHẢI quyết định trước có vẽ chung nguồn phát checkId (thuộc API #4/#5, ngoài phạm vi) hay chỉ note tham chiếu.

### #16 VerifyVoiceAnswer — BL#52, 67
- Ghi chú: 2 endpoint khác nhau, chung 1 service class. BL#52 (`verify/voice-answer`) là luồng SỐNG trong Room; BL#67 (`verify/voice`) là luồng CHẾT trên UI (xem KI-05). Transcript STT trả kèm ở BL#52 liên quan #6 — chỉ note tham chiếu.
- Entry point BL#52: `interview-room.js:1094` (`transcribe()`, fetch `:1104`)
- Entry point BL#67: `biometric-wizard.js:558` (`onRecordingStopped`, fetch `:577` — unreachable, xem KI-05)
- Chuỗi gọi BL#52 (chính):
    Browser `interview-room.js:1104` -> `InterviewController.cs:471` (`Stt`) -> `InterviewService.cs:349` (`TranscribeAudioAsync`) -> Gateway (không strip) -> `SessionVerificationController.cs:181` (`VerifyVoiceAnswer`) -> `BiometricVerificationService.cs:419` (`VerifyVoiceAnswerAsync`)
     -> chạy song song: `AiServiceClient.cs:98` (`TranscribeAsync`, POST `/internal/stt`) + `AiServiceClient.cs:262` (`VerifyVoiceAsync`, POST `/internal/biometrics/verify`, modality=voice)
     -> Repository (qua `BuildVoiceAnswerResponseAsync` :540): `SessionVerificationEventRepository.cs:22` TryAddIncidentAsync + `:47` ListBySessionAsync
     -> ai-service `internal.py:134` (`transcribe_audio`) VÀ `biometrics/router.py:79` (`verify`)
    Chuỗi gọi BL#67 (dead, chỉ tham chiếu): `biometric-wizard.js:577` -> `VerificationController.cs:120` (`VerifyVoice`) -> `VerificationService.cs:72` -> `SessionVerificationController.cs:103` -> `BiometricVerificationService.cs:144` -> `AiServiceClient.cs:297` (`VerifyVoiceChallengeAsync`, POST `/internal/biometrics/voice/verify-challenge`) -> `biometrics/router.py:151`
- Endpoint HTTP: POST `.../verify/voice-answer` (BL#52); POST `.../verify/voice` (BL#67); ai-service POST `/internal/stt`, `/internal/biometrics/verify`, `/internal/biometrics/voice/verify-challenge`
- Participant dự kiến: 8 — Browser, InterviewAPI (2 controller nhóm), Repository, ApiGateway, ai-service (Deepgram+SpeechBrain), SQL Server
- Số message ƯỚC LƯỢNG: 14 (BL#52, do 2 lời gọi ai-service song song)
- Khung rẽ nhánh: 5 alt (Biometrics disabled → skip check; chưa enroll voice; checkId trùng — chống double-submit; `BiometricQualityException`; exception khác/tạm thời) + 1 alt mismatch/strike/end-session / 0 opt rõ ràng
- Service CẤM SỬA có tham gia?: ApiGateway (routing)
- Class dùng chung với chức năng khác: `ApiResponse<T>`, `SessionVerificationEvent` entity (chung #13/#14/#15), `CheatingStrikeHelper.ComputeEffectiveStrikeCount` (chung #15)
- Ghi DB?: `SessionVerificationEvents` INSERT (Phase=InSession, Modality=Voice); nếu shouldEndSession: đóng session (dùng chung code với #15)
- Nhà cung cấp AI: Deepgram STT + SpeechBrain (speaker verify) — BL#67 dùng thêm Deepgram (khớp nội dung challenge)
- Anomaly: xem **KI-05** (BL#67 unreachable từ UI thật — `biometric-wizard.js` mode "verify" yêu cầu 2 phần tử DOM không tồn tại trong `Identity.cshtml`, trang DUY NHẤT load file JS này). KI-01 không xuất hiện.
- Rủi ro khi vẽ: card duy nhất phải vẽ 2 endpoint trong 1 file — rủi ro lớn nhất là gộp lẫn khiến người đọc tưởng cả BL#52 và BL#67 đều hoạt động trong Room. Bắt buộc dùng khung alt/note tách biệt rõ ràng, đánh dấu BL#67 "hiện không có lối vào UI".

### #17 EvaluateInterviewAnswer — BL#73, 75, 70
- Entry point: **actor-gián-tiếp** — KHÔNG có nút bấm gọi thẳng. `User` kích hoạt gián tiếp qua "Gửi câu trả lời/Skip" (`interview-room.js:833-861`, `sendAnswer`). **Lưu ý sai lệch với gợi ý đề bài**: đề bài gợi ý ví dụ "bấm End Session" — SAI so với code thật; trigger thật là SendMessage (mỗi câu trả lời), KHÔNG phải End Session. `ScheduleGrading` được gọi trong `SendMessageAsync`, không phải `EndSessionAsync`.
- Chuỗi gọi:
    Browser `interview-room.js:833-861` -> `InterviewController.cs:433-450` (`Message`) -> `InterviewService.cs:241-251` (POST `.../message`) -> Gateway (không strip) -> `InterviewSessionController.cs:455-469` (`SendMessage`) -> `SessionService.cs:699-827` -> dòng 827 gọi `ScheduleGrading`
     -> `SessionService.cs:1708-1737` (`ScheduleGrading`, `Task.Run` nền, capture bearerToken TRƯỚC khi vào task)
     -> `EvaluationApiClient.cs:17-39` (`EvaluateAnswerAsync`, POST `api/evaluation/sessions/{sessionId}/answers/{sessionQuestionId}`) — **THẲNG `http://localhost:7006`, KHÔNG qua Gateway** (`InterviewAPI/Program.cs:50,90-93`)
     -> `EvaluationAPI/Controllers/EvaluationController.cs:42-58` (`EvaluateAnswer`)
        -> IDOR check: `:32-36` (`UserOwnsSessionAsync`) -> `EvaluationAPI/Clients/InterviewApiClient.cs:23-44` (`OwnsSessionAsync`, GET `api/interview/sessions/{id}/owns`) — **THẲNG `http://localhost:7005`, KHÔNG qua Gateway** (`EvaluationAPI/Program.cs:49-52`) -> `InterviewSessionController.cs:185-193` (`Owns`) -> `SessionService.cs:1629-1633`
     -> `EvaluationAPI/Services/EvaluationService.cs:28-72` (`EvaluateAnswerAsync`)
        -> guard chống chấm trùng: `:33-35` (`GetBySessionQuestionAsync` — đã chấm thì return luôn)
        -> `EvaluationAPI/Clients/AiServiceClient.cs:34-49` (POST `/internal/evaluate-answer`)
     -> ai-service `app/api/internal.py:85-102` (`evaluate_answer`) -> `app/services/evaluator.py:102` (GeminiJudge), `:127` (grade_item), `:233` (enrich_for)
     -> `EvaluationAPI/Repositories/EvaluationRepository.cs:17-22` (`AddEvaluationAsync`)
- Endpoint HTTP: `POST /api/interview/sessions/{id}/message` (qua Gateway) → nền → `POST http://localhost:7006/api/evaluation/sessions/{id}/answers/{answerId}` (thẳng) → `GET http://localhost:7005/api/interview/sessions/{id}/owns` (thẳng) → ai-service `POST /internal/evaluate-answer`
- Participant dự kiến: 7 — User (actor gián tiếp), Browser, WebClient, ApiGateway, InterviewAPI, EvaluationAPI, ai-service
- Số message ƯỚC LƯỢNG: ~10-12
- Khung rẽ nhánh: 3 alt (đã chấm rồi → trả evaluation cũ; không sở hữu session → 403; `response.Score is null` → throw "Grading failed", lỗi chỉ log nội bộ do chạy nền) / 0 opt
- Service CẤM SỬA có tham gia?: ApiGateway (chỉ ở chặng SendMessage qua Gateway; chặng nền InterviewAPI↔EvaluationAPI đi thẳng, bỏ qua Gateway)
- Class dùng chung với chức năng khác: `SessionService.SendMessageAsync`/`ScheduleGrading` (hạ tầng chung #5), `ApiResponse<T>`, `AnswerEvaluation` entity (chung #18)
- Ghi DB?: `AnswerEvaluations` INSERT (nếu chưa tồn tại theo SessionId+SessionQuestionId)
- Nhà cung cấp AI: Gemini (`evaluator.GeminiJudge`)
- Anomaly: **(1)** file `app/api/evaluation.py` + hàm `evaluate()` trong `evaluator.py` (đề bài gợi ý là điểm khởi đầu) THỰC RA KHÔNG nằm trong luồng #17/#18 — không AiServiceClient nào gọi tới `/evaluate`; luồng thật dùng `app/api/internal.py`. `evaluation.py` có thể là code chết/legacy cho use case này — CẦN LƯU Ý khi chọn entry point ai-service lúc vẽ thật. **(2)** `InterviewController.cs:445-446` (action `Message`, entry point thật của #17) dùng pattern KI-01 (xem #5).
- Rủi ro khi vẽ: background job không actor trực tiếp — **cần duyệt cách thể hiện actor gián tiếp + async trước khi vẽ thật** (quyết định của người dùng, không phải của tôi, đúng như DIAGRAM_SCOPE.md §5 đã lưu ý). Luồng thật lặp lại mỗi câu hỏi (không phải 1 lần/phiên) — nên vẽ 1 lần đại diện + note "lặp lại mỗi câu trả lời" thay vì vẽ N lần.

### #18 ViewEvaluationReport — BL#74, 76, 68, 69
- Entry point: `WebClient/Controllers/Evaluation/EvaluationController.cs:20-59` (`Report`, GET) — nguồn phát link tới trang này **CHƯA TRA ĐƯỢC** trong phạm vi khảo sát 2 chức năng này (khả năng cao từ Interview History, ngoài phạm vi #17/#18)
- Chuỗi gọi:
    Browser CHƯA TRA ĐƯỢC -> `EvaluationController.cs:20-59` (`Report`) -> `Services/Evaluation/EvaluationService.cs:35-132` (`GetReportAsync`), 4 bước tuần tự:
    B1: GET `api/interview/sessions/{id}/transcript` (`:40-45`) — [alt] nếu Status != "Completed" → EmptyReason, dừng sớm, KHÔNG gọi Evaluation nào (`:47-68`)
    B2: GET `api/evaluation/sessions/{id}/report` (`:71-74`)
    B3: [alt] nếu null → POST `api/evaluation/sessions/{id}/report` sinh mới (`:81-95`)
    B4: GET `api/evaluation/sessions/{id}/answers` (`:98-101`)
     -> Gateway `interview-route`(`:49-54`)/`evaluation-route`(`:67-72`), cả 2 không strip
     -> `EvaluationAPI/Controllers/EvaluationController.cs:84-104` (GetReport) / `:61-81` (BuildReport) / `:150-165` (GetAnswerEvaluations)
        -> IDOR check mỗi action: `:32-36` -> `InterviewApiClient.OwnsSessionAsync` (GET `.../owns`, THẲNG :7005)
     -> `EvaluationAPI/Services/EvaluationService.cs:200-208` (GetExistingReportAsync) / `:85-199` (BuildReportAsync) / `:227-241` (GetAnswerEvaluationsAsync)
        -> eligibility gate dùng chung: `:244-252` (`EnsureCompletedSessionAsync`) -> `InterviewApiClient.cs:46-79` (GET `.../feedback-eligibility`, THẲNG :7005) -> `InterviewSessionController.cs:148-164` -> `SessionService.cs:1527-1542` (Eligible = Status=="Completed")
        -> [chỉ nhánh BuildReport] `AiServiceClient.cs:51-66` (POST `/internal/report`) -> ai-service `internal.py:117-130` -> `evaluator.py:220` (build_report), `:185` (aggregate)
     -> `EvaluationRepository.cs:50-51`(ReportExistsAsync)/`:31-35`(GetEvaluationsBySessionAsync)/`:37-42`(AddReportAsync)/`:44-48`(AddMetricsAsync)/`:53-56`(GetReportBySessionAsync)
- Endpoint HTTP: GET `.../transcript`, GET `.../feedback-eligibility`, GET/POST `.../report`, GET `.../answers` (qua Gateway) + ai-service POST `/internal/report` (chỉ khi sinh mới)
- Participant dự kiến: 6 — User, Browser, WebClient, ApiGateway, InterviewAPI, EvaluationAPI (+ ai-service chỉ nhánh sinh mới)
- Số message ƯỚC LƯỢNG: ~20-24
- Khung rẽ nhánh: 4 alt (session chưa Completed → EmptyReason dừng sớm; report đã tồn tại → dùng luôn; report chưa có → POST sinh mới; không sở hữu session → 403) / 1 opt (BuildReport thất bại/0 evaluations → emptyReason, trang vẫn render)
- Service CẤM SỬA có tham gia?: ApiGateway (mọi hop Browser→WebClient); hop nội bộ EvaluationAPI↔InterviewAPI đi THẲNG, không qua Gateway. InterviewAPI KHÔNG thuộc danh sách cấm sửa.
- Class dùng chung với chức năng khác: `ApiResponse<T>`, `AnswerEvaluation` entity (ghi bởi #17, đọc bởi #18), `EvaluationRepository`/`EvaluationService` (EvaluationAPI, chung #17), `GetSessionFeedbackEligibilityAsync` (dùng chung với tính năng Submit Interview Feedback — ngoài phạm vi)
- Ghi DB?: chỉ nhánh sinh mới — `FeedbackReports` INSERT, `PerformanceMetrics` INSERT
- Nhà cung cấp AI: Gemini (chỉ nhánh sinh báo cáo mới)
- Anomaly: **(1)** `EnsureCompletedSessionAsync` bị gọi lặp lại độc lập ở CẢ BA action EvaluationAPI (mỗi lần bắn 1 request HTTP thật sang InterviewAPI) — trong khi WebClient's `Report` action cũng tự gọi `feedback-eligibility` riêng cho mục đích khác — dư thừa round-trip, không sai chức năng. **(2)** điều kiện `Status != "Completed" || !Eligible` dư thừa logic (Eligible luôn = Status=="Completed"). KI-01 KHÔNG xuất hiện trong `EvaluationController.cs` (action `Report` dùng `success`/`error` string, không dùng biến `statusCode`).
- Rủi ro khi vẽ: action `Report` gộp 2 mối quan tâm khác nhau (xem báo cáo #18 + hiển thị/nộp form Interview Feedback qua FeedbackAPI) — nếu vẽ literal sẽ kéo cả FeedbackAPI vào, dễ tràn trang và lẫn lộn chức năng; nên cắt nhánh FeedbackAPI ra khỏi diagram #18. Browser-hop (nguồn phát link) CHƯA TRA ĐƯỢC — cần xác nhận trước khi chốt participant đầu.

---

## BƯỚC 2 — Ba câu hỏi chéo

### 2.1 Bảng class dùng chung (xuất hiện ≥ 2 chức năng trong 17 file)

| Class / entity / helper | Số chức năng dùng | Tên các chức năng |
|---|---|---|
| `ApiResponse<T>` | 17 | tất cả — #1,#2,#3,#4,#5,#6,#8,#9,#10,#11,#12,#13,#14,#15,#16,#17,#18 |
| `SessionService` (InterviewAPI) | 7 | #3, #4, #5, #8, #9, #10, #11 |
| `SessionRepository` (InterviewAPI) | 7 | #3, #4, #5, #8, #9, #10, #11 |
| `InterviewSession` (entity) | 7 | #3, #4, #5, #8, #9, #10, #11 |
| `AiServiceClient`/`IAiServiceClient` (InterviewAPI) | 8 | #1, #2, #3, #4, #6, #13, #15, #16 |
| `SessionVerificationEvent` (entity) | 5 | #10, #13, #14, #15, #16 |
| `VerificationController`/`VerificationService` (WebClient) + `SessionVerificationController`/`BiometricVerificationService` (InterviewAPI) | 5 | #12, #13, #14, #15, #16 |
| `BiometricQualityException` | 5 | #1, #2, #13, #15, #16 |
| `ProfileBiometricsRepository`/`IProfileBiometricsRepository` | 4 | #1, #2, #13, #14 |
| `CheatingStrikeHelper.ComputeEffectiveStrikeCount` | 3 | #10, #15, #16 |
| `AttachVerificationCheckAsync`/`GetInSessionCheckAsync` (SessionService helper) | 3 | #4, #5, #15 |
| `PaymentApiClient`/`IPaymentApiClient` | 3 | #1, #2, #4 |
| `ChatMessage` (entity) | 3 | #4, #5, #9 |
| `InterviewRoom` (entity) | 3 | #4, #9, #10 |
| `SessionVerificationChallenge` (entity/Repository) | 2 | #12, #13 |
| `BiometricIdentityMismatchException` | 2 | #1, #2 |
| `EvaluationApiClient` (InterviewAPI → EvaluationAPI) | 2 | #5, #17 |
| `AnswerEvaluation` (entity, EvaluationAPI) | 2 | #17, #18 |
| `EvaluationRepository`/`EvaluationService` (EvaluationAPI) | 2 | #17, #18 |
| `IsTerminalStatus` helper | 2 | #9, #10 |
| `SessionQuestion` (entity) | 2 | #4, #5 |
| `InterviewApiClient` (EvaluationAPI → InterviewAPI, IDOR + eligibility) | 2 | #17, #18 |

**Ý nghĩa cho câu 2.1 (dùng làm dữ liệu quyết định cơ chế tái sử dụng YAML):** `ApiResponse<T>` xuất hiện ở **cả 17/17 file**, và một nhóm ~6-7 class (`SessionService`, `SessionRepository`, `InterviewSession`, `AiServiceClient`, `SessionVerificationEvent`, cụm Verification Controller/Service) mỗi class lặp lại ở **5-8 file**. Theo câu 0.3(a), generator **KHÔNG có cơ chế include/anchor** — mỗi lần các class này xuất hiện trong 1 file sẽ phải **copy-paste thủ công định nghĩa fields/methods** vào đúng `<feature>_classes.yaml` tương ứng (chỉ liệt kê hàm thuộc chức năng đang vẽ theo mục 2.3 QUY_TAC_VE_DIAGRAM.md, nên không hoàn toàn giống hệt nhau giữa các file — nhưng phần khung tên/kế thừa/fields chung vẫn phải gõ lại). Đây là chi phí thật cần lường trước khi bắt tay viết 17 file YAML, không phải lỗi cần sửa ngay — ghi nhận để bàn với người dùng ở giai đoạn kế tiếp.

### 2.2 KI-01 lan rộng bao xa

Grep toàn bộ `source/WebClient` với pattern `statusCode == 0 ? ... : statusCode` (và biến thể `result.StatusCode == 0 ? ...`), xác nhận **CHỈ CÓ 3 VỊ TRÍ THẬT** trong toàn bộ WebClient, cả 3 đều nằm trong **`Controllers/Interview/InterviewController.cs`** (không có file nào khác dùng pattern này — đã kiểm tra không sót bằng grep rộng hơn `== 0 \?` để loại trừ biến thể format khác, các kết quả còn lại đều là ternary phân trang không liên quan):

| File:Line | Action | Thuộc chức năng nào trong 18 file | Trạng thái |
|---|---|---|---|
| `InterviewController.cs:316` | `RecentSessions` | **Không thuộc phạm vi 18 file** — đây là BL#34 "List User Interview Sessions", đã bị loại khỏi phạm vi vẽ vì thuộc người khác (xem DIAGRAM_SCOPE.md §2) | Chưa fix, ngoài phạm vi vẽ diagram nhưng vẫn là bug thật nếu ai đó sửa lại `Assigned to` |
| `InterviewController.cs:445` | `Message` | **#5 ConductInterviewQA** | Chưa fix — hiện KHÔNG reachable thực tế (API `SendMessage` chỉ trả 200/409/429/400, không có nhánh 2xx+Success=false), nhưng code vẫn giữ pattern rủi ro |
| `InterviewController.cs:518` (KNOWN_ISSUES.md ghi 513-519, đã lệch 5 dòng do code đã chỉnh sửa từ lúc ghi KI-01 — không phải lỗi trích dẫn) | `GetHint` (Hint) | **#7 GenerateInterviewHint** (đã vẽ xong, pilot) | Đây chính là KI-01 gốc — reachable thật, đã có bằng chứng cụ thể trong KI-01 |

**Kết luận:** KI-01 KHÔNG lan rộng ra ngoài `InterviewController.cs` — không phát hiện pattern tương tự ở `VerificationController.cs`, `EvaluationController.cs`, `ProfileController.cs`, hay bất kỳ controller nào khác trong toàn bộ 18 file (mỗi card ở Bước 1 đã tự kiểm tra và xác nhận "KI-01 không xuất hiện" cho action tương ứng). Trong 3 vị trí, chỉ có `:518` (Hint, đã biết) là **reachable + có hậu quả xác nhận**; `:445` (Message, #5) có cùng pattern nhưng hiện chưa reachable trong thực tế; `:316` (RecentSessions) nằm ngoài phạm vi 18 file cần vẽ.

### 2.3 Xếp hạng quy mô theo số message ước lượng (giảm dần)

| # | Chức năng | Số message ước lượng | Nguy cơ vượt trang mẫu (2350×3800, Login=98 msg) |
|---|---|---|---|
| #4 | StartInterviewSession | **~110-130** | ⚠️ **VƯỢT** — cao nhất, vượt cả mốc Login rõ rệt |
| #5 | ConductInterviewQA | ~45-70 (có thể tới ~65-70 nếu mở rộng đầy đủ nhánh grading) | Cần theo dõi — cận biên nếu vẽ chi tiết |
| #6 | VoiceInteraction | ~45-55 | Cần theo dõi — cận biên |
| #3 | ConfigureInterviewSession | ~34 | Thấp |
| #1 | EnrollFaceBiometric | ~26 | Thấp |
| #2 | EnrollVoiceBiometric | ~26 | Thấp |
| #18 | ViewEvaluationReport | ~20-24 | Thấp |
| #8 | PauseResumeSession | ~16 | Thấp |
| #9 | EndInterviewSession | ~16 | Thấp |
| #13 | VerifyFaceEntryGate | 14 | Thấp |
| #11 | AcceptIntegrityPolicy | ~14 | Thấp |
| #16 | VerifyVoiceAnswer | 14 | Thấp |
| #10 | ReportCameraViolation | ~12 | Thấp |
| #15 | VerifyFaceInSession | 12 (nhưng nhiều alt lồng nhau nhất — rủi ro layout, không phải rủi ro số message) | Thấp về số lượng, cao về độ phức tạp nhánh |
| #17 | EvaluateInterviewAnswer | ~10-12 | Thấp |
| #12 | GetVerificationChallenge | 8 | Thấp |
| #14 | ValidateEntryGateStatus | 8 | Thấp |

**Chỉ #4 StartInterviewSession có nguy cơ vượt trang thật sự rõ ràng** (ước tính vượt Login 12-32 message). #5 và #6 ở mức cận biên, phụ thuộc cách vẽ nhánh nền/nhánh phụ có được rút gọn thành ref-box hay không. #15 tuy số message thấp nhưng có nhiều tầng alt lồng nhau nhất trong toàn bộ 17 file — rủi ro nằm ở layout/độ rộng frame chứ không phải chiều dài trang.

---

## Tóm tắt cuối

- **Bước 0**: Regression khớp 100% (98 message | 35 class | VALIDATE: OK). HANDOFF.md không tồn tại — đã dùng DIAGRAM_SCOPE.md/LAYOUT_CONSTANTS.md/KNOWN_ISSUES.md thay thế, trả lời đủ 3 câu hỏi bằng cách đọc trực tiếp code generator.
- **Bước 1**: Đã recon đủ **17/17 chức năng**, mỗi card có File:Line thật cho từng hop. Không chức năng nào có chuỗi gọi hoàn toàn "CHƯA TRA ĐƯỢC" — chỉ 2 điểm nhỏ chưa tra ra được: (a) nguồn phát trigger phân biệt "hết câu hỏi" trong #9 EndInterviewSession, (b) nguồn phát link Browser vào trang Report của #18 ViewEvaluationReport.
- **Bước 2**: Bảng class dùng chung cho thấy generator hiện tại (không có include/anchor) sẽ tốn công copy-paste đáng kể ở giai đoạn viết YAML thật. KI-01 xác nhận KHÔNG lan ngoài `InterviewController.cs`, chỉ 3 vị trí. #4 StartInterviewSession là chức năng duy nhất có nguy cơ tràn trang rõ ràng.
- **Anomaly mới**: 7 mục (KI-02 đến KI-08) đã ghi vào `docs/diagrams/KNOWN_ISSUES.md`, giữ nguyên KI-01. KHÔNG có code nào bị sửa trong quá trình khảo sát này.
