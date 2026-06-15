/* eslint-disable no-unsafe-finally */
import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";

import { printerApi } from "../api/printerApi";
import { printJobsApi } from "../api/printJobsApi";
import { useAuth } from "../contexts/auth/useAuth";
import { useLanguage } from "../contexts/language/useLanguage";
import { useTheme } from "../contexts/theme/useTheme";
import EmptyState from "../components/ui/EmptyState";
import StatCard from "../components/ui/StatCard";
import StatusBadge from "../components/ui/StatusBadge";
import { formatDateTime, normalizeStatus, paperSizeLabel } from "../utils/constants";

export default function Dashboard() {
    const { t } = useLanguage();
    const { isAdmin } = useAuth();

    const [printerOnline, setPrinterOnline] = useState(null);
    const [jobs, setJobs] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");

    const fetchDashboardData = async () => {
        const [printerStatus, printJobs] = await Promise.all([
            printerApi.getStatus(),
            printJobsApi.getAll()
        ]);

        return {
            printerOnline: Boolean(printerStatus.online ?? printerStatus.Online),
            jobs: Array.isArray(printJobs) ? printJobs : []
        };
    };

    const loadDashboard = async () => {
        setLoading(true);
        setError("");

        try {
            const data = await fetchDashboardData();
            setPrinterOnline(data.printerOnline);
            setJobs(data.jobs);
        } catch (err) {
            setError(err.message || "Dashboard failed to load.");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        let cancelled = false;

        async function loadInitialDashboard() {
            try {
                const data = await fetchDashboardData();

                if (cancelled) return;

                setPrinterOnline(data.printerOnline);
                setJobs(data.jobs);
                setError("");
            } catch (err) {
                if (cancelled) return;
                setError(err.message || "Dashboard failed to load.");
            } finally {
                if (cancelled) return;
                setLoading(false);
            }
        }

        loadInitialDashboard();

        return () => {
            cancelled = true;
        };
    }, []);

    const stats = useMemo(() => {
        const pending = jobs.filter((job) => normalizeStatus(job.status) === "Pending").length;
        const inProgress = jobs.filter((job) => normalizeStatus(job.status) === "In_progress").length;
        const completed = jobs.filter((job) => normalizeStatus(job.status) === "Completed").length;

        return {
            pending,
            inProgress,
            completed
        };
    }, [jobs]);

    const recentJobs = jobs.slice(0, 6);

    return (
        <div className="page-stack">
            <section className="hero-panel printer-hero">
                <div>
                    <span className="eyebrow">Epson Label Automation</span>
                    <h1>Epson ColorWorks CW-6000AE</h1>
                    <p>
                        Select automatic printing for order-based jobs, or manual printing
                        for direct private-label PDF printing from the label library.
                    </p>

                    <div className="hero-actions mt-4">
                        <Link to="/automatic-printing" className="btn btn-primary-gradient">
                            {t("startAutomatic")}
                        </Link>
                        <Link to="/manual-printing" className="btn btn-outline-primary">
                            {t("startManual")}
                        </Link>
                    </div>
                </div>

                <div className="printer-hero-image">
                    <img src="/printer-placeholder.png" alt="Printer placeholder" />
                </div>
            </section>

            {error && (
                <div className="alert alert-danger d-flex justify-content-between align-items-center">
                    <span>{error}</span>
                    <button className="btn btn-sm btn-outline-danger" onClick={loadDashboard}>
                        {t("retry")}
                    </button>
                </div>
            )}

            <section className="row g-3">
                <div className="col-12 col-sm-6 col-xl-3">
                    <StatCard
                        title={t("printerStatus")}
                        value={
                            loading
                                ? "..."
                                : printerOnline
                                    ? t("online")
                                    : t("offline")
                        }
                        helper="Epson CW-6000AE"
                        tone={printerOnline ? "success" : "danger"}
                    />
                </div>

                <div className="col-12 col-sm-6 col-xl-3">
                    <StatCard title={t("pendingJobs")} value={stats.pending} tone="warning" />
                </div>

                <div className="col-12 col-sm-6 col-xl-3">
                    <StatCard title={t("inProgressJobs")} value={stats.inProgress} tone="primary" />
                </div>

                <div className="col-12 col-sm-6 col-xl-3">
                    <StatCard title={t("completedJobs")} value={stats.completed} tone="success" />
                </div>
            </section>

            <section className="row g-4">
                <div className="col-12 col-xl-8">
                    <div className="panel-card">
                        <div className="panel-header">
                            <div>
                                <h5>{t("recentJobs")}</h5>
                                <p>Latest automatic print jobs from the backend.</p>
                            </div>

                            <button className="btn btn-soft" onClick={loadDashboard}>
                                {t("retry")}
                            </button>
                        </div>

                        {loading ? (
                            <div className="p-4">{t("loading")}</div>
                        ) : recentJobs.length === 0 ? (
                            <EmptyState title={t("noRecentJobs")} />
                        ) : (
                            <div className="table-responsive">
                                <table className="table align-middle modern-table">
                                    <thead>
                                        <tr>
                                            <th>Job</th>
                                            <th>Client</th>
                                            <th>Status</th>
                                            <th>Groups</th>
                                            <th>Created</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {recentJobs.map((job) => (
                                            <tr key={job.printJobID || job.printJobId || job.PrintJobID}>
                                                <td className="fw-semibold">
                                                    {job.printJobID || job.printJobId || job.PrintJobID}
                                                </td>
                                                <td>{job.clientName || job.ClientName || "-"}</td>
                                                <td>
                                                    <StatusBadge status={job.status || job.Status} />
                                                </td>
                                                <td>
                                                    {(job.groups || job.Groups || [])
                                                        .map((group) => paperSizeLabel(group.paperSize || group.PaperSize))
                                                        .join(", ") || "-"}
                                                </td>
                                                <td>{formatDateTime(job.createdAt || job.CreatedAt)}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        )}
                    </div>
                </div>

                <div className="col-12 col-xl-4">
                    <div className="panel-card">
                        <div className="panel-header">
                            <div>
                                <h5>{t("quickActions")}</h5>
                                <p>Fast access for non-technical users.</p>
                            </div>
                        </div>

                        <div className="quick-action-list">
                            <Link to="/automatic-printing" className="quick-action">
                                <span>⚙</span>
                                <div>
                                    <strong>{t("automaticPrinting")}</strong>
                                    <small>Upload order PDF and follow paper confirmations.</small>
                                </div>
                            </Link>

                            <Link to="/manual-printing" className="quick-action">
                                <span>⎙</span>
                                <div>
                                    <strong>{t("manualPrinting")}</strong>
                                    <small>Send a prepared PDF directly to printer.</small>
                                </div>
                            </Link>

                            {isAdmin && (
                                <Link to="/supplies" className="quick-action">
                                    <span>◈</span>
                                    <div>
                                        <strong>{t("supplies")}</strong>
                                        <small>Supply tracking page will connect when controller exists.</small>
                                    </div>
                                </Link>
                            )}
                        </div>
                    </div>
                </div>
            </section>
        </div>
    );
}