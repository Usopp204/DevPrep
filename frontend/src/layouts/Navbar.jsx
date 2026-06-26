import { NavLink } from "react-router-dom";
import { useEffect } from "react";
import "./Navbar.css";
import { useState } from "react";
import LoginModal from "../components/common/LoginModal";
import useAuthStore from "../store/useAuthStore";

function Navbar() {
const user = useAuthStore((state) => state.user);
  const accessToken = useAuthStore((state) => state.accessToken);

  const [showLogin, setShowLogin] = useState(false);

  console.log("Navbar:", accessToken);

  return (
    <header className="navbar">
      <div className="logo">DevPrep</div>

      <ul className="menu">
        <li>
          <NavLink to="/">HOME</NavLink>
        </li>
        <li>
          <NavLink to="/payment">PAYMENT</NavLink>
        </li>
        <li>
          <NavLink to="/news">NEWS</NavLink>
        </li>
        <li>
          <NavLink to="/feedback-system">FEEDBACK SYSTEM</NavLink>
        </li>
        <li>
          <NavLink to="/feedback-ai">FEEDBACK AI</NavLink>
        </li>
        <span className="indicator"></span>
      </ul>

      <div className="nav-btns">
        {!accessToken ? (
          <>
            <button className="start-btn" onClick={() => setShowLogin(true)}>
              Login
            </button>
            <LoginModal show={showLogin} onClose={() => setShowLogin(false)} />
          </>
        ) : (
          <NavLink to="/profile">
            <img
              src={user?.avatar || "https://ui-avatars.com/api/?name=User"}
              alt="Profile"
              className="profile-avatar"
            />
          </NavLink>
        )}
      </div>
    </header>
  );
}

export default Navbar;