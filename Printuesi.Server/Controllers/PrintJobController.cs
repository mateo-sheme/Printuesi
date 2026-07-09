using Microsoft.AspNetCore.Mvc;
using Printuesi.Server.Enums;
using Printuesi.Server.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

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
        /// Upload order PDF, scan products, create print job.
        /// Returns job grouped by paper size.
        /// </summary>
        [HttpPost("create-from-order")]
        public async Task<ActionResult<PrintJobResponse>> CreateFromOrder(
            [FromForm] IFormFile orderPdf,
            [FromForm] string createdByUserID,
            [FromForm] bool includeRrethi = false)
        {
            try
            {
                if (orderPdf == null || orderPdf.Length == 0)
                    return BadRequest(new { message = "Order PDF is required." });

                if (!orderPdf.ContentType.Contains("pdf"))
                    return BadRequest(new { message = "File must be a PDF." });

                if (string.IsNullOrWhiteSpace(createdByUserID))
                    return BadRequest(new { message = "createdByUserID is required." });

                var result = await _printJobService.CreateFromOrderAsync(orderPdf, createdByUserID, includeRrethi);

                return CreatedAtAction(
                    nameof(GetPrintJob),
                    new { printJobID = result.PrintJobID },
                    result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating print job from order");

                return StatusCode(500, new
                {
                    message = "Error creating print job.",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get one print job by ID.
        /// Shows groups, quantities, expiry, LOT, status and missing label files.
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

                return StatusCode(500, new
                {
                    message = "Error retrieving print job.",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Get all print jobs, optionally filtered by status.
        /// Example: /api/printjobs?status=Pending
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

                return StatusCode(500, new
                {
                    message = "Error retrieving print jobs.",
                    error = ex.Message
                });
            }
        }

        [HttpGet("{printJobID}/generated-pdfs")]
        public async Task<ActionResult<List<GeneratedJobPdfResponse>>> GetGeneratedPdfs(
    string printJobID)
        {
            try
            {
                var result = await _printJobService.GetGeneratedPdfsAsync(printJobID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading generated PDFs for job {JobID}", printJobID);

                return StatusCode(500, new
                {
                    message = ex.Message
                });
            }
        }

        [HttpPost("{printJobID}/generated-pdfs/{printJobObjectID}/prepare")]
        public async Task<ActionResult<GeneratedJobPdfResponse>> PrepareGeneratedPdf(
    string printJobID,
    string printJobObjectID,
    [FromQuery] string? kg = null)
        {
            try
            {
                var result = await _printJobService.PrepareGeneratedPdfAsync(
                    printJobID,
                    printJobObjectID,
                    kg);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error preparing generated PDF for job {JobID}, object {ObjectID}",
                    printJobID,
                    printJobObjectID);

                return StatusCode(500, new
                {
                    message = ex.Message
                });
            }
        }

        [HttpGet("{printJobID}/generated-pdfs/{printJobObjectID}/preview")]
        public async Task<IActionResult> PreviewGeneratedPdf(
    string printJobID,
    string printJobObjectID)
        {
            try
            {
                var fullPath = await _printJobService.ResolveGeneratedPdfPathAsync(
                    printJobID,
                    printJobObjectID);

                var stream = new FileStream(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

                Response.Headers["Content-Disposition"] = "inline";

                return File(stream, "application/pdf", enableRangeProcessing: true);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing generated PDF");

                return BadRequest(new { message = ex.Message });
            }
        }
        /// <summary>
        /// User confirms paper was physically changed.
        /// Backend sends the driver/settings file for that paper size.
        /// No label is printed here.
        /// 
        /// Required before printing a test or starting a group.
        /// </summary>
        [HttpPost("{printJobID}/confirm-paper-change")]
        public async Task<ActionResult<PrintGroupResult>> ConfirmPaperChange(
            string printJobID,
            [FromQuery] PaperSize paperSize)
        {
            try
            {
                var result = await _printJobService.ConfirmPaperChangeAsync(printJobID, paperSize);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming paper change for job {ID}", printJobID);

                return StatusCode(500, new
                {
                    message = "Error confirming paper change.",
                    error = ex.Message
                });
            }
        }

        [HttpPost("{printJobID}/print-test")]
        public async Task<ActionResult<PrintGroupResult>> PrintTest(
            string printJobID,
            [FromQuery] PaperSize paperSize,
            [FromQuery] string? kg = null)
        {
            try
            {
                var result = await _printJobService.PrintTestAsync(printJobID, paperSize, kg);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing test for job {ID}", printJobID);
                return StatusCode(500, new { message = "Error printing test.", error = ex.Message });
            }
        }


        [HttpPost("{printJobID}/start-group")]
        public async Task<ActionResult<PrintGroupResult>> StartGroup(
            string printJobID,
            [FromQuery] PaperSize paperSize,
            [FromQuery] string? kg = null)
        {
            try
            {
                var result = await _printJobService.StartGroupAsync(printJobID, paperSize, kg);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting group for job {ID}", printJobID);
                return StatusCode(500, new { message = "Error starting print group.", error = ex.Message });
            }
        }

        [HttpPost("{printJobID}/stop-printing")]
        public async Task<ActionResult<PrintGroupResult>> StopPrinting(string printJobID)
        {
            try
            {
                var result = await _printJobService.StopPrintingAsync(printJobID);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping print job {ID}", printJobID);

                return StatusCode(500, new
                {
                    message = "Error stopping print job.",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Legacy endpoint kept temporarily for compatibility.
        /// In the new flow, frontend should call start-group instead.
        /// 
        /// This should eventually be removed from:
        /// - Controller
        /// - Interface
        /// - Service
        /// - printJobsApi.js
        /// - AutomaticPrinting.jsx
        /// </summary>
        [HttpPost("{printJobID}/confirm-test")]
        public async Task<ActionResult<PrintGroupResult>> ConfirmTest(
        string printJobID,
        [FromQuery] PaperSize paperSize,
        [FromQuery] string? kg = null)
        {
            try
            {
                var result = await _printJobService.ConfirmTestPrintAsync(printJobID, paperSize, kg);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming test print for job {ID}", printJobID);
                return StatusCode(500, new { message = "Error confirming test print.", error = ex.Message });
            }
        }

        /// <summary>
        /// Cancel a print job.
        /// Cannot cancel a completed job.
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

                return StatusCode(500, new
                {
                    message = "Error cancelling print job.",
                    error = ex.Message
                });
            }
        }

        [HttpDelete("{printJobID}/delete")]
        public async Task<IActionResult> DeletePrintJob(string printJobID)
        {
            try
            {
                var deleted = await _printJobService.DeletePrintJobAsync(printJobID);

                if (!deleted)
                    return NotFound(new { message = $"Print job '{printJobID}' not found." });

                return Ok(new { message = "Print job deleted." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting print job {ID}", printJobID);

                return StatusCode(500, new
                {
                    message = "Error deleting print job.",
                    error = ex.Message
                });
            }
        }
    }
}