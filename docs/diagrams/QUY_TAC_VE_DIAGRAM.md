# QUY TẮC VẼ SEQUENCE + CLASS DIAGRAM (draw.io) — DỰ ÁN DevPrep

> File này là "spec" chuẩn. Khi user yêu cầu vẽ diagram cho một chức năng, ĐỌC FILE NÀY TRƯỚC
> rồi làm đúng 100% theo nó. File mẫu chuẩn đã vẽ xong: `docs/diagrams/reference/Login.drawio` (mở tham khảo khi cần — MỐC BẤT BIẾN, không sửa).

---

## 0. QUY TẮC CHUNG

1. **Mỗi chức năng = 1 file `.drawio` có 2 trang**:
   - Trang 1 tên `Sequence - <TênChứcNăng>` (vd `Sequence - Login`)
   - Trang 2 tên `Class - <TênChứcNăng>`
   - Lưu tại **`docs/diagrams/out/<TênChứcNăng>.drawio`** (vd `Login.drawio`)
2. **Trắng đen tuyệt đối**: mọi shape `fillColor=#ffffff;strokeColor=#000000`, không màu.
3. **PHẢI đọc code thật trước khi vẽ** — cả 2 phía:
   - WebClient: `source/WebClient` (Controller → Service → IApiClient)
   - Backend API tương ứng: `source/<Service>API` (Controller → Service → Repository → Model)
   - Contracts dùng chung: `contracts/auth`
   - Flow trong diagram phải khớp từng lời gọi hàm trong code, KHÔNG bịa.
4. Nhãn/label viết **tiếng Anh**.

---


## 0-bis. KHÔNG DÙNG NOTE TRÊN DIAGRAM

Diagram **không được chứa hộp note nào**. Khoá `notes:` trong `specs/<feature>.yaml` đã bỏ.

Mọi câu giải thích trước đây đặt trong note nay ghi vào `FLOWS/<Feature>.md`:

| Nội dung note | Ghi vào mục |
|---|---|
| giải thích bug / hành vi sai | §10 ANOMALY |
| giải thích quyết định vẽ | §11 QUYẾT ĐỊNH |
| giải thích boundary sang file khác | §2 THAM CHIẾU CHỨC NĂNG KHÁC |
| còn lại | §11 |

**Thông tin boundary không được mất**: chỗ nào trước đây dùng note để trỏ sang file diagram khác
thì đưa hậu tố ngắn vào chính nhãn message — `GetEntryGateAsync(id, ct) [see #14]`.

## 0-ter. NHÃN MESSAGE — ĐÚNG CHỮ KÝ, KHÔNG PHẢI CÂU VĂN

Nhãn tối đa **60 ký tự** (không tính số thứ tự). Chỉ dùng 5 dạng sau:

| Dạng | Ví dụ |
|---|---|
| Gọi hàm | `GenerateQuestionAsync(userId, sessionId, ct)` |
| HTTP | `POST api/interview/sessions/{id}/message` |
| SQL | `UPDATE InterviewSessions SET Status` — verb + bảng + cột chính |
| Return | `Return 400 {error}` · `Return InterviewSession?` · `Return` |
| Self-call | `hintBtn.disabled = true` |
| UI | `Show "Challenge unavailable"` |

**Cắt khỏi nhãn**: câu văn giải thích hành vi · lý do/điều kiện trong ngoặc (điều kiện thuộc
**guard của khung**, không thuộc nhãn) · nội dung chuỗi lỗi đầy đủ · chú thích nguồn gốc, số dòng
code, tên biến nội bộ.

**Giữ lại**: tên hàm đúng như code · tham số chính · HTTP method + path · mã status · số thứ tự ·
hậu tố `[best-effort]` và `[see #N]`.

Quá 60 ký tự sau khi cắt → rút tham số thành `(...)`. Vẫn quá → **DỪNG và hỏi**, không cắt cụt tên hàm.

Cùng một hàm xuất hiện ở 2 file phải mang **cùng một nhãn**.

---

## 1. QUY TẮC VẼ SEQUENCE DIAGRAM

### 1.1. Participant (trái → phải, đúng thứ tự này)
1. **Actor hình người** (`shape=umlActor`) — tên theo vai trò dùng chức năng:
   - Chức năng của khách/người dùng thường → `User`
   - Chức năng khu admin → `Admin`; khu manager → `Manager`
2. **`:<TênChứcNăng> UI`** — vd `:Login UI`, `:Register UI` (hình chữ nhật).
3. Từ UI trở đi bám flow code, thường là:
   `:XxxController (WebClient)` → `:XxxService (WebClient)` → `:API Gateway` → `:XxxController (TênAPI)` → `:XxxService (TênAPI)` → các `:Repository` / `:Client` → **SQL Server**.
4. **Class trùng tên ở 2 project** (vd `AuthController` có ở cả WebClient lẫn AuthAPI) → ghi kèm nơi sống: `(WebClient)` / `(AuthAPI)`.
5. **Database = hình trụ** (`shape=cylinder3`) tên `SQL Server`, đặt ngoài cùng bên phải.
6. Header participant là **hình chữ nhật vuông** (`rounded=0`), lặp lại ở **cả đầu trang lẫn cuối trang** (giống mẫu). Actor + cylinder cũng lặp dưới đáy.

### 1.2. Lifeline + activation
- Lifeline = đường **nét đứt dọc** từ header xuống đáy.
- **Activation bar** = hình chữ nhật trắng hẹp (width 10) đè lên lifeline, kéo dài đúng khoảng thời gian object đang xử lý. Mọi mũi tên phải xuất phát/kết thúc ở mép activation bar (x = tâm ± 5).

### 1.3. Message
- **Call (gọi hàm)**: mũi tên **nét liền, đầu tam giác đặc** (`endArrow=block;endFill=1`). Nhãn = tên hàm đúng code, vd `RegisterAsync(model)`, `GetByEmailAsync(email)`; với HTTP thì ghi method + path, vd `POST api/auth/login`.
- **Return**: mũi tên **nét đứt, đầu mở** (`dashed=1;endArrow=open`), nhãn `Return ...`.
- ⚠️ **MỌI call ĐỀU PHẢI có mũi tên Return tương ứng — KHÔNG dùng "return ngầm"** (yêu cầu cứng của user, hội đồng hay bắt lỗi thiếu return). Kể cả `AddAsync`/`SaveChangesAsync`/`INSERT` trong nhánh lỗi cũng phải vẽ Return trước khi vẽ mũi tên `throw`. Không có dữ liệu trả về thì nhãn chỉ cần `Return`. Ngoại lệ duy nhất: self-message (gán biến trên chính object) không cần return.
- Repository → SQL Server ghi câu lệnh mức khái quát: `SELECT Account by Email`, `INSERT Account + 2 EmailVerificationTokens`, `DELETE expired Pending account`...
- **Đánh số**: luồng chính `1, 2, 3,...`; message nằm trong khung alt/opt đánh `N.1, N.2,...` (vd nhánh thứ nhất `11.1 → 11.6`, nhánh hai `12.1 → 12.10`).
- Call best-effort trong code (bọc try/catch nuốt lỗi, không chặn luồng) → thêm hậu tố `[best-effort]` vào nhãn.

### 1.4. Rẽ nhánh (BẮT BUỘC khớp if/else trong code)
- Code có `if ... else` (hoặc nhiều nhánh kết cục khác nhau) → khung **`alt`** (`shape=umlFrame`, value=`alt`), mỗi nhánh 1 section, ngăn nhau bằng **đường nét đứt ngang**, mỗi section có **guard** in đậm dạng `[điều kiện]` (vd `[account exists AND (IsEmailVerified OR Status != "Pending")]`, `[email available for registration]`).
- Code có `if` không else / early-return → khung **`opt`** với guard tương ứng (vd `[ModelState invalid]`, `[expired Pending account exists]`). Khung opt được lồng trong alt nếu code lồng nhau.
- Mỗi nhánh phải vẽ **đủ chuỗi return về tận Actor** (user thấy gì: error message / redirect trang nào).
- Backend ném exception nghiệp vụ (`ConflictException`, `BadRequestException`...) → vẽ return nét đứt nhãn `throw XxxException` rồi chuỗi `Return 409/400 {error}` về. **KHÔNG thêm note lên diagram**: việc `ExceptionHandlingMiddleware` dịch exception thành JSON + HTTP status ghi vào `FLOWS/<Feature>.md` §11.

### 1.5. Layout gợi ý
- Khoảng cách giữa các message ~30px; giữa các lifeline 140–210px tuỳ độ dài tên.
- Trang rộng thoải mái (pageWidth 2300–2500) — đủ chỗ, không chồng chữ.

---

## 2. QUY TẮC VẼ CLASS DIAGRAM

### 2.1. Chọn class
- Vẽ **TẤT CẢ class tham gia tạo nên chức năng đó**, cả 2 phía:
  - WebClient: Controller, IService + Service, IApiClient + ApiClient, ViewModel, Result/ResponseData, BackendResponse&lt;T&gt;
  - Backend: Controller, IService + Service, IRepository + Repository, các I*Client + *Client, DTO request/response, ApiResponse&lt;T&gt;, Exception, Model (entity), DbContext
  - Contracts (record dùng chung giữa các service) nếu chức năng có gọi liên service.
- Vẽ cả **class cha của framework** để nối kế thừa: `Controller` (WebClient MVC), `ControllerBase` (API), `DbContext` (EF) — hộp chỉ có tên, thân rỗng.

### 2.2. Tên class
- **Đúng y tên trong code** (`AuthService`, `IAccountRepository`, `BCryptPasswordHasher`...).
- **KHÔNG ghi stereotype `<<interface>>` / `<<abstract>>` / `<<record>>` trong tên** (user đã bỏ). Interface nhận biết qua tên bắt đầu bằng `I` + mũi tên implements.
- Trùng tên giữa 2 project → thêm `(WebClient)` / `(AuthAPI)` sau tên.

### 2.3. Thành viên trong hộp class
- Cấu trúc hộp: **tên (in đậm, giữa)** → hàng field → **đường kẻ ngang** → hàng method.
- Visibility: `+` public, `-` private (đúng theo code).
- Ghi kiểu đầy đủ: `+ RegisterAsync(request: RegisterRequest): Task<RegisterResponse>`, `- _context: AuthDbContext`.
- **Model (entity) ghi ĐỦ 100% thuộc tính** (kể cả navigation property như `+ EmailVerificationTokens: ICollection<EmailVerificationToken>`). **DbContext ghi đủ mọi DbSet** + constructor.
- Class/interface **nhỏ** (repository, client, hasher, DTO, ViewModel): liệt kê **đủ 100% hàm/thuộc tính**.
- Class **lớn dùng chung nhiều chức năng** (AuthController, AuthService, IAuthService...): chỉ liệt kê **hàm thuộc chức năng đang vẽ** + các hàm private helper mà luồng đó gọi (vd Register: `GenerateOtp()`, `TryRemoveExpiredPendingAccountAsync()`...) để hộp không dài cả trang.

### 2.4. Quan hệ (mũi tên)
| Quan hệ | Kiểu vẽ | Ví dụ |
|---|---|---|
| Kế thừa (extends) | nét **liền** + tam giác **rỗng** trỏ về cha | `AuthController → ControllerBase`, `AuthDbContext → DbContext`, `ConflictException → AppException` |
| Hiện thực (implements) | nét **đứt** + tam giác **rỗng** trỏ về interface | `AuthService ⇢ IAuthService`, `AccountRepository ⇢ IAccountRepository` |
| Phụ thuộc (dùng) | nét **đứt** + đầu mở, label `Use` (hoặc `throw` với exception) | `AuthController ⇢ IAuthService`, `AuthService ⇢ ConflictException` |
| Composition | nét liền + **diamond đặc** ở phía "chứa" | `AccountRepository ◆— AuthDbContext`, `AuthDbContext ◆— Account` |
| Quan hệ 1-nhiều giữa entity | diamond + label `1..*` | `Account ◆—1..*→ EmailVerificationToken` |

### 2.5. Layout — ĐƯỜNG DÂY KHÔNG ĐƯỢC ĐÈ LÊN CLASS (yêu cầu cứng của user)
- Nhóm theo khu: WebClient bên trái → tầng web API giữa → repository/client/model bên phải.
- **Interface đặt NGAY TRÊN class implements nó** → mũi tên realize/extends là đoạn dọc ngắn, không cắt gì.
- Chừa **hành lang trống** (corridor ~80-100px) giữa các cột + 1 lane trống trên đỉnh trang; mọi edge dài phải đi men theo hành lang.
- Mỗi edge dài set `exitX/exitY` + `entryX/entryY` + **waypoint** (`<Array as="points">`) — TUYỆT ĐỐI không để đường cắt ngang/đè lên hộp class. Vẽ xong rà từng edge: đoạn ngang nằm ở y nào, đoạn dọc ở x nào, có hộp nào chiếm vùng đó không.

---

## 3. KỸ THUẬT DRAW.IO (BẮT BUỘC — tránh lỗi đã từng gặp)

1. ⚠️ **Escape 2 lần dấu `< >` trong nhãn**: nhãn dùng `html=1` nên draw.io render text như HTML — nếu chỉ escape 1 lần (`&lt;`) thì `Task<List<Guid>>` sẽ bị nuốt thành `Task>`. Trong XML phải ghi `&amp;lt;` / `&amp;gt;`:
   - SAI:  `value="Task&lt;List&lt;Guid&gt;&gt;"` → hiện `Task>`
   - ĐÚNG: `value="Task&amp;lt;List&amp;lt;Guid&amp;gt;&amp;gt;"` → hiện `Task<List<Guid>>`
2. **Hộp class** = `swimlane;fontStyle=1;childLayout=stackLayout;startSize=26;horizontalStack=0;collapsible=0;rounded=0;fillColor=#ffffff;swimlaneFillColor=#ffffff;html=1` — mỗi member là 1 child `text` cao 20, đường kẻ giữa field/method là child `line` cao 8; child có `parent=<id hộp>`, geometry y cộng dồn.
3. **Sequence dùng toạ độ tuyệt đối**: mũi tên là edge có `sourcePoint`/`targetPoint` (mxPoint) — không connect vào cell để giữ layout cố định. Nhãn edge để `verticalAlign=bottom` (chữ nằm trên đường).
4. Khung alt/opt = `shape=umlFrame` (value `alt`/`opt`), guard = cell `text` in đậm, divider = edge nét đứt ngang.
5. File có thể ghi thành nhiều phần (Write từng chunk vào scratchpad rồi nối) nếu dài.
6. **Sau khi ghi file phải kiểm tra**:
   - Parse XML OK (PowerShell: `[xml](Get-Content file -Raw)`)
   - Không có id trùng; mọi `parent`/`source`/`target` đều trỏ tới id tồn tại.

---

## 4. CHECKLIST KHI USER YÊU CẦU VẼ CHỨC NĂNG MỚI

1. Đọc file quy tắc này.
2. Đọc code luồng chức năng ở cả WebClient lẫn API (controller → service → repo → model → client liên service).
3. Liệt kê participant + đếm message, xác định các if/else để đặt alt/opt.
4. Dựng file `.drawio` 2 trang theo đúng mục 1–3 (tham khảo cấu trúc `reference/Login.drawio`).
5. Validate XML (mục 3.6).
6. Báo user: đường dẫn file + tóm tắt flow + các nhánh alt/opt đã vẽ.
