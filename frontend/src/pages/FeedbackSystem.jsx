import React, { useState, useEffect } from 'react';
import './FeedbackSystem.css';

// DỮ LIỆU MẪU CỐ ĐỊNH (Sử dụng để hiển thị ngay lập tức hoặc làm phương án dự phòng)
const LOCAL_MOCK_DATA = [
  {
    id: 1,
    name: "Alex Rivers",
    role: "SENIOR DEVELOPER",
    time: "2 DAYS AGO",
    rating: 5,
    avatar: "https://i.pravatar.cc/150?img=11",
    comment: "The DevPrep AI simulation is terrifyingly accurate. I had a mock interview for a Staff Engineer position and the questions about distributed systems were exactly what I encountered in the real interview. Saved me hours of generic practice.",
    reply: "Glad we could help you land that role, Alex! We recently updated our System Design module to include more edge-case scenarios. Good luck!"
  },
  {
    id: 2,
    name: "Sarah Chen",
    role: "FULLSTACK DEV",
    time: "5 DAYS AGO",
    rating: 4,
    avatar: "https://i.pravatar.cc/150?img=47",
    comment: "The real-time feedback on my coding logic during the technical round was super helpful. It doesn't just give the answer; it hints at the optimization path like a real interviewer would.",
    reply: null
  },
  {
    id: 3,
    name: "Marcus Thorne",
    role: "DEVOPS ENGINEER",
    time: "1 WEEK AGO",
    rating: 3,
    avatar: "https://i.pravatar.cc/150?img=12",
    comment: "Generally great, but the Kubernetes scenario module felt a bit laggy today. The feedback was still high-quality, though.",
    reply: "Sorry about the lag, Marcus! We were performing a cluster update during that window. Performance should be back to 100ms response times now."
  },
  {
    id: 4,
    name: "Elena Rodriguez",
    role: "UI/UX DEV",
    time: "2 WEEKS AGO",
    rating: 5,
    avatar: "https://i.pravatar.cc/150?img=49",
    comment: "The best career prep tool I've used. Period. The AI actually understands design tokens and accessibility principles when I explain my front-end architecture.",
    reply: null
  },
  {
    id: 5,
    name: "Jordan Wu",
    role: "AI RESEARCHER",
    time: "2 WEEKS AGO",
    rating: 4,
    avatar: "https://i.pravatar.cc/150?img=44",
    comment: "Excellent prompt engineering on the backend. The AI persona stays perfectly in character throughout the session.",
    reply: null
  }
];

function FeedbackSystem() {
  const [feedbacks, setFeedbacks] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [formComment, setFormComment] = useState('');

  // 1. ĐỊNH NGHĨA LINK API Ở ĐÂY ĐỂ SAU NÀY DỄ DÀNG THAY THẾ
  // Hiện tại: Trỏ vào file tĩnh trong thư mục public để giả lập endpoint
  // Sau này: Thay đổi thành 'https://your-backend-api.com/api/feedbacks'
  const API_ENDPOINT = '/data/feedbackSystem.json'; 

  useEffect(() => {
    const fetchFeedbacks = async () => {
      try {
        setIsLoading(true);
        
        // Tạo một khoảng trễ nhỏ 400ms giả lập độ trễ mạng thực tế từ server
        await new Promise(resolve => setTimeout(resolve, 400));

        const response = await fetch(API_ENDPOINT);
        
        if (!response.ok) {
          throw new Error(`Lỗi HTTP! Trạng thái: ${response.status}`);
        }
        
        const data = await response.json();
        setFeedbacks(data);
      } catch (error) {
        console.warn("Chưa cấu trúc API/File JSON hoặc lỗi mạng. Tự động chuyển sang dữ liệu mock data nội bộ:", error.message);
        // Fallback: Nếu không gọi được API, lấy mảng dữ liệu mẫu chạy trực tiếp luôn
        setFeedbacks(LOCAL_MOCK_DATA);
      } finally {
        setIsLoading(false);
      }
    };

    fetchFeedbacks();
  }, []);

  const renderStars = (rating) => {
    return Array.from({ length: 5 }, (_, i) => (
      <span key={i} className={i < rating ? "fs-star fs-filled" : "fs-star fs-empty"}>★</span>
    ));
  };

  // 2. LOGIC ĐỂ SUBMIT DỮ LIỆU LÊN BACKEND SAU NÀY
  const handleSubmit = async (e) => {
    e.preventDefault();
    
    const newFeedback = {
      comment: formComment,
      // Đối với backend thật, các chỉ số rating và thông tin user sẽ lấy động từ state/auth của hệ thống
      technicalRating: 4, 
      speedRating: 5,
      relevanceRating: 4
    };

    try {
      console.log("Đang gửi dữ liệu lên hệ thống...", newFeedback);
      
      /* Cấu trúc sẵn đoạn code POST lên Backend:
      const response = await fetch(API_ENDPOINT, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          // 'Authorization': `Bearer ${token}` // nếu cần đăng nhập
        },
        body: JSON.stringify(newFeedback)
      });
      if(response.ok) { ... }
      */

      alert(`Gửi phản hồi thành công! (Mô phỏng dữ liệu: "${formComment}")`);
      setFormComment('');
    } catch (error) {
      console.error("Lỗi khi gửi feedback lên server:", error);
    }
  };

  return (
    <div className="fs-wrapper">
      <main className="fs-main">
        <div className="fs-title-section">
          <h1>Feedback System</h1>
          <p>Share your experience with DevPrep AI and see what others think.</p>
        </div>

        <div className="fs-content-layout">
          
          {/* CỘT TRÁI: FORM ĐĂNG FEEDBACK */}
          <div className="fs-form-container">
            <h2>Share Your Feedback</h2>
            <form onSubmit={handleSubmit}>
              <div className="fs-rating-inputs">
                <div className="fs-rating-input-row">
                  <span>TECHNICAL ACCURACY</span>
                  <div className="fs-stars-interactive">{renderStars(4)}</div>
                </div>
                <div className="fs-rating-input-row">
                  <span>SYSTEM SPEED</span>
                  <div className="fs-stars-interactive">{renderStars(5)}</div>
                </div>
                <div className="fs-rating-input-row">
                  <span>AI RELEVANCE</span>
                  <div className="fs-stars-interactive">{renderStars(4)}</div>
                </div>
              </div>

              <div className="fs-comment-box">
                <label>YOUR COMMENT</label>
                <textarea 
                  placeholder="How was your career preparation session?" 
                  value={formComment}
                  onChange={(e) => setFormComment(e.target.value)}
                  required
                />
              </div>

              <button type="submit" className="fs-submit-btn">Submit Feedback</button>
            </form>
          </div>

          {/* CỘT PHẢI: DANH SÁCH FEEDBACK */}
          <div className="fs-list-container">
            <div className="fs-filter-bar">
              <div className="fs-sort-select">
                <label>SORT BY</label>
                <select defaultValue="latest">
                  <option value="latest">Latest First</option>
                </select>
              </div>
              <div className="fs-filter-tabs">
                <button className="fs-tab active">All</button>
                <button className="fs-tab">5</button>
                <button className="fs-tab">4</button>
                <button className="fs-tab">3</button>
              </div>
            </div>

            {/* HIỂN THỊ TRẠNG THÁI LOADING TRƯỚC KHI CÓ DỮ LIỆU */}
            {isLoading ? (
              <div className="fs-loading-container">
                <div className="fs-spinner"></div>
                <p>Đang tải danh sách phản hồi...</p>
              </div>
            ) : (
              <div className="fs-feedbacks-list">
                {feedbacks.map((item) => (
                  <div key={item.id} className="fs-card">
                    <div className="fs-card-header">
                      <div className="fs-user-info">
                        <img src={item.avatar} alt={item.name} className="fs-avatar" />
                        <div>
                          <h3>{item.name}</h3>
                          <span className="fs-user-meta">{item.role} • {item.time}</span>
                        </div>
                      </div>
                      <div className="fs-card-stars">
                        {renderStars(item.rating)}
                      </div>
                    </div>

                    <p className="fs-comment-text">{item.comment}</p>

                    {item.reply && (
                      <div className="fs-reply-box">
                        <div className="fs-reply-header">
                          <span className="fs-reply-icon">⚙️</span>
                          <h4>PLATFORM TEAM REPLIED:</h4>
                        </div>
                        <p className="fs-reply-text">{item.reply}</p>
                      </div>
                    )}
                  </div>
                ))}
              </div>
            )}

            {/* PAGINATION */}
            <div className="fs-pagination">
              <button className="fs-page-btn text-btn">&lt;</button>
              <button className="fs-page-btn active">1</button>
              <button className="fs-page-btn">2</button>
              <button className="fs-page-btn">3</button>
              <span className="fs-dots">...</span>
              <button className="fs-page-btn">12</button>
              <button className="fs-page-btn text-btn">&gt;</button>
            </div>
          </div>

        </div>
      </main>

      {/* FOOTER */}
      <footer className="fs-footer">
        <div className="fs-footer-top">
          <div className="fs-footer-logo">DevPrep</div>
          <div className="fs-footer-links">
            <div className="fs-footer-col">
              <h4>PRODUCT</h4>
              <a href="#features">Features</a>
              <a href="#pricing">Pricing</a>
            </div>
            <div className="fs-footer-col">
              <h4>LEGAL</h4>
              <a href="#privacy">Privacy</a>
              <a href="#terms">Terms</a>
            </div>
          </div>
        </div>
        <div className="fs-footer-bottom">
          <p>© 2024 DevPrep. All rights reserved.</p>
          <div className="fs-footer-icons">
            <span>📺</span>
            <span>💻</span>
            <span>🌐</span>
          </div>
        </div>
      </footer>
    </div>
  );
}

export default FeedbackSystem;