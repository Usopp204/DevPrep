import axios from "axios";

// Base URL for API calls
const API_BASE_URL = import.meta.env.VITE_API_URL || "http://localhost:7001/api";

const axiosClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    "Content-Type": "application/json",
  },
});

// Add token to requests
axiosClient.interceptors.request.use((config) => {
  const token = localStorage.getItem("token");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Handle response errors
axiosClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem("token");
      localStorage.removeItem("user");
      window.location.href = "/login";
    }
    return Promise.reject(error);
  }
);

export default axiosClient;

// ===== Authentication API Calls =====

/**
 * Sign in with email and password
 * @param {string} email - User email
 * @param {string} password - User password
 * @returns {Promise} - API response with user data and token
 */
export const signInWithEmail = (email, password) => {
  return axiosClient.post("/auth/signin", {
    email,
    password,
  });
};

/**
 * Sign in with Google
 * @param {string} googleToken - Google ID token from Firebase
 * @returns {Promise} - API response with user data and token
 */
export const signInWithGoogleAPI = (googleToken) => {
  return axiosClient.post("/auth/signin-google", {
    token: googleToken,
  });
};

/**
 * Sign in with GitHub
 * @param {string} githubToken - GitHub access token from Firebase
 * @returns {Promise} - API response with user data and token
 */
export const signInWithGitHubAPI = (githubToken) => {
  return axiosClient.post("/auth/signin-github", {
    token: githubToken,
  });
};

/**
 * Register new user
 * @param {object} userData - User registration data
 * @returns {Promise} - API response
 */
export const registerUser = (userData) => {
  return axiosClient.post("/auth/register", userData);
};

/**
 * Logout user
 * @returns {Promise} - API response
 */
export const logoutUser = () => {
  return axiosClient.post("/auth/logout");
};

/**
 * Get current user info
 * @returns {Promise} - API response with user data
 */
export const getCurrentUser = () => {
  return axiosClient.get("/auth/me");
};

// ===== axiosClient.js - Fire base login (GG - Github) =====
export const firebaseSSOLogin = (idToken) => {
  return axiosClient.post("/auth/sso/firebase", { idToken });
};

// Login type email and pass
export const loginWithEmail = (email, password) => {
  return axiosClient.post("/auth/login", { email, password });
};