# SCHEMA FILE YAML CHO GENERATOR

> Đọc file này trước khi viết `specs/<feature>.yaml`. Đây là schema **thật** mà
> `_tools/build.py` đọc — không suy đoán từ nơi khác.
> File mẫu đầy đủ: `specs/login.yaml` + `specs/login_classes.yaml`.

---

## 1. `specs/<feature>.yaml` — trang Sequence

```yaml
feature: GenerateInterviewHint        # PascalCase, dùng làm tên file .drawio
title: Generate Interview Hint        # hiện trong tên trang
uc_id: "UC-09"                        # LEGACY — xem cảnh báo ngay dưới
uc_label: "[UC-45]"                   # ĐOẠN UC CÓ THẨM QUYỀN, xem dưới
backlog: [44]                         # list số STT trong backlog

participants:                         # thứ tự = thứ tự trái -> phải
  - name: User                        # actor
    kind: actor                       # actor | box | db
    w: 30
  - name: ":Interview Room UI"
    kind: box
    w: 160                            # 140 tên ngắn · 160 mặc định · 180 tên dài
  - name: "SQL Server"
    kind: db
    w: 70

flow:                                 # danh sách item, theo thứ tự thời gian
  - {d: 0, t: 1, k: call, l: "1. click Ask for hint"}
  - {d: 1, t: 1, k: call, l: "2. askHint()", self: true}
  - frame: opt                       # opt | alt — GIÁ TRỊ TRỰC TIẾP, `sections:` là khoá
    sections:                        # NGANG CẤP với `frame:`, KHÔNG lồng bên trong nó
      - guard: "[!currentQuestionId OR ended]"
        body:
          - {d: 1, t: 0, k: ret, l: "2.1. Return (no request sent)"}
  - {d: 1, t: 2, k: call, l: "4. POST /Interview/Hint"}
```

### `uc_label` vs `uc_id` — ĐỪNG "dọn dẹp" `uc_id`

`uc_label` là **nguồn có thẩm quyền** cho đoạn UC trong tên trang. Nó mang nguyên cả đoạn đã
định dạng nên một khoá phủ được cả bốn dạng: `"[UC-41]"` · `"[UC-32, 33, 39]"` ·
`"(UC-33 sub-flow)"` · `"(UC-42, 56 sub-flow)"`. Tên trang sinh ra là
`Sequence|Class - <title> <uc_label> [BL#...]`.

`uc_id` (tiền tố kiểu cũ, sinh ra `Sequence - UC-09 <title>`) **được giữ lại có chủ đích**, vì
2 lý do: (1) truy vết — nó ghi lại số UC mà file từng mang trước đợt đổi tên 2026-08-14;
(2) `_tools/seq.py` / `cls.py` vẫn còn nhánh fallback dùng `uc_id` khi thiếu `uc_label`, và
`specs/login.yaml` là spec DUY NHẤT còn chạy qua nhánh đó — nó chính là regression phủ nhánh này
(`python build.py login` phải luôn ra `Sequence - Login [BL#2]` + `98 message | 35 class`).
Xoá `uc_id` khỏi các spec sẽ khiến nhánh fallback không còn gì kiểm. **Không xoá.**

⚠️ **Lỗi cú pháp hay gặp:** viết `frame:` như một mapping lồng (`frame:\n    type: opt\n
sections:`) sẽ báo `yaml.scanner.ScannerError`. Đúng phải là `- frame: opt` (scalar) rồi
`sections:` là khoá **em của cùng item đó**, không phải khoá con của `frame:`.

### Trường của một message

| Khoá | Bắt buộc | Ý nghĩa |
|---|---|---|
| `d` | ✔ | index participant **nguồn** (0-based, theo thứ tự trong `participants`) |
| `t` | ✔ | index participant **đích** |
| `k` | ✔ | `call` (nét liền, tam giác đặc) hoặc `ret` (nét đứt, đầu mở) |
| `l` | ✔ | nhãn, đã kèm số thứ tự ở đầu — vd `"9. HintAsync(userId, id, qid)"` |
| `self` | ✘ | `true` cho self-message → validator miễn kiểm return |

### Khung alt / opt

```yaml
- frame: alt
  sections:
    - guard: "[question is null OR not owned by user]"
      body: [ ... danh sách message hoặc frame lồng ... ]
    - guard: "[question belongs to this session AND user]"
      body: [ ... ]
```

- `sections` nhiều phần tử → generator tự vẽ đường nét đứt ngăn giữa các nhánh.
- Frame lồng nhau: đặt một `- frame: opt`/`- frame: alt` khác bên trong `body`.
- `guard` viết trong ngoặc vuông, tiếng Anh, khớp điều kiện thật trong code.

### Ngữ nghĩa `opt` vs `alt` — RULE3/RULE3c dựa đúng vào đây

`_tools/validate.py` (RULE3) duyệt **toàn bộ** flow, kể cả khung thứ 2 trở đi và khung lồng bên
trong khung khác — bản vá 2026, trước đó chỉ kiểm khung đầu tiên của mỗi file (kể cả `login.yaml`
gốc, đã lọt 1 lỗi thật: 3 section rỗng thừa khiến 6 call không có return, xem lịch sử sửa). Validator
phân biệt:

- **`opt`** = nhánh **có thể thoát sớm** (early exit). Guard `false` → không có gì xảy ra, phần
  sau khung tiếp tục với ngăn xếp **y như trước khung**. Guard `true` mà section đó tự đóng lại
  đúng những gì NÓ mở (không đụng tới call mở từ trước khung, như `opt J` trong
  `VerifyFaceInSession`) → cũng tiếp tục bình thường vào phần sau khung. Nhưng nếu section đóng
  luôn cả call mở từ TRƯỚC khung (ví dụ throw rồi return ngược hết về Actor, như F1-F5 trong
  `VerifyFaceEntryGate`/`VerifyFaceInSession`) → validator coi đó là **điểm kết thúc** (dead end),
  không nối tiếp phần sau khung (nếu nối, sẽ báo lỗi giả cho các message không bao giờ thật sự chạy
  tới).
- **`alt`** = **hội tụ**, không có nhánh "bỏ qua" — luôn có đúng 1 section chạy, và **mọi section
  phải đóng ngăn xếp về đúng 1 trạng thái** sau khung (RULE3c) vì bất kể nhánh nào chạy, phần sau
  khung vẫn y hệt nhau.

Hệ quả thực hành: viết đủ return cho MỌI nhánh lỗi **bên trong chính khung đó** (đừng viết tắt rồi
định "để message sau khung tự đóng nốt" — với `opt` điều đó không hoạt động, vì nhánh thoát sớm
không nối vào phần sau).

### Note — ĐÃ BỎ

Khoá `notes:` không còn được dùng. Diagram không chứa hộp note nào; mọi câu giải thích ghi vào
`FLOWS/<Feature>.md` (§10 / §11 / §2) — xem `QUY_TAC_VE_DIAGRAM.md` §0-bis.

Nhãn message tối đa 60 ký tự và chỉ dùng 6 dạng cố định — xem `QUY_TAC_VE_DIAGRAM.md` §0-ter.

---

## 2. `specs/<feature>_classes.yaml` — trang Class

```yaml
classes:
  - name: "InterviewController (WebClient)"
    column: 0                         # 0 = trái nhất; generator tự xếp cột + hành lang
    w: 380                            # tuỳ chọn, mặc định 320
    fields:
      - "- _interviewService: IInterviewService"
    methods:
      - "+ Hint(request: HintProxyRequest): Task<IActionResult>"

  - name: Controller                  # class cha framework: để rỗng fields/methods
    column: 0
    fields: []
    methods: []

relations:
  - {from: "InterviewController (WebClient)", to: Controller, type: extends}
  - {from: "InterviewService (WebClient)", to: IInterviewService, type: implements}
  - {from: "InterviewController (WebClient)", to: IInterviewService, type: use, label: Use}
  - {from: InterviewDbContext, to: SessionQuestion, type: composition, label: "1..*"}
```

### `type` hợp lệ

| type | Kiểu vẽ |
|---|---|
| `extends` | nét liền + tam giác rỗng |
| `implements` | nét đứt + tam giác rỗng |
| `use` | nét đứt + đầu mở (label `Use` hoặc `throw`) |
| `composition` | nét liền + diamond đặc phía "chứa" |

### Quy tắc generator tự lo — KHÔNG cần khai báo

- Xếp cột, hành lang 100px, khoảng cách dọc 60px.
- **Interface tự động đặt ngay trên class implements nó, cùng x** → mũi tên realize là đoạn dọc ngắn.
- Edge băng cột tự sinh waypoint, mỗi edge một lane riêng (18px) → không chồng nhau.
- Chiều cao hộp tự tính: 26 + 20×(số member) + 8 (đường ngăn field/method).
- `pageWidth` / `pageHeight` tự tính.

### Viết kiểu generic

Viết `<` `>` **bình thường** trong YAML — `emit.esc()` lo escape 2 lần:

```yaml
methods:
  - "+ GetHintAsync(id: Guid): Task<(bool, string?, string?)>"
```

Sinh ra `Task&amp;lt;...&amp;gt;`, draw.io hiện đúng `Task<...>`.
**KHÔNG** tự gõ `&lt;` trong YAML — sẽ thành escape 3 lần.

### Quy ước escape — MỘT nguồn duy nhất

> **YAML luôn chứa ký tự thật. `emit.esc()` là nơi DUY NHẤT được escape.**
> `cls.py` và `seq.py` không tự escape — cả hai đi qua `vertex()` / `child()` /
> `free_edge()` / `cell_edge()`, và tất cả đều gọi chung `emit.esc()`.

Viết `Task<List<Guid>>` trong YAML. **KHÔNG** gõ `&lt;` — chuỗi đó sẽ bị escape thêm một lần
nữa và draw.io hiện ra **nguyên chữ** `Task&lt;List&lt;Guid&gt;&gt;` thay vì `Task<List<Guid>>`.

Lỗi này đã từng xảy ra thật: `specs/login_classes.yaml` có 55 chỗ gõ sẵn `&lt;`/`&gt;` và
`specs/login.yaml` có 4 chỗ — cả hai **đã được sửa về ký tự thật** (bản cũ giữ ở
`specs/*.yaml.bak`), `reference/Login.drawio` đã dựng lại từ bản đã sửa.
**`validate.py` RULE14 giờ chặn tận gốc**: nó giải mã nhãn đúng như draw.io rồi báo lỗi nếu
kết quả còn hiện ra chữ `&lt;` / `&gt;` / `&amp;`. Lưu ý RULE14 **không** cấm ký tự `&` đơn lẻ
— nhãn `GET ...?sessionId&modality=face` là hợp lệ; chỉ chuỗi entity còn sót lại mới là lỗi.

### `include:` — tái dùng class dùng chung giữa nhiều file spec

Generator gốc **không có** cơ chế include/anchor (đã xác nhận khi khảo sát `_tools/build.py`).
Đã thêm 1 cơ chế nhỏ trong `_tools/build.py` (hàm `resolve_includes`, chạy trước khi gọi
`cls.build`) để tránh phải copy-paste tay các class dùng ở nhiều chức năng
(`ApiResponse<T>` dùng ở 17/17 file, `InterviewSession` 7 file, `AiServiceClient` 8 file...).

**Vị trí file shared:** `docs/diagrams/specs/_shared/classes/<Ten>.yaml` — mỗi file chứa
**đúng 1 khối class**, cùng khoá `name`/`fields`/`methods` như 1 phần tử trong `classes:` bình
thường, nhưng **KHÔNG có `x`/`y`/`column`** (vị trí luôn do nơi include quyết định — xem lý do ở
mục A0(a) recon: `cls.py` chỉ tự động xếp cột (`auto_layout`) khi **mọi** class trong file thiếu
`x`; nếu 1 class thiếu `x` mà class khác đã có `x` tường minh, `auto_layout` sẽ CHẠY và GHI ĐÈ vị
trí của TẤT CẢ class trong file, kể cả những class vốn đã có `x`. Vì vậy: file spec mới **chỉ nên
dùng `column` cho mọi class, không bao giờ trộn với `x`/`y` thủ công** — làm vậy include mới an
toàn).

Khai báo ở `specs/<feature>_classes.yaml`, khoá cấp cao nhất `include:`:

```yaml
include:
  - file: ApiResponse.yaml              # tên file trong specs/_shared/classes/
    name: "ApiResponse<T> (InterviewAPI)"  # tuỳ chọn — đổi tên hiển thị/dùng trong relations
    column: 3                            # tuỳ chọn — cột layout (giống class thường)
    w: 300                               # tuỳ chọn — bề rộng hộp

classes:
  - name: InterviewSessionController
    column: 2
    ...

relations:
  - {from: InterviewSessionController, to: "ApiResponse<T> (InterviewAPI)", type: use, label: Use}
```

- `resolve_includes` nạp từng file shared, merge `name`/`column`/`w` cục bộ đè lên bản shared, rồi
  nối vào `classes:` TRƯỚC khi `cls.build` chạy — từ đó về sau `include:` vô hình, mọi thứ (layout,
  quan hệ, escape) chạy y như class khai báo tay.
- **Trùng tên** (class cùng tên đã có trong `classes:` cục bộ, hoặc 2 include cùng resolve về 1
  tên) → **báo lỗi rõ ràng và dừng build**, không âm thầm ghi đè. Muốn include cùng 1 file shared
  2 lần trong 1 trang (ví dụ `ApiResponse<T>` xuất hiện ở cả nhánh InterviewAPI lẫn EvaluationAPI
  trong cùng 1 chức năng) → bắt buộc đặt `name:` khác nhau ở mỗi include để phân biệt (đúng quy
  tắc "trùng tên giữa 2 project → thêm hậu tố project" ở `QUY_TAC_VE_DIAGRAM.md` mục 2.2).
- File shared không tồn tại → báo lỗi kèm đường dẫn, không bỏ qua âm thầm.
- **Quan hệ (extends/implements/use/composition) KHÔNG đặt trong file shared** — mỗi chức năng có
  tập quan hệ khác nhau với cùng 1 class, nên `relations:` luôn khai báo ở file cục bộ, tham chiếu
  tới đúng `name` đã resolve (kể cả khi tên đó tới từ include).
- File `specs/login_classes.yaml`/`specs/login.yaml` **không dùng include** — đây là fixture đối
  chứng cho regression `python build.py login` (phải luôn ra `98 message | 35 class |
  VALIDATE: OK`; con số này KHÔNG đổi sau đợt sửa layout, vì chỉ hình học thay đổi chứ số
  message/class thì không). Hai file này đã được chuyển từ toạ độ `x`/`y` thủ công sang
  `column:` để bề rộng hộp tính theo nội dung — xem mục bề rộng dưới đây.

### Bề rộng hộp class — tính theo nội dung, KHÔNG khai báo tay

`cls.content_w()` đo chữ bằng bảng advance width Helvetica thật (`emit.text_w`) và lấy
`max(bề rộng tên class in đậm 12px, bề rộng thành viên dài nhất 11px) + padding`, làm tròn lên
bội số 10 và nhân hệ số an toàn 1.04. Mọi hộp trong **cùng một cột dùng chung một bề rộng** =
bề rộng lớn nhất của cột đó — nhờ vậy interface và class implement nó thẳng tâm, mũi tên realize
là đoạn dọc ngắn thay vì phải đi vòng.

Khoá `w:` trong YAML chỉ còn là **cận dưới** (`max(w, content_w)`), không bao giờ làm hẹp hộp.
`S_MEMBER` có `overflow=hidden` nên hộp hẹp hơn nội dung sẽ **cắt cụt chữ giữa chừng** — RULE12
chặn đúng trường hợp này.

**Danh sách file shared hiện có** (`specs/_shared/classes/`, kiểm bằng `ls` — cập nhật 2026, lô A):

| File | Class | Dùng cho |
|---|---|---|
| `ApiResponse.yaml` | `ApiResponse<T>` (Success/Message/Data + `Ok`/`Fail`) | Y HỆT nhau ở `InterviewAPI/DTOs/ApiResponse.cs` và `EvaluationAPI/DTOs/ApiResponse.cs` (đã đối chiếu từng dòng) — include kèm `name: "ApiResponse<T> (InterviewAPI)"` hoặc `"... (EvaluationAPI)"` tuỳ chức năng đang vẽ nằm ở service nào. |
| `InterviewSession.yaml` | `InterviewSession` (entity, FULL) | Mọi chức năng InterviewAPI đụng tới session (#12-#15…). |
| `InterviewRoom.yaml` | `InterviewRoom` (entity, FULL) | Từ #15 `VerifyFaceInSession` — mọi chức năng đóng/đọc room (#15, #9 EndInterviewSession…). |
| `SessionVerificationEvent.yaml` | `SessionVerificationEvent` (entity, FULL) | #13-#16 (mọi check biometrics trong phiên). |
| `AiServiceClient.yaml` | `AiServiceClient` (class hiện thực, FULL — có field `_httpClient`/`_logger`) | Mọi chức năng InterviewAPI gọi ai-service. **Chỉ include cho CLASS hiện thực, không include cho interface `IAiServiceClient`** — xem ngoại lệ ngay dưới. |
| `BiometricQualityException.yaml` | `BiometricQualityException` (exception, FULL) | #13-#16. |
| `CheatingStrikeHelper.yaml` | `CheatingStrikeHelper` (static helper, FULL) | Từ #15 — dùng chung #10 `ReportCameraViolation`, #15, #16 `VerifyVoiceAnswer`. |
| `ControllerBase.yaml` | `ControllerBase` (framework, rỗng) | Mọi controller InterviewAPI/EvaluationAPI. |

**Ngoại lệ đã gặp — interface và class hiện thực KHÔNG dùng chung 1 file shared:** `IAiServiceClient`
là interface (không field private) trong khi `AiServiceClient.yaml` là THÂN class hiện thực (có
`_httpClient`/`_logger`) — include cùng 1 file cho cả hai sẽ gắn nhầm field private vào interface.
Cách làm đúng (đã áp dụng ở #13-#15): khai báo `IAiServiceClient` cục bộ (method y hệt, fields rỗng)
ở mỗi file cần nó, chỉ `include: AiServiceClient.yaml` cho class hiện thực `AiServiceClient (InterviewAPI)`.

**⚠️ KHÔNG dùng `ApiResponse.yaml` cho phía WebClient.** `WebClient/Models/Shared/ApiResponse.cs`
trùng TÊN nhưng khác THÂN class — chỉ có `Success`/`Message`/`Data` (fields), **không có**
`Ok`/`Fail` (nó chỉ là DTO để deserialize JSON, không phải class dựng response). Nếu 1 chức năng
cần vẽ `ApiResponse<T> (WebClient)`, khai báo class đó cục bộ (hoặc tạo file shared riêng
`ApiResponse_WebClient.yaml` nếu về sau nhiều file cùng cần) — KHÔNG include nhầm file này.

**Danh sách SHARED đã duyệt nhưng CHƯA có file vật lý** — nêu tên trong
`FLOWS/FLOWS__TEMPLATE.md` §7 để lô sau khỏi phải hỏi lại, nhưng tính tới lô A **chưa có chức năng
nào thực sự cần vẽ** nên chưa tạo file: `ChatMessage`, `SessionQuestion`,
`BiometricIdentityMismatchException`, `InterviewDbContext`, `IApiClient`, `ApiClient`, hộp
framework rỗng `Controller`/`DbContext`. Gặp trước ở lô B → tạo file `_shared/classes/<Tên>.yaml`
theo đúng mẫu (FULL, không `x`/`y`/`column`), rồi thêm 1 dòng vào bảng trên — không cần hỏi lại vì
đã nằm trong danh sách đã duyệt.

**Class KHÔNG nên tạo file shared** (dù xuất hiện ≥ 2 chức năng): `SessionService`,
`SessionRepository`, `BiometricVerificationService`, và mọi Controller/Service nghiệp vụ khác.
Lý do: theo `QUY_TAC_VE_DIAGRAM.md` mục 2.3, các class lớn dùng chung nhiều chức năng chỉ liệt kê
**hàm thuộc chức năng đang vẽ** — nội dung `methods:` do đó khác nhau giữa các file, không có
1 bản "chuẩn" nào để chia sẻ. Chỉ chia sẻ được các class/entity/DTO/exception phải liệt kê **100%
thành viên** bất kể chức năng nào (xem quy tắc tương ứng trong `QUY_TAC_VE_DIAGRAM.md`).

---

## 3. Build và validate

```bash
cd docs/diagrams/_tools
python build.py generateinterviewhint     # tên file, không có .yaml
```

Kết quả mong đợi:

```
-> .../out/GenerateInterviewHint.drawio  | 62 message | 31 class
VALIDATE: OK
```

Regression kiểm generator còn nguyên vẹn:

```bash
python build.py login
# phải ra: 98 message | 35 class | VALIDATE: OK
```

---

## 4. Validator báo lỗi gì

| Rule | Nội dung |
|---|---|
| 1 | XML parse được |
| 2 | Không id trùng trong 1 trang; mọi `parent`/`source`/`target` tồn tại |
| 3 | **Theo từng đường đi gốc→lá**: mọi call phải có return trên mỗi nhánh nó đi qua. KHÔNG phải tỉ lệ 1:1 — file mẫu Login có 31 call / 67 return vì alt nhiều nhánh |
| 4 | Không nhãn nào escape 1 lần |
| 5 | Mọi shape trắng-đen |
| 6 | Không hộp class chồng nhau |
| 7 | Đúng 2 trang, tên trang đúng định dạng |

Lỗi RULE3 in ra tên nhánh + nhãn call thiếu return — sửa YAML, đừng tắt rule.
