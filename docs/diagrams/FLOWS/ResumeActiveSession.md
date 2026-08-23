# FLOW — `ResumeActiveSession` · BL#`chưa có` · UC-48

> Đặt tại `docs/diagrams/FLOWS/ResumeActiveSession.md`
> File diagram: `#21` · `out/ResumeActiveSession.drawio`
>
> Trạng thái: `ĐÃ DUYỆT 2026-08-14` — Khảo sát: đọc trực tiếp `source/`.
>
> ⚠️ **KHÔNG CÓ DÒNG BACKLOG.** `GET api/interview/sessions/active` có thật trong code và có
> lối vào UI thật, nhưng backlog chưa bao giờ ghi nhận nó. BL#34 (`List User Interview Sessions`,
> `GET /api/interview/sessions`) là **endpoint khác** và thuộc thành viên khác — không tái sử dụng.
> Xem `KNOWN_DEVIATIONS.md` mục B1. Tên trang vì vậy không có hậu tố `[BL#...]` — cố ý, không sót.

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Tìm phiên `InProgress`/`Paused` mới nhất của user để mở lại phiên bỏ dở; đồng thời là guard chặn tạo phiên mới khi đang có phiên chạy |
| Điểm vào (user thao tác gì) | **2 lối vào, CÙNG 1 service method** — (A) mở Dashboard → banner "interview in progress" + nút Resume · (B) bấm Start khi đang có phiên → bị 409, redirect thẳng vào phòng |
| Kết thúc (user thấy gì) | (A) Banner + nút Resume, bấm vào là vào phòng · (B) Không thấy lỗi, được đưa thẳng vào phòng kèm `RoomInfoMessage` |
| Quy mô | 9 participant · 70 message · 12 class · 1 alt vào + 3 opt + 1 alt ra |
| Service ngoài quyền sở hữu | không |
| **Gọi BYPASS Gateway?** | không |
| Nhà cung cấp AI | không |
| Ghi DB | **CÓ (lazy, không hiển nhiên)** — auto-resume pause quá hạn; finalize phiên hết giờ + đóng room |
| Trừ credit | không |
| KI liên quan | không |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| `GetTimerAsync` cũng gọi `AutoResumeIfPauseExpiredAsync` | [SessionService.cs:1127](../../../source/InterviewAPI/Services/SessionService.cs#L1127) | Luồng khác, WebClient không gọi (KI-07) |
| `ResumeSessionAsync` (`GET .../{id}/resume`) | [SessionService.cs:1444](../../../source/InterviewAPI/Services/SessionService.cs#L1444) | Tên giống nhưng là endpoint khác — thuộc `#19 EnterInterviewRoom` |
| 2 lệnh config còn lại (`level`, `interaction`) ở lối vào B | [InterviewService.cs:170,177](../../../source/WebClient/Services/Interview/InterviewService.cs#L170) | Không bao giờ chạy tới: `config/duration` đã ném 409 trước |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| `StartInterviewAsync` — chuỗi 3 PUT config + init | `#3 ConfigureInterviewSession` | `10. StartInterviewAsync(...) [see #3]` | [InterviewService.cs:128-184](../../../source/WebClient/Services/Interview/InterviewService.cs#L128) |
| Mở phòng sau khi bấm Resume | `#19 EnterInterviewRoom` | `D.8. GET /Interview/Room/{id} [see #19]` | [Index.cshtml:29](../../../source/WebClient/Views/Interview/Index.cshtml#L29) |
| Mở phòng sau redirect 409 | `#19 EnterInterviewRoom` | `E.7. GET /Interview/Room/{id} [see #19]` | [InterviewController.cs:97](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L97) |

---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 1 | Candidate | actor | — | — |
| 2 | :Interview Dashboard UI | box | `Views/Interview/Index.cshtml` | — |
| 3 | :InterviewController (WebClient) | box | `Controllers/Interview/InterviewController.cs` | — |
| 4 | :InterviewService (WebClient) | box | `Services/Interview/InterviewService.cs` | — |
| 5 | :API Gateway | box | `interview-route` (không strip prefix) | có |
| 6 | :InterviewSessionController (InterviewAPI) | box | `Controllers/InterviewSessionController.cs` | — |
| 7 | :SessionService (InterviewAPI) | box | `Services/SessionService.cs` (SUBSET) | — |
| 8 | :SessionRepository (InterviewAPI) | box | `Repositories/SessionRepository.cs` (SUBSET) | — |
| 9 | SQL Server | cylinder | `DevPrep_Interview` | — |

## 4. BẢNG MESSAGE — các mốc nghiệp vụ

> Danh sách đầy đủ ở `specs/resumeactivesession.yaml` (đầu vào generator, bản có thẩm quyền).

| # | Nội dung | File:Line |
|---|---|---|
| 3 | Lối vào A — Dashboard đọc active session `[best-effort]` | [InterviewController.cs:64-66](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L64) |
| 7 | `GetActiveSessionAsync(userId, ct)` từ controller | [InterviewSessionController.cs:106](../../../source/InterviewAPI/Controllers/InterviewSessionController.cs#L106) |
| 13-15 | Lối vào B — `ConfigDurationAsync` → `ResolveSessionAsync` → **cùng service method** | [SessionService.cs:1809](../../../source/InterviewAPI/Services/SessionService.cs#L1809) |
| 17 | `WHERE Status IN ("InProgress","Paused") ORDER BY StartedAt DESC` | [SessionRepository.cs:28-33](../../../source/InterviewAPI/Repositories/SessionRepository.cs#L28) |
| **20 / B.*** | **Ghi DB ẩn 1** — auto-resume pause quá hạn (UPDATE nguyên tử) | [SessionService.cs:1160](../../../source/InterviewAPI/Services/SessionService.cs#L1160) → [SessionRepository.cs:342](../../../source/InterviewAPI/Repositories/SessionRepository.cs#L342) |
| **21 / C.*** | **Ghi DB ẩn 2** — finalize phiên hết giờ, đóng room, trả `null` | [SessionService.cs:1161](../../../source/InterviewAPI/Services/SessionService.cs#L1161) → [:160-190](../../../source/InterviewAPI/Services/SessionService.cs#L160) |
| D.6 | Banner + nút Resume | [Index.cshtml:24-31](../../../source/WebClient/Views/Interview/Index.cshtml#L24) |
| E.1 | `throw SessionGuardException(409, "active_session_exists")` | [SessionService.cs:1810-1812](../../../source/InterviewAPI/Services/SessionService.cs#L1810) |
| E.6 | `TempData["RoomInfoMessage"]` — **không hiện lỗi**, redirect vào phòng | [InterviewController.cs:89-97](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L89) |

## 5. KHUNG alt / opt

| Khung | Guard | File:Line |
|---|---|---|
| alt (vào) | lối vào A (Dashboard) / lối vào B (Start bị 409) | — |
| opt A | `session is null` → trả `Data=null`, không banner | [SessionService.cs:1153](../../../source/InterviewAPI/Services/SessionService.cs#L1153) |
| opt B | `Paused` và tổng pause ≥ 300s | [SessionService.cs:138-139](../../../source/InterviewAPI/Services/SessionService.cs#L138) |
| opt C | `InProgress`, có `StartedAt`, còn lại < −5s | [SessionService.cs:161-169](../../../source/InterviewAPI/Services/SessionService.cs#L161) |
| alt (ra) | A render banner / B ném 409 rồi redirect | — |

## 6. NOTE

Không có (§0-bis).

## 7-9. CLASS

Xem `class_specs/resumeactivesession.py` (12 class, back-end scope InterviewAPI). `ISessionRepository`
liệt kê cả `TryAutoResumeExpiredPauseAsync`/`HasSubstantiveAnswerAsync`/`UpdateRoomAsync` vì hai
tác dụng phụ ghi DB đều đi qua đây.

## 10. ANOMALY / BUG

| # | Nội dung |
|---|---|
| A1 | **Endpoint không có dòng backlog** — xem banner đầu file và `KNOWN_DEVIATIONS.md` B1. |
| A2 | **Method mang tên "Get" nhưng có thể GHI DB.** `GetActiveSessionAsync` gọi cả `AutoResumeIfPauseExpiredAsync` (UPDATE) lẫn `FinalizeIfTimeExpiredAsync` (UPDATE + đóng room). Một phiên vừa bị finalize sẽ được báo về là `null` — đúng ý đồ ([comment tại :1156-1159](../../../source/InterviewAPI/Services/SessionService.cs#L1156)) nhưng tên method không gợi ra điều đó. Ghi nhận, không sửa. |

## 11. QUYẾT ĐỊNH ĐÃ DUYỆT

1. **Vẽ cả 2 lối vào trong 1 alt** (duyentq yêu cầu). Được vì cả hai dùng đúng cùng dãy participant `0→1→2→3→4→5→6`, nên RULE3c (mọi section phải đóng ngăn xếp về cùng trạng thái) thoả.
2. **Hai tác dụng phụ ghi DB phải nhìn thấy được** (duyentq yêu cầu) — vẽ thành opt B và opt C thay vì gộp vào một self-message.
3. **Không vẽ tiếp vào phòng**: dừng ở cú click Resume / redirect, kèm `[see #19]`.
4. **Guard 409 thuộc file này, không thuộc `#3`**: `#3 ConfigureInterviewSession` chỉ tham chiếu chéo tới đây, vì cùng một `GetActiveSessionAsync` phục vụ cả hai.

## 12. THIẾU / CHƯA TRA ĐƯỢC

| # | Nội dung |
|---|---|
| T1 | Số dòng backlog cho `GET .../sessions/active` — chờ nhóm bổ sung, rồi điền vào `backlog:` trong `specs/resumeactivesession.yaml`. |
