using Printuesi.Server.Enums;

namespace Printuesi.Server.Services
{
    public interface ManualPrintingService_Interface
    {
        Task<List<ManualLabelResponse>> GetLabelsAsync(string? search = null, string? clientFolder = null);
        Task<string> ResolveSafeLabelPathAsync(string relativePath);
        Task<ManualDriverResponse> SendDriverAsync(ManualSendDriverRequest request);
        Task<ManualPrintResponse> PrintLabelAsync(ManualPrintLabelRequest request);
        Task<ManualPrintResponse> PrintLabelWithDriverAsync(ManualPrintWithDriverRequest request);
        Task<ManualTestLabelResponse> TestLabelAsync(ManualTestLabelRequest request);
        Task<string> ResolveSafeGeneratedOutputPathAsync(string relativePath);
    }

    public class ManualLabelResponse
    {
        public string FileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string ClientFolder { get; set; } = string.Empty;
        public string? DetectedPaperSize { get; set; }
        public string? ProductCode { get; set; }
        public long SizeBytes { get; set; }
        public DateTime LastModified { get; set; }
        public string PreviewUrl { get; set; } = string.Empty;
    }

    public class ManualSendDriverRequest
    {
        public PaperSize PaperSize { get; set; }
    }

    public class ManualPrintLabelRequest
    {
        public string RelativePath { get; set; } = string.Empty;
        public int Copies { get; set; }
        public PaperSize? PaperSize { get; set; }
        public string? Kg { get; set; }
        public string? ExpiryDate { get; set; }
        public string? LotNumber { get; set; }
        public string? DateFormat { get; set; }
        public int? ExpiryOffsetMonths { get; set; }

    }

    public class ManualPrintWithDriverRequest
    {
        public string RelativePath { get; set; } = string.Empty;
        public PaperSize PaperSize { get; set; }
        public int Copies { get; set; }

        public string? Kg { get; set; }
        public string? ExpiryDate { get; set; }
        public string? LotNumber { get; set; }
        public string? DateFormat { get; set; }
        public int? ExpiryOffsetMonths { get; set; }
    }

    public class ManualDriverResponse
    {
        public bool Success { get; set; }
        public PaperSize PaperSize { get; set; }
        public string DriverFileName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
    }

    public class ManualPrintResponse
    {
        public bool Success { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public PaperSize? PaperSize { get; set; }

        public int CopiesRequested { get; set; }
        public int CopiesSent { get; set; }

        public string ExpiryDate { get; set; } = string.Empty;
        public string LotNumber { get; set; } = string.Empty;
        public string Kg { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
    }

    public class ManualTestLabelRequest
    {
        public string RelativePath { get; set; } = string.Empty;
        public PaperSize? PaperSize { get; set; }

        public string? Kg { get; set; }
        public string? ExpiryDate { get; set; }
        public string? LotNumber { get; set; }
        public string? DateFormat { get; set; }
        public int? ExpiryOffsetMonths { get; set; }
    }

    public class ManualTestLabelResponse
    {
        public bool Success { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string TemplateRelativePath { get; set; } = string.Empty;
        public string GeneratedRelativePath { get; set; } = string.Empty;
        public string PreviewUrl { get; set; } = string.Empty;

        public PaperSize? PaperSize { get; set; }
        public string ExpiryDate { get; set; } = string.Empty;
        public string LotNumber { get; set; } = string.Empty;
        public string Kg { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
    }
}