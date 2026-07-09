import { apiRequest } from "./http";

const BASE_URL = "/api/clients";

export const clientsApi = {
    getAll() {
        return apiRequest(BASE_URL);
    },

    getById(clientID) {
        return apiRequest(`${BASE_URL}/${encodeURIComponent(clientID)}`);
    },

    getPrintJobs(clientID, status = "Completed") {
        const query = status ? `?status=${encodeURIComponent(status)}` : "";

        return apiRequest(
            `${BASE_URL}/${encodeURIComponent(clientID)}/print-jobs${query}`
        );
    }
};