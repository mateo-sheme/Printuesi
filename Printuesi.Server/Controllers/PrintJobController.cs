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
        /// Step 1: Upload order PDF → scan → create print job.
        /// Returns job with items grouped by paper size.
        /// Check LabelFileFound on each item — if false the label PDF is missing on the share.
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
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating print job from order");
                return StatusCode(500, new { message = "Error creating print job.", error = ex.Message });
            }
        }

        /// <summary>
        /// Get print job by ID — shows all items grouped by paper size with current status.
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
        /// Get all print jobs, optionally filtered by status.
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
        /// Step 3: Start printing a paper size group.
        /// 
        /// Possible responses:
        /// - NeedsPaperChange = true  → call confirm-paper-change when paper is loaded
        /// - NeedsTestConfirm = true  → call confirm-test or reprint-test
        /// - JobCompleted = true      → all done
        /// - NextPaperSize set        → call start-group again for next size
        /// </summary>
        [HttpPost("{printJobID}/start-group")]
        public async Task<ActionResult<PrintGroupResult>> StartGroup(
            string printJobID,
            [FromQuery] PaperSize paperSize)
        {
            try
            {
                var result = await _printJobService.StartGroupAsync(printJobID, paperSize);
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

        /// <summary>
        /// Step 4a: User has physically changed the paper and confirms it is loaded.
        /// Continues printing for this paper size group.
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
                return StatusCode(500, new { message = "Error confirming paper change.", error = ex.Message });
            }
        }

        /// <summary>
        /// Step 4b: User confirms test print alignment is correct.
        /// Prints remaining copies (quantity - 1).
        /// </summary>
        [HttpPost("{printJobID}/confirm-test")]
        public async Task<ActionResult<PrintGroupResult>> ConfirmTest(
            string printJobID,
            [FromQuery] PaperSize paperSize)
        {
            try
            {
                var result = await _printJobService.ConfirmTestPrintAsync(printJobID, paperSize);
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
        /// Step 4c: Test print alignment is NOT ok — reprints 1 test copy.
        /// Call confirm-test after checking, or cancel the job.
        /// </summary>
        [HttpPost("{printJobID}/reprint-test")]
        public async Task<ActionResult<PrintGroupResult>> ReprintTest(
            string printJobID,
            [FromQuery] PaperSize paperSize)
        {
            try
            {
                var result = await _printJobService.ReprintTestAsync(printJobID, paperSize);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reprinting test for job {ID}", printJobID);
                return StatusCode(500, new { message = "Error reprinting test.", error = ex.Message });
            }
        }

        /// <summary>
        /// Cancel a print job.
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
    }
}