import React from "react";
import ReactDOM from "react-dom/client";
import "bootstrap/dist/css/bootstrap.min.css";
import "./styles/theme.css";

import App from "./App";

import AuthProvider from "./contexts/auth/AuthProvider";
import ThemeProvider from "./contexts/theme/ThemeProvider";
import LanguageProvider from "./contexts/language/LanguageProvider";

ReactDOM.createRoot(document.getElementById("root")).render(
    <React.StrictMode>
        <ThemeProvider>
            <LanguageProvider>
                <AuthProvider>
                    <App />
                </AuthProvider>
            </LanguageProvider>
        </ThemeProvider>
    </React.StrictMode>
);