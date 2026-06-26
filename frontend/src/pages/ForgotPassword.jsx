import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { MdEmail } from "react-icons/md";
import { sendPasswordResetEmail } from "firebase/auth";
import { auth } from "../firebase";
import { toast } from "react-toastify";
import "./Login.css";

function ForgotPassword({ isModal, switchToLogin }) {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!email) {
      toast.warning("Please enter your email address.");
      return;
    }

    setIsLoading(true);
    try {
      // Gửi mail reset mật khẩu thông qua Firebase Auth
      await sendPasswordResetEmail(auth, email);
      toast.success("Password reset email sent! Please check your inbox.");
      
      setTimeout(() => {
        if (switchToLogin) {
          switchToLogin();
        } else {
          navigate("/login");
        }
      }, 2000);
    } catch (error) {
      console.error(error);
      const message = error.message || "Failed to send link.";
      toast.error(message);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div id="login-page">
      <div className={isModal ? "login-page modal-login" : "login-page"}>
        <div className="login-card">
          <h1>Forgot Password?</h1>
          <p className="subtitle">
            Enter your email address and we'll send you a link to reset your password.
          </p>

          <form onSubmit={handleSubmit}>
            <div className="form-group" style={{ marginBottom: "20px" }}>
              <label>Email Address</label>
              <div className="input-wrapper">
                <MdEmail className="input-icon" />
                <input
                  type="email"
                  name="email"
                  placeholder="engineer@devprep.ai"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                />
              </div>
            </div>

            <button type="submit" className="login-btn" disabled={isLoading}>
              {isLoading ? "Sending..." : "Send Reset Link"}
            </button>
          </form>

          <p className="register-text" style={{ marginTop: "24px" }}>
            Remember your password?
            {switchToLogin ? (
              <span onClick={switchToLogin} style={{ color: "#a78bfa", cursor: "pointer", fontWeight: 600 }}> Back to Login</span>
            ) : (
              <a href="/login"> Back to Login</a>
            )}
          </p>
        </div>
      </div>
    </div>
  );
}

export default ForgotPassword;