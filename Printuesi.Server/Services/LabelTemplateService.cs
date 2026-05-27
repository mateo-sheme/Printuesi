using Microsoft.EntityFrameworkCore;
using Printuesi.Server.Data;
using Printuesi.Server.Entities;
using Printuesi.Server.Enums;
using Printuesi.Server.Helpers;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace Printuesi.Server.Services
{
    public class LabelTemplateService : LabelTemplate_Interface
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<LabelTemplateService> _logger;
        private readonly string _uploadPath;

        public LabelTemplateService(
            ApplicationDbContext db,
            IWebHostEnvironment environment,
            ILogger<LabelTemplateService> logger)
        {
            _db = db;
            _environment = environment;
            _logger = logger;

            var webRoot = environment?.WebRootPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
            _uploadPath = Path.Combine(webRoot, "uploads", "pdfs");

            // Ensure upload directory exists
            if (!Directory.Exists(_uploadPath))
                Directory.CreateDirectory(_uploadPath);
        }

        public async Task<TemplateResponse> CreateTemplateAsync(CreateTemplateRequest request)
        {
            try
            {
                // Validation
                if (request == null)
                    throw new ArgumentNullException(nameof(request));
                if (string.IsNullOrWhiteSpace(request.ClientID))
                    throw new ArgumentException("ClientID is required.", nameof(request.ClientID));
                if (string.IsNullOrWhiteSpace(request.LabelName))
                    throw new ArgumentException("Label name is required.", nameof(request.LabelName));
                if (string.IsNullOrWhiteSpace(request.DateFormat))
                    throw new ArgumentException("Date format is required.", nameof(request.DateFormat));
                if (request.ExpiryOffsetMonths < 1)
                    throw new ArgumentException("Expiry offset must be at least 1 month.", nameof(request.ExpiryOffsetMonths));

                // Verify client exists
                var clientExists = await _db.Clients.AnyAsync(c => c.ClientID == request.ClientID);
                if (!clientExists)
                    throw new InvalidOperationException($"Client with ID '{request.ClientID}' not found.");

                // Check if template already exists for this client and paper size
                var existingTemplate = await _db.LabelTemplates
                    .FirstOrDefaultAsync(t => t.ClientID == request.ClientID && t.PaperSize == request.PaperSize);

                if (existingTemplate != null)
                    throw new InvalidOperationException(
                        $"A template for this client and paper size '{request.PaperSize}' already exists.");

                // Create new template (PDF will be saved by controller via file upload)
                var template = new LabelTemplate
                {
                    LabelID = ID_Generator.Template(),
                    ClientID = request.ClientID,
                    LabelName = request.LabelName,
                    PaperSize = request.PaperSize,
                    DateFormat = request.DateFormat,
                    ExpiryOffsetMonths = request.ExpiryOffsetMonths,
                    PdfPath = "" // Will be set by controller after file upload
                };

                _db.LabelTemplates.Add(template);
                await _db.SaveChangesAsync();

                _logger.LogInformation($"Template {template.LabelID} created for client {request.ClientID}");

                return MapToResponse(template);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating template: {ex.Message}");
                throw;
            }
        }

        public async Task<TemplateResponse?> GetTemplateAsync(string TemplateID)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TemplateID))
                    throw new ArgumentException("TemplateID is required.", nameof(TemplateID));

                var template = await _db.LabelTemplates.FirstOrDefaultAsync(t => t.LabelID == TemplateID);

                if (template == null)
                    return null;

                return MapToResponse(template);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving template {TemplateID}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<TemplateResponse>> GetTemplatesByClientAsync(string ClientID)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ClientID))
                    throw new ArgumentException("ClientID is required.", nameof(ClientID));

                var templates = await _db.LabelTemplates
                    .Where(t => t.ClientID == ClientID)
                    .ToListAsync();

                return templates.Select(MapToResponse).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving templates for client {ClientID}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<TemplateResponse>> GetTemplatesByPaperSizeAsync(PaperSize size)
        {
            try
            {
                var templates = await _db.LabelTemplates
                    .Where(t => t.PaperSize == size)
                    .ToListAsync();

                return templates.Select(MapToResponse).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving templates for paper size {size}: {ex.Message}");
                throw;
            }
        }

        public async Task<TemplateResponse?> UpdateTemplateAsync(string TemplateID, UpdateTemplateRequest request)
        {
            try
            {
                // Validation
                if (string.IsNullOrWhiteSpace(TemplateID))
                    throw new ArgumentException("TemplateID is required.", nameof(TemplateID));
                if (request == null)
                    throw new ArgumentNullException(nameof(request));

                var template = await _db.LabelTemplates.FirstOrDefaultAsync(t => t.LabelID == TemplateID);
                if (template == null)
                    return null;

                // Update only if provided
                if (!string.IsNullOrWhiteSpace(request.LabelName))
                    template.LabelName = request.LabelName;

                if (!string.IsNullOrWhiteSpace(request.DateFormat))
                    template.DateFormat = request.DateFormat;

                if (request.ExpiryOffsetMonths.HasValue && request.ExpiryOffsetMonths > 0)
                    template.ExpiryOffsetMonths = request.ExpiryOffsetMonths.Value;

                _db.LabelTemplates.Update(template);
                await _db.SaveChangesAsync();

                _logger.LogInformation($"Template {TemplateID} updated");

                return MapToResponse(template);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating template {TemplateID}: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteTemplateAsync(string TemplateID)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TemplateID))
                    throw new ArgumentException("TemplateID is required.", nameof(TemplateID));

                var template = await _db.LabelTemplates.FirstOrDefaultAsync(t => t.LabelID == TemplateID);
                if (template == null)
                    return false;

                // Delete PDF file if exists
                if (!string.IsNullOrWhiteSpace(template.PdfPath))
                {
                    var filePath = Path.Combine(_uploadPath, Path.GetFileName(template.PdfPath));
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        _logger.LogInformation($"PDF file deleted: {filePath}");
                    }
                }

                _db.LabelTemplates.Remove(template);
                await _db.SaveChangesAsync();

                _logger.LogInformation($"Template {TemplateID} deleted");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting template {TemplateID}: {ex.Message}");
                throw;
            }
        }

        public async Task<List<string>> ValidatePlaceholdersAsync(string pdfPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pdfPath))
                    throw new ArgumentException("PDF path is required.", nameof(pdfPath));

                if (!File.Exists(pdfPath))
                    throw new FileNotFoundException($"PDF file not found: {pdfPath}");

                var foundPlaceholders = new List<string>();

                using (var pdfDocument = new PdfDocument(new PdfReader(pdfPath)))
                {
                    // Extract text from all pages
                    for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                    {
                        var page = pdfDocument.GetPage(i);
                        var text = PdfTextExtractor.GetTextFromPage(page);

                        // Find all {{PLACEHOLDER}} patterns
                        var matches = Regex.Matches(text, @"\{\{([A-Z_]+)\}\}");

                        foreach (Match match in matches)
                        {
                            string placeholder = match.Value;  // "{{EXPIRY_DATE}}"
                            if (!foundPlaceholders.Contains(placeholder))
                                foundPlaceholders.Add(placeholder);
                        }
                    }
                }

                // Validate required placeholders exist
                if (!foundPlaceholders.Contains("{{EXPIRY_DATE}}"))
                    throw new InvalidOperationException("PDF must contain {{EXPIRY_DATE}} placeholder");

                if (!foundPlaceholders.Contains("{{LOT_NUMBER}}"))
                    throw new InvalidOperationException("PDF must contain {{LOT_NUMBER}} placeholder");

                _logger.LogInformation($"PDF validation passed. Found placeholders: {string.Join(", ", foundPlaceholders)}");

                return foundPlaceholders;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error validating PDF placeholders: {ex.Message}");
                throw;
            }
        }

        // Helper method to save PDF file and update template
        public async Task<string> SavePdfFileAsync(string templateId, IFormFile pdfFile)
        {
            try
            {
                if (pdfFile == null || pdfFile.Length == 0)
                    throw new ArgumentException("PDF file is required.", nameof(pdfFile));

                if (!pdfFile.ContentType.Contains("pdf"))
                    throw new InvalidOperationException("File must be a PDF.");

                // Validate PDF placeholders before saving
                var tempPath = Path.Combine(Path.GetTempPath(), pdfFile.FileName);
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await pdfFile.CopyToAsync(stream);
                }

                // Validate placeholders
                await ValidatePlaceholdersAsync(tempPath);

                // Save to permanent location
                var fileName = $"{templateId}_{Guid.NewGuid()}_{pdfFile.FileName}";
                var filePath = Path.Combine(_uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await pdfFile.CopyToAsync(stream);
                }

                // Delete temp file
                File.Delete(tempPath);

                // Update template with file path
                var template = await _db.LabelTemplates.FirstOrDefaultAsync(t => t.LabelID == templateId);
                if (template != null)
                {
                    template.PdfPath = Path.Combine("uploads", "pdfs", fileName);
                    _db.LabelTemplates.Update(template);
                    await _db.SaveChangesAsync();
                }

                _logger.LogInformation($"PDF file saved: {filePath}");
                return template?.PdfPath ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving PDF file: {ex.Message}");
                throw;
            }
        }

        private TemplateResponse MapToResponse(LabelTemplate template)
        {
            return new TemplateResponse
            {
                TemplateID = template.LabelID,
                ClientID = template.ClientID,
                LabelName = template.LabelName,
                PaperSize = template.PaperSize,
                PdfPath = template.PdfPath,
                DateFormat = template.DateFormat,
                CreatedAt = template.CreatedAt,
                ExpiryOffsetMonths = template.ExpiryOffsetMonths,
            };
        }
    }
}
