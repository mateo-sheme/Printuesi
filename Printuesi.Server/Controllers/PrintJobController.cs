using Microsoft.AspNetCore.Mvc;
using Printuesi.Server.Enums;
using Printuesi.Server.Services;

namespace Printuesi.Server.Controllers
{
    [ApiController]
    [Route("api/printjobs")]
    public class PrintJobsController : ControllerBase
    {
        private readonly PrintJobService_Interface _printJobService;
        private readonly ILogger<PrintJobsController> _logger;

        public PrintJobsController(
            PrintJobService_Interface printJobService,
            ILogger<PrintJobsController> logger)
        {
            _printJobService = printJobService;
            _logger = logger;
        }

        /// <summary>
        /// Step 1: Upload order PDF → scan → create print job
        /// Returns the full job with items grouped by paper size,
        /// and flags which label files were found/missing on the share.
        /// </summary>
        [HttpPost("create-from-order")]
        public async Task<ActionResult<PrintJobResponse>> CreateFromOrder(
            IFormFile orderPdf,
            [FromQuery] string createdByUserID)
        {
            try
            {
                if (orderPdf == null || orderPdf.Length == 0)
                    return BadRequest(new { message = "Order PDF is required." });

                if (!orderPdf.ContentType.Contains("pdf"))
                    return BadRequest(new { message = "File must be a PDF." });

                if (string.IsNullOrWhiteSpace(createdByUserID))
                    return BadRequest(new { message = "createdByUserID is required." });

                var result = await _printJobService.CreateFromOrderAsync(orderPdf, createdByUserID);
                return CreatedAtAction(nameof(GetPrintJob),
                    new { printJobID = result.PrintJobID }, result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Business error creating print job: {Message}", ex.Message);
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating print job from order");
                return StatusCode(500, new { message = "Error creating print job.", error = ex.Message });
            }
        }

        /// <summary>
        /// Step 2: Get print job — shows all items grouped by paper size
        /// Frontend uses this to display what paper to load first
        /// </summary>
        [HttpGet("{printJobID}")]
        public async Task<ActionResult<PrintJobResponse>> GetPrintJob(string printJobID)
        {
            try
            {
                var result = await _printJobService.GetPrintJobAsync(printJobID);
                if (result == null)
                    return NotFound(new { message = $"Print job '{printJobID}' not found." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving print job {ID}", printJobID);
                return StatusCode(500, new { message = "Error retrieving print job.", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all print jobs, optionally filtered by status
        /// </summary>
        [HttpGet("")]
        public async Task<ActionResult<List<PrintJobResponse>>> GetAllPrintJobs(
            [FromQuery] Status? status = null)
        {
            try
            {
                var result = await _printJobService.GetAllPrintJobsAsync(status);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving print jobs");
                return StatusCode(500, new { message = "Error retrieving print jobs.", error = ex.Message });
            }
        }

        /// <summary>
        /// Step 3: User has loaded the paper and confirmed — triggers PDF fill + print
        /// Call this once per paper size group.
        /// Response tells you: what printed, what failed, and what paper size to load next.
        /// </summary>
        [HttpPost("{printJobID}/confirm-and-print")]
        public async Task<ActionResult<PrintGroupResult>> ConfirmAndPrint(
            string printJobID,
            [FromQuery] PaperSize paperSize)
        {
            try
            {
                var result = await _printJobService.ConfirmAndPrintGroupAsync(printJobID, paperSize);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Business error confirming print: {Message}", ex.Message);
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming print for job {ID}", printJobID);
                return StatusCode(500, new { message = "Error during print.", error = ex.Message });
            }
        }

        /// <summary>
        /// Cancel a print job
        /// </summary>
        [HttpDelete("{printJobID}")]
        public async Task<ActionResult> CancelPrintJob(string printJobID)
        {
            try
            {
                var result = await _printJobService.CancelPrintJobAsync(printJobID);
                if (!result)
                    return NotFound(new { message = $"Print job '{printJobID}' not found." });

                return Ok(new { message = "Print job cancelled." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling print job {ID}", printJobID);
                return StatusCode(500, new { message = "Error cancelling print job.", error = ex.Message });
            }
        }

        [HttpPost("debug-scan")]
        public async Task<ActionResult> DebugScan(IFormFile orderPdf)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"debug_{Guid.NewGuid()}.pdf");
            await using (var fs = new FileStream(tempPath, FileMode.Create))
                await orderPdf.CopyToAsync(fs);

            using var reader = new iText.Kernel.Pdf.PdfReader(tempPath);
            using var doc = new iText.Kernel.Pdf.PdfDocument(reader);
            var text = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor
                .GetTextFromPage(doc.GetPage(1));

            // Show hex values of first 300 chars so we can see exactly what ë looks like
            var hex = string.Join(" ", text.Take(300).Select(c => $"{(int)c:X4}"));

            return Ok(new
            {
                rawText = text.Substring(0, Math.Min(300, text.Length)),
                hexDump = hex
            });
        }
    }
}