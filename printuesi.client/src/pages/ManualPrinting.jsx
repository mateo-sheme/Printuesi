import { useEffect, useMemo, useState } from "react";
import { clientsApi } from "../api/clientsApi";
import { labelsApi } from "../api/labelsApi";
import { useLanguage } from "../contexts/language/useLanguage";
import { paperSizeLabel } from "../utils/constants";

const PAPER_OPTIONS = ["Normale", "Mini", "Prodhim", "Rrethi"];

function getValue(object, camelKey, pascalKey, fallback = "") {
    return object?.[camelKey] ?? object?.[pascalKey] ?? fallback;
}

function normalizeText(value) {
    return String(value || "")
        .trim()
        .toLowerCase();
}

function getInitials(name) {
    return String(name || "")
        .split(" ")
        .filter(Boolean)
        .slice(0, 2)
        .map((part) => part[0]?.toUpperCase())
        .join("") || "C";
}

export default function ManualPrinting() {
    const { t } = useLanguage();

    const [clients, setClients] = useState([]);
    const [labels, setLabels] = useState([]);

    const [selectedClient, setSelectedClient] = useState(null);
    const [selectedLabel, setSelectedLabel] = useState(null);

    const [paperSize, setPaperSize] = useState("Normale");
    const [copies, setCopies] = useState(1);

    const [clientSearch, setClientSearch] = useState("");
    const [labelSearch, setLabelSearch] = useState("");

    const [loading, setLoading] = useState(true);
    const [printing, setPrinting] = useState(false);

    const [printStage, setPrintStage] = useState("");
    const [message, setMessage] = useState("");
    const [error, setError] = useState("");

    useEffect(() => {
        let cancelled = false;

        async function loadInitialData() {
            try {
                const [clientsResult, labelsResult] = await Promise.all([
                    clientsApi.getAll(),
                    labelsApi.getLabels()
                ]);

                if (cancelled) return;

                setClients(Array.isArray(clientsResult) ? clientsResult : []);
                setLabels(Array.isArray(labelsResult) ? labelsResult : []);
                setError("");
            } catch (err) {
                if (cancelled) return;
                setError(err.message || "Failed to load manual printing data.");
            } finally {
                if (cancelled) return;
                setLoading(false);
            }
        }

        loadInitialData();

        return () => {
            cancelled = true;
        };
    }, []);

    const clientCards = useMemo(() => {
        const search = normalizeText(clientSearch);

        const dbClients = clients
            .map((client) => {
                const clientID = getValue(client, "clientID", "ClientID");
                const name = getValue(client, "name", "Name");
                const nipt = getValue(client, "nipt", "NIPT");

                return {
                    id: clientID,
                    name,
                    nipt,
                    source: "db"
                };
            })
            .filter((client) => client.name);

        const labelFolders = Array.from(
            new Set(
                labels
                    .map((label) => getValue(label, "clientFolder", "ClientFolder"))
                    .filter(Boolean)
            )
        ).map((folder) => ({
            id: `folder-${folder}`,
            name: folder,
            nipt: "",
            source: "folder"
        }));

        const merged = [...dbClients];

        for (const folderClient of labelFolders) {
            const exists = merged.some(
                (client) => normalizeText(client.name) === normalizeText(folderClient.name)
            );

            if (!exists) {
                merged.push(folderClient);
            }
        }

        return merged
            .filter((client) => {
                if (!search) return true;

                return (
                    normalizeText(client.name).includes(search) ||
                    normalizeText(client.nipt).includes(search)
                );
            })
            .sort((a, b) => a.name.localeCompare(b.name));
    }, [clients, labels, clientSearch]);

    const labelsForSelectedClient = useMemo(() => {
        const search = normalizeText(labelSearch);
        const selectedClientName = selectedClient?.name || "";

        return labels
            .filter((label) => {
                const fileName = getValue(label, "fileName", "FileName");
                const clientFolder = getValue(label, "clientFolder", "ClientFolder");
                const productCode = getValue(label, "productCode", "ProductCode");

                const matchesClient =
                    !selectedClientName ||
                    normalizeText(clientFolder) === normalizeText(selectedClientName);

                const matchesSearch =
                    !search ||
                    normalizeText(fileName).includes(search) ||
                    normalizeText(productCode).includes(search) ||
                    normalizeText(clientFolder).includes(search);

                return matchesClient && matchesSearch;
            })
            .sort((a, b) => {
                const aName = getValue(a, "fileName", "FileName");
                const bName = getValue(b, "fileName", "FileName");
                return aName.localeCompare(bName);
            });
    }, [labels, selectedClient, labelSearch]);

    const selectClient = (client) => {
        setSelectedClient(client);
        setSelectedLabel(null);
        setMessage("");
        setError("");
    };

    const selectLabel = (label) => {
        setSelectedLabel(label);
        setMessage("");
        setError("");

        const detectedPaper =
            getValue(label, "detectedPaperSize", "DetectedPaperSize") ||
            getValue(label, "paperSize", "PaperSize");

        if (detectedPaper) {
            setPaperSize(paperSizeLabel(detectedPaper));
        }
    };

    const handleManualPrint = async () => {
        if (!selectedLabel) {
            setError("Please select a label first.");
            return;
        }

        if (!copies || Number(copies) < 1) {
            setError("Copies must be at least 1.");
            return;
        }

        const relativePath = getValue(selectedLabel, "relativePath", "RelativePath");

        setPrinting(true);
        setError("");
        setMessage("");
        setPrintStage(`Sending ${paperSize} driver. Please stand by...`);

        try {
            await labelsApi.sendDriver(paperSize);

            setPrintStage("Driver sent successfully. Sending label to printer...");

            const result = await labelsApi.printLabel({
                relativePath,
                copies: Number(copies)
            });

            setPrintStage("");
            setMessage(result.message || result.Message || "Manual print completed.");
        } catch (err) {
            setPrintStage("");
            setError(err.message || "Manual print failed.");
        } finally {
            setPrinting(false);
        }
    };

    const selectedLabelFileName = selectedLabel
        ? getValue(selectedLabel, "fileName", "FileName")
        : "";

    const selectedLabelClient = selectedLabel
        ? getValue(selectedLabel, "clientFolder", "ClientFolder")
        : "";

    const selectedLabelCode = selectedLabel
        ? getValue(selectedLabel, "productCode", "ProductCode")
        : "";

    return (
        <div className="page-stack manual-page-compact">
            <section className="hero-panel compact-hero">
                <div>
                    <span className="eyebrow">{t("manualPrinting")}</span>
                    <h1>Manual Label Printing</h1>
                    <p>
                        Select a client, choose one PDF label, confirm paper type and
                        copies, then send driver and print.
                    </p>
                </div>
            </section>

            {error && <div className="alert alert-danger">{error}</div>}
            {message && <div className="alert alert-success">{message}</div>}

            {printStage && (
                <div className="alert alert-info d-flex align-items-center gap-3">
                    <div className="spinner-border spinner-border-sm" />
                    <strong>{printStage}</strong>
                </div>
            )}

            <section className="panel-card compact-panel">
                <div className="clients-top-row">
                    <div>
                        <span className="eyebrow">Step 1</span>
                        <h5>Clients</h5>
                        <p>Select the hotel/customer first.</p>
                    </div>

                    <div className="clients-search-box">
                        <input
                            className="form-control"
                            placeholder="Search client..."
                            value={clientSearch}
                            onChange={(event) => setClientSearch(event.target.value)}
                        />
                    </div>
                </div>

    {loading ? (
        <div className="small text-muted">{t("loading")}</div>
    ) : (
        <div className="client-square-grid">
            <button
                type="button"
                className={`client-square-card ${selectedClient === null ? "selected" : ""}`}
                onClick={() => {
                    setSelectedClient(null);
                    setSelectedLabel(null);
                }}
            >
                <div className="client-square-avatar all">ALL</div>

                <div className="client-square-content">
                    <strong>All clients</strong>
                    <span>{labels.length} total labels</span>
                </div>
            </button>

            {clientCards.map((client) => {
                const count = labels.filter(
                    (label) =>
                        normalizeText(getValue(label, "clientFolder", "ClientFolder")) ===
                        normalizeText(client.name)
                ).length;

                const isSelected =
                    selectedClient &&
                    normalizeText(selectedClient.name) === normalizeText(client.name);

                return (
                    <button
                        type="button"
                        key={`${client.source}-${client.id || client.name}`}
                        className={`client-square-card ${isSelected ? "selected" : ""}`}
                        onClick={() => selectClient(client)}
                    >
                        <div className="client-square-avatar">
                            {getInitials(client.name)}
                        </div>

                        <div className="client-square-content">
                            <strong>{client.name}</strong>
                            <span>
                                {count} label{count === 1 ? "" : "s"}
                            </span>

                            {client.nipt && (
                                <small>{client.nipt}</small>
                            )}
                        </div>
                    </button>
                );
            })}
        </div>
    )}
</section>

            <section className="manual-workspace">
                <div className="panel-card compact-panel labels-panel">
                    <div className="manual-section-header">
                        <div>
                            <span className="eyebrow">Step 2</span>
                            <h5>
                                Labels{" "}
                                {selectedClient ? (
                                    <span className="text-muted">/ {selectedClient.name}</span>
                                ) : (
                                    <span className="text-muted">/ All clients</span>
                                )}
                            </h5>
                            <p>{labelsForSelectedClient.length} PDF labels found.</p>
                        </div>

                        <div className="manual-search-box">
                            <input
                                className="form-control"
                                placeholder="Search label or code..."
                                value={labelSearch}
                                onChange={(event) => setLabelSearch(event.target.value)}
                            />
                        </div>
                    </div>

                    {loading ? (
                        <div className="small text-muted">{t("loading")}</div>
                    ) : labelsForSelectedClient.length === 0 ? (
                        <div className="empty-state compact-empty">
                            <div className="empty-icon">□</div>
                            <h5>No labels found</h5>
                            <p>Check the client folder name or label files.</p>
                        </div>
                    ) : (
                        <div className="label-grid compact-label-grid">
                            {labelsForSelectedClient.map((label) => {
                                const relativePath = getValue(label, "relativePath", "RelativePath");
                                const fileName = getValue(label, "fileName", "FileName");
                                const clientFolder = getValue(label, "clientFolder", "ClientFolder");
                                const productCode = getValue(label, "productCode", "ProductCode");
                                const detectedPaper =
                                    getValue(label, "detectedPaperSize", "DetectedPaperSize") ||
                                    getValue(label, "paperSize", "PaperSize");

                                const selectedRelativePath = selectedLabel
                                    ? getValue(selectedLabel, "relativePath", "RelativePath")
                                    : "";

                                const isSelected = selectedRelativePath === relativePath;

                                return (
                                    <button
                                        type="button"
                                        key={relativePath}
                                        className={`label-card compact-label-card ${isSelected ? "selected" : ""
                                            }`}
                                        onClick={() => selectLabel(label)}
                                    >
                                        <div className="pdf-preview-frame compact-pdf-preview">
                                            <iframe
                                                src={labelsApi.getPreviewUrl(relativePath)}
                                                title={fileName}
                                            />
                                        </div>

                                        <div className="label-card-body">
                                            <strong>{fileName}</strong>

                                            <div className="label-card-meta">
                                                <span>
                                                    {detectedPaper
                                                        ? paperSizeLabel(detectedPaper)
                                                        : "Unknown paper"}
                                                </span>
                                            </div>
                                        </div>
                                    </button>
                                );
                            })}
                        </div>
                    )}
                </div>

                <aside className="panel-card compact-panel print-side-panel">
                    <div className="manual-section-header">
                        <div>
                            <span className="eyebrow">Step 3</span>
                            <h5>Print</h5>
                            <p>Confirm paper and quantity.</p>
                        </div>
                    </div>

                    {!selectedLabel ? (
                        <div className="empty-state compact-empty">
                            <div className="empty-icon">□</div>
                            <h5>No label selected</h5>
                            <p>Select one PDF label first.</p>
                        </div>
                    ) : (
                        <>
                            <div className="selected-label-box compact-selected-box">
                                <strong>{selectedLabelFileName}</strong>
                            </div>

                            <div className="mb-3">
                                <label className="form-label">Paper type</label>
                                <select
                                    className="form-select"
                                    value={paperSize}
                                    onChange={(event) => setPaperSize(event.target.value)}
                                >
                                    {PAPER_OPTIONS.map((paper) => (
                                        <option key={paper} value={paper}>
                                            {paper}
                                        </option>
                                    ))}
                                </select>
                            </div>

                            <div className="mb-3">
                                <label className="form-label">Copies</label>
                                <input
                                    className="form-control"
                                    type="number"
                                    min="1"
                                    max="500"
                                    value={copies}
                                    onChange={(event) => setCopies(event.target.value)}
                                />
                            </div>

                            <button
                                className="btn btn-primary-gradient w-100"
                                disabled={printing}
                                onClick={handleManualPrint}
                            >
                                {printing ? "Printing..." : "Send Driver & Print"}
                            </button>

                            <div className="manual-warning mt-3">
                                First the driver is sent, then the selected PDF label is
                                printed.
                            </div>
                        </>
                    )}
                </aside>
            </section>
        </div>
    );
}