import { useEffect, useMemo, useState } from "react";
import { printJobsApi } from "../api/printJobsApi";
import StatusBadge from "../components/ui/StatusBadge";
import { useLanguage } from "../contexts/language/useLanguage";

const STATUS_FILTERS = [
    { value: "", label: "All" },
    { value: "Pending", label: "Pending" },
    { value: "In_progress", label: "In progress" },
    { value: "Completed", label: "Completed" },
    { value: "Cancelled", label: "Cancelled" }
];

function getValue(object, camelKey, pascalKey, fallback = "") {
    return object?.[camelKey] ?? object?.[pascalKey] ?? fallback;
}

function normalizeStatus(status) {
    return String(status || "").replace("_", " ");
}

function formatDate(value) {
    if (!value) return "-";

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "-";

    return date.toLocaleString();
}

function getJobStatus(job) {
    return getValue(job, "status", "Status");
}

function getJobId(job) {
    return getValue(job, "printJobID", "PrintJobID");
}

function getGroups(job) {
    return getValue(job, "groups", "Groups", []);
}

function getGroupPaperSize(group) {
    return getValue(group, "paperSize", "PaperSize");
}

function getGroupItems(group) {
    return getValue(group, "items", "Items", []);
}

function isFinishedJob(job) {
    const status = normalizeStatus(getJobStatus(job)).toLowerCase();
    return status === "completed" || status === "cancelled";
}

export default function PrintJobs() {
    const { t } = useLanguage();

    const [jobs, setJobs] = useState([]);
    const [selectedJob, setSelectedJob] = useState(null);
    const [activePaperSize, setActivePaperSize] = useState("");

    const [statusFilter, setStatusFilter] = useState("");
    const [loading, setLoading] = useState(true);
    const [actionLoading, setActionLoading] = useState(false);

    const [message, setMessage] = useState("");
    const [error, setError] = useState("");
    const [lastResult, setLastResult] = useState(null);

    async function loadJobs(nextStatus = statusFilter) {
        setLoading(true);
        setError("");

        try {
            const result = await printJobsApi.getAll(nextStatus);
            const safeJobs = Array.isArray(result) ? result : [];

            setJobs(safeJobs);

            if (selectedJob) {
                const selectedId = getJobId(selectedJob);
                const updatedSelected = safeJobs.find((job) => getJobId(job) === selectedId);

                if (updatedSelected) {
                    setSelectedJob(updatedSelected);
                }
            }
        } catch (err) {
            setError(err.message || "Failed to load print jobs.");
        } finally {
            setLoading(false);
        }
    }

    async function refreshSelectedJob(printJobID) {
        const updated = await printJobsApi.getById(printJobID);
        setSelectedJob(updated);

        const groups = getGroups(updated);
        const stillExists = groups.some((group) => getGroupPaperSize(group) === activePaperSize);

        if (!activePaperSize || !stillExists) {
            setActivePaperSize(groups[0] ? getGroupPaperSize(groups[0]) : "");
        }

        await loadJobs(statusFilter);
    }

    useEffect(() => {
        let cancelled = false;

        async function initialLoad() {
            try {
                const result = await printJobsApi.getAll(statusFilter);

                if (cancelled) return;

                setJobs(Array.isArray(result) ? result : []);
            } catch (err) {
                if (cancelled) return;
                setError(err.message || "Failed to load print jobs.");
            } finally {
                if (cancelled) return;
                setLoading(false);
            }
        }

        initialLoad();

        return () => {
            cancelled = true;
        };
    }, [statusFilter]);

    const selectedGroups = useMemo(() => {
        return selectedJob ? getGroups(selectedJob) : [];
    }, [selectedJob]);

    const activeGroup = useMemo(() => {
        return selectedGroups.find((group) => getGroupPaperSize(group) === activePaperSize);
    }, [selectedGroups, activePaperSize]);

    const selectJob = async (job) => {
        const printJobID = getJobId(job);

        setMessage("");
        setError("");
        setLastResult(null);
        setSelectedJob(job);

        const groups = getGroups(job);
        setActivePaperSize(groups[0] ? getGroupPaperSize(groups[0]) : "");

        try {
            const freshJob = await printJobsApi.getById(printJobID);
            setSelectedJob(freshJob);

            const freshGroups = getGroups(freshJob);
            setActivePaperSize(freshGroups[0] ? getGroupPaperSize(freshGroups[0]) : "");
        } catch (err) {
            setError(err.message || "Failed to open print job.");
        }
    };

    const runGroupAction = async (action, successFallback) => {
        if (!selectedJob || !activePaperSize) {
            setError("Select a print job and paper group first.");
            return;
        }

        const printJobID = getJobId(selectedJob);

        setActionLoading(true);
        setError("");
        setMessage("");
        setLastResult(null);

        try {
            const result = await action(printJobID, activePaperSize);

            setLastResult(result);

            if (result.success === false || result.Success === false) {
                const errors = result.errors || result.Errors || [];
                setError(result.message || result.Message || errors[0] || "Action failed.");
            } else {
                setMessage(result.message || result.Message || successFallback);
            }

            await refreshSelectedJob(printJobID);
        } catch (err) {
            setError(err.message || "Action failed.");
        } finally {
            setActionLoading(false);
        }
    };

    const cancelSelectedJob = async () => {
        if (!selectedJob) {
            setError("Select a print job first.");
            return;
        }

        const printJobID = getJobId(selectedJob);

        const confirmed = window.confirm("Cancel this print job?");
        if (!confirmed) return;

        setActionLoading(true);
        setError("");
        setMessage("");

        try {
            const result = await printJobsApi.cancel(printJobID);
            setMessage(result.message || "Print job cancelled.");

            const updated = await printJobsApi.getAll(statusFilter);
            setJobs(Array.isArray(updated) ? updated : []);
            setSelectedJob(null);
            setActivePaperSize("");
        } catch (err) {
            setError(err.message || "Failed to cancel print job.");
        } finally {
            setActionLoading(false);
        }
    };

    const selectedJobID = selectedJob ? getJobId(selectedJob) : "";
    const selectedStatus = selectedJob ? getJobStatus(selectedJob) : "";
    const selectedClientName = selectedJob
        ? getValue(selectedJob, "clientName", "ClientName")
        : "";

    const selectedCreatedAt = selectedJob
        ? getValue(selectedJob, "createdAt", "CreatedAt")
        : "";

    const canRunActions = selectedJob && activePaperSize && !isFinishedJob(selectedJob);

    return (
        <div className="page-stack">
            <section className="hero-panel compact-hero">
                <div>
                    <span className="eyebrow">Print Jobs</span>
                    <h1>Job Control Center</h1>
                    <p>
                        Open a job, confirm paper, print test labels, start production,
                        or cancel jobs that are not completed.
                    </p>
                </div>
            </section>

            {error && <div className="alert alert-danger">{error}</div>}
            {message && <div className="alert alert-success">{message}</div>}

            {lastResult && (lastResult.errors?.length > 0 || lastResult.Errors?.length > 0) && (
                <div className="alert alert-warning">
                    {(lastResult.errors || lastResult.Errors).map((item, index) => (
                        <div key={`${item}-${index}`}>{item}</div>
                    ))}
                </div>
            )}

            <section className="jobs-layout">
                <div className="panel-card compact-panel jobs-list-panel">
                    <div className="panel-header">
                        <div>
                            <h5>Jobs</h5>
                            <p>Choose one job to manage.</p>
                        </div>

                        <select
                            className="form-select jobs-status-filter"
                            value={statusFilter}
                            onChange={(event) => {
                                setSelectedJob(null);
                                setActivePaperSize("");
                                setStatusFilter(event.target.value);
                            }}
                        >
                            {STATUS_FILTERS.map((item) => (
                                <option key={item.value} value={item.value}>
                                    {item.label}
                                </option>
                            ))}
                        </select>
                    </div>

                    {loading ? (
                        <div className="small text-muted">{t("loading")}</div>
                    ) : jobs.length === 0 ? (
                        <div className="empty-state compact-empty">
                            <div className="empty-icon">□</div>
                            <h5>No jobs found</h5>
                            <p>Create a job from automatic printing first.</p>
                        </div>
                    ) : (
                        <div className="job-card-list">
                            {jobs.map((job) => {
                                const jobID = getJobId(job);
                                const clientName = getValue(job, "clientName", "ClientName");
                                const status = getJobStatus(job);
                                const createdAt = getValue(job, "createdAt", "CreatedAt");
                                const groups = getGroups(job);

                                const isSelected = selectedJobID === jobID;

                                return (
                                    <button
                                        type="button"
                                        key={jobID}
                                        className={`job-list-card ${isSelected ? "selected" : ""}`}
                                        onClick={() => selectJob(job)}
                                    >
                                        <div>
                                            <strong>{clientName || "Unknown client"}</strong>
                                            <span>{jobID}</span>
                                        </div>

                                        <div className="job-list-card-meta">
                                            <StatusBadge status={status} />
                                            <small>{groups.length} group(s)</small>
                                            <small>{formatDate(createdAt)}</small>
                                        </div>
                                    </button>
                                );
                            })}
                        </div>
                    )}
                </div>

                <div className="panel-card compact-panel job-detail-panel">
                    {!selectedJob ? (
                        <div className="empty-state compact-empty">
                            <div className="empty-icon">□</div>
                            <h5>No job selected</h5>
                            <p>Select one print job from the left list.</p>
                        </div>
                    ) : (
                        <>
                            <div className="job-detail-header">
                                <div>
                                    <span className="eyebrow">Selected job</span>
                                    <h5>{selectedClientName}</h5>
                                    <p>
                                        {selectedJobID} · {formatDate(selectedCreatedAt)}
                                    </p>
                                </div>

                                <StatusBadge status={selectedStatus} />
                            </div>

                            <div className="paper-tabs">
                                {selectedGroups.map((group) => {
                                    const paper = getGroupPaperSize(group);
                                    const completed =
                                        getValue(group, "isCompleted", "IsCompleted", false);

                                    return (
                                        <button
                                            type="button"
                                            key={paper}
                                            className={`paper-tab ${activePaperSize === paper ? "active" : ""
                                                }`}
                                            onClick={() => setActivePaperSize(paper)}
                                        >
                                            {paper}
                                            {completed && <span>✓</span>}
                                        </button>
                                    );
                                })}
                            </div>

                            <div className="automatic-action-bar mt-3 mb-3">
                                <button
                                    className="btn btn-primary-gradient"
                                    disabled={!canRunActions || actionLoading}
                                    onClick={() =>
                                        runGroupAction(
                                            printJobsApi.confirmPaperChange,
                                            "Paper confirmed and driver sent."
                                        )
                                    }
                                >
                                    Kam ndërruar letrën
                                </button>

                                <button
                                    className="btn btn-success"
                                    disabled={!canRunActions || actionLoading}
                                    onClick={() =>
                                        runGroupAction(
                                            printJobsApi.printTest,
                                            "Test label sent."
                                        )
                                    }
                                >
                                    Printo testin
                                </button>

                                <button
                                    className="btn btn-warning"
                                    disabled={!canRunActions || actionLoading}
                                    onClick={() =>
                                        runGroupAction(
                                            printJobsApi.reprintTest,
                                            "Test label reprinted."
                                        )
                                    }
                                >
                                    Riprinto testin
                                </button>

                                <button
                                    className="btn btn-primary-gradient"
                                    disabled={!canRunActions || actionLoading}
                                    onClick={() =>
                                        runGroupAction(
                                            printJobsApi.startGroup,
                                            "Group started."
                                        )
                                    }
                                >
                                    Nis grupin
                                </button>

                                <button
                                    className="btn btn-outline-danger"
                                    disabled={!selectedJob || isFinishedJob(selectedJob) || actionLoading}
                                    onClick={cancelSelectedJob}
                                >
                                    Anulo punën
                                </button>
                            </div>

                            {actionLoading && (
                                <div className="alert alert-info d-flex align-items-center gap-3">
                                    <div className="spinner-border spinner-border-sm" />
                                    <strong>Processing printer action...</strong>
                                </div>
                            )}

                            {!activeGroup ? (
                                <div className="empty-state compact-empty">
                                    <div className="empty-icon">□</div>
                                    <h5>No paper group selected</h5>
                                    <p>Select one paper group above.</p>
                                </div>
                            ) : (
                                <div className="table-responsive">
                                    <table className="table modern-table align-middle">
                                        <thead>
                                            <tr>
                                                <th>Code</th>
                                                <th>Product</th>
                                                <th>Requested</th>
                                                <th>Printed</th>
                                                <th>Expiry</th>
                                                <th>LOT</th>
                                                <th>Status</th>
                                                <th>File</th>
                                            </tr>
                                        </thead>

                                        <tbody>
                                            {getGroupItems(activeGroup).map((item) => {
                                                const id = getValue(
                                                    item,
                                                    "printJobObjectID",
                                                    "PrintJobObjectID"
                                                );

                                                const code = getValue(
                                                    item,
                                                    "productCode",
                                                    "ProductCode"
                                                );

                                                const product = getValue(
                                                    item,
                                                    "productName",
                                                    "ProductName",
                                                    code
                                                );

                                                const requested =
                                                    getValue(
                                                        item,
                                                        "quantityRequested",
                                                        "QuantityRequested",
                                                        0
                                                    ) ?? 0;

                                                const printed =
                                                    getValue(
                                                        item,
                                                        "quantityPrinted",
                                                        "QuantityPrinted",
                                                        0
                                                    ) ?? 0;

                                                const expiry = getValue(
                                                    item,
                                                    "expiryDate",
                                                    "ExpiryDate",
                                                    "-"
                                                );

                                                const lot = getValue(
                                                    item,
                                                    "lotNumber",
                                                    "LotNumber",
                                                    "-"
                                                );

                                                const status = getValue(
                                                    item,
                                                    "status",
                                                    "Status"
                                                );

                                                const found = getValue(
                                                    item,
                                                    "labelFileFound",
                                                    "LabelFileFound",
                                                    false
                                                );

                                                return (
                                                    <tr key={id || code}>
                                                        <td>
                                                            <strong>{code}</strong>
                                                        </td>
                                                        <td>{product}</td>
                                                        <td>{requested}</td>
                                                        <td>{printed}</td>
                                                        <td>{expiry || "-"}</td>
                                                        <td>{lot || "-"}</td>
                                                        <td>
                                                            <StatusBadge status={status} />
                                                        </td>
                                                        <td>
                                                            {found ? (
                                                                <span className="text-success fw-bold">
                                                                    Found
                                                                </span>
                                                            ) : (
                                                                <span className="text-danger fw-bold">
                                                                    Missing
                                                                </span>
                                                            )}
                                                        </td>
                                                    </tr>
                                                );
                                            })}
                                        </tbody>
                                    </table>
                                </div>
                            )}
                        </>
                    )}
                </div>
            </section>
        </div>
    );
}