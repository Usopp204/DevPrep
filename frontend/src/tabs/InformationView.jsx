import React, { useState } from "react";
import { MdCloudUpload } from "react-icons/md";
import { FiAlertCircle } from "react-icons/fi";
import { BsShieldCheck, BsEye, BsGraphUp } from "react-icons/bs";

function InformationView() {
  const [profileData, setProfileData] = useState({
    fullName: "Alex Rivera",
    email: "alex.rivera@devprep.ai",
    phoneNumber: "+1 (555) 000-123",
    dob: "1992-05-24",
    currentCity: "San Francisco, CA",
    targetRole: "Full-Stack Engineer",
  });

  const [phoneError, setPhoneError] = useState("Please provide a valid international format.");

  const handleChange = (e) => {
    setProfileData({
      ...profileData,
      [e.target.name]: e.target.value,
    });
  };

  const handleSave = (e) => {
    e.preventDefault();
    console.log("Cập nhật thông tin thành công:", profileData);
  };

  return (
    <div className="tab-view-container">
      <div className="view-title">
        <h1>Profile Information</h1>
        <p>Manage your technical identity and career preferences across the platform.</p>
      </div>

      <div className="profile-grid">
        {/* AVATAR CARD */}
        <section className="card avatar-card">
          <div className="avatar-wrapper">
            <img src="https://i.pravatar.cc/300?img=68" alt="Alex Rivera" />
          </div>
          <h3 className="user-name">{profileData.fullName}</h3>
          <p className="user-role">FULL-STACK ENGINEER</p>
          
          <button className="upload-btn">
            <MdCloudUpload size={16} /> Update Photo
          </button>
          <span className="upload-note">JPG, GIF or PNG. Max size of 800K</span>
        </section>

        {/* PERSONAL DETAILS FORM */}
        <section className="card details-card">
          <h2 className="card-heading">Personal Details</h2>
          <span className="last-updated">✓ LAST UPDATED: 2 DAYS AGO</span>

          <form onSubmit={handleSave} className="details-form">
            <div className="form-row">
              <div className="form-group">
                <label>Full Name</label>
                <input 
                  type="text" 
                  name="fullName" 
                  value={profileData.fullName} 
                  onChange={handleChange} 
                />
              </div>
              <div className="form-group">
                <label>Email Address</label>
                <div className="input-with-badge">
                  <input 
                    type="email" 
                    name="email" 
                    value={profileData.email} 
                    disabled 
                  />
                  <span className="verified-badge">✓ VERIFIED</span>
                </div>
              </div>
            </div>

            <div className="form-row">
              <div className="form-group">
                <label>Phone Number</label>
                <input 
                  type="text" 
                  name="phoneNumber" 
                  className={phoneError ? "input-error" : ""}
                  value={profileData.phoneNumber} 
                  onChange={handleChange} 
                />
                {phoneError && (
                  <p className="error-message">
                    <FiAlertCircle /> {phoneError}
                  </p>
                )}
              </div>
              <div className="form-group">
                <label>Date of Birth</label>
                <input 
                  type="date" 
                  name="dob" 
                  value={profileData.dob} 
                  onChange={handleChange} 
                />
              </div>
            </div>

            <div className="form-row">
              <div className="form-group">
                <label>Current City</label>
                <input 
                  type="text" 
                  name="currentCity" 
                  value={profileData.currentCity} 
                  onChange={handleChange} 
                />
              </div>
              <div className="form-group">
                <label>Target Role</label>
                <select name="targetRole" value={profileData.targetRole} onChange={handleChange}>
                  <option value="Full-Stack Engineer">Full-Stack Engineer</option>
                  <option value="Frontend Engineer">Frontend Engineer</option>
                  <option value="Backend Engineer">Backend Engineer</option>
                </select>
              </div>
            </div>

            <div className="form-actions">
              <button type="button" className="btn-cancel">Cancel</button>
              <button type="submit" className="btn-save">Save Changes</button>
            </div>
          </form>
        </section>
      </div>

      {/* FOOTER WIDGETS */}
      <footer className="widgets-grid">
        <div className="widget-box">
          <div className="widget-icon-wrapper purple"><BsShieldCheck size={18} /></div>
          <div>
            <h4>Secure Account</h4>
            <p>2FA is currently enabled</p>
          </div>
        </div>
        <div className="widget-box">
          <div className="widget-icon-wrapper blue"><BsEye size={18} /></div>
          <div>
            <h4>Profile Visibility</h4>
            <p>Public to Recruiters</p>
          </div>
        </div>
        <div className="widget-box">
          <div className="widget-icon-wrapper gray"><BsGraphUp size={18} /></div>
          <div>
            <h4>Activity Score</h4>
            <p>98th Percentile</p>
          </div>
        </div>
      </footer>
    </div>
  );
}

export default InformationView;