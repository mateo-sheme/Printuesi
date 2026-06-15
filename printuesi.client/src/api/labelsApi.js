import { apiRequest } from "./http";

export const labelsApi = {
    getLabels({ search = "", clientFolder = "" } = {}) {
        const params = new URLSearchParams();

        if (search) {
            params.set("search", search);
        }

        if (clientFolder && clientFolder !== "all") {
            params.set("clientFolder", clientFolder);
        }

        const query = params.toString();

        return apiRequest(`/api/manual-printing/labels${query ? `?${query}` : ""}`);
    },

    getPreviewUrl(relativePath) {
        return `/api/manual-printing/labels/preview?relativePath=${encodeURIComponent(relativePath)}`;
    },

    sendDriver(paperSize) {
        return apiRequest("/api/manual-printing/send-driver", {
            method: "POST",
            body: JSON.stringify({ paperSize })
        });
    },

    printLabel({ relativePath, copies }) {
        return apiRequest("/api/manual-printing/print-label", {
            method: "POST",
            body: JSON.stringify({
                relativePath,
                copies: Number(copies)
            })
        });
    },

    printLabelWithDriver({ relativePath, paperSize, copies }) {
        return apiRequest("/api/manual-printing/print-label-with-driver", {
            method: "POST",
            body: JSON.stringify({
                relativePath,
                paperSize,
                copies: Number(copies)
            })
        });
    }
};