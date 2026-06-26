import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { MdEmail, MdLock, MdPerson } from "react-icons/md";
import { FcGoogle } from "react-icons/fc";
import {
  AiFillGithub,
  AiOutlineEye,
  AiOutlineEyeInvisible,
} from "react-icons/ai";
import {
  GithubAuthProvider,
  GoogleAuthProvider,
  signInWithPopup,
} from "firebase/auth";
import { auth } from "../firebase";
import { toast } from "react-toastify";
import useAuthStore from "../store/useAuthStore";
import "./Login.css"; // Tái sử dụng file CSS chung
import { firebaseSSOLogin } from "../api/axiosClient"; 

function Register({ isModal, onClose, switchToLogin }) {
  const navigate = useNavigate();
  const { setUser, setAccessToken } = useAuthStore();

  const [formData, setFormData] = useState({
    name: "",
    email: "",
    password: "",
    confirmPassword: "",
  });
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [authError, setAuthError] = useState("");

  const handleChange = (e) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value,
    });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!formData.name || !formData.email || !formData.password || !formData.confirmPassword) {
      toast.warning("Please fill in all fields.");
      return;
    }

    if (formData.password !== formData.confirmPassword) {
      toast.error("Passwords do not match.");
      return;
    }

    try {
      // TODO: Thay thế bằng API đăng ký thực tế từ axiosClient của bạn khi có
      // const response = await registerWithEmail(formData.name, formData.email, formData.password);
      
      toast.success("Registration successful! Please login.");
      if (switchToLogin) {
        switchToLogin();
      } else {
        navigate("/login");
      }
    } catch (error) {
      console.error(error);
      const message = error.response?.data?.message || "Registration failed";
      setAuthError(message);
      toast.error(message);
    }
  };

  const saveUserData = (firebaseUser, accessToken) => {
    const user = {
      uid: firebaseUser.uid,
      name: firebaseUser.displayName,
      email: firebaseUser.email,
      avatar: firebaseUser.photoURL,
    };
    localStorage.setItem("user", JSON.stringify(user));
    localStorage.setItem("token", accessToken);
    setUser(user);
    setAccessToken(accessToken);
    if (onClose) onClose();
    toast.success(`Welcome ${user.name}`);
    setTimeout(() => { navigate("/"); }, 0);
  };

  const signInWithGoogle = async () => {
    const provider = new GoogleAuthProvider();
    try {
      const result = await signInWithPopup(auth, provider);
      const firebaseIdToken = await result.user.getIdToken();
      const response = await firebaseSSOLogin(firebaseIdToken);
      const backendToken = response.data.data.accessToken;
      saveUserData(result.user, backendToken);
    } catch (error) {
      console.error(error);
      toast.error("Google login failed");
    }
  };

  const signInWithGitHub = async () => {
    const provider = new GithubAuthProvider();
    try {
      const result = await signInWithPopup(auth, provider);
      const firebaseIdToken = await result.user.getIdToken();
      const response = await firebaseSSOLogin(firebaseIdToken);
      const backendToken = response.data.data.accessToken;
      saveUserData(result.user, backendToken);
    } catch (error) {
      console.error(error);
      toast.error("GitHub login failed");
    }
  };

  return (
    <div id="login-page">
      <div className={isModal ? "login-page modal-login" : "login-page"}>
        <div className="login-card">
          <h1>Create Account</h1>
          <p className="subtitle">Sign up to start your preparation.</p>

          <form onSubmit={handleSubmit}>
            {/* Full Name */}
            <div className="form-group">
              <label>Full Name</label>
              <div className="input-wrapper">
                <MdPerson className="input-icon" />
                <input
                  type="text"
                  name="name"
                  placeholder="John Doe"
                  value={formData.name}
                  onChange={handleChange}
                  required
                />
              </div>
            </div>

            {/* Email */}
            <div className="form-group">
              <label>Email Address</label>
              <div className="input-wrapper">
                <MdEmail className="input-icon" />
                <input
                  type="email"
                  name="email"
                  placeholder="engineer@devprep.ai"
                  value={formData.email}
                  onChange={handleChange}
                  required
                />
              </div>
            </div>

            {/* Password */}
            <div className="form-group">
              <label>Password</label>
              <div className="input-wrapper">
                <MdLock className="input-icon" />
                <input
                  type={showPassword ? "text" : "password"}
                  name="password"
                  placeholder="Create a password"
                  value={formData.password}
                  onChange={handleChange}
                  required
                />
                <button
                  type="button"
                  className="password-toggle"
                  onClick={() => setShowPassword((prev) => !prev)}
                >
                  {showPassword ? <AiOutlineEyeInvisible /> : <AiOutlineEye />}
                </button>
              </div>
            </div>

            {/* Confirm Password */}
            <div className="form-group">
              <label>Confirm Password</label>
              <div className="input-wrapper">
                <MdLock className="input-icon" />
                <input
                  type={showConfirmPassword ? "text" : "password"}
                  name="confirmPassword"
                  placeholder="Repeat your password"
                  value={formData.confirmPassword}
                  onChange={handleChange}
                  required
                />
                <button
                  type="button"
                  className="password-toggle"
                  onClick={() => setShowConfirmPassword((prev) => !prev)}
                >
                  {showConfirmPassword ? <AiOutlineEyeInvisible /> : <AiOutlineEye />}
                </button>
              </div>
            </div>

            <button type="submit" className="login-btn" style={{ marginTop: "10px" }}>
              Sign Up
            </button>

            {authError && <p className="auth-error">{authError}</p>}
          </form>

          <div className="divider">
            <span>OR</span>
          </div>

          <div className="social-buttons">
            <button type="button" className="social-btn" onClick={signInWithGoogle}>
              <FcGoogle size={18} />
              <span>Sign up with Google</span>
            </button>

            <button type="button" className="social-btn" onClick={signInWithGitHub}>
              <AiFillGithub size={18} />
              <span>Sign up with GitHub</span>
            </button>
          </div>

          <p className="register-text">
            Already have an account? 
            {switchToLogin ? (
              <span onClick={switchToLogin} style={{ color: "#a78bfa", cursor: "pointer", fontWeight: 600 }}> Login</span>
            ) : (
              <a href="/login"> Login</a>
            )}
          </p>
        </div>
      </div>
    </div>
  );
}

export default Register;