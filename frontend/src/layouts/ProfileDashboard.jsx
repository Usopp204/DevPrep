import React, { useState } from "react";
import { 
  MdPerson, MdDescription, MdVerifiedUser, 
  MdHistory, MdLockReset, MdLogout, //  
  MdNotifications, MdSettings 
} from "react-icons/md";

// Import các sub-components (sẽ định nghĩa chi tiết bên dưới)
import InformationView from "../tabs/InformationView";
import CVView from "../tabs/CVView";
import IdentityView from "../tabs/IdentityView";
import TransactionView from "../tabs/TransactionView";
import ChangePasswordView from "../tabs/ChangePasswordView";
import { useNavigate } from "react-router-dom";
import { toast } from "react-toastify";
import "./ProfileDashboard.css";
import useAuthStore from "../store/useAuthStore"; // Import store vào
function ProfileDashboard() {
  const [activeTab, setActiveTab] = useState("information"); // Mặc định tab CV như ảnh minh họa
const navigate = useNavigate(); // Thay vì dùng biến đặt tên là 'navigation'
const logout = useAuthStore((state) => state.logout);
  const renderActiveView = () => {
    switch (activeTab) {
      case "information": return <InformationView />;
      case "cv": return <CVView />;
      case "identity": return <IdentityView />;
      case "transactions": return <TransactionView />;
      case "password": return <ChangePasswordView />;
      default: return <InformationView />;
    }
  };
 const SignOutBtn = () => {
// 1. Gọi hàm logout của Zustand (Nó sẽ tự dọn dẹp cả State lẫn LocalStorage)
    logout();

    // 2. Bắn thông báo thành công
    toast.info(`You have been logged out successfully.`);

    // 3. Điều hướng mượt mà về trang chủ không cần F5
    navigate("/");
 
};
  return (
    <div id="devprep-dashboard">
      {/* ================= NAVBAR TOP ================= */}
      <header className="dashboard-header">
      <div className="header-logo" onClick={() => navigate("/")} style={{ cursor: 'pointer' }}>
  DevPrep AI
</div>
        <div className="header-actions">
          <button className="icon-btn"><MdNotifications size={20} /></button>
          <button className="icon-btn"><MdSettings size={20} /></button>
          <div className="user-avatar-mini">
            <img src="https://i.pravatar.cc/150?img=68" alt="Avatar" />
          </div>
        </div>
      </header>

      {/* ================= CORE BODY ================= */}
      <div className="dashboard-body">
        
        {/* SIDEBAR LEFT */}
        <aside className="dashboard-sidebar">
          <div className="sidebar-profile-card">
            <div className="avatar-square-mini">
              <img src="https://i.pravatar.cc/150?img=68" alt="Profile" />
            </div>
            <div className="sidebar-user-info">
              <h3>Developer Profile</h3>
              <span className="tier-badge">ELITE TIER</span>
            </div>
          </div>

          <nav className="sidebar-nav">
            <button 
              className={`nav-item ${activeTab === "information" ? "active" : ""}`}
              onClick={() => setActiveTab("information")}
            >
              <MdPerson className="nav-icon" /> Information
            </button>
            <button 
              className={`nav-item ${activeTab === "cv" ? "active" : ""}`}
              onClick={() => setActiveTab("cv")}
            >
              <MdDescription className="nav-icon" /> CV
            </button>
            <button 
              className={`nav-item ${activeTab === "identity" ? "active" : ""}`}
              onClick={() => setActiveTab("identity")}
            >
              <MdVerifiedUser className="nav-icon" /> Identity Verification
            </button>
            <button 
              className={`nav-item ${activeTab === "transactions" ? "active" : ""}`}
              onClick={() => setActiveTab("transactions")}
            >
              <MdHistory className="nav-icon" /> Transaction History
            </button>
            <button 
              className={`nav-item ${activeTab === "password" ? "active" : ""}`}
              onClick={() => setActiveTab("password")}
            >
              <MdLockReset className="nav-icon" /> Change Password
            </button>
          </nav>

          <button className="sidebar-signout" onClick={SignOutBtn}>
  <MdLogout className="nav-icon" /> Sign Out
</button>
        </aside>

        {/* INTERACTIVE CONTENT ZONE */}
        <main className="dashboard-content">
          {renderActiveView()}
        </main>

      </div>
    </div>
  );
}

export default ProfileDashboard;