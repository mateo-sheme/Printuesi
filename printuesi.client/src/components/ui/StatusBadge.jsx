import { normalizeStatus, statusVariant } from "../../utils/constants";

export default function StatusBadge({ status }) {
    const normalized = normalizeStatus(status);
    const variant = statusVariant(status);

    return (
        <span className={`badge rounded-pill text-bg-${variant}`}>
            {normalized.replace("_", " ")}
        </span>
    );
}