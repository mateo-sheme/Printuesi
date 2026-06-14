import { apiRequest } from "./http";

export const authApi = {
    login(credentials) {
        return apiRequest("/api/auth/login", {
            method: "POST",
            body: JSON.stringify(credentials)
        });
    },

    logout() {
        return apiRequest("/api/auth/logout", {
            method: "POST"
        });
    }
};