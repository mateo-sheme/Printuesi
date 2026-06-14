import { useState } from "react";
import { printJobsApi } from "../api/printJobsApi";
import { useAuth } from "../contexts/auth/useAuth";
import { useLanguage } from "../contexts/language/useLanguage";
import { useTheme } from "../contexts/theme/useTheme";
import StatusBadge from "../components/ui/StatusBadge";
import { paperSizeLabel } from "../utils/constants";

export default function AutomaticPrinting() {
    const { user } = useAuth();
    const { t } = useLanguage();

    const [file, setFile] = useState(null);
    const [job, setJob] = useState(null);
    const [lastResult, setLastResult] = useState(null);
    const [activePaperSize, setActivePaperSize] = useState("");
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState("");

    const refreshJob = async (printJobID) => {
        const refreshed = await printJobsApi.getById(printJobID);
        setJob(refreshed);
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
            const created = await printJobsApi.createFromOrder(file, user.userID);
            setJob(created);

            const firstGroup = (created.groups || created.Groups || [])[0];
            if (firstGroup) {
                setActivePaperSize(paperSizeLabel(firstGroup.paperSize || firstGroup.PaperSize));
            }
        } catch (err) {
            setError(err.message || "Failed to create print job.");
        } finally {
            setLoading(false);
        }
    };

    const runAction = async (action) => {
        if (!job || !activePaperSize) return;

        const printJobID = job.printJobID || job.PrintJobID;

        setLoading(true);
        setError("");

        try {
            const result = await action(printJobID, activePaperSize);
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

        const printJobID = job.printJobID || job.PrintJobID;

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

    const groups = job?.groups || job?.Groups || [];

    return (
        <div className="page-stack">
            <section className="hero-panel">
                <div>
                    <span className="eyebrow">{t("automaticPrinting")}</span>
                    <h1>{t("uploadOrderPdf")}</h1>
                    <p>
                        Upload the order PDF. The backend scans it, groups products by paper
                        size, fills expiry date and LOT, then waits for confirmations.
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
                                Job ID: <strong>{job.printJobID || job.PrintJobID}</strong>
                            </p>
                        </div>

                        <StatusBadge status={job.status || job.Status} />
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
                                    const size = paperSizeLabel(group.paperSize || group.PaperSize);

                                    return (
                                        <option key={size} value={size}>
                                            {size}
                                        </option>
                                    );
                                })}
                            </select>
                        </div>
                    </div>

                    <div className="action-bar">
                        <button
                            className="btn btn-primary-gradient"
                            disabled={loading || !activePaperSize}
                            onClick={() => runAction(printJobsApi.startGroup)}
                        >
                            {t("startGroup")}
                        </button>

                        <button
                            className="btn btn-outline-primary"
                            disabled={loading || !activePaperSize}
                            onClick={() => runAction(printJobsApi.confirmPaperChange)}
                        >
                            {t("confirmPaperChange")}
                        </button>

                        <button
                            className="btn btn-success"
                            disabled={loading || !activePaperSize}
                            onClick={() => runAction(printJobsApi.confirmTest)}
                        >
                            {t("confirmTest")}
                        </button>

                        <button
                            className="btn btn-warning"
                            disabled={loading || !activePaperSize}
                            onClick={() => runAction(printJobsApi.reprintTest)}
                        >
                            {t("reprintTest")}
                        </button>

                        <button className="btn btn-outline-danger" disabled={loading} onClick={cancelJob}>
                            {t("cancelJob")}
                        </button>
                    </div>

                    {lastResult && (
                        <div className="alert alert-info mt-4">
                            <strong>{lastResult.message || lastResult.Message || "Result"}</strong>

                            <div className="mt-2 small">
                                Needs paper change:{" "}
                                {String(lastResult.needsPaperChange ?? lastResult.NeedsPaperChange)}
                                <br />
                                Needs test confirm:{" "}
                                {String(lastResult.needsTestConfirm ?? lastResult.NeedsTestConfirm)}
                                <br />
                                Job completed: {String(lastResult.jobCompleted ?? lastResult.JobCompleted)}
                            </div>

                            {(lastResult.errors || lastResult.Errors || []).length > 0 && (
                                <ul className="mt-2 mb-0">
                                    {(lastResult.errors || lastResult.Errors || []).map((item, index) => (
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
                                    <th>Status</th>
                                    <th>Expiry</th>
                                    <th>LOT</th>
                                </tr>
                            </thead>
                            <tbody>
                                {groups.flatMap((group) => {
                                    const paper = paperSizeLabel(group.paperSize || group.PaperSize);
                                    const items = group.items || group.Items || [];

                                    return items.map((item) => (
                                        <tr key={item.printJobObjectID || item.PrintJobObjectID || item.productCode}>
                                            <td>{paper}</td>
                                            <td>
                                                <strong>{item.productCode || item.ProductCode}</strong>
                                                <div className="small text-muted">
                                                    {item.productName || item.ProductName}
                                                </div>
                                            </td>
                                            <td>{item.quantityRequested || item.QuantityRequested}</td>
                                            <td>{item.quantityPrinted || item.QuantityPrinted}</td>
                                            <td>
                                                <StatusBadge status={item.status || item.Status} />
                                            </td>
                                            <td>{item.expiryDate || item.ExpiryDate || "-"}</td>
                                            <td>{item.lotNumber || item.LotNumber || "-"}</td>
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