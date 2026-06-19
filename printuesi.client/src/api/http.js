const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || "";
const STORAGE_KEY = "printuesi_auth";

function getToken() {
    try {
        const raw = localStorage.getItem(STORAGE_KEY);
        if (!raw) return null;

        const data = JSON.parse(raw);
        return data?.token || null;
    } catch {
        return null;
    }
}

async function parseResponse(response) {
    const contentType = response.headers.get("content-type") || "";

    if (contentType.includes("application/json")) {
        return response.json();
    }

    const text = await response.text();
    return text ? { message: text } : {};
}

export async function apiRequest(path, options = {}) {
    const token = getToken();
    const isFormData = options.body instanceof FormData;

    const headers = {
        ...(options.headers || {})
    };

    if (!isFormData) {
        headers["Content-Type"] = "application/json";
    }

    if (token) {
        headers.Authorization = `Bearer ${token}`;
    }

    const response = await fetch(`${API_BASE_URL}${path}`, {
        ...options,
        headers
    });

    const data = await parseResponse(response);

    if (!response.ok) {
        const message =
            data?.message ||
            data?.title ||
            data?.error ||
            `Request failed with status ${response.status}`;

        const error = new Error(message);
        error.status = response.status;
        error.data = data;
        throw error;
    }

    return data;
}

export async function apiBlobRequest(path, options = {}) {
    const token = getToken();

    const headers = {
        ...(options.headers || {})
    };

    if (token) {
        headers.Authorization = `Bearer ${token}`;
    }

    const response = await fetch(`${API_BASE_URL}${path}`, {
        ...options,
        headers
    });

    if (!response.ok) {
        let message = `Request failed with status ${response.status}`;

        try {
            const contentType = response.headers.get("content-type") || "";

            if (contentType.includes("application/json")) {
                const data = await response.json();
                message =
                    data?.message ||
                    data?.title ||
                    data?.error ||
                    message;
            } else {
                const text = await response.text();
                if (text) {
                    message = text;
                }
            }
        } catch {
            // Keep default message.
        }

        const error = new Error(message);
        error.status = response.status;
        throw error;
    }

    return response.blob();
}