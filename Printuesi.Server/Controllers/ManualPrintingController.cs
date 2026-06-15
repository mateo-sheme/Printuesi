using Microsoft.AspNetCore.Mvc;
using Printuesi.Server.Services;

namespace Printuesi.Server.Controllers
{
    [ApiController]
    [Route("api/manual-printing")]
    public class ManualPrintingController : ControllerBase
    {
        private readonly ManualPrintingService_Interface _manualPrintingService;
        private readonly ILogger<ManualPrintingController> _logger;

        public ManualPrintingController(
            ManualPrintingService_Interface manualPrintingService,
            ILogger<ManualPrintingController> logger)
        {
            _manualPrintingService = manualPrintingService;
            _logger = logger;
        }

        [HttpGet("labels")]
        public async Task<ActionResult<List<ManualLabelResponse>>> GetLabels(
            [FromQuery] string? search = null,
            [FromQuery] string? clientFolder = null)
        {
            try
            {
                var labels = await _manualPrintingService.GetLabelsAsync(search, clientFolder);
                return Ok(labels);
            }
            catch (DirectoryNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading manual labels");
                return StatusCode(500, new
                {
                    message = "Error loading manual labels.",
                    error = ex.Message
                });
            }
        }

        [HttpGet("labels/preview")]
        public async Task<IActionResult> PreviewLabel([FromQuery] string relativePath)
        {
            try
            {
                var fullPath = await _manualPrintingService.ResolveSafeLabelPathAsync(relativePath);

                var stream = new FileStream(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

                Response.Headers["Content-Disposition"] =
                    $"inline; filename=\"{Path.GetFileName(fullPath)}\"";

                return File(stream, "application/pdf", enableRangeProcessing: true);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error previewing manual label");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("send-driver")]
        public async Task<ActionResult<ManualDriverResponse>> SendDriver(
            [FromBody] ManualSendDriverRequest request)
        {
            try
            {
                var result = await _manualPrintingService.SendDriverAsync(request);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ManualDriverResponse
                {
                    Success = false,
                    Message = ex.Message,
                    Errors = new List<string> { ex.Message }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending manual print driver");
                return StatusCode(500, new ManualDriverResponse
                {
                    Success = false,
                    Message = "Error sending driver.",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("print-label")]
        public async Task<ActionResult<ManualPrintResponse>> PrintLabel(
            [FromBody] ManualPrintLabelRequest request)
        {
            try
            {
                var result = await _manualPrintingService.PrintLabelAsync(request);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ManualPrintResponse
                {
                    Success = false,
                    Message = ex.Message,
                    Errors = new List<string> { ex.Message }
                });
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new ManualPrintResponse
                {
                    Success = false,
                    Message = ex.Message,
                    Errors = new List<string> { ex.Message }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing manual label");
                return StatusCode(500, new ManualPrintResponse
                {
                    Success = false,
                    Message = "Error printing manual label.",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("print-label-with-driver")]
        public async Task<ActionResult<ManualPrintResponse>> PrintLabelWithDriver(
            [FromBody] ManualPrintWithDriverRequest request)
        {
            try
            {
                var result = await _manualPrintingService.PrintLabelWithDriverAsync(request);

                if (!result.Success)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ManualPrintResponse
                {
                    Success = false,
                    Message = ex.Message,
                    Errors = new List<string> { ex.Message }
                });
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(new ManualPrintResponse
                {
                    Success = false,
                    Message = ex.Message,
                    Errors = new List<string> { ex.Message }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing manual label with driver");
                return StatusCode(500, new ManualPrintResponse
                {
                    Success = false,
                    Message = "Error printing manual label with driver.",
                    Errors = new List<string> { ex.Message }
                });
            }
        }
    }
}