# FLOW — `<FeatureName>` · BL#`<...>` · UC-`<...>`

> Đặt tại `docs/diagrams/FLOWS/<FeatureName>.md`
> **Vai trò:** nguồn dữ liệu chính để luồng 2 vẽ diagram. Luồng 2 được phép mở source đối
> chiếu, nhưng mọi trường phải bù từ code đều bị ghi vào §12 — coi như lỗi của file này.
>
> Trạng thái: `DRAFT` | `ĐÃ DUYỆT <ngày>` — Khảo sát: luồng 1, lô `<A-G>`, `<ngày>`
>
> ⚠️ **NGƯỠNG KÍCH THƯỚC.** Ước lượng trong RECON.md thấp hơn thực tế **5-6 lần** (đo trên lô A:
> 8→49, 14→71, 8→47, 12→79). Đừng tin con số RECON. Đếm thật, điền vào §0.
> Nếu §4 vượt **150 dòng message**: DỪNG, báo trước khi viết tiếp, kèm đề xuất tách trang
> Sequence theo pha (mốc so sánh: Login = 98 message trên 1 trang).

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | |
| Điểm vào (user thao tác gì) | |
| Kết thúc (user thấy gì) | |
| Quy mô | `<N>` participant · `<M>` message · `<X>` alt / `<Y>` opt |
| Đối chiếu ước lượng RECON | RECON đoán `<n>` · thật `<M>` · hệ số `<M/n>×` |
| Service ngoài quyền sở hữu | `<tên + vì sao>` / `không` |
| **Gọi BYPASS Gateway?** | `<service + port, vd PaymentAPI :7008 gọi thẳng>` / `không` |
| Nhà cung cấp AI | Gemini / Piper TTS / ElevenLabs TTS / Deepgram / InsightFace / SpeechBrain / ChromaDB / `không` |
| Ghi DB | `<bảng + thao tác>` / `không` |
| Trừ credit | `có (<số>)` / `không` |
| KI liên quan | `<KI-0x>` / `không` |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| | | vd: route legacy test-UI · dead code (KI-05) · endpoint không caller |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

> Khác §1: phần này **có vẽ**, nhưng thu gọn thành **đúng 1 cặp call + return**, kèm note
> trỏ sang file diagram khác. Không mở rộng nội dung bên trong.
> ⚠️ Đây là cơ chế chính giữ #4 không phình. Bỏ trống mục này = diagram sẽ phình.

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| vd: biometrics entry gate | `#13 VerifyFaceEntryGate` | `VerifyEntryGate(sessionId)` | |

---

## 3. PARTICIPANT

> Thứ tự = thứ tự **được gọi lần đầu**. `SQL Server` hình trụ, ngoài cùng bên phải.
> Trùng tên 2 project → kèm `(WebClient)` / `(InterviewAPI)`.
> ⚠️ Service gọi bypass Gateway thì **KHÔNG** vẽ hop ApiGateway cho message đó.
>
> **ACTOR GIÁN TIẾP — quy ước chung, không quyết theo từng file.**
> Ranh giới duy nhất: *người dùng có mặt lúc luồng chạy hay không.*
> - **Có mặt** (luồng chạy tự động nhưng user đang ngồi trước màn hình, vd kiểm tra khuôn mặt
>   giữa phiên) → **vẫn dùng actor `User`**, message đầu ghi rõ đây là trigger tự động
>   (vd `[auto] Face check triggered on new question`).
> - **Không có mặt** (user đã rời đi, tác vụ chạy nền, vd chấm điểm fire-and-forget)
>   → **KHÔNG vẽ actor hình người**. Participant trái nhất là service khởi phát
>   (vd `:SessionService (InterviewAPI)`), kèm note giải thích đây là tác vụ nền.

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 1 | User | actor | — | — |
| 2 | :`<Feature>` UI | box | | — |
| … | | box / cylinder | | có / **không (bypass)** |

## 4. BẢNG MESSAGE

> Dòng không có `File:Line` thì **không được đưa vào bảng**. Mọi `call` phải có `ret`
> tương ứng kể cả nhánh lỗi; ngoại lệ duy nhất là self-message.
> Đánh số: luồng chính `1,2,3…`; trong khung `N.1, N.2…`.
>
> ⛔ **CẤM VIẾT TẮT.** Không được ghi kiểu `F3.2..F3.6` hay "lặp lại như trên". Luồng 2 sẽ
> phải tự bung ra, tức tự quyết nội dung — đúng thứ mô hình 2 luồng sinh ra để ngăn.
> Mỗi dòng message là một dòng bảng.
>
> ✅ **CÁCH ĐÚNG ĐỂ GỌN — gộp nhánh lỗi trùng khít (xem §5).** Nếu nhiều nhánh lỗi có chuỗi
> return **giống hệt nhau**, đừng vẽ 5 section alt lặp lại: gộp thành **1 section, guard ghép**
> bằng `OR`, viết **một** chuỗi return đầy đủ. Vẫn giữ trọn quy tắc "mọi call có return" và
> "mỗi nhánh return về tận Actor".
> Điều kiện gộp — phải thoả **cả hai**: cùng HTTP status, **và** cùng nội dung user nhìn thấy.
> Lệch một trong hai thì tách riêng.

| # | From | To | call/ret | Label (tiếng Anh) | Frame (guard) | File:Line | Cờ |
|---|---|---|---|---|---|---|---|
| 1 | User | | call | | — | | |

**Cờ:** `[best-effort]` (try/catch nuốt lỗi, vd `ScheduleGrading`) · `self` · `throw` · `boundary` (theo §2)

## 5. KHUNG alt / opt

| Khung | Loại | Guard (tiếng Anh) | Bao message # → # | Lồng trong | Gộp từ mấy nhánh? |
|---|---|---|---|---|---|
| F1 | alt | `[...]` | | — | — |
| F2 | alt | `[sessionId invalid OR session not found OR challenge expired]` | | F1 | 3 (cùng 400, cùng thông điệp) |

## 6. NOTE — KHÔNG DÙNG

Diagram không chứa hộp note nào (`QUY_TAC_VE_DIAGRAM.md` §0-bis). Câu giải thích đi vào
§10 (bug), §11 (quyết định vẽ / hành vi cố ý) hoặc §2 (boundary) của chính file này.

---

## 7. CLASS — danh sách

> `FULL` = 100% thành viên (class nhỏ, entity, DTO, exception, DbContext, hộp framework rỗng)
> `SUBSET` = chỉ hàm thuộc chức năng này (class lớn dùng chung: SessionService…)
> `SHARED` = lấy từ `specs/_shared/classes/<tên>.yaml`, **không gõ lại** ở §8
>
> **Danh sách SHARED đã duyệt** (✅ = đã có file `specs/_shared/classes/<tên>.yaml`, tự `include:`;
> ⏳ = đã duyệt nhưng CHƯA có file — tạo khi lần đầu chạm tới, không cần hỏi lại):
> ✅ `ApiResponse` · ✅ `InterviewSession` · ✅ `AiServiceClient` · ✅ `SessionVerificationEvent` ·
> ✅ `BiometricQualityException` · ✅ `InterviewRoom` · ✅ `CheatingStrikeHelper` ·
> ✅ `ControllerBase` · ⏳ `ChatMessage` · ⏳ `SessionQuestion` ·
> ⏳ `BiometricIdentityMismatchException` · ⏳ `InterviewDbContext` · ⏳ `IApiClient` ·
> ⏳ `ApiClient` · ⏳ hộp framework rỗng `Controller`/`DbContext`.
> ⚠️ **`IAiServiceClient` (interface) KHÔNG include chung file với `AiServiceClient` (class hiện
> thực)** — thân class có field private `_httpClient`/`_logger` mà interface không có. Khai báo
> `IAiServiceClient` cục bộ ở mỗi file cần nó (method y hệt, fields rỗng); chỉ class hiện thực mới
> `include: AiServiceClient.yaml`.
> Tiêu chí để đề xuất thêm: **liệt kê FULL và nội dung bất biến giữa các chức năng.**
> ⛔ KHÔNG bao giờ SHARED: `SessionService`, `SessionRepository`,
> `BiometricVerificationService`, các Controller nghiệp vụ — nhóm SUBSET, nội dung khác nhau.

| # | Tên hiển thị | Loại | File:Line | Liệt kê | Nguồn |
|---|---|---|---|---|---|
| | | class/interface/entity/DTO/framework-base | | FULL / SUBSET | SHARED / local |

## 8. CLASS — chi tiết

> Chỉ viết khối cho class có `Nguồn = local`. Class `SHARED` bỏ qua ở đây.
> Ghi kiểu **đầy đủ** (`Task<List<Guid>>`). Viết `<` `>` bình thường, generator tự escape.

### `<TênClass>` (`<project>`)
- File: `<path:line>` — Liệt kê: `FULL` | `SUBSET`
- Field: `- _field: Type`
- Method: `+ MethodAsync(param: Type): Task<Result>`
- *(SUBSET)* Đã bỏ `<n>` method ngoài chức năng này.

**Class SHARED lần đầu xuất hiện trong toàn dự án** → dán nội dung đầy đủ vào đây kèm ghi chú
"→ tạo file `_shared/classes/<tên>.yaml`", để luồng 2 tạo file shared. Các lần sau chỉ ghi tên.

## 9. QUAN HỆ CLASS

> Quan hệ luôn khai báo cục bộ, kể cả với class SHARED.

| Loại | From | To | Label |
|---|---|---|---|
| extends / implements / use / composition / 1..* | | | `Use` · `throw` · `1..*` · — |

---

## 10. ANOMALY / BUG

> Không sửa code. Cách vẽ theo phân loại đã chốt:
> bug hành vi có hậu quả thật → **vẽ hành vi ĐÚNG** + note ·
> comment/tài liệu lệch code → **vẽ hành vi THẬT** ·
> race condition → **vẽ luồng thường** ·
> fire-and-forget → **vẽ thật**, gắn cờ `[best-effort]` ·
> code chết → §1 hoặc note theo quyết định phạm vi.

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| | | | | | |

## 11. QUYẾT ĐỊNH CẦN DUYENTQ DUYỆT

| # | Vấn đề | Phương án | Đề xuất |
|---|---|---|---|

## 12. THIẾU / CHƯA TRA ĐƯỢC

> Thà trống có ghi chú còn hơn đoán. Luồng 2 gặp mục ở đây sẽ dừng hỏi thay vì bịa.
> Luồng 2 bổ sung vào bảng này mọi trường phải tự mở code để bù.

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ | Ai ghi (luồng 1/2) |
|---|---|---|---|
