import { apiRequest } from "./http";

function buildGroupQuery(paperSize, kg = "") {
    const params = new URLSearchParams();
    params.set("paperSize", paperSize);

    if (kg) {
        params.set("kg", kg);
    }

    return params.toString();
}

export const printJobsApi = {
    createFromOrder(file, createdByUserID) {
        const formData = new FormData();
        formData.append("orderPdf", file);

        return apiRequest(
            `/api/printjobs/create-from-order?createdByUserID=${encodeURIComponent(createdByUserID)}`,
            {
                method: "POST",
                body: formData,
                isFormData: true
            }
        );
    },

    getAll(status = "") {
        const query = status ? `?status=${encodeURIComponent(status)}` : "";
        return apiRequest(`/api/printjobs${query}`);
    },

    getById(printJobID) {
        return apiRequest(`/api/printjobs/${encodeURIComponent(printJobID)}`);
    },

    confirmPaperChange(printJobID, paperSize) {
        return apiRequest(
            `/api/printjobs/${encodeURIComponent(printJobID)}/confirm-paper-change?paperSize=${encodeURIComponent(paperSize)}`,
            { method: "POST" }
        );
    },

    printTest(printJobID, paperSize, kg = "") {
        return apiRequest(
            `/api/printjobs/${encodeURIComponent(printJobID)}/print-test?${buildGroupQuery(paperSize, kg)}`,
            { method: "POST" }
        );
    },

    reprintTest(printJobID, paperSize, kg = "") {
        return apiRequest(
            `/api/printjobs/${encodeURIComponent(printJobID)}/reprint-test?${buildGroupQuery(paperSize, kg)}`,
            { method: "POST" }
        );
    },

    startGroup(printJobID, paperSize, kg = "") {
        return apiRequest(
            `/api/printjobs/${encodeURIComponent(printJobID)}/start-group?${buildGroupQuery(paperSize, kg)}`,
            { method: "POST" }
        );
    },

    cancel(printJobID) {
        return apiRequest(`/api/printjobs/${encodeURIComponent(printJobID)}`, {
            method: "DELETE"
        });
    }
};