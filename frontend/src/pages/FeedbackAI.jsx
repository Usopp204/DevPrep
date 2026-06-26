import React, { useState, useEffect } from 'react';
import './FeedbackAI.css';

function FeedbackAI() {
  const [feedbacks, setFeedbacks] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  // Giả lập gọi API lấy dữ liệu
  useEffect(() => {
    const fetchData = async () => {
      try {
        setIsLoading(true);
        // Sau này có backend, bạn chỉ cần thay đường dẫn này thành API thật (vd: 'http://localhost:5000/api/feedback')
        const response = await fetch('/data/mockData.json'); 
        const data = await response.json();
        setFeedbacks(data);
      } catch (error) {
        console.error("Lỗi khi fetch dữ liệu feedback:", error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchData();
  }, []);

  const renderStars = (rating) => {
    return Array.from({ length: 5 }, (_, i) => (
      <span key={i} className={i < rating ? "fai-star fai-filled" : "fai-star fai-empty"}>★</span>
    ));
  };

  return (
    <div className="fai-wrapper">
      {/* ĐÃ BỎ HEADER Ở ĐÂY - BẠN CHÈN <Header /> CỦA BẠN Ở FILE CHA (App.jsx) */}
      
      <main className="fai-main">
        <div className="fai-title-section">
          <h1>AI Interview Feedback</h1>
          <p>Read what users are saying about their AI mock interview sessions. (Read-only)</p>
        </div>

        {/* FILTER BAR */}
        <div className="fai-filter-bar">
          <div className="fai-filter-group">
            <div className="fai-filter-item">
              <label>ROLE</label>
              <select defaultValue="All">
                <option value="All">All Roles</option>
              </select>
            </div>
            
            <div className="fai-filter-item">
              <label>RATING</label>
              <div className="fai-rating-tabs">
                <button className="fai-tab active">All</button>
                <button className="fai-tab">5 ★</button>
                <button className="fai-tab">4 ★</button>
                <button className="fai-tab">3 ★</button>
              </div>
            </div>
          </div>

          <div className="fai-filter-item fai-sort-item">
            <label>SORT BY</label>
            <select defaultValue="recent">
              <option value="recent">Most Recent</option>
            </select>
          </div>
        </div>

        {/* LOADING STATE / CARDS GRID */}
        {isLoading ? (
          <div className="fai-loading">Đang tải dữ liệu feedback...</div>
        ) : (
          <div className="fai-grid">
            {feedbacks.map((item) => (
              <div key={item.id} className="fai-card">
                <div className="fai-card-header">
                  <div className="fai-user-info">
                    <img src={item.avatar} alt={item.name} className="fai-avatar" />
                    <div>
                      <h3>{item.name}</h3>
                      <span className="fai-date">{item.date}</span>
                    </div>
                  </div>
                  <div className={`fai-score-tag ${item.scoreType}`}>
                    Score: {item.score}
                  </div>
                </div>

                <div className="fai-tags">
                  <span className="fai-tag role-tag">{item.role}</span>
                  <span className="fai-tag level-tag">{item.level}</span>
                </div>

                <div className="fai-ratings">
                  <div className="fai-rating-row">
                    <span>Technical Accuracy</span>
                    <div className="fai-stars">{renderStars(item.ratings.technical)}</div>
                  </div>
                  <div className="fai-rating-row">
                    <span>Communication Clarity</span>
                    <div className="fai-stars">{renderStars(item.ratings.communication)}</div>
                  </div>
                  <div className="fai-rating-row">
                    <span>AI Persona Realism</span>
                    <div className="fai-stars">{renderStars(item.ratings.realism)}</div>
                  </div>
                </div>

                <p className="fai-comment">"{item.comment}"</p>
                <div className="fai-session-id">Session ID: {item.sessionId}</div>
              </div>
            ))}
          </div>
        )}

        {/* PAGINATION */}
        <div className="fai-pagination">
          <button className="fai-page-btn text-btn">&lt;</button>
          <button className="fai-page-btn active">1</button>
          <button className="fai-page-btn">2</button>
          <button className="fai-page-btn">3</button>
          <span className="fai-dots">...</span>
          <button className="fai-page-btn">12</button>
          <button className="fai-page-btn text-btn">&gt;</button>
        </div>
      </main>

      {/* FOOTER */}
      <footer className="fai-footer">
        <div className="fai-footer-top">
          <div className="fai-footer-logo">DevPrep</div>
          <div className="fai-footer-links">
            <div className="fai-footer-col">
              <h4>PRODUCT</h4>
              <a href="#features">Features</a>
              <a href="#pricing">Pricing</a>
            </div>
            <div className="fai-footer-col">
              <h4>LEGAL</h4>
              <a href="#privacy">Privacy</a>
              <a href="#terms">Terms</a>
            </div>
          </div>
        </div>
        <div className="fai-footer-bottom">
          <p>© 2024 DevPrep. All rights reserved.</p>
          <div className="fai-footer-icons">
            <span>📺</span>
            <span>💻</span>
            <span>🌐</span>
          </div>
        </div>
      </footer>
    </div>
  );
}

export default FeedbackAI;