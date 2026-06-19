import { apiRequest, apiBlobRequest } from "./http";

function buildGroupQuery(paperSize, kg = "") {
    const params = new URLSearchParams();
    params.set("paperSize", paperSize);

    if (kg) {
        params.set("kg", kg);
    }

    return params.toString();
}

export const printJobsApi = {
    createFromOrder(file, createdByUserID, includeRrethi = false) {
        const formData = new FormData();

        formData.append("orderPdf", file);
        formData.append("createdByUserID", createdByUserID);
        formData.append("includeRrethi", includeRrethi);

        return apiRequest("/api/printjobs/create-from-order", {
            method: "POST",
            body: formData
        });
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
            {
                method: "POST"
            }
        );
    },

    printTest(printJobID, paperSize, kg = "") {
        return apiRequest(
            `/api/printjobs/${encodeURIComponent(printJobID)}/print-test?${buildGroupQuery(paperSize, kg)}`,
            {
                method: "POST"
            }
        );
    },

    startGroup(printJobID, paperSize, kg = "") {
        return apiRequest(
            `/api/printjobs/${encodeURIComponent(printJobID)}/start-group?${buildGroupQuery(paperSize, kg)}`,
            {
                method: "POST"
            }
        );
    },

    stopPrinting(printJobID) {
        return apiRequest(
            `/api/printjobs/${encodeURIComponent(printJobID)}/stop-printing`,
            {
                method: "POST"
            }
        );
    },

    cancel(printJobID) {
        return apiRequest(`/api/printjobs/${encodeURIComponent(printJobID)}`, {
            method: "DELETE"
        });
    },

    deleteJob(printJobID) {
        return apiRequest(
            `/api/printjobs/${encodeURIComponent(printJobID)}/delete`,
            {
                method: "DELETE"
            }
        );
    },

    getGeneratedPdfs(printJobID) {
        return apiRequest(
            `/api/printjobs/${encodeURIComponent(printJobID)}/generated-pdfs`
        );
    },

    prepareGeneratedPdf(printJobID, printJobObjectID, kg = "") {
        const query = kg ? `?kg=${encodeURIComponent(kg)}` : "";

        return apiRequest(
            `/api/printjobs/${encodeURIComponent(printJobID)}/generated-pdfs/${encodeURIComponent(printJobObjectID)}/prepare${query}`,
            {
                method: "POST"
            }
        );
    },

    getGeneratedPdfPreviewBlob(printJobID, printJobObjectID) {
        return apiBlobRequest(
            `/api/printjobs/${encodeURIComponent(printJobID)}/generated-pdfs/${encodeURIComponent(printJobObjectID)}/preview`
        );
    }
};