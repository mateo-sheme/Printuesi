using Printuesi.Server.Enums;

namespace Printuesi.Server.Services
{
    public interface PrintJobService_Interface
    {
        /// <summary>
        /// Step 1: Upload order PDF → scan → create print job with all items
        /// </summary>
        Task<PrintJobResponse> CreateFromOrderAsync(IFormFile orderPdf, string createdByUserID);

        /// <summary>
        /// Step 2: Get full print job with items grouped by paper size
        /// </summary>
        Task<PrintJobResponse?> GetPrintJobAsync(string printJobID);

        /// <summary>
        /// Step 3: User confirms paper is loaded for a given paper size group → triggers print
        /// </summary>
        Task<PrintGroupResult> ConfirmAndPrintGroupAsync(string printJobID, PaperSize paperSize);

        /// <summary>
        /// Get all print jobs, optionally filtered by status
        /// </summary>
        Task<List<PrintJobResponse>> GetAllPrintJobsAsync(Status? status = null);

        /// <summary>
        /// Cancel a print job
        /// </summary>
        Task<bool> CancelPrintJobAsync(string printJobID);
    }

    // ── Request / Response DTOs ───────────────────────────────────────────────

    public class PrintJobResponse
    {
        public string PrintJobID { get; set; } = string.Empty;
        public string ClientID { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public Status Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Notes { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public List<PrintGroupResponse> Groups { get; set; } = new(); // grouped by paper size
        public List<string> UnrecognizedProducts { get; set; } = new();
    }

    /// <summary>
    /// Items grouped by paper size — each group is one physical paper load
    /// </summary>
    public class PrintGroupResponse
    {
        public PaperSize PaperSize { get; set; }
        public List<PrintJobItemResponse> Items { get; set; } = new();
        public bool IsCompleted { get; set; }
    }

    public class PrintJobItemResponse
    {
        public string PrintJobObjectID { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public PaperSize PaperSize { get; set; }
        public int QuantityRequested { get; set; }
        public int QuantityPrinted { get; set; }
        public string ExpiryDate { get; set; } = string.Empty;
        public string LotNumber { get; set; } = string.Empty;
        public Status Status { get; set; }
        public string? LabelFilePath { get; set; }  // resolved path to the label PDF
        public bool LabelFileFound { get; set; }
    }

    /// <summary>
    /// Result returned after confirming paper and triggering a print group
    /// </summary>
    public class PrintGroupResult
    {
        public bool Success { get; set; }
        public PaperSize PaperSize { get; set; }
        public int ItemsPrinted { get; set; }
        public int ItemsFailed { get; set; }
        public List<string> Errors { get; set; } = new();
        public bool JobCompleted { get; set; }  // true if this was the last group
        public PaperSize? NextPaperSize { get; set; } // next group to load, if any
    }
}