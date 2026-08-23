# LAYOUT CONSTANTS — trích từ file mẫu `Login.drawio`

> Nguồn sự thật cho generator. Mọi số ở đây đo trực tiếp từ file mẫu đã được duyệt,
> KHÔNG phải suy đoán. Khi spec chữ (`QUY_TAC_VE_DIAGRAM.md`) và file này mâu thuẫn,
> **file này thắng** — vì nó phản ánh diagram thật đã qua kiểm.
>
> Đặt tại: `docs/diagrams/LAYOUT_CONSTANTS.md`
>
> ⚠️ **Cập nhật sau đợt sửa cắt-chữ:** `reference/Login.drawio` đã được **dựng lại từ generator**
> (không còn là file vẽ tay). Các con số toạ độ cột ở §2.2 và bề rộng hộp ở §2.1 dưới đây là
> **số của bản vẽ tay cũ, chỉ còn giá trị tham khảo lịch sử** — bề rộng hộp giờ do
> `cls.content_w()` tính theo nội dung, vị trí cột do `cls.auto_layout()` tính, không còn khai
> báo tay trong YAML. Số message/class của mốc regression **không đổi**: `98 message | 35 class`.

---

## 0. Tổng quan file mẫu

| | Trang 1 (Sequence) | Trang 2 (Class) |
|---|---|---|
| Tên trang | `Sequence - Login` | `Class - Login` |
| pageWidth × pageHeight | 2290 × 4327 | 3270 × 1410 |
| Số cell | 197 | 237 |
| Nội dung | 13 participant, 98 message, 32 activation bar, 4 frame, 17 lifeline | 35 hộp class, 37 edge (16 có waypoint), 11 line ngăn field/method |

`<mxfile ... pages="2">`, mỗi trang là `<diagram id="..." name="...">`.
Trang 1 `id="seq-login"`, đặt id theo mẫu `seq-<feature>` / `cls-<feature>`.

---

## 1. SEQUENCE — hằng số

### 1.1. Participant header

| Loại | style | w | h | y |
|---|---|---|---|---|
| Actor | `shape=umlActor;verticalLabelPosition=bottom;verticalAlign=top;html=1;outlineConnect=0;fillColor=#ffffff;strokeColor=#000000;` | 30 | 45 | 10 |
| Hộp thường | `rounded=0;whiteSpace=wrap;html=1;fillColor=#ffffff;strokeColor=#000000;` | 140–180 | 40 | 30 |
| Database | `shape=cylinder3;whiteSpace=wrap;html=1;boundedLbl=1;backgroundOutline=1;size=15;fillColor=#ffffff;strokeColor=#000000;` | 70 | 55 | 10 |

- Hộp mặc định **w=160**; tên dài (`:RefreshTokenRepository`, `:LoginAuditRepository`) → **w=180**; tên ngắn (`:API Gateway`, `:TokenService`) → **w=140**.
- Xuống dòng trong nhãn dùng `\n` thật: `:AuthController&#10;(WebClient)` — dòng 2 là `(WebClient)` / `(AuthAPI)`.
- Header **lặp lại ở đáy trang** với cùng x/w/h, y = pageHeight − ~150.

### 1.2. Toạ độ x thực tế của file mẫu

```
actor       45   (tâm  60)
:Login UI              130  w160  (tâm 210)
:AuthController (WC)   310  w160  (tâm 390)
:AuthService (WC)      490  w160  (tâm 570)
:API Gateway           670  w140  (tâm 740)
:AuthController (API)  840  w160  (tâm 920)
:AuthService (API)    1025  w160  (tâm 1105)
:BCryptPasswordHasher 1205  w170  (tâm 1290)
:TokenService         1390  w140  (tâm 1460)
:AccountRepository    1560  w160  (tâm 1640)
:RefreshTokenRepo     1740  w180  (tâm 1830)
:LoginAuditRepo       1930  w180  (tâm 2020)
SQL Server            2145  w70   (tâm 2180)
```

**Quy tắc suy ra:** `x[i+1] = x[i] + w[i] + 20`. Khoảng cách tâm–tâm ≈ **180**.

### 1.3. Lifeline + activation bar

- Lifeline: edge dọc, `dashed=1;html=1;strokeColor=#000000;endArrow=none;`, x = tâm participant, từ y≈70 (đáy header) tới đáy trang.
- Activation bar: `rounded=0;html=1;fillColor=#ffffff;strokeColor=#000000;`
  - **w = 10**, **x = tâm − 5**
  - y bắt đầu = y của call kích hoạt − ~5
  - h = (y return cuối cùng) − (y bắt đầu)
  - Actor cũng có bar (x=55, tức tâm 60 − 5).

### 1.4. Message

| Loại | style (nguyên văn) |
|---|---|
| CALL | `html=1;rounded=0;endArrow=block;endFill=1;strokeColor=#000000;fontSize=11;verticalAlign=bottom;` |
| RETURN | `html=1;rounded=0;dashed=1;endArrow=open;endFill=0;strokeColor=#000000;fontSize=11;verticalAlign=bottom;` |

- **y-step = 30** (chuẩn). Dùng 35–40 khi qua ranh giới nhánh, 60–70 khi mở/đóng frame.
- x xuất phát = tâm nguồn ± 5 (mép activation bar); x đích = tâm đích ∓ 5.
- Edge dùng **toạ độ tuyệt đối**: `<mxPoint as="sourcePoint">` / `<mxPoint as="targetPoint">`, KHÔNG connect vào cell.
- Nhãn: `<số>. <nội dung>` — vd `2. POST /Auth/Login (LoginViewModel)`, `9. Return account row / empty`.

### 1.5. Đánh số

- Luồng chính: `1.` `2.` `3.` …
- Trong frame: `<N>.<k>.` — vd nhánh sai mật khẩu `11.1` → `11.8`.
- Alt lồng trong alt: vẫn 2 cấp, tiếp tục đếm `16.21` → `16.26`.

### 1.6. Frame alt / opt

`shape=umlFrame;whiteSpace=wrap;html=1;width=60;height=25;fillColor=#ffffff;strokeColor=#000000;`

Kích thước thực trong file mẫu:

| value | x | y | w | h |
|---|---|---|---|---|
| `opt` | 140 | 170 | 340 | 90 |
| `alt` | 120 | 525 | 2150 | 3110 |
| `opt` | 1020 | 1845 | 440 | 80 |
| `alt` | 50 | 3365 | 440 | 255 |

- Guard: cell `text`, `text;html=1;align=left;verticalAlign=middle;fontStyle=1;fontSize=11;` — vd `[ModelState invalid]`. Đặt x = frame.x + 70, y = frame.y (ngang nhãn frame).
- Divider giữa 2 nhánh: edge ngang **không nhãn**, `dashed=1`, kéo hết chiều rộng frame.
- Frame bao trọn cả các message của nó, chừa lề ~20px mỗi bên.

---

## 2. CLASS — hằng số

### 2.1. Hộp class

```
swimlane;fontStyle=1;align=center;verticalAlign=top;childLayout=stackLayout;
horizontal=1;startSize=26;horizontalStack=0;resizeParent=1;resizeParentMax=0;
rounded=0;shadow=0;strokeWidth=1;fillColor=#ffffff;swimlaneFillColor=#ffffff;
fontSize=12;html=1;
```

- `startSize=26` → phần tên cao 26.
- Mỗi member = child `mxCell` style `text;strokeColor=none;fillColor=none;align=left;verticalAlign=middle;spacingLeft=6;spacingRight=6;overflow=hidden;points=[[0,0.5],[1,0.5]];portConstraint=eastwest;rounded=0;fontSize=11;html=1;`, **h = 20**, w = w hộp.
- Đường ngăn field/method = child style `line;strokeWidth=1;fillColor=none;align=left;verticalAlign=middle;spacingTop=-1;spacingLeft=3;spacingRight=3;rotatable=0;labelPosition=right;points=[];portConstraint=eastwest;strokeColor=#000000;`, **h = 8**.
- Chiều cao hộp = 26 + Σ(20 mỗi member) + 8 (nếu có đường ngăn).
- Hộp cha framework (`Controller`, `ControllerBase`, `DbContext`): w=180–220, h=40, thân rỗng.

### 2.2. Cột và hành lang

x các cột trong file mẫu:

```
40    → WebClient (Controller, IService, Service, IApiClient, ApiClient)
520   → ViewModel / Result / BackendResponse<T>
880   → AuthAPI (ControllerBase, Controller, IService, Service, Exception)
1430  → DTO request/response, ApiResponse<T>
1830  → ITokenService/TokenService, IPasswordHasher/Hasher, IAuditRepo/Repo
2260  → IAccountRepository/Repo, IRefreshTokenRepo/Repo, DbContext
2740  → Entity (Account, RefreshToken, LoginAudit)
```

- **Hành lang giữa cột: 80–120px** (mép phải hộp rộng nhất → x cột kế).
- Khoảng cách dọc giữa 2 hộp cùng cột: **60–110px**.
- **Interface đặt ngay TRÊN class implements nó, cùng x** — vd `IAuthService (WebClient)` y=340 → `AuthService (WebClient)` y=450. Nhờ vậy mũi tên realize là đoạn dọc ngắn, không cắt gì.

### 2.3. Quan hệ

| Quan hệ | Kiểu edge | Số trong mẫu |
|---|---|---|
| Dependency (`Use`, `throw`) | `dashed=1` + `endArrow=open` | 17 |
| Implements | `dashed=1` + `endArrow=block;endFill=0` (tam giác rỗng) | 8 |
| Extends | solid + `endArrow=block;endFill=0` | 5 |
| Composition | solid + `startArrow=diamondThin;startFill=1` | 6 |

- edgeStyle: `edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#000000;`
- **16/37 edge có waypoint** (`<Array as="points"><mxPoint .../></Array>`) — mọi edge băng cột đều phải có.

---

## 3. QUY TẮC VALIDATOR (đã hiệu chỉnh theo file mẫu)

> ⚠️ File mẫu có **31 CALL / 67 RETURN**. Một call trong khung `alt` có return riêng ở
> từng nhánh. Vì vậy KHÔNG được kiểm tra call:return theo tỉ lệ 1:1.

1. XML parse OK.
2. Không id trùng; mọi `parent`/`source`/`target` trỏ tới id tồn tại.
3. **Call/return theo đường đi**: với mỗi tổ hợp nhánh alt/opt (mỗi path gốc→lá), stack call
   phải đóng hết. Một call hợp lệ khi có ≥1 return trên **mỗi** nhánh nó đi qua.
   Self-message được miễn.
4. Mọi `<` `>` trong nhãn đã escape 2 lần (`&amp;lt;` / `&amp;gt;`) — grep tìm `&lt;`
   đơn lẻ trong attribute `value` là lỗi.
5. Mọi shape có `fillColor=#ffffff` và `strokeColor=#000000`; không màu nào khác.
6. **Hình học**: không hộp class nào giao nhau; không đoạn thẳng nào của edge cắt qua
   bounding box của hộp class (trừ hộp nguồn/đích của chính edge đó).
7. Đúng 2 trang, tên trang khớp `Sequence - ...` / `Class - ...`.
8. Mọi message nằm trong biên frame chứa nó; frame không chồng lấn sai cấp.

---

## 4. QUY ƯỚC ĐẶT TÊN

- File: `docs/diagrams/out/<TenChucNang>.drawio` — PascalCase, **không** chứa mã UC.
- Tên trang khi chưa có UC: `Sequence - Generate Interview Hint [BL#44]`
- Tên trang sau khi có UC: `Sequence - UC-10 Generate Interview Hint [BL#44]`
- `BL#` = số STT trong `SEP490_Backlog__2_.xlsx`, dùng để tra chéo độ phủ.
- Trong YAML spec:

```yaml
feature: GenerateInterviewHint
title: Generate Interview Hint
uc_id: ""          # để trống, điền "UC-10" sau
backlog: [44]
```
