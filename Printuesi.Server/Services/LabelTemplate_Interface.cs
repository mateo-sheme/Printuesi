
using Printuesi.Server.Enums;

namespace Printuesi.Server.Services
{
    public interface LabelTemplate_Interface
    {
        Task<TemplateResponse> CreateTemplateAsync(CreateTemplateRequest request);
        Task<TemplateResponse?> GetTemplateAsync(string TemplateID);
        Task<List<TemplateResponse>> GetTemplatesByClientAsync(string ClientID);
        Task<List<TemplateResponse>> GetTemplatesByPaperSizeAsync(PaperSize size);
        Task<TemplateResponse?> UpdateTemplateAsync(string TemplateID, UpdateTemplateRequest request);
        Task<bool> DeleteTemplateAsync(string TemplateID);
        Task<List<string>> ValidatePlaceholdersAsync(string PdfPath);
        Task<string> SavePdfFileAsync(string templateId, IFormFile pdfFile);
    }

    public class CreateTemplateRequest
    {
        public string ClientID { get; set; } = string.Empty;
        public string LabelName { get; set; } = string.Empty;
        public PaperSize PaperSize { get; set; }
        public string DateFormat { get; set; } = string.Empty;
        public int ExpiryOffsetMonths { get; set; }
    }

    public class UpdateTemplateRequest
    {
        public string? LabelName { get; set; }
        public string? DateFormat { get; set; }
        public int? ExpiryOffsetMonths { get; set; }
    }

    public class TemplateResponse
    {
        public string TemplateID { get; set; } = string.Empty;
        public string ClientID { get; set; } = string.Empty;
        public string LabelName { get; set; } = string.Empty;
        public PaperSize PaperSize { get; set; }
        public string PdfPath { get; set; } = string.Empty;
        public string DateFormat { get; set; } = string.Empty;
        public int ExpiryOffsetMonths { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
