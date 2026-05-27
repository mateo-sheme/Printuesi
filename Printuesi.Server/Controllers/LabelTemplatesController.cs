using Microsoft.AspNetCore.Mvc;
using Printuesi.Server.Enums;
using Printuesi.Server.Services;

namespace Printuesi.Server.Controllers
{
    [ApiController]
    [Route("api/templates")]
    public class LabelTemplatesController : ControllerBase
    {
        private readonly LabelTemplate_Interface _templateService;
        private readonly ILogger<LabelTemplatesController> _logger;

        public LabelTemplatesController(LabelTemplate_Interface templateService, ILogger<LabelTemplatesController> logger)
        {
            _templateService = templateService;
            _logger = logger;
        }

        /// <summary>
        /// Create a new label template (PDF uploaded separately via UploadPdf endpoint)
        /// </summary>
        [HttpPost("create")]
        public async Task<ActionResult<TemplateResponse>> CreateTemplate([FromBody] CreateTemplateRequest request)
        {
            try
            {
                var result = await _templateService.CreateTemplateAsync(request);
                return CreatedAtAction(nameof(GetTemplate), new { templateID = result.TemplateID }, result);
            }
            catch (ArgumentNullException ex)
            {
                _logger.LogWarning($"Validation error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Validation error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning($"Business logic error: {ex.Message}");
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating template: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while creating the template.", error = ex.Message });
            }
        }

        /// <summary>
        /// Upload PDF file for a template
        /// </summary>
        [HttpPost("{templateID}/upload-pdf")]
        public async Task<ActionResult<TemplateResponse>> UploadPdf(string templateID, IFormFile pdfFile)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(templateID))
                    return BadRequest(new { message = "TemplateID is required." });

                if (pdfFile == null || pdfFile.Length == 0)
                    return BadRequest(new { message = "PDF file is required." });

                if (!pdfFile.ContentType.Contains("pdf"))
                    return BadRequest(new { message = "File must be a PDF." });

                // Save PDF and validate placeholders
                await _templateService.SavePdfFileAsync(templateID, pdfFile);

                // Return updated template
                var result = await _templateService.GetTemplateAsync(templateID);
                if (result == null)
                    return NotFound(new { message = $"Template with ID '{templateID}' not found." });

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Validation error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning($"Business logic error: {ex.Message}");
                return Conflict(new { message = ex.Message });
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning($"File error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading PDF: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while uploading the PDF.", error = ex.Message });
            }
        }

        /// <summary>
        /// Get template by ID
        /// </summary>
        [HttpGet("{templateID}")]
        public async Task<ActionResult<TemplateResponse>> GetTemplate(string templateID)
        {
            try
            {
                var result = await _templateService.GetTemplateAsync(templateID);
                if (result == null)
                    return NotFound(new { message = $"Template with ID '{templateID}' not found." });

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Validation error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving template: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while retrieving the template.", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all templates for a specific client
        /// </summary>
        [HttpGet("client/{clientID}")]
        public async Task<ActionResult<List<TemplateResponse>>> GetTemplatesByClient(string clientID)
        {
            try
            {
                var result = await _templateService.GetTemplatesByClientAsync(clientID);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Validation error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving templates for client: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while retrieving templates.", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all templates for a specific paper size
        /// </summary>
        [HttpGet("paper-size/{paperSize}")]
        public async Task<ActionResult<List<TemplateResponse>>> GetTemplatesByPaperSize(PaperSize paperSize)
        {
            try
            {
                var result = await _templateService.GetTemplatesByPaperSizeAsync(paperSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving templates by paper size: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while retrieving templates.", error = ex.Message });
            }
        }

        /// <summary>
        /// Update template details (name, date format, expiry offset)
        /// </summary>
        [HttpPut("{templateID}")]
        public async Task<ActionResult<TemplateResponse>> UpdateTemplate(string templateID, [FromBody] UpdateTemplateRequest request)
        {
            try
            {
                var result = await _templateService.UpdateTemplateAsync(templateID, request);
                if (result == null)
                    return NotFound(new { message = $"Template with ID '{templateID}' not found." });

                return Ok(result);
            }
            catch (ArgumentNullException ex)
            {
                _logger.LogWarning($"Validation error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Validation error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating template: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while updating the template.", error = ex.Message });
            }
        }

        /// <summary>
        /// Delete template and associated PDF file
        /// </summary>
        [HttpDelete("{templateID}")]
        public async Task<ActionResult> DeleteTemplate(string templateID)
        {
            try
            {
                var result = await _templateService.DeleteTemplateAsync(templateID);
                if (!result)
                    return NotFound(new { message = $"Template with ID '{templateID}' not found." });

                return Ok(new { message = "Template deleted successfully." });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Validation error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting template: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while deleting the template.", error = ex.Message });
            }
        }

        /// <summary>
        /// Validate PDF file has required placeholders ({{EXPIRY_DATE}}, {{LOT_NUMBER}})
        /// </summary>
        [HttpPost("validate-pdf")]
        public async Task<ActionResult<ValidatePdfResponse>> ValidatePdf(IFormFile pdfFile)
        {
            try
            {
                if (pdfFile == null || pdfFile.Length == 0)
                    return BadRequest(new { message = "PDF file is required." });

                if (!pdfFile.ContentType.Contains("pdf"))
                    return BadRequest(new { message = "File must be a PDF." });

                // Save temporarily
                var tempPath = Path.Combine(Path.GetTempPath(), pdfFile.FileName);
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await pdfFile.CopyToAsync(stream);
                }

                try
                {
                    // Validate placeholders
                    var placeholders = await _templateService.ValidatePlaceholdersAsync(tempPath);

                    return Ok(new ValidatePdfResponse
                    {
                        IsValid = true,
                        FoundPlaceholders = placeholders,
                        Message = "PDF validation successful. All required placeholders found."
                    });
                }
                finally
                {
                    // Clean up temp file
                    try
                    {
                        System.IO.File.Delete(tempPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Failed to delete temporary PDF file at {TempPath}: {Message}", tempPath, ex.Message);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning($"PDF validation failed: {ex.Message}");
                return BadRequest(new ValidatePdfResponse
                {
                    IsValid = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error validating PDF: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while validating the PDF.", error = ex.Message });
            }
        }
    }

    /// <summary>
    /// Response for PDF validation
    /// </summary>
    public class ValidatePdfResponse
    {
        public bool IsValid { get; set; }
        public List<string> FoundPlaceholders { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}
