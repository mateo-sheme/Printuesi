using Microsoft.AspNetCore.Mvc;
using Printuesi.Server.Data;
using Printuesi.Server.Services;

namespace Printuesi.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PrinterController : ControllerBase
    {
        private readonly IPrinterService _printerService;

        public PrinterController(IPrinterService printerService)
        {
            _printerService = printerService;
        }

        [HttpGet("status")]
        public async Task<ActionResult> GetStatus()
        {
            var isOnline = await _printerService.TestConnectionAsync();
            return Ok(new { online = isOnline });
        }

        // POST /api/printer/test-lpr  — sends a file via LPR
        [HttpPost("test-lpr")]
        public async Task<ActionResult> TestLpr([FromBody] PrintFileRequest request)
        {
            var success = await _printerService.SendFileLprAsync(request.FilePath);
            if (!success)
            {
                return BadRequest(new { message = "Failed to send via LPR" });
            }
            return Ok(new { message = "Sent via LPR" });
        }

        [HttpPost("test-raw")]
        public async Task<ActionResult> TestRaw([FromBody] PrintFileRequest request)
        {
            var success = await _printerService.SendFileRawAsync(request.FilePath);
            if (!success)
                return BadRequest(new { message = "Failed to send via Raw TCP" });

            return Ok(new { message = "Sent via Raw TCP" });
        
        }
    }

    public class PrintFileRequest
    {
        public string FilePath { get; set; } = string.Empty;
    }
}
