import React, { useState, useEffect } from "react";
import "./LoginModal.css";
import Login from "../../pages/Login";
import Register from "../../pages/Register"; // Import component Register vừa tạo
import ForgotPassword from "../../pages/ForgotPassword"; // Import component ForgotPassword vừa tạo

function LoginModal({ show, onClose }) {
  // Quản lý view hiện tại bên trong Modal: 'login' | 'register' | 'forgot'
  const [view, setView] = useState("login");

  // Mỗi khi mở lại Modal, tự động reset về màn hình Login chính
  useEffect(() => {
    if (show) {
      setView("login");
    }
  }, [show]);

  if (!show) return null;

  const handleBackdropClick = (e) => {
    if (e.target.classList.contains("modal-backdrop")) onClose();
  };

  return (
    <div className="modal-backdrop" onClick={handleBackdropClick}>
      <div className="modal-content">
        <button className="modal-close" onClick={onClose} aria-label="Close modal">
          &times;
        </button>

        {/* RENDER THEO VIEW TRẠNG THÁI */}
        {view === "login" && (
          <Login 
            isModal 
            onClose={onClose} 
            switchToRegister={() => setView("register")}
            switchToForgot={() => setView("forgot")}
          />
        )}

        {view === "register" && (
          <Register 
            isModal 
            onClose={onClose} 
            switchToLogin={() => setView("login")} 
          />
        )}

        {view === "forgot" && (
          <ForgotPassword 
            isModal 
            onClose={onClose} 
            switchToLogin={() => setView("login")} 
          />
        )}
      </div>
    </div>
  );
}

export default LoginModal;