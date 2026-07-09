import { apiRequest } from "./http";

const BASE_URL = "/api/users";

export const usersApi = {
    getAll() {
        return apiRequest(BASE_URL);
    },

    createUser({ username, password, role }) {
        return apiRequest(BASE_URL, {
            method: "POST",
            body: JSON.stringify({
                username,
                password,
                role
            })
        });
    },

    updateUser(userID, { username, role, isActive = true }) {
        return apiRequest(`${BASE_URL}/${encodeURIComponent(userID)}`, {
            method: "PUT",
            body: JSON.stringify({
                username,
                role,
                isActive
            })
        });
    },

    changePassword(userID, password) {
        return apiRequest(
            `${BASE_URL}/${encodeURIComponent(userID)}/change-password`,
            {
                method: "POST",
                body: JSON.stringify({
                    password
                })
            }
        );
    },

    deleteUser(userID) {
        return apiRequest(`${BASE_URL}/${encodeURIComponent(userID)}`, {
            method: "DELETE"
        });
    }
};