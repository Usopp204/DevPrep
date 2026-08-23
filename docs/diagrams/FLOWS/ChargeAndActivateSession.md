# FLOW — `ChargeAndActivateSession` · BL#54 · UC-43

> Đặt tại `docs/diagrams/FLOWS/ChargeAndActivateSession.md`
> File diagram: `#20` · `out/ChargeAndActivateSession.drawio`
>
> Trạng thái: `ĐÃ DUYỆT 2026-08-14` — Khảo sát: đọc trực tiếp `source/`, lô tách #20 khỏi #19.
>
> **Nguồn gốc:** file này được TÁCH khỏi `#19 EnterInterviewRoom` (Phase 2 cũ, 81 message).
> Lý do tách nằm ở §11 mục 1 — tóm tắt: đây là điểm trừ credit DUY NHẤT của cả InterviewAPI,
> và nó đang nằm trong một file mang tên "vào phòng phỏng vấn".

---

## 0. TÓM TẮT

| | |
|---|---|
| Mục đích chức năng | Kích hoạt phiên phỏng vấn: chuyển `Initialized → InProgress`, **trừ credit đúng một lần**, đặt `StartedAt` (khởi động đồng hồ), rồi phát câu hỏi chưa trả lời đầu tiên |
| Điểm vào (user thao tác gì) | Không bấm trực tiếp. `InterviewController.Room` gọi tự động mỗi lần vào/refresh phòng ([InterviewController.cs:280](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L280)) |
| Kết thúc (user thấy gì) | Phòng phỏng vấn hiện câu hỏi hiện tại + đồng hồ đếm ngược + camera; hoặc bị đẩy về Dashboard kèm banner lỗi |
| Quy mô | 12 participant · 102 message · 20 class · 8 khung opt |
| Service ngoài quyền sở hữu | **PaymentAPI (:7008)** — chỉ gọi qua `IPaymentApiClient`, không sửa |
| **Gọi BYPASS Gateway?** | **CÓ** — `PaymentApiClient` gọi thẳng PaymentAPI `:7008`, không qua Gateway |
| Nhà cung cấp AI | không (câu hỏi đã được `#4 StartInterviewSession` sinh sẵn từ trước) |
| Ghi DB | `SessionQuestions.AskedAt` · `InterviewSessions.Status/CreditCost/StartedAt` · `INSERT ChatMessage` |
| Trừ credit | **CÓ** — `CreditConfig.GetCreditCost(DurationMinutes)`, 2 credit/phút |
| KI liên quan | không |

---

## 1. LOẠI TRỪ — code CÓ nhưng KHÔNG vẽ

| Đoạn code | File:Line | Lý do |
|---|---|---|
| `GetEffectivePausedSeconds` trong tính `elapsedSeconds` | [SessionService.cs:789](../../../source/InterviewAPI/Services/SessionService.cs#L789) | Số học thuần, không đổi trạng thái; gộp vào self-message 25 |
| `_logger.LogInformation` ở Step D | [SessionService.cs:708-711](../../../source/InterviewAPI/Services/SessionService.cs#L708) | Ghi log, không phải nghiệp vụ |

## 2. THAM CHIẾU CHỨC NĂNG KHÁC (boundary — vẽ thu gọn)

| Đoạn boundary | Thuộc file diagram nào | Vẽ thành 1 message nhãn gì | File:Line |
|---|---|---|---|
| 3 guard vào phòng đã chạy xong trước khi tới đây | `#19 EnterInterviewRoom` | `1. Room entry guards passed [see #19]` | [InterviewController.cs:243-278](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L243) |
| Đọc audit event Entry để kiểm face đã pass | `#14 ValidateEntryGateStatus` | `12. ListBySessionAsync(sessionId, "Entry", ct) [see #14]` | [SessionService.cs:667](../../../source/InterviewAPI/Services/SessionService.cs#L667) |
| Trả về phòng để render | `#19 EnterInterviewRoom` | `30. View Room with the current question [see #19]` | [InterviewController.cs:287](../../../source/WebClient/Controllers/Interview/InterviewController.cs#L287) |

---

## 3. PARTICIPANT

| # | Tên hiển thị | Loại | Class/file thật | Qua Gateway? |
|---|---|---|---|---|
| 1 | Candidate | actor | — | — |
| 2 | :Interview Room UI | box | `Views/Interview/Room.cshtml` | — |
| 3 | :InterviewController (WebClient) | box | `Controllers/Interview/InterviewController.cs` | — |
| 4 | :InterviewService (WebClient) | box | `Services/Interview/InterviewService.cs` | — |
| 5 | :API Gateway | box | `interview-route` (không strip prefix) | có |
| 6 | :InterviewSessionController (InterviewAPI) | box | `Controllers/InterviewSessionController.cs` | — |
| 7 | :SessionService (InterviewAPI) | box | `Services/SessionService.cs` (SUBSET) | — |
| 8 | :SessionRepository (InterviewAPI) | box | `Repositories/SessionRepository.cs` (SUBSET) | — |
| 9 | :SessionVerificationEventRepository | box | `Repositories/SessionVerificationEventRepository.cs` | — |
| 10 | :PaymentApiClient (InterviewAPI) | box | `Clients/PaymentApiClient.cs` | — |
| 11 | :PaymentAPI (:7008) | box | service khác | **KHÔNG — gọi thẳng** |
| 12 | SQL Server | cylinder | `DevPrep_Interview` | — |

## 4. BẢNG MESSAGE — các mốc nghiệp vụ

> Danh sách message đầy đủ, đúng thứ tự, nằm ở `specs/chargeandactivatesession.yaml` — đó là
> đầu vào của generator nên nó là bản có thẩm quyền. Bảng dưới chỉ ghi các mốc có hệ quả.

| # | Nội dung | File:Line |
|---|---|---|
| 7 | `GenerateQuestionAsync(userId, sessionId, ct)` | [SessionService.cs:651](../../../source/InterviewAPI/Services/SessionService.cs#L651) |
| 8 | Step A — nạp session kèm câu hỏi, kiểm quyền + trạng thái | [SessionService.cs:655-659](../../../source/InterviewAPI/Services/SessionService.cs#L655) |
| 12 | Step A.1 — cổng sinh trắc, phải có Entry/Face `Pass` | [SessionService.cs:665-672](../../../source/InterviewAPI/Services/SessionService.cs#L665) |
| 16 | Step A.2 — bắt buộc đã chấp nhận Integrity Policy | [SessionService.cs:677](../../../source/InterviewAPI/Services/SessionService.cs#L677) |
| 17 | Step B — tìm câu hỏi chưa có `ChatMessage` User/Answer | [SessionService.cs:680-693](../../../source/InterviewAPI/Services/SessionService.cs#L680) |
| E.1 | Step C — `AskedAt = now` lần phát đầu tiên | [SessionService.cs:700-704](../../../source/InterviewAPI/Services/SessionService.cs#L700) |
| **19** | **Step D — UPDATE nguyên tử `Initialized → InProgress`** | [SessionService.cs:710](../../../source/InterviewAPI/Services/SessionService.cs#L710) → [SessionRepository.cs:250](../../../source/InterviewAPI/Repositories/SessionRepository.cs#L250) |
| **F.2** | **Step D.1 — TRỪ CREDIT** | [SessionService.cs:718](../../../source/InterviewAPI/Services/SessionService.cs#L718) |
| G.1 | Rollback `InProgress → Initialized` khi trừ thất bại | [SessionService.cs:725](../../../source/InterviewAPI/Services/SessionService.cs#L725) |
| **F.10** | **Đặt `CreditCost` + `StartedAt` — khởi động đồng hồ** | [SessionService.cs:744-746](../../../source/InterviewAPI/Services/SessionService.cs#L744) |
| 23 | Step E — guard `alreadyAsked` chống chèn câu hỏi trùng | [SessionService.cs:755-759](../../../source/InterviewAPI/Services/SessionService.cs#L755) |
| K.1 | Hoàn credit khi INSERT ChatMessage lỗi | [SessionService.cs:781](../../../source/InterviewAPI/Services/SessionService.cs#L781) |

## 5. KHUNG alt / opt

| Khung | Guard | File:Line |
|---|---|---|
| opt A | session null / không sở hữu / sai trạng thái | [SessionService.cs:656-659](../../../source/InterviewAPI/Services/SessionService.cs#L656) |
| opt B | chưa có Entry event Face=Pass | [SessionService.cs:670](../../../source/InterviewAPI/Services/SessionService.cs#L670) |
| opt C | chưa chấp nhận Integrity Policy | [SessionService.cs:54-56](../../../source/InterviewAPI/Services/SessionService.cs#L54) |
| opt D | đã trả lời hết → `Ended=true` | [SessionService.cs:696-697](../../../source/InterviewAPI/Services/SessionService.cs#L696) |
| opt E | `AskedAt is null` | [SessionService.cs:700](../../../source/InterviewAPI/Services/SessionService.cs#L700) |
| **opt F** | **`isFirstEntry AND statusChanged`** — cổng exactly-once | [SessionService.cs:715](../../../source/InterviewAPI/Services/SessionService.cs#L715) |
| opt G | `!deductResult.Success` | [SessionService.cs:721](../../../source/InterviewAPI/Services/SessionService.cs#L721) |
| opt H | `!deductResult.ServiceAvailable` | [SessionService.cs:729](../../../source/InterviewAPI/Services/SessionService.cs#L729) |
| opt J | `!alreadyAsked` | [SessionService.cs:762](../../../source/InterviewAPI/Services/SessionService.cs#L762) |
| opt K | INSERT lỗi sau khi đã trừ credit | [SessionService.cs:775-784](../../../source/InterviewAPI/Services/SessionService.cs#L775) |

## 6. NOTE

Không có (§0-bis cấm hộp note). Mọi giải thích nằm ở file này.

## 7-9. CLASS

Xem `class_specs/chargeandactivatesession.py` (20 class, 20 quan hệ, back-end scope InterviewAPI).
Docstring đầu file ghi rõ vì sao `IPaymentApiClient` là ranh giới C# được vẽ, còn PaymentAPI chỉ
xuất hiện ở trang Sequence.

## 10. ANOMALY / BUG

| # | Nội dung |
|---|---|
| A1 | **Phase 2 cũ trong `#19` đã lỗi thời về mã lỗi.** Nó vẽ `Return 400 ApiResponse.Fail(ex.Message)` cho 3 nhánh (session sai, entry gate, hết credit). Code hiện tại trả **503 + thông điệp generic** ([InterviewSessionController.cs:634-639](../../../source/InterviewAPI/Controllers/InterviewSessionController.cs#L634)). Diagram này vẽ theo code hiện tại. |
| A2 | **Hết credit trả 503, không phải 400.** `InvalidOperationException` từ [SessionService.cs:734](../../../source/InterviewAPI/Services/SessionService.cs#L734) rơi vào `catch (Exception)` → 503 + thông điệp generic, nên **lý do thật ("Insufficient credits") không tới được người dùng**. Ghi nhận, chưa sửa — nằm ngoài phạm vi vẽ diagram. |
| A3 | Phase 2 cũ **thiếu nhánh `Ended=true`** ([SessionService.cs:696](../../../source/InterviewAPI/Services/SessionService.cs#L696)). Đã bổ sung ở đây (opt D). |

## 11. QUYẾT ĐỊNH ĐÃ DUYỆT

1. **Tách khỏi `#19`** (duyentq duyệt 2026-08-14, phương án A). Đo được: `GenerateQuestionAsync` chiếm 81/117 message của `#19` trong khi tên file nói về việc vào phòng. Đây là điểm trừ credit duy nhất ([SessionService.cs:718](../../../source/InterviewAPI/Services/SessionService.cs#L718)) và nơi duy nhất đặt `StartedAt` ([:745](../../../source/InterviewAPI/Services/SessionService.cs#L745)).
2. **Tên file** đặt theo ngữ nghĩa tính-tiền-và-kích-hoạt, không theo "generate question" — việc phát câu hỏi là phần ít quan trọng nhất của method này.
3. **`ServiceAvailable` vẽ bằng `opt` chứ không `alt`**: code là early-return `if` rồi `throw` vô điều kiện ([SessionService.cs:729-734](../../../source/InterviewAPI/Services/SessionService.cs#L729)), không phải hai nhánh hội tụ. Validator RULE3b đã bắt lỗi này ở bản nháp.

## 12. THIẾU / CHƯA TRA ĐƯỢC

Không có.
