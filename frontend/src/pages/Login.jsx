import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { MdEmail, MdLock } from "react-icons/md";
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
  linkWithCredential,
  fetchSignInMethodsForEmail,
} from "firebase/auth";
import { auth } from "../firebase";
import { toast } from "react-toastify";
import useAuthStore from "../store/useAuthStore";
import "./Login.css";
import { firebaseSSOLogin, loginWithEmail } from "../api/axiosClient"; // Import the new API function

function Login({ isModal, onClose ,switchToRegister, switchToForgot}) {
  const navigate = useNavigate();
  const { setUser, setAccessToken } = useAuthStore();

  const [formData, setFormData] = useState({
    email: "",
    password: "",
  });
  const [showPassword, setShowPassword] = useState(false);
  const [authError, setAuthError] = useState("");
  const [pendingCredential, setPendingCredential] = useState(null);
  const [pendingEmail, setPendingEmail] = useState(null);
  const [linkedProvider, setLinkedProvider] = useState(null);
  const handleChange = (e) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value,
    });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!formData.email || !formData.password) {
      toast.warning("Please fill in all fields.");
      return;
    }

    try {
      const response = await loginWithEmail(formData.email, formData.password);
      const backendToken = response.data.data.accessToken; // ⚠️ kiểm tra field name

      // Tạo user object từ response backend
      const user = {
        uid: response.data.data.userId, // ⚠️ kiểm tra field name
        name: response.data.data.name, // ⚠️ kiểm tra field name
        email: response.data.data.email, // ⚠️ kiểm tra field name
        avatar: response.data.data.avatar, // ⚠️ kiểm tra field name
      };

      localStorage.setItem("user", JSON.stringify(user));
      localStorage.setItem("token", backendToken);

      setUser(user);
      setAccessToken(backendToken);

      if (onClose) {
        onClose();
      }
      toast.success(`Welcome ${user.name}`);

      setTimeout(() => {
        navigate("/");
      }, 0);
    } catch (error) {
      console.error(error);
      const message = error.response?.data?.message || "Login failed";
      setAuthError(message);
      toast.error(message);
    }
  };

  // lấy thông tin từ firebase user và lưu vào localStorage (sau này sửa lạ thành call api profile và hiển thị lên)
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
    // close madal login
if (onClose) {
  onClose();
}
    toast.success(`Welcome ${user.name}`);
setTimeout(() => {
    navigate("/");},0)
  };

  // Login with Google
  const signInWithGoogle = async () => {
    const provider = new GoogleAuthProvider();
    try {
      const result = await signInWithPopup(auth, provider);
      // Lấy Firebase ID Token
      const firebaseIdToken = await result.user.getIdToken();
      // Call backend
      const response = await firebaseSSOLogin(firebaseIdToken);
      const backendToken = response.data.data.accessToken; // ⚠️ đổi nếu backend dùng tên field khác
      // Lưu token từ backend (không phải Firebase token)
      saveUserData(result.user, backendToken);
      console.log("LOGIN RESPONSE:", response.data.data);
    } catch (error) {
      console.error(error);
      // Handle account linking error
      if (error.code === "auth/account-exists-with-different-credential") {
        const pendingCred = GoogleAuthProvider.credentialFromError(error);
        const email = error.customData.email;
        // Check which providers are already linked
        try {
          const methods = await fetchSignInMethodsForEmail(auth, email);
          const provider = methods[0]; // e.g., 'github.com', 'google.com'
          setLinkedProvider(provider);
          setPendingCredential(pendingCred);
          setPendingEmail(email);
          const providerName = provider.includes("github")
            ? "GitHub"
            : "Google";
          const errorMsg = `This email is already linked to ${providerName}. Sign in with ${providerName} first, then link Google.`;
          setAuthError(errorMsg);
          toast.warning(errorMsg);
        } catch (fetchError) {
          console.error(fetchError);
          setAuthError("Email already in use. Please try another provider.");
          toast.error("Email already in use.");
        }
      } else {
        setAuthError("Google login failed");
        toast.error("Google login failed");
      }
    }
  };

  // Login with github
  const signInWithGitHub = async () => {
    const provider = new GithubAuthProvider();

    try {
      const result = await signInWithPopup(auth, provider);
      const firebaseIdToken = await result.user.getIdToken();

      const response = await firebaseSSOLogin(firebaseIdToken);
      const backendToken = response.data.data.accessToken;

      saveUserData(result.user, backendToken);
      console.log("GitHub login successful:", response);
    } catch (error) {
      console.error(error);

      if (error.code === "auth/account-exists-with-different-credential") {
        const pendingCred = GithubAuthProvider.credentialFromError(error);
        const email = error.customData.email;

        try {
          const methods = await fetchSignInMethodsForEmail(auth, email);
          const provider = methods[0];
          setLinkedProvider(provider);
          setPendingCredential(pendingCred);
          setPendingEmail(email);

          const providerName = provider.includes("github")
            ? "GitHub"
            : "Google";
          const errorMsg = `This email is already linked to ${providerName}. Sign in with ${providerName} first, then link GitHub.`;
          setAuthError(errorMsg);
          toast.warning(errorMsg);
        } catch (fetchError) {
          console.error(fetchError);
          setAuthError("Email already in use. Please try another provider.");
          toast.error("Email already in use.");
        }
      } else {
        setAuthError("GitHub login failed");
        toast.error("GitHub login failed");
      }
    }
  };

  // Handle linking pending credential to existing account
  const handleLinkAccount = async () => {
    if (!pendingCredential || !auth.currentUser) return;

    try {
      await linkWithCredential(auth.currentUser, pendingCredential);
      const accessToken = auth.currentUser.getIdToken();

      saveUserData(auth.currentUser, accessToken);
      setPendingCredential(null);
      setPendingEmail(null);
      setLinkedProvider(null);

      toast.success("Account linked successfully!");
    } catch (error) {
      console.error(error);
      toast.error("Failed to link accounts. Please try again.");
    }
  };

  return (
    <div id="login-page">
      <div className={isModal ? "login-page modal-login" : "login-page"}>
        <div className="login-card">
          <h1>Welcome Back</h1>

          <p className="subtitle">Sign in to continue your preparation.</p>

          <form onSubmit={handleSubmit}>
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

            <div className="form-group">
              <label>Password</label>

              <div className="input-wrapper">
                <MdLock className="input-icon" />
                <input
                  type={showPassword ? "text" : "password"}
                  name="password"
                  placeholder="Enter your password"
                  value={formData.password}
                  onChange={handleChange}
                  required
                />
                <button
                  type="button"
                  className="password-toggle"
                  onClick={() => setShowPassword((prev) => !prev)}
                  aria-label={showPassword ? "Hide password" : "Show password"}
                >
                  {showPassword ? <AiOutlineEyeInvisible /> : <AiOutlineEye />}
                </button>
              </div>
            </div>

            <div className="form-options">
              <label>
                <input type="checkbox" />
                Remember me
              </label>

              {/* Kiểm tra nếu chạy trong Modal thì đổi view, nếu chạy trang độc lập thì giữ link thẻ a */}
              {isModal && switchToForgot ? (
                <span onClick={switchToForgot} style={{ color: "#a78bfa", cursor: "pointer" }}>
                  Forgot Password?
                </span>
              ) : (
                <a href="/forgot-password">Forgot Password?</a>
              )}
            </div>

            <button type="submit" className="login-btn">
              Sign In
            </button>

            {authError && <p className="auth-error">{authError}</p>}

            {pendingCredential && auth.currentUser && (
              <button
                type="button"
                className="login-btn"
                onClick={handleLinkAccount}
                style={{ marginTop: "10px", backgroundColor: "#28a745" }}
              >
                ✓ Link{" "}
                {linkedProvider?.includes("github") ? "GitHub" : "Google"}{" "}
                Account
              </button>
            )}

            {pendingCredential && !auth.currentUser && (
              <div
                style={{
                  marginTop: "10px",
                  padding: "10px",
                  backgroundColor: "#fff3cd",
                  borderRadius: "5px",
                  textAlign: "center",
                }}
              >
                <p style={{ margin: "0 0 10px 0", fontSize: "14px" }}>
                  Please sign in with{" "}
                  <strong>
                    {linkedProvider?.includes("github") ? "GitHub" : "Google"}
                  </strong>{" "}
                  first to link this account.
                </p>
              </div>
            )}
          </form>

          <div className="divider">
            <span>OR</span>
          </div>

          <div className="social-buttons">
            <button
              type="button"
              className="social-btn"
              onClick={() => signInWithGoogle()}
            >
              <FcGoogle size={18} />
              <span>Sign in with Google</span>
            </button>

            <button
              type="button"
              className="social-btn"
              onClick={() => signInWithGitHub()}
            >
              <AiFillGithub size={18} />
              <span>Sign in with GitHub</span>
            </button>
          </div>

          <p className="register-text">
            Don't have an account?
            {isModal && switchToRegister ? (
              <span onClick={switchToRegister} style={{ color: "#a78bfa", cursor: "pointer", fontWeight: 600 }}>
                {" "}Register
              </span>
            ) : (
              <a href="/register"> Register</a>
            )}
          </p>
        </div>
      </div>
    </div>
  );
}

export default Login;
