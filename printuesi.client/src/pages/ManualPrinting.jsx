import { useState } from "react";
import { printerApi } from "../api/printerApi";
import { useAuth } from "../contexts/auth/useAuth";
import { useLanguage } from "../contexts/language/useLanguage";
import { useTheme } from "../contexts/theme/useTheme";

export default function ManualPrinting() {
    const { t } = useLanguage();

    const [file, setFile] = useState(null);
    const [message, setMessage] = useState("");
    const [error, setError] = useState("");
    const [loading, setLoading] = useState(false);

    const sendFile = async (event) => {
        event.preventDefault();

        if (!file) {
            setError("Please select a PDF file.");
            return;
        }

        setLoading(true);
        setMessage("");
        setError("");

        try {
            const result = await printerApi.sendLprFile(file);
            setMessage(result.message || "File sent to printer.");
        } catch (err) {
            setError(err.message || "Failed to send file to printer.");
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="page-stack">
            <section className="hero-panel">
                <div>
                    <span className="eyebrow">{t("manualPrinting")}</span>
                    <h1>{t("uploadPdfToPrint")}</h1>
                    <p>
                        Use this only for prepared PDFs. Automatic date/LOT replacement is
                        handled by the automatic print flow, not this manual screen.
                    </p>
                </div>
            </section>

            {message && <div className="alert alert-success">{message}</div>}
            {error && <div className="alert alert-danger">{error}</div>}

            <div className="panel-card">
                <form onSubmit={sendFile}>
                    <label className="form-label">{t("uploadPdfToPrint")}</label>

                    <input
                        className="form-control form-control-lg"
                        type="file"
                        accept="application/pdf"
                        onChange={(event) => setFile(event.target.files?.[0] || null)}
                    />

                    {file && (
                        <div className="small text-muted mt-2">
                            {t("selectedFile")}: {file.name}
                        </div>
                    )}

                    <button className="btn btn-primary-gradient mt-3" disabled={loading}>
                        {loading ? t("loading") : t("sendToPrinter")}
                    </button>
                </form>
            </div>
        </div>
    );
}