import { BrowserRouter, Routes, Route } from "react-router-dom";

import MainLayout from "../layouts/MainLayout";
import AuthLayout from "../layouts/AuthLayout";

import Home from "../pages/Home";
import Login from "../pages/Login";
import Register from "../pages/Register";
import ForgotPassword from "../pages/ForgotPassword";
import NewsList from "../pages/NewsList";
import NewsDetail from "../pages/NewsDetail";
import FeedbackAI from "../pages/FeedbackAI";
import Payment from "../pages/Payment";
import FeedbackSystem from "../pages/FeedbackSystem";
import ProfileDashboard from "../layouts/ProfileDashboard";
function AppRoutes() {
  return (
    <BrowserRouter>
      <Routes>
        {/* Public Pages */}
        <Route element={<MainLayout />}>
          <Route index element={<Home />} />
          <Route path="/payment" element={<Payment />} />
          <Route path="/news" element={<NewsList />} />
          <Route path="/news/:id" element={<NewsDetail />} />
          <Route path="/feedback-ai" element={<FeedbackAI />} />
          <Route path="/feedback-system" element={<FeedbackSystem />} />


        </Route>
        <Route element={<ProfileDashboard />}>
          <Route path="/profile/*" element={<ProfileDashboard />} />

        </Route>
        {/* Auth Pages */}
        <Route element={<AuthLayout />}>
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route path="/forgot-password" element={<ForgotPassword />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default AppRoutes;
