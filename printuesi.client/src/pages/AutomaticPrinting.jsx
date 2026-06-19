import { useState } from "react";
import { printJobsApi } from "../api/printJobsApi";
import { useAuth } from "../contexts/auth/useAuth";
import { useLanguage } from "../contexts/language/useLanguage";
import StatusBadge from "../components/ui/StatusBadge";
import { paperSizeLabel } from "../utils/constants";

function getValue(object, camelKey, pascalKey, fallback = "") {
    return object?.[camelKey] ?? object?.[pascalKey] ?? fallback;
}

function normalizeBool(value) {
    return value === true || value === "true" || value === "True";
}

export default function AutomaticPrinting() {
    const { user } = useAuth();
    const { t } = useLanguage();

    const [file, setFile] = useState(null);
    const [job, setJob] = useState(null);
    const [lastResult, setLastResult] = useState(null);
    const [activePaperSize, setActivePaperSize] = useState("");
    const [selectedKg, setSelectedKg] = useState("1 kg");

    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    const [includeRrethi, setIncludeRrethi] = useState(false);

    const refreshJob = async (printJobID) => {
        const refreshed = await printJobsApi.getById(printJobID);
        setJob(refreshed);

        const requiresKg = normalizeBool(getValue(refreshed, "requiresKg", "RequiresKg", false));
        const kgOptions = getValue(refreshed, "kgOptions", "KgOptions", ["0.5 kg", "1 kg", "2 kg"]);

        if (requiresKg && Array.isArray(kgOptions) && kgOptions.length > 0) {
            setSelectedKg((current) => (kgOptions.includes(current) ? current : kgOptions[0]));
        }
    };

    const createJob = async (event) => {
        event.preventDefault();

        if (!file) {
            setError("Please select an order PDF.");
            return;
        }

        setLoading(true);
        setError("");
        setLastResult(null);    

        try {
            const created = await printJobsApi.createFromOrder(file, user.userID, includeRrethi);
            setJob(created);

            const firstGroup = getValue(created, "groups", "Groups", [])[0];

            if (firstGroup) {
                setActivePaperSize(paperSizeLabel(getValue(firstGroup, "paperSize", "PaperSize")));
            }

            const requiresKg = normalizeBool(getValue(created, "requiresKg", "RequiresKg", false));
            const kgOptions = getValue(created, "kgOptions", "KgOptions", ["0.5 kg", "1 kg", "2 kg"]);

            if (requiresKg && Array.isArray(kgOptions) && kgOptions.length > 0) {
                setSelectedKg(kgOptions[0]);
            }
        } catch (err) {
            setError(err.message || "Failed to create print job.");
        } finally {
            setLoading(false);
        }
    };

    const runAction = async (action, { sendKg = false } = {}) => {
        if (!job || !activePaperSize) return;

        const printJobID = getValue(job, "printJobID", "PrintJobID");
        const requiresKg = normalizeBool(getValue(job, "requiresKg", "RequiresKg", false));

        if (sendKg && requiresKg && !selectedKg) {
            setError("Please select KG first.");
            return;
        }

        setLoading(true);
        setError("");

        try {
            const kgToSend = sendKg && requiresKg ? selectedKg : "";
            const result = await action(printJobID, activePaperSize, kgToSend);

            setLastResult(result);
            await refreshJob(printJobID);
        } catch (err) {
            setError(err.message || "Print action failed.");
        } finally {
            setLoading(false);
        }
    };

    const cancelJob = async () => {
        if (!job) return;

        const printJobID = getValue(job, "printJobID", "PrintJobID");

        setLoading(true);
        setError("");

        try {
            await printJobsApi.cancel(printJobID);
            await refreshJob(printJobID);
        } catch (err) {
            setError(err.message || "Failed to cancel job.");
        } finally {
            setLoading(false);
        }
    };

    const groups = getValue(job, "groups", "Groups", []);
    const requiresKg = job ? normalizeBool(getValue(job, "requiresKg", "RequiresKg", false)) : false;
    const kgOptions = job
        ? getValue(job, "kgOptions", "KgOptions", ["0.5 kg", "1 kg", "2 kg"])
        : ["0.5 kg", "1 kg", "2 kg"];

    return (
        <div className="page-stack">
            <section className="hero-panel">
                <div>
                    <span className="eyebrow">{t("automaticPrinting")}</span>
                    <h1>{t("uploadOrderPdf")}</h1>
                    <p>
                        Upload the order PDF. The backend scans it, groups products by paper
                        size, fills expiry date, LOT, and KG when required.
                    </p>
                </div>
            </section>

            {error && <div className="alert alert-danger">{error}</div>}

            <div className="panel-card">
                <form onSubmit={createJob}>
                    <label className="form-label">{t("uploadOrderPdf")}</label>

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

                    <div className="form-check mt-3">
                        <input
                            className="form-check-input"
                            type="checkbox"
                            id="includeRrethi"
                            checked={includeRrethi}
                            onChange={(event) => setIncludeRrethi(event.target.checked)}
                        />

                        <label className="form-check-label" htmlFor="includeRrethi">
                            Printim me etiketë rreth përsipër?
                        </label>
                    </div>

                    <button className="btn btn-primary-gradient mt-3" disabled={loading}>
                        {loading ? t("loading") : t("createJob")}
                    </button>
                </form>
            </div>

            {job && (
                <div className="panel-card">
                    <div className="panel-header">
                        <div>
                            <h5>{t("jobCreated")}</h5>
                            <p>
                                Job ID: <strong>{getValue(job, "printJobID", "PrintJobID")}</strong>
                            </p>
                        </div>

                        <StatusBadge status={getValue(job, "status", "Status")} />
                    </div>

                    <div className="row g-3 mb-4">
                        <div className="col-12 col-md-6">
                            <label className="form-label">Paper group</label>
                            <select
                                className="form-select form-select-lg"
                                value={activePaperSize}
                                onChange={(event) => setActivePaperSize(event.target.value)}
                            >
                                {groups.map((group) => {
                                    const size = paperSizeLabel(getValue(group, "paperSize", "PaperSize"));

                                    return (
                                        <option key={size} value={size}>
                                            {size}
                                        </option>
                                    );
                                })}
                            </select>
                        </div>

                        {requiresKg && (
                            <div className="col-12 col-md-6">
                                <label className="form-label">KG</label>
                                <select
                                    className="form-select form-select-lg"
                                    value={selectedKg}
                                    onChange={(event) => setSelectedKg(event.target.value)}
                                >
                                    {kgOptions.map((option) => (
                                        <option key={option} value={option}>
                                            {option}
                                        </option>
                                    ))}
                                </select>
                                <div className="small text-muted mt-2">
                                    This replaces {"{{KG}}"} on the PDF label.
                                </div>
                            </div>
                        )}
                    </div>

                    <div className="action-bar automatic-action-bar">
                        <button
                            className="btn btn-primary-gradient"
                            disabled={loading || !activePaperSize}
                            onClick={() => runAction(printJobsApi.confirmPaperChange)}
                        >
                            {t("confirmPaperChange")}
                        </button>

                        <button
                            className="btn btn-success"
                            disabled={loading || !activePaperSize}
                            onClick={() => runAction(printJobsApi.printTest, { sendKg: true })}
                        >
                            Printo testin
                        </button>

                        <button
                            className="btn btn-primary-gradient"
                            disabled={loading || !activePaperSize}
                            onClick={() => runAction(printJobsApi.startGroup, { sendKg: true })}
                        >
                            {t("startGroup")}
                        </button>

                        <button className="btn btn-outline-danger" disabled={loading} onClick={cancelJob}>
                            {t("cancelJob")}
                        </button>
                    </div>

                    {lastResult && (
                        <div className="alert alert-info mt-4">
                            <strong>{getValue(lastResult, "message", "Message", "Result")}</strong>

                            <div className="mt-2 small">
                                Needs paper change:{" "}
                                {String(getValue(lastResult, "needsPaperChange", "NeedsPaperChange", false))}
                                <br />
                                Needs test confirm:{" "}
                                {String(getValue(lastResult, "needsTestConfirm", "NeedsTestConfirm", false))}
                                <br />
                                Job completed:{" "}
                                {String(getValue(lastResult, "jobCompleted", "JobCompleted", false))}
                            </div>

                            {getValue(lastResult, "errors", "Errors", []).length > 0 && (
                                <ul className="mt-2 mb-0">
                                    {getValue(lastResult, "errors", "Errors", []).map((item, index) => (
                                        <li key={index}>{item}</li>
                                    ))}
                                </ul>
                            )}
                        </div>
                    )}

                    <div className="table-responsive mt-4">
                        <table className="table modern-table align-middle">
                            <thead>
                                <tr>
                                    <th>Paper</th>
                                    <th>Product</th>
                                    <th>Requested</th>
                                    <th>Printed</th>
                                    {requiresKg && <th>KG</th>}
                                    <th>Status</th>
                                    <th>Expiry</th>
                                    <th>LOT</th>
                                </tr>
                            </thead>

                            <tbody>
                                {groups.flatMap((group) => {
                                    const paper = paperSizeLabel(getValue(group, "paperSize", "PaperSize"));
                                    const items = getValue(group, "items", "Items", []);

                                    return items.map((item) => (
                                        <tr
                                            key={
                                                getValue(item, "printJobObjectID", "PrintJobObjectID") ||
                                                getValue(item, "productCode", "ProductCode")
                                            }
                                        >
                                            <td>{paper}</td>
                                            <td>
                                                <strong>{getValue(item, "productCode", "ProductCode")}</strong>
                                                <div className="small text-muted">
                                                    {getValue(item, "productName", "ProductName")}
                                                </div>
                                            </td>
                                            <td>{getValue(item, "quantityRequested", "QuantityRequested", 0)}</td>
                                            <td>{getValue(item, "quantityPrinted", "QuantityPrinted", 0)}</td>
                                            {requiresKg && <td>{selectedKg || "-"}</td>}
                                            <td>
                                                <StatusBadge status={getValue(item, "status", "Status")} />
                                            </td>
                                            <td>{getValue(item, "expiryDate", "ExpiryDate", "-") || "-"}</td>
                                            <td>{getValue(item, "lotNumber", "LotNumber", "-") || "-"}</td>
                                        </tr>
                                    ));
                                })}
                            </tbody>
                        </table>
                    </div>
                </div>
            )}
        </div>
    );
}