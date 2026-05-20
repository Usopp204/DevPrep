# 🚀 DevPrep - Pure Microservices Architecture

Dự án DevPrep - Hệ thống hỗ trợ luyện phỏng vấn IT thông minh. 
Hệ thống được thiết kế theo kiến trúc **Pure Microservices**, bao gồm Backend (.NET 8), Frontend (React/Vite), AI Service (FastAPI) và cơ sở hạ tầng giao tiếp qua RabbitMQ/API Gateway.

---

## 📂 Cấu trúc Repository (Repository Structure)

    DevPrep/
    ├── backend/                  # Chứa toàn bộ .NET 8 Microservices
    │   ├── DevPrep.sln           # Solution file quản lý 7 APIs
    │   ├── DevPrep.ApiGateway/   # Cổng giao tiếp trung tâm (YARP)
    │   ├── DevPrep.AuthService/  # Xử lý xác thực (Auth) & User Profile
    │   ├── DevPrep.DocumentService/ # Xử lý luồng CV & JD
    │   ├── DevPrep.InterviewService/# Xử lý luồng hỏi đáp Q&A Real-time
    │   ├── DevPrep.EvaluationService/# Xử lý luồng đánh giá & Report
    │   ├── DevPrep.PaymentService/  # Xử lý luồng thanh toán
    │   └── DevPrep.AdminService/    # Xử lý luồng quản trị hệ thống
    ├── ai-service/               # Core AI (Python/FastAPI)
    ├── frontend/                 # Giao diện người dùng (React/Vite)
    ├── contracts/                # Cross-service DTO/Schema JSONs (Shared)
    ├── infra/                    # Cấu hình DevOps / Docker Compose
    ├── .env.example              # Template biến môi trường (KHÔNG chứa secret)
    └── README.md                 # Tài liệu hướng dẫn dự án

---

## 🛠 Yêu cầu hệ thống (Prerequisites)

Để code dự án này, máy tính của bạn cần cài đặt sẵn:
1. **IDE:** Visual Studio Code (khuyên dùng) hoặc Visual Studio 2022.
2. **Backend:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
3. **Frontend:** [Node.js v18+](https://nodejs.org/).
4. **AI:** [Python 3.10+](https://www.python.org/).
5. **Database:** SQL Server (Và SSMS để quản lý DB).
6. **Hạ tầng (Tùy chọn):** Docker Desktop (Chỉ cần thiết khi muốn test luồng tích hợp có RabbitMQ).

---

## ⚙️ Cài đặt lần đầu (First-time Setup)

Khi bạn vừa `git clone` dự án về máy, hãy thực hiện các bước sau:

**Bước 1: Thiết lập Biến môi trường (.env)**
1. Tại thư mục gốc của dự án, copy file `.env.example` và đổi tên bản sao thành `.env`.
2. Mở file `.env`, điền thông tin kết nối SQL Server của máy bạn vào biến `DB_CONNECTION_STRING`.
3. Đảm bảo biến `USE_RABBITMQ=false` (Điều này giúp bạn code tính năng mượt mà, dùng In-Memory Bus mà không cần tốn RAM chạy Docker).

**Bước 2: Cài đặt thư viện**
* **Frontend:** Mở terminal, vào thư mục `frontend` và chạy `npm install`.
* **AI Service:** Mở terminal, vào thư mục `ai-service` và chạy `pip install -r requirements.txt`.

**Bước 3: Cập nhật Database**
* Mở terminal vào thư mục `backend/DevPrep.AuthService` và chạy:
  ```bash
  dotnet ef database update
  ```

---

## 💻 Cách khởi chạy dự án hàng ngày (Daily Development)

Bạn chuyên mảng nào thì chỉ cần bật Service mảng đó. Nhờ cấu hình `USE_RABBITMQ=false`, hệ thống sẽ tự động dùng *In-Memory Bus*, **KHÔNG YÊU CẦU BẬT DOCKER/RABBITMQ**.

1. **Khởi động API Gateway (Bắt buộc nếu làm Frontend/Fullstack):**
   ```bash
   cd backend/DevPrep.ApiGateway
   dotnet run
   ```
2. **Khởi động Service bạn đang code (Ví dụ AuthService):**
   ```bash
   cd backend/DevPrep.AuthService
   dotnet run
   ```
3. **Khởi động Frontend:**
   ```bash
   cd frontend
   npm run dev
   ```
4. **Khởi động AI Service (Nếu làm AI):**
   ```bash
   cd ai-service
   uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
   ```

---

## 🐳 Chạy Full Hệ thống (Integration Testing)

Khi cần test luồng giao tiếp chéo giữa các Microservices thông qua Message Broker thực tế:
1. Mở Docker Desktop.
2. Sửa file `.env`: Đổi `USE_RABBITMQ=true`.
3. Mở terminal tại thư mục gốc, gõ lệnh để khởi động RabbitMQ:
   ```bash
   docker-compose up -d
   ```
4. Chạy lại các dịch vụ `.NET` như bình thường.

---

## 🌿 Quy trình làm việc với Git (Git Workflow)

🚨 **LUẬT THÉP CỦA TEAM:** * **KHÔNG BAO GIỜ** làm việc và push code trực tiếp lên nhánh `main` hoặc nhánh `dev`.
* Nhánh `main` là Source Base gốc và là bản hoàn chỉnh cuối cùng sau khi nhóm thực hiện xong dự án (chỉ Team Leader quản lý). 
* Nhánh `dev` (Develop) là nhánh tích hợp code hàng ngày của cả team.

Để tránh conflict và đảm bảo hệ thống luôn chạy tốt, mọi thành viên **BẮT BUỘC** tuân thủ quy trình sau khi làm tính năng mới:

**1. Cập nhật code mới nhất từ nhánh `dev`:**
```bash
git checkout dev
git pull origin dev
```

**2. Tạo nhánh làm việc riêng (Tách từ nhánh `dev`):**
Tên nhánh sử dụng tiếng Anh theo quy tắc: `feature/ten_chuc_nang`, `fix/ten_bug`, hoặc `optimize/ten_toi_uu`.
```bash
git checkout -b feature/login_jwt
```

**3. Code, Build và Test tại Local:**
Thực hiện code chức năng. Đảm bảo build và run thành công, không có lỗi trên máy cá nhân.

**4. ĐỒNG BỘ CODE TRƯỚC KHI PUSH (Bước Cực Kỳ Quan Trọng):**
Trước khi push code của bạn, phải kiểm tra xem nhánh `dev` có ai đó vừa cập nhật code mới không, bằng cách kéo code từ `dev` về nhánh hiện tại của bạn:
```bash
git pull origin dev
```
👉 *Lưu ý:* Sau khi pull, **BẮT BUỘC** phải Build và Test chạy thử lại hệ thống một lần nữa để đảm bảo code mới kéo về không gây xung đột (conflict) làm hỏng tính năng bạn vừa code.

**5. Lưu thay đổi và Đẩy nhánh (Commit & Push):**
```bash
git add .
git commit -m "feat: implement login with jwt"
git push origin feature/login_jwt
```

**6. Kiểm tra và Tạo Merge Request (Pull Request):**
* Lên trang GitHub của dự án, kiểm tra xem nhánh chức năng của bạn đã thực sự nhận được code vừa push chưa.
* Nhấn nút tạo **Merge Request (Pull Request)** để xin phép merge code từ nhánh `feature/...` của bạn vào nhánh **`dev`** (Tuyệt đối KHÔNG merge vào `main`).

**7. Báo cáo Team Leader:**
Sau khi tạo Merge Request thành công, nhắn tin ngay qua Zalo cho Team Leader để Leader tiến hành review code, test hệ thống. Khi Leader duyệt và bấm merge vào `dev`, task của bạn mới chính thức được tính là hoàn thành!