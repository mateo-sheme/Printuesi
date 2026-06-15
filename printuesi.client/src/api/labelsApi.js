import { apiRequest } from "./http";

const BASE_URL = "/api/manual-printing";

export const labelsApi = {
    getPrintSettings() {
        return apiRequest(`${BASE_URL}/print-settings`);
    },

    getLabels({ search = "", clientFolder = "" } = {}) {
        const params = new URLSearchParams();

        if (search) params.set("search", search);
        if (clientFolder) params.set("clientFolder", clientFolder);

        const query = params.toString();

        return apiRequest(`${BASE_URL}/labels${query ? `?${query}` : ""}`);
    },

    getPreviewUrl(relativePath) {
        return `${BASE_URL}/labels/preview?relativePath=${encodeURIComponent(relativePath)}`;
    },

    sendDriver(paperSize) {
        console.log("SEND DRIVER API CALLED:", paperSize);

        return apiRequest(`${BASE_URL}/send-driver`, {
            method: "POST",
            body: JSON.stringify({
                paperSize
            })
        });
    },

    printLabel({ relativePath, copies, kg = "" }) {
        return apiRequest(`${BASE_URL}/print-label`, {
            method: "POST",
            body: JSON.stringify({
                relativePath,
                copies,
                kg
            })
        });
    }
};