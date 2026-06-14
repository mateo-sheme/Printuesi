import { apiRequest } from "./http";

export const printerApi = {
    getStatus() {
        return apiRequest("/api/printer/status");
    },

    sendLprFile(file) {
        const formData = new FormData();
        formData.append("file", file);

        return apiRequest("/api/printer/LPR", {
            method: "POST",
            body: formData
        });
    }
};