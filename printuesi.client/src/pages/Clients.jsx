import { useEffect, useMemo, useState } from "react";
import { clientsApi } from "../api/clientsApi";

const STATUS_FILTERS = [
    { value: "", label: "All" },
    { value: "Pending", label: "Pending" },
    { value: "In_progress", label: "In progress" },
    { value: "Stopped", label: "Stopped" },
    { value: "Completed", label: "Completed" },
    { value: "Cancelled", label: "Cancelled" }
];

function getValue(object, camelKey, pascalKey, fallback = "") {
    return object?.[camelKey] ?? object?.[pascalKey] ?? fallback;
}

function formatDate(value) {
    if (!value) return "-";

    const date = new Date(value);

    if (Number.isNaN(date.getTime())) return "-";

    return date.toLocaleString();
}

function getStatusKey(status) {
    return String(status || "")
        .toLowerCase()
        .replace("_", " ")
        .trim();
}

function getJobDisplayStatus(status) {
    const key = getStatusKey(status);

    if (key === "completed") return "Done";
    if (key === "in progress") return "In progress";
    if (key === "cancelled") return "Cancelled";
    if (key === "stopped") return "Stopped";

    return "Pending";
}

function getJobBadgeClass(status) {
    const key = getStatusKey(status);

    if (key === "completed") return "done";
    if (key === "in progress") return "progress";
    if (key === "cancelled") return "cancelled";
    if (key === "stopped") return "stopped";

    return "pending";
}

function getClientId(client) {
    return getValue(client, "clientID", "ClientID");
}

function getClientName(client) {
    return getValue(client, "name", "Name", "Unknown client");
}

function getClientNipt(client) {
    return getValue(client, "nipt", "NIPT", "");
}

export default function Clients() {
    const [clients, setClients] = useState([]);
    const [selectedClient, setSelectedClient] = useState(null);
    const [clientJobs, setClientJobs] = useState([]);

    const [search, setSearch] = useState("");
    const [jobStatusFilter, setJobStatusFilter] = useState("Completed");

    const [loading, setLoading] = useState(true);
    const [jobsLoading, setJobsLoading] = useState(false);
    const [error, setError] = useState("");

    useEffect(() => {
        let cancelled = false;

        async function loadClients() {
            setLoading(true);
            setError("");

            try {
                const result = await clientsApi.getAll();

                if (cancelled) return;

                setClients(Array.isArray(result) ? result : []);
            } catch (err) {
                if (cancelled) return;

                setError(err.message || "Failed to load clients.");
            } finally {
                if (cancelled) return;

                setLoading(false);
            }
        }

        loadClients();

        return () => {
            cancelled = true;
        };
    }, []);

    useEffect(() => {
        if (!selectedClient) return;

        const clientID = getClientId(selectedClient);

        if (!clientID) return;

        let cancelled = false;

        async function loadJobs() {
            setJobsLoading(true);
            setError("");

            try {
                const result = await clientsApi.getPrintJobs(
                    clientID,
                    jobStatusFilter
                );

                if (cancelled) return;

                setClientJobs(Array.isArray(result) ? result : []);
            } catch (err) {
                if (cancelled) return;

                setError(err.message || "Failed to load client print jobs.");
            } finally {
                if (cancelled) return;

                setJobsLoading(false);
            }
        }

        loadJobs();

        return () => {
            cancelled = true;
        };
    }, [selectedClient, jobStatusFilter]);

    const filteredClients = useMemo(() => {
        const term = search.trim().toLowerCase();

        if (!term) return clients;

        return clients.filter((client) => {
            const name = getClientName(client).toLowerCase();
            const nipt = getClientNipt(client).toLowerCase();

            return name.includes(term) || nipt.includes(term);
        });
    }, [clients, search]);

    async function selectClient(client) {
        setSelectedClient(client);
        setClientJobs([]);
        setError("");

        const clientID = getClientId(client);

        if (!clientID) {
            setError("Selected client does not have a valid ClientID.");
            return;
        }

        setJobsLoading(true);

        try {
            const result = await clientsApi.getPrintJobs(
                clientID,
                jobStatusFilter
            );

            setClientJobs(Array.isArray(result) ? result : []);
        } catch (err) {
            setError(err.message || "Failed to load client print jobs.");
        } finally {
            setJobsLoading(false);
        }
    }

    const selectedClientID = selectedClient ? getClientId(selectedClient) : "";

    return (
        <div className="page-stack">
            <section className="hero-panel compact-hero">
                <div>
                    <span className="eyebrow">Clients</span>
                    <h1>Clients Control</h1>
                    <p>
                        View clients from the database and check their print job
                        history.
                    </p>
                </div>
            </section>

            {error && <div className="alert alert-danger">{error}</div>}

            <section className="panel-card compact-panel">
                <div className="clients-top-row">
                    <div>
                        <span className="eyebrow">Client list</span>
                        <h5>Clients</h5>
                        <p>Select one client to view its print jobs.</p>
                    </div>

                    <div className="clients-search-box">
                        <input
                            className="form-control"
                            value={search}
                            onChange={(event) => setSearch(event.target.value)}
                            placeholder="Search by client name or NIPT..."
                        />
                    </div>
                </div>

                {loading ? (
                    <div className="small text-muted">Loading clients...</div>
                ) : filteredClients.length === 0 ? (
                    <div className="empty-state compact-empty">
                        <div className="empty-icon">□</div>
                        <h5>No clients found</h5>
                        <p>No clients match your search.</p>
                    </div>
                ) : (
                    <div className="client-square-grid">
                        {filteredClients.map((client) => {
                            const clientID = getClientId(client);
                            const name = getClientName(client);
                            const nipt = getClientNipt(client);
                            const isSelected = selectedClientID === clientID;

                            return (
                                <button
                                    type="button"
                                    key={clientID}
                                    className={`client-square-card ${isSelected ? "selected" : ""
                                        }`}
                                    onClick={() => selectClient(client)}
                                >
                                    <div className="client-square-avatar">
                                        {name.slice(0, 2).toUpperCase()}
                                    </div>

                                    <div className="client-square-content">
                                        <strong>{name}</strong>

                                        {nipt ? (
                                            <span>{nipt}</span>
                                        ) : (
                                            <span>No NIPT</span>
                                        )}

                                        <small>{clientID}</small>
                                    </div>
                                </button>
                            );
                        })}
                    </div>
                )}
            </section>

            {selectedClient && (
                <section className="panel-card compact-panel">
                    <div className="panel-header">
                        <div>
                            <span className="eyebrow">Client print history</span>
                            <h5>{getClientName(selectedClient)}</h5>
                            <p>
                                See print jobs created for this client from the
                                database.
                            </p>
                        </div>

                        <select
                            className="form-select jobs-status-filter"
                            value={jobStatusFilter}
                            onChange={(event) =>
                                setJobStatusFilter(event.target.value)
                            }
                        >
                            {STATUS_FILTERS.map((item) => (
                                <option key={item.value} value={item.value}>
                                    {item.label}
                                </option>
                            ))}
                        </select>
                    </div>

                    {jobsLoading ? (
                        <div className="small text-muted">
                            Loading client print jobs...
                        </div>
                    ) : clientJobs.length === 0 ? (
                        <div className="empty-state compact-empty">
                            <div className="empty-icon">□</div>
                            <h5>No print jobs found</h5>
                            <p>No jobs match the selected status.</p>
                        </div>
                    ) : (
                        <div className="table-responsive">
                            <table className="table modern-table align-middle">
                                <thead>
                                    <tr>
                                        <th>Job</th>
                                        <th>Status</th>
                                        <th>Created</th>
                                        <th>Completed</th>
                                        <th>Requested</th>
                                        <th>Printed</th>
                                        <th>Groups</th>
                                        <th>Paper</th>
                                        <th>Created by</th>
                                    </tr>
                                </thead>

                                <tbody>
                                    {clientJobs.map((job) => {
                                        const jobID = getValue(
                                            job,
                                            "printJobID",
                                            "PrintJobID"
                                        );

                                        const status = getValue(
                                            job,
                                            "status",
                                            "Status"
                                        );

                                        const createdAt = getValue(
                                            job,
                                            "createdAt",
                                            "CreatedAt"
                                        );

                                        const completedAt = getValue(
                                            job,
                                            "completedAt",
                                            "CompletedAt"
                                        );

                                        const totalRequested =
                                            getValue(
                                                job,
                                                "totalRequested",
                                                "TotalRequested",
                                                0
                                            ) ?? 0;

                                        const totalPrinted =
                                            getValue(
                                                job,
                                                "totalPrinted",
                                                "TotalPrinted",
                                                0
                                            ) ?? 0;

                                        const groupsCount =
                                            getValue(
                                                job,
                                                "groupsCount",
                                                "GroupsCount",
                                                0
                                            ) ?? 0;

                                        const paperSizes =
                                            getValue(
                                                job,
                                                "paperSizes",
                                                "PaperSizes",
                                                []
                                            ) || [];

                                        const createdBy = getValue(
                                            job,
                                            "createdBy",
                                            "CreatedBy",
                                            "-"
                                        );

                                        return (
                                            <tr key={jobID}>
                                                <td>
                                                    <strong>{jobID}</strong>
                                                </td>

                                                <td>
                                                    <span
                                                        className={`job-badge ${getJobBadgeClass(
                                                            status
                                                        )}`}
                                                    >
                                                        {getJobDisplayStatus(status)}
                                                    </span>
                                                </td>

                                                <td>{formatDate(createdAt)}</td>
                                                <td>{formatDate(completedAt)}</td>
                                                <td>{totalRequested}</td>
                                                <td>{totalPrinted}</td>
                                                <td>{groupsCount}</td>

                                                <td>
                                                    {paperSizes.length > 0
                                                        ? paperSizes.join(", ")
                                                        : "-"}
                                                </td>

                                                <td>{createdBy || "-"}</td>
                                            </tr>
                                        );
                                    })}
                                </tbody>
                            </table>
                        </div>
                    )}
                </section>
            )}
        </div>
    );
}