export const PAPER_SIZE = {
  1: "Normale",
  2: "Mini",
  3: "Rrethi",
  4: "Prodhim",
  Normale: "Normale",
  Mini: "Mini",
  Rrethi: "Rrethi",
  Prodhim: "Prodhim"
};

export const STATUS = {
  1: "Completed",
  2: "In_progress",
  3: "Pending",
  4: "Cancelled",
  Completed: "Completed",
  In_progress: "In_progress",
  Pending: "Pending",
  Cancelled: "Cancelled"
};

export function normalizeStatus(value) {
  return STATUS[value] || value || "Unknown";
}

export function paperSizeLabel(value) {
  return PAPER_SIZE[value] || value || "Unknown";
}

export function statusVariant(value) {
  const status = normalizeStatus(value);

  switch (status) {
    case "Completed":
      return "success";
    case "In_progress":
      return "primary";
    case "Pending":
      return "warning";
    case "Cancelled":
      return "danger";
    default:
      return "secondary";
  }
}

export function formatDateTime(value) {
  if (!value) return "-";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "-";

  return date.toLocaleString();
}