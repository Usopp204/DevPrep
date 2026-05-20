# DevPrep — Pure Microservices Baseline (24 Rules)

> Nội dung dưới đây **trích nguyên văn** “Ma Trận 24 Quyết Định Kiến Trúc Cốt Lõi” từ `docs/devprep-architecture-baseline.md`. **Không tóm tắt, không diễn đạt lại.**

---

## 3. Ma Trận 24 Quyết Định Kiến Trúc Cốt Lõi (Architecture Decision Records)

### Tầng API Gateway & Routing

1. **Thư viện Gateway:** Sử dụng **YARP (Yet Another Reverse Proxy)** của Microsoft tích hợp trên .NET 8. Hiệu năng cao, cấu hình dynamic và gọn nhẹ.
2. **Cơ chế Routing:** YARP hoạt động như một **Proxy thuần túy (Reverse Proxy)** dẫn đường, tuyệt đối KHÔNG triển khai mô hình BFF (Backend-For-Frontend) hoặc Gom chuỗi dữ liệu (Request Aggregation) tại Gateway để tránh nghẽn cổ chai.
3. **Quy hoạch URL Path:** Toàn bộ route đi qua Gateway phải dùng Prefix phẳng đại diện cho từng service và **KHÔNG dùng API Versioning (v1/v2)** tại Gateway:
   - `/api/auth/*` -> Map sang `DevPrep.AuthService`
   - `/api/document/*` -> Map sang `DevPrep.DocumentService`
   - `/api/interview/*` -> Map sang `DevPrep.InterviewService`
   - `/api/evaluation/*` -> Map sang `DevPrep.EvaluationService`
   - `/api/payment/*` -> Map sang `DevPrep.PaymentService`
   - `/api/admin/*` -> Map sang `DevPrep.AdminService`
4. **Giới hạn Hạ tầng Gateway:** Cấu hình YARP giới hạn dung lượng request body tối đa là **10MB** (phục vụ upload file CV lớn). Cài đặt `Global Timeout` tại Gateway cố định ở mức **30 giây** để phòng ngừa các luồng AI xử lý sâu bị quá tải thời gian. Tích hợp Rate-Limiting cơ bản dựa trên Client IP (100 requests/phút).

### Xác Thực, Phân Quyền & Bảo Mật

5. **JWT Trust Model (Mô hình Xác thực):** API Gateway (YARP) chịu trách nhiệm xác thực lớp ngoài cùng (Validate chữ ký Token, Nhà phát hành Issuer, Đối tượng Audience, và Thời hạn Expiry). Nếu hợp lệ, Gateway chuyển tiếp nguyên vẹn header `Authorization: Bearer <token>` xuống các downstream services. Các service nội bộ dùng thư viện JWT tiêu chuẩn để decode lấy thông tin người dùng (`UserId`), bỏ qua các cấu hình mã hóa mTLS phức tạp giữa các dịch vụ nội bộ.
6. **Vòng đời Token (JWT Lifecycle):** - `Access Token`: Thời gian sống (**TTL**) cố định **1 giờ**, lưu thông tin claims hoàn toàn dưới dạng Stateless, không dùng cơ chế kiểm tra danh sách đen (Blacklist via Redis) khi đăng xuất để tối ưu tốc độ đọc.
   - `Refresh Token`: Thời gian sống (**TTL**) là **7 ngày**, quản lý dưới dạng Stateful, lưu trữ trực tiếp trong cơ sở dữ liệu `DevPrep_IdentityDB` tại bảng `RefreshTokens` để phục vụ xoay tua token (Token Rotation).
7. **Cơ chế Đăng xuất (Logout):** Khi người dùng gọi API logout, hệ thống chỉ cần cập nhật cột `RevokedAt = SYSUTCDATETIME()` của Refresh Token tương ứng trong Database. Kể từ lúc này, Access Token cũ nếu còn hạn vẫn có thể gọi các API thường trong tối đa 1 giờ, nhưng tuyệt đối không thể lấy mã Access Token mới.
8. **Bảo mật dịch vụ AI (FastAPI):** Dịch vụ AI chạy hoàn toàn trong phân vùng **Internal-only** (Private Network hoặc chỉ lắng nghe port Localhost). Không public cổng của FastAPI ra ngoài Internet. Nhờ vậy, backend .NET gọi sang FastAPI qua REST API mà **KHÔNG CẦN truyền API Key hoặc Bearer JWT Header**, giúp giảm thiểu overhead cấu hình bảo mật chéo.
9. **Ma trận Phân quyền (Access Control Matrix):** Phân quyền dựa trên JWT Roles/Claims:
   - Toàn bộ endpoint thuộc `DevPrep.AuthService` (Login, Register, Google/GitHub SSO, OTP): **Public** (Ai cũng vào được).
   - Toàn bộ endpoint thuộc `DevPrep.AdminService`: Yêu cầu quyền **Admin** hoặc **SuperAdmin**.
   - Toàn bộ các endpoints còn lại (`Interview`, `Document`, `Evaluation`, `Payment`): Yêu cầu người dùng đã xác thực (Role = **User**).

### Giao Tiếp Liên Dịch Vụ Bất Đồng Bộ (Async Event‑Driven)

10. **Hạ tầng Tin nhắn:** Sử dụng **RabbitMQ** làm Message Broker kết hợp với thư viện **MassTransit** trên .NET.
11. **Quy tắc cô lập Hợp đồng (Strict Duck-Typing):** Để tuân thủ tuyệt đối quy tắc cấm sử dụng thư mục Shared hay Class Library dùng chung, các Microservices sẽ **không chia sẻ file `.dll` code C# chứa Event Class**. Thay vào đó, mỗi service sẽ tự định nghĩa một `C# Record / Class` nội bộ của riêng mình. Chỉ cần tên Class và các thuộc tính JSON khớp nhau, tính năng **Duck-Typing** của MassTransit sẽ tự động map dữ liệu một cách hoàn hảo.
12. **Quy ước Đặt tên Sự kiện:** Tên các Event Class gửi lên RabbitMQ bắt buộc tuân theo format hành động quá khứ: `[Tên_Hành_Động]CompletedEvent` (Ví dụ: `CvExtractedEvent`, `PaymentCompletedEvent`, `ScoreCalculatedEvent`).
13. **Cấu hình Quản lý Lỗi (Fault Tolerance):** Tận dụng cấu hình mặc định của MassTransit:
    - Bật chính sách thử lại: `UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(2)))` (Thử lại tối đa 3 lần, mỗi lần cách nhau 2 giây nếu xử lý event bị crash).
    - Nếu sau 3 lần vẫn lỗi, MassTransit tự động move tin nhắn lỗi vào hàng đợi chết (**Dead Letter Queue**) có hậu tố `_error` để Admin kiểm tra, không làm tắc nghẽn hệ thống. Bỏ qua các logic Idempotency phức tạp để giảm tải code đồ án.

### Giao Tiếp Đồng Bộ & Trải Nghiệm Realtime

14. **Giao tiếp Đồng Bộ (Sync Direct S2S):** Khi một dịch vụ bắt buộc phải đọc dữ liệu tức thời từ một dịch vụ khác, sử dụng HttpClient gọi trực tiếp qua URL nội bộ lấy từ file `.env`. Tích hợp thư viện **Polly** để xử lý khả năng phục hồi mạng:
    - Cho phép tự động thử lại tối đa 2 lần (`Retry count = 2`) với thời gian chờ tối đa (`Global Timeout`) là 15 giây.
    - **RÀNG BUỘC CHỐNG LỖI GIAO DỊCH (SIDE-EFFECTS):** Chỉ cho phép Polly tự động Retry đối với các HTTP Methods an toàn/an định (**Idempotent**) bao gồm `GET`, `PUT`, `DELETE`. **TUYỆT ĐỐI CẤM** retry tự động đối với request `POST` (như API tạo đơn hàng, API trừ tiền) để tránh rủi ro user bị mất tiền hoặc nhân đôi dữ liệu khi mạng bị lag.
15. **Hạ tầng Realtime (SignalR Hub Placement):** Cổng kết nối WebSocket Hub (`InterviewHub`) được đặt duy nhất tại **DevPrep.InterviewService**. API Gateway (YARP) chịu trách nhiệm cấu hình Proxy cho giao thức WebSockets để giữ kết nối thông suốt từ Client React xuống thẳng InterviewService. Các service chạy nền khác (như Document hay Evaluation) khi hoàn thành tác vụ ngầm sẽ bắn tin nhắn qua RabbitMQ, InterviewService consume tin nhắn đó và đẩy notification xuống Client thông qua Hub. Cách tiếp cận này loại bỏ hoàn toàn việc cài đặt thêm Redis Backplane phức tạp.
16. **Xử lý Mất kết nối / Refresh Trang (SignalR Reconnect Policy):** Sử dụng `UserId` (bóc tách từ JWT claim khi handshake) làm khóa định danh duy nhất cho Connection. Hệ thống map dữ liệu dạng `UserId -> ConnectionId` trực tiếp vào bộ nhớ RAM (In-Memory Dictionary). Khi người dùng nhấn F5 refresh trang hoặc chuyển tab làm đứt mạng giữa chừng, SignalR Client sẽ tự động kết nối lại, sinh ra một `ConnectionId` mới, backend sẽ đè Id mới lên Id cũ trong Dictionary. Client React có trách nhiệm chủ động gọi một lệnh HTTP GET API để kéo lại toàn bộ lịch sử đoạn chat từ database để hiển thị lại UI, SignalR không chịu trách nhiệm lưu trữ trạng thái lịch sử chat.
17. **Kế hoạch B cho AI (AI Sync Fallback & SLA):** Luồng phỏng vấn Q&A diễn ra theo dạng đồng bộ HTTP giữa `InterviewService` và dịch vụ AI Python với mức cam kết thời gian (**SLA Timeout**) gắt gao là **7 giây**. Nếu quá 7 giây hoặc con AI trả về lỗi 5xx do quá tải mạng, Backend .NET sẽ lập tức kích hoạt **Kế hoạch B (Fallback Mechanism)**: Bắt exception, truy vấn nhanh vào bộ nhớ cache local lấy ra một **Câu hỏi tĩnh (Static Fallback Question)** phù hợp với Tech Stack và Level của user (đã được pre-fetch từ AdminService ở đầu phiên) rồi nhả ngay qua SignalR cho người dùng, đảm bảo trải nghiệm phỏng vấn thời gian thực không bao giờ bị đứt mạch.

### Quản Lý Dữ Liệu, Lưu Trữ & Chấm Điểm

18. **Lưu trữ tài liệu thô (Object Storage):** Toàn bộ file CV/JD tải lên hệ thống sẽ do `DocumentService` tiếp nhận dưới dạng Stream dữ liệu, đẩy trực tiếp lên dịch vụ đám mây **Cloudinary (Free Tier)**. Database của dịch vụ chỉ lưu trữ chuỗi `CloudStorageUrl` cùng metadata cấu trúc, không lưu file thô dưới dạng nhị phân (`varbinary`) vào SQL Server để tránh làm phình đại database. Giới hạn file cứng: Tối đa **5MB**, chỉ chấp nhận định dạng **PDF** và **DOCX**.
19. **Bảo mật File trên Đám mây (Security by Obscurity):** Để tránh việc phải phát sinh code xử lý Signed URL có thời hạn (TTL) gây tốn tài nguyên, toàn bộ file CV đẩy lên Cloudinary sẽ được hệ thống tự động đổi tên thành một chuỗi băm ngẫu nhiên bằng mã **UUID/GUID** (Ví dụ: `e3a512d7-b892-411a.pdf`). File được lưu ở chế độ public, nhưng độ dài chuỗi băm khiến kẻ tấn công không thể đoán mò cấu trúc URL, đảm bảo an toàn dữ liệu cho ứng viên một cách đơn giản và hiệu quả. Không triển khai chính sách dọn dẹp (Cleanup policy) file rác để giữ gọn scope đồ án.
20. **Theo dõi trạng thái Tiến trình ngầm (Async Job State Persist):** Vì các tác vụ AI bóc tách CV/JD hay chấm điểm tổng hợp diễn ra bất đồng bộ và tốn nhiều thời gian (10-20 giây), hệ thống **bắt buộc phải lưu trạng thái tiến trình** vào database (Ví dụ các trạng thái: `Pending_Extraction`, `Processing`, `Completed`, `Failed`). Khi người dùng reload lại trang web, Frontend React sẽ gọi API kiểm tra trạng thái này trong DB để tái lập lại giao diện loading xoay tròn, đồng thời chờ đợi tín hiệu hoàn thành từ SignalR push về.
21. **Quản lý Luồng Tiền phân tán (Payment Choreography Saga):** Luồng trừ Credit khi khởi tạo phiên phỏng vấn áp dụng kiến trúc Saga hướng sự kiện (Choreography):
    - `InterviewService` tạo phiên ở trạng thái nháp (`Status = 'Ready'`) -> Publish sự kiện `SessionInitializedEvent`.
    - `PaymentService` consume sự kiện -> Kiểm tra số dư khả dụng trong bảng `UserCredits`.
    - _Kịch bản thành công:_ `PaymentService` thực hiện trừ 1 Credit, ghi sổ cái bảng `Transactions`, sau đó bắn sự kiện `CreditDeductedEvent`. `InterviewService` lắng nghe và đổi trạng thái phòng sang `InProgress` để kích hoạt AI phỏng vấn.
    - _Kịch bản thất bại (Hết tiền/Lỗi hệ thống):_ `PaymentService` bắn sự kiện `CreditFailedEvent`. `InterviewService` nhận được mã lỗi liền cập nhật trạng thái phiên thành `Cancelled` để giải phóng phòng.
22. **Quy tắc sở hữu Logic chấm điểm (Bounded Context):** Cấu hình trọng số và các ngưỡng quy đổi điểm của tài liệu mẫu `Scoring Spec v2` là **Cấu hình tĩnh (Static Config)**, khai báo trực tiếp trong file cấu hình `.env` hoặc `appsettings.json` của riêng **DevPrep.EvaluationService**. Áp dụng triết lý "Đóng vùng lãnh thổ" (Bounded Context): **Chỉ duy nhất** dịch vụ Evaluation có quyền được biết công thức và thực hiện tính toán điểm số. Các dịch vụ khác (kể cả giao diện Frontend) tuyệt đối không tự ý tính lại điểm mà chỉ bốc kết quả `FinalScore` từ EvaluationService về hiển thị. Không lưu cấu hình trọng số vào Database.
23. **Quản lý Cơ sở dữ liệu (Database-per-Service):** Mỗi dịch vụ sở hữu độc quyền 1 Database riêng biệt, chạy trên hệ quản trị **SQL Server**. Sử dụng công nghệ **EF Core Code-First Migrations** để quản lý phiên bản database. Thư mục `Migrations/` nằm độc lập trong từng project API tương ứng. Toàn bộ dữ liệu danh mục gốc (Seed data như Tech Stacks, Credit Packages, Tài khoản SuperAdmin mặc định) bắt buộc phải viết mã nạp cứng (hardcode) bằng Fluent API bên trong hàm `OnModelCreating` của từng file `DbContext`, tự động kích hoạt nạp dữ liệu khi lập trình viên chạy lệnh `dotnet ef database update`. Tuyệt đối cấm viết câu lệnh truy vấn chéo database (Cross-database query).

### Quản Trị Hệ Thống & Vận Hành Nhóm

24. **Đồng bộ Hợp đồng Dữ liệu (Contracts & Env Layout):** Toàn bộ cấu hình hệ thống chạy local được tập trung vào **DUY NHẤT 1 file `.env` đặt tại thư mục root `DevPrep/`** để toàn bộ 6 thành viên trong team dễ dàng quản lý và chia sẻ cấu hình, phân vùng bảo mật bằng tiền tố rõ ràng (`GLOBAL_`, `AUTH_`, `DOC_`, `INTERVIEW_`...). Các file JSON Schema giao tiếp chung đặt tại thư mục `/contracts` ngoài root. Vì hệ thống không sử dụng API Versioning, quy trình quản trị thay đổi là **"Monorepo Lock-step Release"**: Khi có bất kỳ sự thay đổi nào về tên trường dữ liệu trong file JSON schema, lập trình viên thực hiện sửa đổi có trách nhiệm thông báo ngay lên kênh chat nội bộ nhóm (Zalo/Discord), thực hiện viết mã cập nhật đồng thời ở cả Backend và Frontend trong cùng một nhánh PR để deploy đồng bộ, tránh lỗi lệch pha dữ liệu.
