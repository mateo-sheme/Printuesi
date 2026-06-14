import { useMemo, useState } from "react";
import { authApi } from "../../api/authApi";
import { AuthContext, AUTH_STORAGE_KEY } from "./AuthContext";

function readStoredAuth() {
    try {
        const raw = localStorage.getItem(AUTH_STORAGE_KEY);
        return raw ? JSON.parse(raw) : null;
    } catch {
        return null;
    }
}

function normalizeLoginResponse(response) {
    return {
        token: response.token || response.Token,
        userID: response.userID || response.userId || response.UserID,
        name: response.name || response.Name,
        role: response.role || response.Role,
        expiresAt: response.expiresAt || response.ExpiresAt
    };
}

export default function AuthProvider({ children }) {
    const [auth, setAuth] = useState(readStoredAuth);

    const login = async ({ name, password }) => {
        const response = await authApi.login({ name, password });
        const normalized = normalizeLoginResponse(response);

        if (!normalized.token) {
            throw new Error("Login response did not include a token.");
        }

        localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(normalized));
        setAuth(normalized);

        return normalized;
    };

    const logout = async () => {
        try {
            await authApi.logout();
        } catch {
            // Local logout continues even if backend logout fails.
        }

        localStorage.removeItem(AUTH_STORAGE_KEY);
        setAuth(null);
    };

    const value = useMemo(
        () => ({
            auth,
            user: auth,
            token: auth?.token || null,
            isAuthenticated: Boolean(auth?.token),
            isAdmin: String(auth?.role || "").toLowerCase() === "admin",
            login,
            logout
        }),
        [auth]
    );

    return (
        <AuthContext.Provider value={value}>
            {children}
        </AuthContext.Provider>
    );
}