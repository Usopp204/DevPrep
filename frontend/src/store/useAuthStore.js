import { create } from "zustand";

const useAuthStore = create((set) => ({
  user: JSON.parse(localStorage.getItem("user")) || null,
  accessToken: localStorage.getItem("token"),
  isAuthenticated: !!localStorage.getItem("token"),

  setUser: (user) =>
    set({
      user,
      isAuthenticated: !!user,
    }),

  setAccessToken: (token) =>
    set({
      accessToken: token,
      isAuthenticated: !!token,
    }),

  logout: () => {
   localStorage.clear(); // Xóa sạch localStorage

    set({
      user: null,
      accessToken: null,
      isAuthenticated: false,
    });
  },
}));

export default useAuthStore;