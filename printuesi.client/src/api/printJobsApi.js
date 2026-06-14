import { apiRequest } from "./http";

export const printJobsApi = {
    getAll(status = null) {
        const query = status ? `?status=${encodeURIComponent(status)}` : "";
        return apiRequest(`/api/printjobs${query}`);
    },

    getById(printJobID) {
        return apiRequest(`/api/printjobs/${printJobID}`);
    },

    createFromOrder(orderPdf, createdByUserID) {
        const formData = new FormData();
        formData.append("orderPdf", orderPdf);

        return apiRequest(
            `/api/printjobs/create-from-order?createdByUserID=${encodeURIComponent(createdByUserID)}`,
            {
                method: "POST",
                body: formData
            }
        );
    },

    startGroup(printJobID, paperSize) {
        return apiRequest(
            `/api/printjobs/${printJobID}/start-group?paperSize=${encodeURIComponent(paperSize)}`,
            {
                method: "POST"
            }
        );
    },

    confirmPaperChange(printJobID, paperSize) {
        return apiRequest(
            `/api/printjobs/${printJobID}/confirm-paper-change?paperSize=${encodeURIComponent(paperSize)}`,
            {
                method: "POST"
            }
        );
    },

    confirmTest(printJobID, paperSize) {
        return apiRequest(
            `/api/printjobs/${printJobID}/confirm-test?paperSize=${encodeURIComponent(paperSize)}`,
            {
                method: "POST"
            }
        );
    },

    reprintTest(printJobID, paperSize) {
        return apiRequest(
            `/api/printjobs/${printJobID}/reprint-test?paperSize=${encodeURIComponent(paperSize)}`,
            {
                method: "POST"
            }
        );
    },

    cancel(printJobID) {
        return apiRequest(`/api/printjobs/${printJobID}`, {
            method: "DELETE"
        });
    }
};