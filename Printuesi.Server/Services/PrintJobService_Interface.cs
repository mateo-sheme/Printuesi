using Printuesi.Server.Enums;

namespace Printuesi.Server.Services
{
    public interface PrintJobService_Interface
    {
        /// <summary>
        /// Upload order PDF, scan it, create print job and print job objects.
        /// </summary>
        Task<PrintJobResponse> CreateFromOrderAsync(IFormFile orderPdf, string createdByUserID);

        /// <summary>
        /// Get one print job by ID.
        /// </summary>
        Task<PrintJobResponse?> GetPrintJobAsync(string printJobID);

        /// <summary>
        /// Get all print jobs, optionally filtered by status.
        /// </summary>
        Task<List<PrintJobResponse>> GetAllPrintJobsAsync(Status? status = null);

        /// <summary>
        /// User confirms paper was physically changed.
        /// Backend sends driver/settings and stores current paper state.
        /// </summary>
        Task<PrintGroupResult> ConfirmPaperChangeAsync(string printJobID, PaperSize paperSize);

        /// <summary>
        /// Prints one test label.
        /// Mandatory for Mini/Rrethi before starting group.
        /// Optional for Normale/Prodhim.
        /// </summary>
        Task<PrintGroupResult> PrintTestAsync(string printJobID, PaperSize paperSize, string? kg = null );

        /// <summary>
        /// Reprints one test label.
        /// </summary>
        Task<PrintGroupResult> ReprintTestAsync(string printJobID, PaperSize paperSize, string? kg = null);

        /// <summary>
        /// Starts the real production group.
        /// Mini/Rrethi require a previous successful test.
        /// Normale/Prodhim can start without test.
        /// </summary>
        Task<PrintGroupResult> StartGroupAsync(string printJobID, PaperSize paperSize, string? kg = null);

        /// <summary>
        /// Legacy compatibility endpoint.
        /// Later remove this from controller, service, interface, frontend API.
        /// </summary>
        Task<PrintGroupResult> ConfirmTestPrintAsync(string printJobID, PaperSize paperSize, string? kg = null);

        /// <summary>
        /// Cancel print job.
        /// </summary>
        Task<bool> CancelPrintJobAsync(string printJobID);


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
        public bool RequiresKg { get; set; }
        public List<string> KgOptions { get; set; } = new();
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

        public bool NeedsPaperChange { get; set; }
        public bool NeedsTestConfirm { get; set; }
        public string? Message { get; set; }
    }
}