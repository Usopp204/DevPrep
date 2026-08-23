# FLOW — `GenerateInterviewHint` · BL#44 · UC-`UC-09`

> Đặt tại `docs/diagrams/FLOWS/GenerateInterviewHint.md`
> Trạng thái: `PARTIAL` — tạo ở prompt 07 (bỏ note / rút nhãn), **không phải** từ một lượt khảo sát code.
>
> ⚠️ **File này chưa đầy đủ.** `GenerateInterviewHint` là chức năng pilot, được vẽ ở một phiên
> trước khi quy ước `FLOWS/<Feature>.md` ra đời, nên nó chưa bao giờ có file FLOW. Prompt 07 cấm
> đọc `source/` và `ai-service/`, nên các mục cần tra code — §1 loại trừ, §3 participant đầy đủ,
> §5–§9 khung/note/class, §12 — **chưa viết được**. Cột `File:Line` trong §4 vì vậy để trống.
>
> File được tạo để 3 note gỡ khỏi diagram và bảng nhãn ngắn/chi tiết có chỗ lưu, không bị mất.
> Ai làm lượt khảo sát code cho chức năng này: điền nốt các mục còn thiếu, đừng tạo file mới.

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Ứng viên bấm nút Hint trong phòng phỏng vấn để xin một gợi ý cho câu hỏi hiện tại; mỗi câu hỏi chỉ được xin hint **một lần** |
| Điểm vào (user thao tác gì) | Bấm `hintBtn` trong Interview Room |
| Kết thúc (user thấy gì) | Bong bóng chat của AI kèm biểu tượng gợi ý và nội dung hint, đọc to bằng TTS (best-effort) |
| Quy mô | `13` participant · `81` message · `34` class |
| Nhà cung cấp AI | **Gemini** (qua `GeminiJudge` trong ai-service) |
| KI liên quan | **KI-01** (xem §10) |

---

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| Đọc to nội dung hint bằng TTS | `#6 VoiceInteraction` | `speak(hintText) [best-effort]` | — |

---

## 4. BẢNG MESSAGE

> Cột **Label** = đúng chữ đang hiện trên diagram. Cột **Chi tiết** = phần văn giải thích đã được
> gỡ khỏi nhãn ở prompt 07 (giữ lại nguyên văn để không mất thông tin).
>
> Bảng này chưa có cột `File:Line` — xem cảnh báo ở đầu file.

*(chưa điền — cần một lượt khảo sát code)*

---

## 10. ANOMALY / BUG

| ID | File:Line | Mô tả | Phân loại | Diagram vẽ gì | Đã ghi KNOWN_ISSUES? |
|---|---|---|---|---|---|
| KI-01 | `InterviewController.cs:513-519` | Xem mục gỡ từ note bên dưới | forward status code thô | hành vi ĐÃ SỬA | có |


### Gỡ từ note trên diagram — anomaly

- **(cạnh message `15.21`)** Diagram reflects the FIXED behavior of InterviewController.cs:513-519 (currently returns 200, not 502 - see KNOWN_ISSUES.md KI-01). Code fix not applied in this session. *(gỡ từ note trên diagram)*

## 11. QUYẾT ĐỊNH ĐÃ TỰ CHỐT (ghi lại, không chặn)

| # | Vấn đề | Đã chọn | Lý do |
|---|---|---|---|
| 1 | Chức năng chưa có file FLOW | Tạo file `PARTIAL` này | Prompt 07 cấm đọc code nên không viết đủ được; vẫn cần chỗ lưu note đã gỡ và bảng nhãn |


### Gỡ từ note trên diagram — quyết định vẽ / hành vi cố ý

- **(cạnh message `4`)** JwtDelegatingHandler attaches the Bearer token from the access_token cookie before every WebClient -> Gateway call (not shown as a separate participant). *(gỡ từ note trên diagram)*
- **(cạnh message `15.19`)** WebClient also guards against malformed JSON (JsonException) - omitted for clarity. *(gỡ từ note trên diagram)*

## 12. THIẾU / CHƯA TRA ĐƯỢC

| Mục | Vì sao chưa tra được | Ai/việc gì để gỡ |
|---|---|---|
| §1, §3, §5–§9, §12 và cột `File:Line` của §4 | Prompt 07 cấm đọc `source/` và `ai-service/` | Một lượt khảo sát code riêng cho `GenerateInterviewHint` |
