# DevPrep — Project Overview (Pre‑Implementation)

> **VI:** Tài liệu này mô tả **toàn bộ khái niệm, logic, kiến trúc, yêu cầu, chuẩn code, kế hoạch trước triển khai source base** của dự án DevPrep. **Không bao gồm** các giai đoạn sau khi đã triển khai source base (post‑scaffold).
>
> **EN:** This document captures the **full concept, logic, architecture, requirements, coding standards, and pre‑scaffold implementation plan** of DevPrep. It **excludes** any phases after the source base scaffold is implemented (post‑scaffold).

---

## 1. Tóm tắt dự án / Project Summary

**VI:** DevPrep là nền tảng mô phỏng phỏng vấn AI và đánh giá năng lực IT, tập trung vào trải nghiệm realtime, chấm điểm minh bạch, và phản hồi hữu ích. Hệ thống gồm backend (.NET), AI service (FastAPI), frontend (React), và hợp đồng dữ liệu dùng chung.

**EN:** DevPrep is an AI‑powered mock interview simulator and IT competency evaluation platform focused on real‑time experience, transparent scoring, and actionable feedback. The system consists of a backend (.NET), an AI service (FastAPI), a frontend (React), and shared data contracts.

---

## 2. Tầm nhìn & mục tiêu / Vision & Goals

**VI:**

- Giúp người dùng luyện tập phỏng vấn IT theo vai trò cụ thể.
- Cung cấp chấm điểm công bằng, nhất quán, và có thể giải thích.
- Đưa ra phản hồi giúp cải thiện kỹ năng (kiến thức, tốc độ, cấu trúc trả lời).
- Đảm bảo trải nghiệm realtime, ổn định, và có thể mở rộng.

**EN:**

- Help users practice IT interviews for specific roles.
- Provide fair, consistent, and explainable scoring.
- Deliver actionable feedback to improve knowledge, speed, and answer structure.
- Ensure real‑time, stable, and scalable experience.

---

## 3. Đối tượng người dùng / Target Users

**VI:**

- Ứng viên IT luyện phỏng vấn.
- Người mới chuyển ngành, cần đánh giá năng lực theo tiêu chí cụ thể.
- Nhóm học viên cần phản hồi khách quan từ hệ thống AI.

**EN:**

- IT candidates preparing for interviews.
- Career switchers seeking structured competency evaluation.
- Learners who need objective feedback from AI.

---

## 4. Nguyên tắc sản phẩm / Product Principles

**VI:**

- **Realtime‑first:** giảm độ trễ trong Q&A và scoring.
- **Minh bạch:** điểm số phải có lý do & cấu trúc rõ ràng.
- **Không hardcode:** mọi hằng số/threshold phải cấu hình được.
- **Đa dịch vụ rõ ràng:** backend điều phối, AI service tập trung AI.

**EN:**

- **Realtime‑first:** minimize latency in Q&A and scoring.
- **Transparency:** scores must be explainable and structured.
- **No hardcoding:** all constants/thresholds must be configurable.
- **Clear service boundaries:** backend orchestrates, AI service focuses on AI.

---

## 5. Phạm vi & Non‑Goals / Scope & Non‑Goals

**VI (In‑Scope trước scaffold):**

- Xác định khái niệm, logic, kiến trúc, hợp đồng dữ liệu.
- Thiết kế chuẩn code, quy tắc cấu hình.
- Lập kế hoạch triển khai **đến giai đoạn tạo source base**.

**EN (In‑Scope pre‑scaffold):**

- Define concept, logic, architecture, and data contracts.
- Establish coding standards and configuration rules.
- Plan implementation **up to source base scaffolding**.

**VI (Out‑of‑Scope sau scaffold):**

- Mọi pha sau khi đã có source base (tính năng hoàn chỉnh, triển khai production, CI/CD, tối ưu nâng cao).

**EN (Out‑of‑Scope post‑scaffold):**

- Any phase after the base scaffold (full feature implementation, production deployment, CI/CD, advanced optimization).

---

## 6. Luồng chính (6 flows) / Core Flows (6)

> **Lưu ý / Note:** Các flow được mô tả ở mức khái niệm để định hướng kiến trúc. Chi tiết có thể điều chỉnh trong giai đoạn thiết kế sâu hơn.

**Flow 1 — Onboarding & Hồ sơ người dùng / Onboarding & Profile**

- **VI:** Thiết lập hồ sơ, mục tiêu, và kinh nghiệm.
- **EN:** Set up profile, goals, and experience.

**Flow 2 — Nạp dữ liệu đầu vào / Input Materials**

- **VI:** Người dùng cung cấp CV, JD hoặc vai trò mục tiêu.
- **EN:** Users provide CV, job description, or target role.

**Flow 3 — Chuẩn bị phiên phỏng vấn / Interview Session Setup**

- **VI:** Chọn vai trò, độ khó, và cấu hình phiên.
- **EN:** Select role, difficulty, and session config.

**Flow 4 — Phỏng vấn realtime / Real‑Time Interview**

- **VI:** AI đặt câu hỏi, người dùng trả lời (text/voice); hệ thống theo dõi thời gian.
- **EN:** AI asks questions; user answers (text/voice); time tracking is enforced.

**Flow 5 — Chấm điểm & phản hồi / Scoring & Feedback**

- **VI:** Tính điểm theo Scoring Spec v2, trả về điểm và nhận xét.
- **EN:** Compute score per Scoring Spec v2, return scores and feedback.

**Flow 6 — Tổng kết & cải thiện / Summary & Improvement**

- **VI:** Hiển thị báo cáo, khuyến nghị luyện tập.
- **EN:** Show summary report and improvement guidance.

---

## 7. Logic chấm điểm (Scoring Spec v2)

**VI:** Scoring Spec v2 là hợp đồng chuẩn giữa các dịch vụ. **Không hardcode công thức**; trọng số và ngưỡng phải cấu hình được.

**EN:** Scoring Spec v2 is the canonical contract between services. **No hardcoded formulas**; weights and thresholds must be configurable.

**Trường dữ liệu chính / Key Fields:**

| Field              | Type    | VI (Ý nghĩa)               | EN (Meaning)           |
| ------------------ | ------- | -------------------------- | ---------------------- |
| sessionId          | uuid    | ID phiên phỏng vấn         | Interview session ID   |
| scoringVersion     | string  | Phiên bản chấm điểm        | Scoring version        |
| theoryScore        | number  | Điểm lý thuyết/kiến thức   | Theory/knowledge score |
| speedScore         | number  | Điểm tốc độ trả lời        | Speed score            |
| baseScore          | number  | Điểm nền tảng (tổng hợp)   | Base aggregated score  |
| earlyBonusEligible | boolean | Có đủ điều kiện thưởng sớm | Early bonus eligible   |
| earlyBonusScore    | number  | Điểm thưởng sớm            | Early bonus score      |
| finalScore         | number  | Điểm cuối cùng             | Final score            |
| finishedReason     | string  | Lý do kết thúc phiên       | Session end reason     |

**Nguyên tắc tính điểm / Scoring Principles:**

- **VI:** baseScore là tổng hợp từ theoryScore và speedScore theo trọng số cấu hình. finalScore = baseScore + earlyBonusScore (nếu đủ điều kiện). Các ngưỡng và điều kiện bonus phải configurable.
- **EN:** baseScore aggregates theoryScore and speedScore using configurable weights. finalScore = baseScore + earlyBonusScore (if eligible). Thresholds and bonus criteria must be configurable.

---

## 8. Feedback Use‑Cases

**VI:** Hệ thống thu thập phản hồi sau phiên để đánh giá chất lượng hệ thống và độ phù hợp của phỏng vấn.

**EN:** The system collects post‑session feedback to evaluate system quality and interview relevance.

**System Feedback (toàn hệ thống / system‑level):**

- overallRating (1‑5)
- uiUxRating (1‑5)
- performanceRating (1‑5)
- trustRating (1‑5)
- comment (string)

**Interview Feedback (phiên phỏng vấn / interview‑level):**

- questionQualityRating (1‑5)
- accuracyRating (1‑5)
- jobRelevanceRating (1‑5)
- cvAlignmentRating (1‑5)
- scoringFairnessRating (1‑5)
- comment (string)

---

## 9. Kiến trúc tổng thể / High‑Level Architecture

**VI:** Kiến trúc theo mô hình **Thuần Microservices (Pure Microservices)** cho backend và **AI service tách riêng**. Monorepo chứa toàn bộ mã nguồn. Quản lý 7 APIs thông qua 1 file `DevPrep.sln` duy nhất để tiện phát triển local. Tầng khách hàng (Frontend) chỉ giao tiếp với hệ thống thông qua API Gateway.

**EN:** Architecture uses **Pure Microservices** for the backend and a **separate AI service**. Monorepo hosts all services. Managed by a single `DevPrep.sln` file for local development convenience. The client layer (Frontend) communicates with the system solely through the API Gateway.

**Thành phần / Components:**

1. **API Gateway (YARP - C# .NET 8)**
   - **VI:** Điểm vào duy nhất. Xử lý routing request xuống 6 microservices còn lại.
   - **EN:** Single entry point using YARP. Routes requests to the 6 downstream microservices.

2. **Backend Microservices (C# .NET 8)**
   - **VI:** 6 APIs độc lập: Auth, Document (CV&JD), Interview, Evaluation, Payment, Admin. Không sử dụng thư mục Shared dùng chung để đảm bảo tính độc lập tuyệt đối (Decoupling).
   - **EN:** 6 independent APIs. No shared/BuildingBlocks folders to enforce strict isolation.

3. **Giao tiếp liên dịch vụ / Cross-Service Communication**
   - **VI:** Bất đồng bộ (Async) dùng Message Broker RabbitMQ + MassTransit (vd: Saga pattern khi thanh toán). Đồng bộ (Sync) dùng HTTP/HttpClient tiêu chuẩn.
   - **EN:** Async using RabbitMQ + MassTransit. Sync using standard HTTP/HttpClient.

4. **AI Service (Python 3.11, FastAPI)**
   - **VI:** Tạo câu hỏi, phân tích câu trả lời, đánh giá. Đóng vai trò như một AI Worker độc lập.
   - **EN:** Question generation, answer analysis, evaluation. Acts as an independent AI Worker.

5. **Frontend (React + TS + Vite)**
   - **VI:** UI realtime, hiển thị session, scoring, feedback.
   - **EN:** Real‑time UI for sessions, scoring, feedback.

6. **Databases (SQL Server)**
   - **VI:** Pattern Database-per-service. Mỗi service sở hữu một DB riêng rẽ.
   - **EN:** Database-per-service pattern. Each service owns its isolated database.

---

## 9.1 Technical Baseline & Rules

**VI:** Chi tiết toàn bộ 24 quyết định kiến trúc, quy tắc cấu hình và chuẩn code đã chốt cứng, vui lòng tuân thủ tuyệt đối theo file `docs/architecture/pure-microservices-baseline.md`.

**EN:** The full set of 24 locked architecture decisions, configuration rules, and coding standards must be followed in `docs/architecture/pure-microservices-baseline.md`.

---

## 10. Chuẩn code & tiêu chuẩn kỹ thuật / Coding & Engineering Standards

**VI:**

- **Không hardcode**: ports, URLs, thresholds, weights.
- **Constants tách riêng** nằm trực tiếp bên trong từng microservice. Không có global shared library (Không dùng thư mục dùng chung để tránh dính líu dependency).
- **Config qua env**: `.env` hoặc biến môi trường.
- **Database-per-service**: Microservice này tuyệt đối không được query trực tiếp vào Database của Microservice khác.
- **Realtime‑first**: ưu tiên async I/O, tránh blocking.

**EN:**

- **No hardcoding**: ports, URLs, thresholds, weights.
- **Separate constants** directly inside each microservice. No global shared library (No shared folders to avoid tight coupling).
- **Env‑based config**: `.env` or environment variables.
- **Database-per-service**: Strict database isolation. No cross-database querying permitted.
- **Realtime‑first**: prefer async I/O, avoid blocking.

---

## 11. Quản lý cấu hình & bí mật / Config & Secrets

**VI:**

- `.env.example` chỉ chứa placeholders.
- `.env` không commit vào repo.
- Default values nằm trong constants/config classes.

**EN:**

- `.env.example` contains placeholders only.
- `.env` must not be committed.
- Defaults live in constants/config classes.

---

## 12. Hợp đồng dữ liệu / Data Contracts

**VI:** Các hợp đồng chính hiện có:

- `contracts/interview/score-v2.schema.json`
- `contracts/feedback/system-feedback.schema.json`
- `contracts/feedback/interview-feedback.schema.json`

**EN:** Current canonical contracts:

- `contracts/interview/score-v2.schema.json`
- `contracts/feedback/system-feedback.schema.json`
- `contracts/feedback/interview-feedback.schema.json`

---

## 13. Kế hoạch triển khai trước scaffold / Pre‑Scaffold Plan

> **VI:** Chỉ đến giai đoạn tạo source base. Không đi vào triển khai tính năng sau đó.
>
> **EN:** Only up to base source scaffold. No post‑scaffold implementation details.

**Giai đoạn 0 — Khai báo yêu cầu & kiến trúc / Requirements & Architecture**

- **VI:** Xác định phạm vi, flows, scoring logic, contracts, constraints.
- **EN:** Define scope, flows, scoring logic, contracts, constraints.

**Giai đoạn 1 — Chuẩn hoá cấu trúc monorepo / Monorepo Structure**

- **VI:** Tạo thư mục backend/ai‑service/frontend/contracts/infra, README, .env.example.
- **EN:** Create backend/ai‑service/frontend/contracts/infra, README, .env.example.

**Giai đoạn 2 — Scaffold nền tảng service / Base Service Scaffold**

- **VI:** Tạo skeleton tối thiểu cho backend (.NET), AI service (FastAPI), frontend (React+Vite).
- **EN:** Minimal skeleton for backend (.NET), AI service (FastAPI), frontend (React+Vite).

---

## 14. Ràng buộc cố định / Hard Constraints

**VI:**

- Architecture: Pure Microservices (1 sln file, 7 APIs)
- Gateway: YARP (Yet Another Reverse Proxy)
- Message Broker: RabbitMQ (via MassTransit)
- Backend: C# ASP.NET Core (.NET 8)
- AI Service: Python 3.11 + FastAPI
- Frontend: React + TypeScript + Vite (Node 20)
- DB: SQL Server (Database-per-service pattern)
- Deploy: Môi trường Infra dự phòng, hiện tại chạy local (không bắt buộc dùng Docker khi dev local).

**EN:**

- Architecture: Pure Microservices (1 sln file, 7 APIs)
- Gateway: YARP (Yet Another Reverse Proxy)
- Message Broker: RabbitMQ (via MassTransit)
- Backend: C# ASP.NET Core (.NET 8)
- AI Service: Python 3.11 + FastAPI
- Frontend: React + TypeScript + Vite (Node 20)
- DB: SQL Server (Database-per-service pattern)
- Deploy: Infra folder reserved, currently running locally (Docker not required for local dev).

---

## 15. Rủi ro & giả định / Risks & Assumptions

**VI:**

- Rủi ro: realtime latency khi AI trả lời; cần kiến trúc async.
- Giả định: dữ liệu đầu vào đủ rõ để AI tạo câu hỏi phù hợp.

**EN:**

- Risk: realtime latency when AI responds; async architecture is required.
- Assumption: input data is sufficient for relevant AI questions.

---

## 16. Phụ lục: Quy ước đặt tên / Naming Conventions

**VI:**

- API routes: camel‑case hoặc kebab‑case thống nhất theo backend constants.
- DTO/Contracts: PascalCase cho C#, snake_case cho JSON nếu cần mapping.

**EN:**

- API routes: consistent camel‑case or kebab‑case via backend constants.
- DTO/Contracts: PascalCase in C#, snake_case in JSON if mapping required.

---

**End of Document — Pre‑Implementation Only**
