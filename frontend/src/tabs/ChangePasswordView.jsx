import React, { useState } from "react";
import { MdLock, MdVisibility, MdVisibilityOff } from "react-icons/md";

function ChangePasswordView() {
  const [showPassword, setShowPassword] = useState({ current: false, new: false, confirm: false });
  const [passwords, setPasswords] = useState({ current: "", new: "", confirm: "" });

  const toggleVisibility = (field) => {
    setShowPassword({ ...showPassword, [field]: !showPassword[field] });
  };

  const handleInputChange = (e) => {
    setPasswords({ ...passwords, [e.target.name]: e.target.value });
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    if(passwords.new !== passwords.confirm) {
      alert("New passwords do not match!");
      return;
    }
    console.log("Mật khẩu đã được thay đổi bảo mật.");
  };

  return (
    <div className="tab-view-container">
      <div className="view-title">
        <h1>Change Password</h1>
        <p>Update your password regularly to keep your developer profile and metrics safe.</p>
      </div>

      <div className="table-wrapper-card password-card-padding">
        <form onSubmit={handleSubmit} className="details-form max-width-form">
          
          <div className="form-group">
            <label>Current Password</label>
            <div className="password-input-wrapper">
              <input 
                type={showPassword.current ? "text" : "password"} 
                name="current"
                placeholder="Enter current password"
                value={passwords.current}
                onChange={handleInputChange}
                required
              />
              <button type="button" className="eye-toggle" onClick={() => toggleVisibility("current")}>
                {showPassword.current ? <MdVisibilityOff /> : <MdVisibility />}
              </button>
            </div>
          </div>

          <div className="form-group">
            <label>New Password</label>
            <div className="password-input-wrapper">
              <input 
                type={showPassword.new ? "text" : "password"} 
                name="new"
                placeholder="Minimum 8 characters"
                value={passwords.new}
                onChange={handleInputChange}
                required
              />
              <button type="button" className="eye-toggle" onClick={() => toggleVisibility("new")}>
                {showPassword.new ? <MdVisibilityOff /> : <MdVisibility />}
              </button>
            </div>
            {/* Password strength guide */}
            <div className="password-strength-meter">
              <span className={`bar ${passwords.new.length > 7 ? "strong" : "weak"}`}></span>
              <p>Password must include letters, numbers, and special characters.</p>
            </div>
          </div>

          <div className="form-group">
            <label>Confirm New Password</label>
            <div className="password-input-wrapper">
              <input 
                type={showPassword.confirm ? "text" : "password"} 
                name="confirm"
                placeholder="Repeat your new password"
                value={passwords.confirm}
                onChange={handleInputChange}
                required
              />
              <button type="button" className="eye-toggle" onClick={() => toggleVisibility("confirm")}>
                {showPassword.confirm ? <MdVisibilityOff /> : <MdVisibility />}
              </button>
            </div>
          </div>

          <div className="form-actions border-top-actions">
            <button type="button" className="btn-cancel">Reset Fields</button>
            <button type="submit" className="btn-buy-credits">Update Password</button>
          </div>

        </form>
      </div>
    </div>
  );
}

export default ChangePasswordView;