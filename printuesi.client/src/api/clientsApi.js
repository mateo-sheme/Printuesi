import { apiRequest } from "./http";

export const clientsApi = {
    getAll() {
        return apiRequest("/api/clients");
    }
};