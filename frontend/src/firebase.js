import { initializeApp } from "firebase/app";
import { getAuth } from "firebase/auth";

// Your Firebase configuration for login with Google and GitHub
const firebaseConfig = {
  apiKey: "AIzaSyDMBq3gBut_une3SM88aILCHy_P-b2yRaY",
  authDomain: "devprep-6872e.firebaseapp.com",
  projectId: "devprep-6872e",
  appId: "1:5087839335:web:3c52a527f1fe746643c0c2",
};

// Initialize Firebase
const app = initializeApp(firebaseConfig);

// Initialize Firebase Authentication
export const auth = getAuth(app);
