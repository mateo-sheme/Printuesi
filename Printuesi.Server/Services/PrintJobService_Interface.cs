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
        /// Step 3: Start printing a paper size group.
        /// - Checks if paper change needed → if yes, returns NeedsPaperChange = true, caller waits
        /// - For Mini/Rrethi: sends 1 test copy, returns NeedsTestConfirm = true
        /// - For Normale/Prodhim: prints all copies directly
        /// </summary>
        Task<PrintGroupResult> StartGroupAsync(string printJobID, PaperSize paperSize);

        /// <summary>
        /// Step 4a: User confirmed paper has been changed → send driver + continue
        /// </summary>
        Task<PrintGroupResult> ConfirmPaperChangeAsync(string printJobID, PaperSize paperSize);

        /// <summary>
        /// Step 4b: User confirmed test print is ok → print remaining copies
        /// </summary>
        Task<PrintGroupResult> ConfirmTestPrintAsync(string printJobID, PaperSize paperSize);

        /// <summary>
        /// Step 4c: User said test print is NOT ok → reprint 1 test copy
        /// </summary>
        Task<PrintGroupResult> ReprintTestAsync(string printJobID, PaperSize paperSize);

        /// <summary>
        /// Get all print jobs, optionally filtered by status
        /// </summary>
        Task<List<PrintJobResponse>> GetAllPrintJobsAsync(Status? status = null);

        /// <summary>
        /// Cancel a print job
        /// </summary>
        Task<bool> CancelPrintJobAsync(string printJobID);

        Task<PrintJobResponse> CreateManualJobAsync(CreateManualJobRequest request, string createdByUserID);
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

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
        public List<PrintGroupResponse> Groups { get; set; } = new();
        public List<string> UnrecognizedProducts { get; set; } = new();
    }

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
        public string? LabelFilePath { get; set; }
        public bool LabelFileFound { get; set; }
    }

    public class PrintGroupResult
    {
        public bool Success { get; set; }
        public PaperSize PaperSize { get; set; }
        public int ItemsPrinted { get; set; }
        public int ItemsFailed { get; set; }
        public List<string> Errors { get; set; } = new();
        public bool JobCompleted { get; set; }
        public PaperSize? NextPaperSize { get; set; }

        // State flags — frontend reacts to these
        public bool NeedsPaperChange { get; set; }     // user must change paper
        public bool NeedsTestConfirm { get; set; }     // user must confirm test print
        public string? Message { get; set; }            // human readable status message

    }

    public class CreateManualJobRequest
    {
        public string ClientID { get; set; } = string.Empty;
        public List<ManualJobItem> Items { get; set; } = new();
        public DateTime? ProductionDate { get; set; } // defaults to today if null
    }

    public class ManualJobItem
    {
        public string TemplateID { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    // New interface methods to add:
    // Task<List<ManualLabelItem>> GetAvailableLabelsAsync(string clientID);
    // Task<PrintJobResponse> CreateManualJobAsync(CreateManualJobRequest request, string createdByUserID);


}