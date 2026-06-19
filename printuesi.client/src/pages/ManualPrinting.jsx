import { useEffect, useMemo, useState } from "react";
import { clientsApi } from "../api/clientsApi";
import { labelsApi } from "../api/labelsApi";
import { useAuth } from "../contexts/auth/useAuth";
import { useLanguage } from "../contexts/language/useLanguage";
import { paperSizeLabel } from "../utils/constants";

const PAPER_OPTIONS = ["Normale", "Mini", "Prodhim", "Rrethi"];

const DEFAULT_KG_CLIENTS = ["RHG"];
const DEFAULT_KG_OPTIONS = ["0.5 kg", "1 kg", "2 kg"];

const LAST_DRIVER_STORAGE_KEY = "printuesi.manual.lastDriver";

function getValue(object, camelKey, pascalKey, fallback = "") {
    return object?.[camelKey] ?? object?.[pascalKey] ?? fallback;
}

function normalizeText(value) {
    return String(value || "")
        .trim()
        .toLowerCase();
}

function getInitials(name) {
    return (
        String(name || "")
            .split(" ")
            .filter(Boolean)
            .slice(0, 2)
            .map((part) => part[0]?.toUpperCase())
            .join("") || "C"
    );
}

function clientRequiresKg(client, variableKgClients) {
    const clientName = getValue(client, "name", "Name");

    return variableKgClients.some(
        (name) => normalizeText(name) === normalizeText(clientName)
    );
}

function readStoredLastDriver() {
    try {
        const raw = localStorage.getItem(LAST_DRIVER_STORAGE_KEY);
        return raw ? JSON.parse(raw) : null;
    } catch {
        return null;
    }
}

function writeStoredLastDriver(driver) {
    try {
        localStorage.setItem(LAST_DRIVER_STORAGE_KEY, JSON.stringify(driver));
    } catch {
        // localStorage can fail in private mode; ignore safely.
    }
}

function formatDateTime(value) {
    if (!value) return "-";

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "-";

    return date.toLocaleString();
}

function createBatchItemId() {
    if (window.crypto?.randomUUID) {
        return window.crypto.randomUUID();
    }

    return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

export default function ManualPrinting() {
    const { t } = useLanguage();
    const { isAdmin, user } = useAuth();

    const canUseAdminTools = Boolean(
        isAdmin ||
        user?.role === "Admin" ||
        user?.Role === "Admin" ||
        user?.role === "admin" ||
        user?.Role === "admin"
    );

    const [testResult, setTestResult] = useState(null);

    const [clients, setClients] = useState([]);
    const [labels, setLabels] = useState([]);

    const [selectedClient, setSelectedClient] = useState(null);
    const [selectedLabel, setSelectedLabel] = useState(null);

    const [paperSize, setPaperSize] = useState("Normale");
    const [copies, setCopies] = useState(1);

    const [clientSearch, setClientSearch] = useState("");
    const [labelSearch, setLabelSearch] = useState("");

    const [variableKgClients, setVariableKgClients] = useState(DEFAULT_KG_CLIENTS);
    const [kgOptions, setKgOptions] = useState(DEFAULT_KG_OPTIONS);
    const [selectedKg, setSelectedKg] = useState("1 kg");

    const [lastDriver, setLastDriver] = useState(() => readStoredLastDriver());
    const [driverConfirmed, setDriverConfirmed] = useState("");

    const [batchItems, setBatchItems] = useState([]);

    const [clientsLoading, setClientsLoading] = useState(true);
    const [labelsLoading, setLabelsLoading] = useState(false);
    const [actionLoading, setActionLoading] = useState(false);

    const [printStage, setPrintStage] = useState("");
    const [message, setMessage] = useState("");
    const [error, setError] = useState("");

    useEffect(() => {
        let cancelled = false;

        async function loadClientsAndSettings() {
            try {
                const settingsPromise =
                    typeof labelsApi.getPrintSettings === "function"
                        ? labelsApi.getPrintSettings()
                        : Promise.resolve(null);

                const [clientsResult, settingsResult] = await Promise.allSettled([
                    clientsApi.getAll(),
                    settingsPromise
                ]);

                if (cancelled) return;

                if (clientsResult.status === "fulfilled") {
                    setClients(Array.isArray(clientsResult.value) ? clientsResult.value : []);
                    setError("");
                } else {
                    setError(clientsResult.reason?.message || "Failed to load clients.");
                }

                if (settingsResult.status === "fulfilled" && settingsResult.value) {
                    const fetchedKgClients =
                        settingsResult.value.variableKgClients ||
                        settingsResult.value.VariableKgClients ||
                        DEFAULT_KG_CLIENTS;

                    const fetchedKgOptions =
                        settingsResult.value.kgOptions ||
                        settingsResult.value.KgOptions ||
                        DEFAULT_KG_OPTIONS;

                    const safeKgClients = Array.isArray(fetchedKgClients)
                        ? fetchedKgClients
                        : DEFAULT_KG_CLIENTS;

                    const safeKgOptions =
                        Array.isArray(fetchedKgOptions) && fetchedKgOptions.length > 0
                            ? fetchedKgOptions
                            : DEFAULT_KG_OPTIONS;

                    setVariableKgClients(safeKgClients);
                    setKgOptions(safeKgOptions);
                    setSelectedKg(safeKgOptions[0]);
                }
            } catch (err) {
                if (cancelled) return;
                setError(err.message || "Failed to load manual printing data.");
            } finally {
                if (cancelled) return;
                setClientsLoading(false);
            }
        }

        loadClientsAndSettings();

        return () => {
            cancelled = true;
        };
    }, []);

    const selectedClientRequiresKg = Boolean(
        selectedClient && clientRequiresKg(selectedClient, variableKgClients)
    );

    const selectedLabelFileName = selectedLabel
        ? getValue(selectedLabel, "fileName", "FileName")
        : "";

    const selectedLabelClient = selectedLabel
        ? getValue(selectedLabel, "clientFolder", "ClientFolder")
        : "";

    const selectedLabelRelativePath = selectedLabel
        ? getValue(selectedLabel, "relativePath", "RelativePath")
        : "";

    const lastDriverMatchesPaper =
        lastDriver &&
        normalizeText(lastDriver.paperSize) === normalizeText(paperSize);

    const canPrintSelectedLabel = Boolean(
        selectedClient &&
        selectedLabel &&
        driverConfirmed === "yes" &&
        !actionLoading
    );

    const batchItemsForCurrentPaper = useMemo(() => {
        return batchItems.filter(
            (item) => normalizeText(item.paperSize) === normalizeText(paperSize)
        );
    }, [batchItems, paperSize]);

    const batchItemsForOtherPaper = useMemo(() => {
        return batchItems.filter(
            (item) => normalizeText(item.paperSize) !== normalizeText(paperSize)
        );
    }, [batchItems, paperSize]);

    const canPrintBatch = Boolean(
        selectedClient &&
        batchItemsForCurrentPaper.length > 0 &&
        driverConfirmed === "yes" &&
        !actionLoading
    );

    const clientCards = useMemo(() => {
        const search = normalizeText(clientSearch);

        return clients
            .map((client) => {
                const clientID = getValue(client, "clientID", "ClientID");
                const name = getValue(client, "name", "Name");
                const nipt = getValue(client, "nipt", "NIPT");

                return {
                    id: clientID,
                    name,
                    nipt
                };
            })
            .filter((client) => client.name)
            .filter((client) => {
                if (!search) return true;

                return (
                    normalizeText(client.name).includes(search) ||
                    normalizeText(client.nipt).includes(search)
                );
            })
            .sort((a, b) => a.name.localeCompare(b.name));
    }, [clients, clientSearch]);

    const labelsForSelectedClient = useMemo(() => {
        const search = normalizeText(labelSearch);

        return labels
            .filter((label) => {
                const fileName = getValue(label, "fileName", "FileName");
                const productCode = getValue(label, "productCode", "ProductCode");

                return (
                    !search ||
                    normalizeText(fileName).includes(search) ||
                    normalizeText(productCode).includes(search)
                );
            })
            .sort((a, b) => {
                const aName = getValue(a, "fileName", "FileName");
                const bName = getValue(b, "fileName", "FileName");
                return aName.localeCompare(bName);
            });
    }, [labels, labelSearch]);

    const resetDriverConfirmation = () => {
        setDriverConfirmed("");
    };

    const handlePaperSizeChange = (event) => {
        setPaperSize(event.target.value);
        resetDriverConfirmation();
        setMessage("");
        setError("");
    };

    const selectClient = async (client) => {
        setSelectedClient(client);
        setSelectedLabel(null);
        setLabels([]);
        setBatchItems([]);
        setLabelSearch("");
        setMessage("");
        setError("");
        setLabelsLoading(true);
        resetDriverConfirmation();
        setTestResult(null);

        if (clientRequiresKg(client, variableKgClients) && kgOptions.length > 0) {
            setSelectedKg(kgOptions[0]);
        }

        try {
            const result = await labelsApi.getLabels({
                clientFolder: client.name
            });

            setLabels(Array.isArray(result) ? result : []);
        } catch (err) {
            setError(err.message || `Failed to load labels for ${client.name}.`);
        } finally {
            setLabelsLoading(false);
        }
    };

    const selectLabel = (label) => {
        setSelectedLabel(label);
        setMessage("");
        setError("");
        setTestResult(null);

        const detectedPaper =
            getValue(label, "detectedPaperSize", "DetectedPaperSize") ||
            getValue(label, "paperSize", "PaperSize");

        if (detectedPaper) {
            const nextPaper = paperSizeLabel(detectedPaper);

            if (nextPaper !== paperSize) {
                setPaperSize(nextPaper);
                resetDriverConfirmation();
            }
        }
    };

    const validateSelectedLabel = ({ requireCopies, requireDriver = true }) => {
        if (!selectedClient) {
            setError("Please select a client first.");
            return false;
        }

        if (!selectedLabel) {
            setError("Please select a label first.");
            return false;
        }

        if (selectedClientRequiresKg && !selectedKg) {
            setError("Please select KG first.");
            return false;
        }

        if (requireCopies && (!copies || Number(copies) < 1)) {
            setError("Copies must be at least 1.");
            return false;
        }

        if (requireDriver && driverConfirmed !== "yes") {
            setError("Please confirm that the correct driver is loaded before printing.");
            return false;
        }

        return true;
    };

    const validateBatch = () => {
        if (!selectedClient) {
            setError("Please select a client first.");
            return false;
        }

        if (batchItemsForCurrentPaper.length === 0) {
            setError(`No batch labels for ${paperSize}. Add labels for this paper type first.`);
            return false;
        }

        if (driverConfirmed !== "yes") {
            setError("Please confirm that the correct driver is loaded before printing.");
            return false;
        }

        return true;
    };

    const handleSendDriver = async () => {
        setActionLoading(true);
        setError("");
        setMessage("");
        setPrintStage(`Sending ${paperSize} driver. Please stand by...`);

        try {
            const result = await labelsApi.sendDriver(paperSize);

            const driverFileName =
                result.driverFileName ||
                result.DriverFileName ||
                result.fileName ||
                result.FileName ||
                `${paperSize} driver`;

            const savedDriver = {
                paperSize,
                driverFileName,
                sentAt: new Date().toISOString()
            };

            setLastDriver(savedDriver);
            writeStoredLastDriver(savedDriver);
            setDriverConfirmed("yes");

            setPrintStage("");
            setMessage(result.message || result.Message || `${paperSize} driver sent.`);
        } catch (err) {
            setPrintStage("");
            setDriverConfirmed("no");
            setError(err.message || "Failed to send driver.");
        } finally {
            setActionLoading(false);
        }
    };

    const handleAdminTestLabelPreview = async () => {
        if (!validateSelectedLabel({ requireCopies: false, requireDriver: false })) {
            return;
        }

        setActionLoading(true);
        setError("");
        setMessage("");
        setPrintStage("Generating test label preview. No print will be sent...");
        setTestResult(null);

        // Open blank window immediately so popup blockers do not block it.
        const previewWindow = window.open("", "_blank");

        try {
            const result = await labelsApi.testLabel({
                relativePath: selectedLabelRelativePath,
                paperSize,
                kg: selectedClientRequiresKg ? selectedKg : ""
            });

            setTestResult(result);

            const generatedPath =
                result.generatedRelativePath ||
                result.GeneratedRelativePath;

            if (!generatedPath) {
                throw new Error("Test label was generated, but preview path was missing.");
            }

            const blob = await labelsApi.getTestPreviewBlob(generatedPath);
            const blobUrl = URL.createObjectURL(blob);

            if (previewWindow) {
                previewWindow.location.href = blobUrl;
            } else {
                window.open(blobUrl, "_blank");
            }

            setPrintStage("");
            setMessage(result.message || result.Message || "Test label generated. No print was sent.");

            setTimeout(() => {
                URL.revokeObjectURL(blobUrl);
            }, 60_000);
        } catch (err) {
            if (previewWindow) {
                previewWindow.close();
            }

            const message =
                err?.data?.message ||
                err?.data?.Message ||
                err?.message ||
                "Failed to generate test label.";

            setPrintStage("");
            setError(message);
            window.alert(message);
        } finally {
            setActionLoading(false);
        }
    };

    const handlePrintTestLabel = async () => {
        if (!validateSelectedLabel({ requireCopies: false })) return;

        setActionLoading(true);
        setError("");
        setMessage("");
        setPrintStage("Sending 1 test label...");

        try {
            const result = await labelsApi.printLabel({
                relativePath: selectedLabelRelativePath,
                copies: 1,
                paperSize,
                kg: selectedClientRequiresKg ? selectedKg : ""
            });

            setPrintStage("");
            setMessage(result.message || result.Message || "Test label printed.");
        } catch (err) {
            const message =
                err?.data?.message ||
                err?.data?.Message ||
                err?.message ||
                "Failed to print test label.";

            setPrintStage("");
            setError(message);
            window.alert(message);
        } finally {
            setActionLoading(false);
        }
    };

    const handleFinalPrint = async () => {
        if (!validateSelectedLabel({ requireCopies: true })) return;

        setActionLoading(true);
        setError("");
        setMessage("");
        setPrintStage(`Printing ${copies} label(s)...`);

        try {
            const result = await labelsApi.printLabel({
                relativePath: selectedLabelRelativePath,
                copies: Number(copies),
                paperSize,
                kg: selectedClientRequiresKg ? selectedKg : ""
            });

            setPrintStage("");
            setMessage(result.message || result.Message || "Final print completed.");
        } catch (err) {
            const message =
                err?.data?.message ||
                err?.data?.Message ||
                err?.message ||
                "Final print failed.";

            setPrintStage("");
            setError(message);
            window.alert(message);
        } finally {
            setActionLoading(false);
        }
    };

    const addSelectedLabelToBatch = () => {
        if (!selectedClient) {
            setError("Please select a client first.");
            return;
        }

        if (!selectedLabel) {
            setError("Please select a label first.");
            return;
        }

        if (!copies || Number(copies) < 1) {
            setError("Copies must be at least 1.");
            return;
        }

        if (selectedClientRequiresKg && !selectedKg) {
            setError("Please select KG first.");
            return;
        }

        const batchKg = selectedClientRequiresKg ? selectedKg : "";
        const batchCopies = Number(copies);

        const newItem = {
            id: createBatchItemId(),
            relativePath: selectedLabelRelativePath,
            fileName: selectedLabelFileName,
            clientFolder: selectedLabelClient,
            paperSize,
            copies: batchCopies,
            kg: batchKg
        };

        setBatchItems((current) => {
            const existingIndex = current.findIndex(
                (item) =>
                    item.relativePath === newItem.relativePath &&
                    item.paperSize === newItem.paperSize &&
                    item.kg === newItem.kg
            );

            if (existingIndex === -1) {
                return [...current, newItem];
            }

            return current.map((item, index) =>
                index === existingIndex
                    ? {
                        ...item,
                        copies: item.copies + batchCopies
                    }
                    : item
            );
        });

        setMessage(`${selectedLabelFileName} added to batch list.`);
        setError("");
    };

    const updateBatchItemCopies = (itemId, nextCopies) => {
        const safeCopies = Math.max(1, Number(nextCopies) || 1);

        setBatchItems((current) =>
            current.map((item) =>
                item.id === itemId
                    ? {
                        ...item,
                        copies: safeCopies
                    }
                    : item
            )
        );
    };

    const removeBatchItem = (itemId) => {
        setBatchItems((current) => current.filter((item) => item.id !== itemId));
    };

    const clearBatch = () => {
        setBatchItems([]);
        setMessage("Batch list cleared.");
        setError("");
    };

    const handlePrintBatchCurrentPaper = async () => {
        if (!validateBatch()) return;

        setActionLoading(true);
        setError("");
        setMessage("");
        setPrintStage(`Printing ${batchItemsForCurrentPaper.length} batch item(s) for ${paperSize}...`);

        const printedIds = [];

        try {
            for (const item of batchItemsForCurrentPaper) {
                setPrintStage(`Printing ${item.fileName} × ${item.copies}...`);

                const result = await labelsApi.printLabel({
                    relativePath: item.relativePath,
                    copies: Number(item.copies),
                    paperSize: item.paperSize,
                    kg: item.kg || ""
                });

                const success = getValue(result, "success", "Success", true);

                if (success === false) {
                    throw new Error(
                        getValue(result, "message", "Message", `Failed to print ${item.fileName}`)
                    );
                }

                printedIds.push(item.id);
            }

            setBatchItems((current) =>
                current.filter((item) => !printedIds.includes(item.id))
            );

            setPrintStage("");
            setMessage(`Batch printed for ${paperSize}. Printed ${printedIds.length} item(s).`);
        } catch (err) {
            setBatchItems((current) =>
                current.filter((item) => !printedIds.includes(item.id))
            );

            const message =
                printedIds.length > 0
                    ? `${printedIds.length} item(s) printed, then batch stopped: ${err.message}`
                    : err.message || "Batch print failed.";

            setPrintStage("");
            setError(message);
            window.alert(message);
        } finally {
            setActionLoading(false);
        }
    };

    return (
        <div className="page-stack manual-page-compact">
            <section className="hero-panel compact-hero">
                <div>
                    <span className="eyebrow">{t("manualPrinting")}</span>
                    <h1>Manual Label Printing</h1>
                    <p>
                        Select a client, choose one PDF label, confirm the driver, print one label,
                        or build a manual batch list.
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

                {clientsLoading ? (
                    <div className="small text-muted">{t("loading")}</div>
                ) : clientCards.length === 0 ? (
                    <div className="empty-state compact-empty">
                        <div className="empty-icon">□</div>
                        <h5>No clients found</h5>
                        <p>Create clients first or check the search value.</p>
                    </div>
                ) : (
                    <div className="client-square-grid">
                        {clientCards.map((client) => {
                            const isSelected =
                                selectedClient &&
                                normalizeText(selectedClient.name) === normalizeText(client.name);

                            const requiresKg = clientRequiresKg(client, variableKgClients);

                            return (
                                <button
                                    type="button"
                                    key={client.id || client.name}
                                    className={`client-square-card ${isSelected ? "selected" : ""}`}
                                    onClick={() => selectClient(client)}
                                >
                                    <div className="client-square-avatar">
                                        {getInitials(client.name)}
                                    </div>

                                    <div className="client-square-content">
                                        <strong>{client.name}</strong>

                                        {client.nipt ? (
                                            <small>{client.nipt}</small>
                                        ) : (
                                            <span>Hotel/customer</span>
                                        )}

                                        {requiresKg && <small>KG required</small>}
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
                            <h5>Labels</h5>
                            <p>
                                {selectedClient
                                    ? `${labelsForSelectedClient.length} PDF labels found for ${selectedClient.name}.`
                                    : "Select a client to load labels."}
                            </p>
                        </div>

                        <div className="manual-search-box">
                            <input
                                className="form-control"
                                placeholder="Search label..."
                                value={labelSearch}
                                onChange={(event) => setLabelSearch(event.target.value)}
                                disabled={!selectedClient || labelsLoading}
                            />
                        </div>
                    </div>

                    {!selectedClient ? (
                        <div className="empty-state compact-empty">
                            <div className="empty-icon">□</div>
                            <h5>No client selected</h5>
                            <p>Select a hotel/customer first. Labels will load after selection.</p>
                        </div>
                    ) : labelsLoading ? (
                        <div className="small text-muted">{t("loading")}</div>
                    ) : labelsForSelectedClient.length === 0 ? (
                        <div className="empty-state compact-empty">
                            <div className="empty-icon">□</div>
                            <h5>No labels found</h5>
                            <p>Check that the client folder name matches the client name.</p>
                        </div>
                    ) : (
                        <div className="label-grid compact-label-grid">
                            {labelsForSelectedClient.map((label) => {
                                const relativePath = getValue(label, "relativePath", "RelativePath");
                                const fileName = getValue(label, "fileName", "FileName");

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
                            <h5>Print Control</h5>
                            <p>Single print or build a manual batch list.</p>
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
                                {selectedLabelClient && <small>{selectedLabelClient}</small>}
                            </div>

                            <div
                                className={`last-driver-card ${lastDriverMatchesPaper ? "driver-ok" : "driver-warning"
                                    }`}
                            >
                                <div className="last-driver-main">
                                    <span className="eyebrow">Last driver sent : </span>

                                    {lastDriver ? (
                                        <strong>{lastDriver.paperSize}</strong>
                                    ) : (
                                        <strong>No driver sent</strong>
                                    )}
                                </div>

                                {lastDriver ? (
                                    <div className="last-driver-date">
                                        {formatDateTime(lastDriver.sentAt)}
                                    </div>
                                ) : (
                                    <div className="last-driver-date">
                                        You can continue if the printer already has the correct driver.
                                    </div>
                                )}

                                <div className="driver-confirm-box mt-3">
                                    <label className="form-label mb-2">
                                        Is the correct driver loaded for <strong>{paperSize}</strong>?
                                    </label>

                                    <div className="driver-confirm-actions">
                                        <button
                                            type="button"
                                            className={`btn ${driverConfirmed === "yes"
                                                    ? "btn-success"
                                                    : "btn-outline-success"
                                                }`}
                                            onClick={() => setDriverConfirmed("yes")}
                                        >
                                            Yes, continue
                                        </button>

                                        <button
                                            type="button"
                                            className={`btn ${driverConfirmed === "no"
                                                    ? "btn-warning"
                                                    : "btn-outline-warning"
                                                }`}
                                            onClick={() => setDriverConfirmed("no")}
                                        >
                                            No, send driver
                                        </button>
                                    </div>
                                </div>

                                {driverConfirmed === "no" && (
                                    <button
                                        className="btn btn-primary-gradient w-100 mt-3"
                                        disabled={actionLoading}
                                        onClick={handleSendDriver}
                                    >
                                        {actionLoading ? "Sending driver..." : `Send ${paperSize} Driver`}
                                    </button>
                                )}
                            </div>

                            <div className="mb-3">
                                <label className="form-label">Paper type</label>
                                <select
                                    className="form-select"
                                    value={paperSize}
                                    onChange={handlePaperSizeChange}
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

                            {selectedClientRequiresKg && (
                                <div className="mb-3 kg-selector-box">
                                    <label className="form-label">KG</label>

                                    <select
                                        className="form-select"
                                        value={selectedKg}
                                        onChange={(event) => setSelectedKg(event.target.value)}
                                    >
                                        {kgOptions.map((option) => (
                                            <option key={option} value={option}>
                                                {option}
                                            </option>
                                        ))}
                                    </select>

                                    <small className="text-muted">
                                        This value replaces {"{{KG}}"} on the PDF label.
                                    </small>
                                </div>
                            )}

                            <div className="manual-print-actions">
                                {canUseAdminTools && (
                                    <button
                                        type="button"
                                        className="btn btn-outline-info w-100"
                                        disabled={actionLoading || !selectedLabel}
                                        onClick={handleAdminTestLabelPreview}
                                    >
                                        Test Label Preview - No Print
                                    </button>
                                )}

                                <button
                                    className="btn btn-success w-100"
                                    disabled={!canPrintSelectedLabel}
                                    onClick={handlePrintTestLabel}
                                >
                                    Print Test Label
                                </button>

                                <button
                                    className="btn btn-warning w-100"
                                    disabled={!canPrintSelectedLabel}
                                    onClick={handleFinalPrint}
                                >
                                    Final Print Selected Label
                                </button>

                                <button
                                    className="btn btn-outline-primary w-100"
                                    disabled={actionLoading}
                                    onClick={addSelectedLabelToBatch}
                                >
                                    Add Selected Label to Batch
                                </button>
                            </div>

                            {canUseAdminTools && testResult && (
                                <div className="alert alert-info mt-3">
                                    <strong>Test label generated.</strong>

                                    <div className="small mt-2">
                                        Expiry:{" "}
                                        <strong>
                                            {testResult.expiryDate || testResult.ExpiryDate}
                                        </strong>
                                        {" | "}
                                        LOT:{" "}
                                        <strong>
                                            {testResult.lotNumber || testResult.LotNumber}
                                        </strong>
                                        {testResult.kg || testResult.Kg ? (
                                            <>
                                                {" | "}
                                                KG:{" "}
                                                <strong>
                                                    {testResult.kg || testResult.Kg}
                                                </strong>
                                            </>
                                        ) : null}
                                    </div>

                                    {(testResult.generatedRelativePath ||
                                        testResult.GeneratedRelativePath) && (
                                            <a
                                                className="btn btn-sm btn-outline-info mt-2"
                                                href={labelsApi.getTestPreviewUrl(
                                                    testResult.generatedRelativePath ||
                                                    testResult.GeneratedRelativePath
                                                )}
                                                target="_blank"
                                                rel="noreferrer"
                                            >
                                                Open generated PDF
                                            </a>
                                        )}
                                </div>
                            )}

                            <div className="manual-warning mt-3">
                                Test print sends 1 label. Final print sends the selected number of copies.
                                Batch print prints only items for the currently selected paper type.
                            </div>
                        </>
                    )}

                    <div className="batch-panel mt-4">
                        <div className="d-flex justify-content-between align-items-start gap-2 mb-3">
                            <div>
                                <span className="eyebrow">Batch list</span>
                                <h6>Manual Batch Printing</h6>
                                <p className="mb-0">
                                    {batchItems.length} total item(s).{" "}
                                    {batchItemsForCurrentPaper.length} item(s) ready for {paperSize}.
                                </p>
                            </div>

                            {batchItems.length > 0 && (
                                <button
                                    type="button"
                                    className="btn btn-sm btn-outline-danger"
                                    onClick={clearBatch}
                                    disabled={actionLoading}
                                >
                                    Clear
                                </button>
                            )}
                        </div>

                        {batchItems.length === 0 ? (
                            <div className="empty-state compact-empty">
                                <div className="empty-icon">□</div>
                                <h5>No batch items</h5>
                                <p>Select labels and click “Add Selected Label to Batch”.</p>
                            </div>
                        ) : (
                            <>
                                {batchItemsForOtherPaper.length > 0 && (
                                    <div className="manual-warning mb-3">
                                        {batchItemsForOtherPaper.length} item(s) are for another paper type.
                                        Change paper type later and print them separately.
                                    </div>
                                )}

                                <div className="batch-list">
                                    {batchItems.map((item) => {
                                        const isCurrentPaper =
                                            normalizeText(item.paperSize) === normalizeText(paperSize);

                                        return (
                                            <div
                                                key={item.id}
                                                className={`batch-item-card ${isCurrentPaper ? "current-paper" : "other-paper"
                                                    }`}
                                            >
                                                <div>
                                                    <strong>{item.fileName}</strong>
                                                    <small>
                                                        {item.paperSize}
                                                        {item.kg ? ` · ${item.kg}` : ""}
                                                    </small>
                                                </div>

                                                <div className="batch-item-controls">
                                                    <input
                                                        className="form-control form-control-sm"
                                                        type="number"
                                                        min="1"
                                                        max="500"
                                                        value={item.copies}
                                                        onChange={(event) =>
                                                            updateBatchItemCopies(item.id, event.target.value)
                                                        }
                                                    />

                                                    <button
                                                        type="button"
                                                        className="btn btn-sm btn-outline-danger"
                                                        onClick={() => removeBatchItem(item.id)}
                                                        disabled={actionLoading}
                                                    >
                                                        Remove
                                                    </button>
                                                </div>
                                            </div>
                                        );
                                    })}
                                </div>

                                <button
                                    className="btn btn-primary-gradient w-100 mt-3"
                                    disabled={!canPrintBatch}
                                    onClick={handlePrintBatchCurrentPaper}
                                >
                                    Print Batch for {paperSize}
                                </button>
                            </>
                        )}
                    </div>
                </aside>
            </section>
        </div>
    );
}