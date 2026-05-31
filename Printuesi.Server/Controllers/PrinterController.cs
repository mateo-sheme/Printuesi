using Microsoft.AspNetCore.Mvc;
using Printuesi.Server.Data;
using Printuesi.Server.Services;

namespace Printuesi.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PrinterController : ControllerBase
    {
        private readonly PrinterService_Interface _printerService;

        public PrinterController(PrinterService_Interface printerService)
        {
            _printerService = printerService;
        }

        [HttpGet("status")]
        public async Task<ActionResult> GetStatus()
        {
            var isOnline = await _printerService.TestConnectionAsync();
            return Ok(new { online = isOnline });
        }

        // POST /api/printer/LPR  — sends a file via LPR
        [HttpPost("LPR")]
        public async Task<ActionResult> LPR(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File is required." });

            var tempPath = Path.Combine(Path.GetTempPath(), $"lpr_{Guid.NewGuid()}_{file.FileName}");
            try
            {
                await using (var fs = new FileStream(tempPath, FileMode.Create))
                    await file.CopyToAsync(fs);

                var success = await _printerService.SendFileLprAsync(tempPath);
                if (!success)
                    return BadRequest(new { message = "Failed to send via LPR" });

                return Ok(new { message = "Sent via LPR" });
            }
            finally
            {
                try { System.IO.File.Delete(tempPath); } catch { }
            }
        }

        [HttpPost("RAW")]
        public async Task<ActionResult> TestRaw([FromBody] PrintFileRequest request)
        {
            var success = await _printerService.SendFileRawAsync(request.FilePath);
            if (!success)
                return BadRequest(new { message = "Failed to send via Raw TCP" });

            return Ok(new { message = "Sent via Raw TCP" });

        }

        [HttpGet("probe")]
        public async Task<ActionResult> ProbePrinter()
        {
            var results = new Dictionary<string, object>();

            // Test 1: Basic connectivity LPR port 515
            try
            {
                using var client1 = new System.Net.Sockets.TcpClient();
                var t1 = client1.ConnectAsync("192.168.88.93", 515);
                var connected = await Task.WhenAny(t1, Task.Delay(3000)) == t1;
                results["lpr_port_515_open"] = connected;

                if (connected)
                {
                    // Test 2: Send LPR status query command (0x01 = print any waiting jobs)
                    var stream = client1.GetStream();
                    var statusCmd = new byte[] { 0x01 }
                        .Concat(System.Text.Encoding.ASCII.GetBytes("PASSTHRU"))
                        .Concat(new byte[] { 0x0A })
                        .ToArray();
                    await stream.WriteAsync(statusCmd);

                    // Read response if any
                    var buffer = new byte[1024];
                    var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
                    if (await Task.WhenAny(readTask, Task.Delay(2000)) == readTask)
                    {
                        var bytesRead = readTask.Result;
                        var response = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        var hex = string.Join(" ", buffer.Take(bytesRead).Select(b => $"{b:X2}"));
                        results["lpr_status_response_text"] = response;
                        results["lpr_status_response_hex"] = hex;
                        results["lpr_status_bytes_received"] = bytesRead;
                    }
                    else
                    {
                        results["lpr_status_response"] = "no response within 2 seconds";
                    }
                }
            }
            catch (Exception ex)
            {
                results["lpr_error"] = ex.Message;
            }

            // Test 3: Raw TCP port 9100
            try
            {
                using var client2 = new System.Net.Sockets.TcpClient();
                var t2 = client2.ConnectAsync("192.168.88.93", 9100);
                var connected = await Task.WhenAny(t2, Task.Delay(3000)) == t2;
                results["raw_port_9100_open"] = connected;

                if (connected)
                {
                    // Test 4: Send ESC/P status request
                    var stream = client2.GetStream();
                    var statusQuery = System.Text.Encoding.ASCII.GetBytes("\x1B@\x1BF");
                    await stream.WriteAsync(statusQuery);

                    var buffer = new byte[1024];
                    var readTask = stream.ReadAsync(buffer, 0, buffer.Length);
                    if (await Task.WhenAny(readTask, Task.Delay(2000)) == readTask)
                    {
                        var bytesRead = readTask.Result;
                        var response = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        var hex = string.Join(" ", buffer.Take(bytesRead).Select(b => $"{b:X2}"));
                        results["raw_status_response_text"] = response;
                        results["raw_status_response_hex"] = hex;
                        results["raw_status_bytes_received"] = bytesRead;
                    }
                    else
                    {
                        results["raw_status_response"] = "no response within 2 seconds";
                    }
                }
            }
            catch (Exception ex)
            {
                results["raw_error"] = ex.Message;
            }

            // Test 5: Port 9101 (some Epson models use this for status)
            try
            {
                using var client3 = new System.Net.Sockets.TcpClient();
                var t3 = client3.ConnectAsync("192.168.88.93", 9101);
                results["port_9101_open"] = await Task.WhenAny(t3, Task.Delay(2000)) == t3;
            }
            catch
            {
                results["port_9101_open"] = false;
            }

            // Test 6: Port 80 (web interface — many printers have this)
            try
            {
                using var http = new System.Net.Http.HttpClient();
                http.Timeout = TimeSpan.FromSeconds(3);
                var response = await http.GetAsync("http://192.168.88.93/");
                results["http_port_80"] = $"{(int)response.StatusCode} {response.StatusCode}";
                var body = await response.Content.ReadAsStringAsync();
                results["http_body_preview"] = body.Length > 200 ? body[..200] : body;
            }
            catch (Exception ex)
            {
                results["http_port_80"] = $"failed: {ex.Message}";
            }

            return Ok(results);
        }

        [HttpGet("probe2")]
        public async Task<ActionResult> ProbePrinter2()
        {
            var results = new Dictionary<string, object>();

            using var http = new System.Net.Http.HttpClient();
            http.Timeout = TimeSpan.FromSeconds(3);

            var endpoints = new[]
            {
        "http://192.168.88.93/",
        "http://192.168.88.93/PRESENTATION/HTML/TOP/INDEX.HTML",
        "http://192.168.88.93/cgi-bin/info.cgi",
        "http://192.168.88.93/cgi-bin/status.cgi",
        "http://192.168.88.93/statuscheck",
        "http://192.168.88.93/printer/status",
        "http://192.168.88.93/status",
        "http://192.168.88.93/index.html"
    };

            foreach (var url in endpoints)
            {
                try
                {
                    var response = await http.GetAsync(url);
                    var body = await response.Content.ReadAsStringAsync();
                    results[url] = new
                    {
                        status = (int)response.StatusCode,
                        bodyPreview = body.Length > 300 ? body[..300] : body
                    };
                }
                catch (Exception ex)
                {
                    results[url] = new { error = ex.Message };
                }
            }

            return Ok(results);
        }

        [HttpGet("probe3")]
        public async Task<ActionResult> ProbePrinter3()
        {
            var results = new Dictionary<string, object>();

            var handler = new System.Net.Http.HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                Credentials = new System.Net.NetworkCredential("admin", "Epson.nona")
            };

            using var http = new System.Net.Http.HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(5);

            var endpoints = new[]
            {
        "https://192.168.88.93/",
        "https://192.168.88.93/PRESENTATION/HTML/TOP/INDEX.HTML",
        "https://192.168.88.93/cgi-bin/info.cgi",
        "https://192.168.88.93/cgi-bin/status.cgi",
        "https://192.168.88.93/status",
        "https://192.168.88.93/printer/status",
        "https://192.168.88.93/cgi-bin/getconfig.cgi",
        "https://192.168.88.93/cgi-bin/medialist.cgi"
    };

            foreach (var url in endpoints)
            {
                try
                {
                    var response = await http.GetAsync(url);
                    var body = await response.Content.ReadAsStringAsync();
                    results[url] = new
                    {
                        status = (int)response.StatusCode,
                        bodyPreview = body.Length > 400 ? body[..400] : body
                    };
                }
                catch (Exception ex)
                {
                    results[url] = new { error = ex.Message };
                }
            }

            return Ok(results);
        }

        [HttpGet("probe4")]
        public async Task<ActionResult> ProbePrinter4()
        {
            var results = new Dictionary<string, object>();

            var handler = new System.Net.Http.HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                Credentials = new System.Net.NetworkCredential("admin", "Epson.nona")
            };

            using var http = new System.Net.Http.HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(5);

            // Epson CW-C6000 specific endpoints
            var endpoints = new[]
            {
        "https://192.168.88.93/PRESENTATION/HTML/TOP/INDEX.HTML",
        "https://192.168.88.93/PRESENTATION/HTML/PRTINFO/INDEX.HTML",
        "https://192.168.88.93/PRESENTATION/HTML/MEDIASETTING/INDEX.HTML",
        "https://192.168.88.93/PRESENTATION/HTML/SUPPLYINFO/INDEX.HTML",
        "https://192.168.88.93/cgi-bin/home.cgi",
        "https://192.168.88.93/cgi-bin/printer.cgi",
        "https://192.168.88.93/cgi-bin/supply.cgi",
        "https://192.168.88.93/cgi-bin/media.cgi",
        "https://192.168.88.93/cgi-bin/prtinfo.cgi",
        "https://192.168.88.93/PRTINFO.htm",
        "https://192.168.88.93/MEDIASETTING.htm",
        "https://192.168.88.93/api/printer/status",
        "https://192.168.88.93/api/media",
        "https://192.168.88.93/api/supply"
    };

            foreach (var url in endpoints)
            {
                try
                {
                    var response = await http.GetAsync(url);
                    var body = await response.Content.ReadAsStringAsync();
                    results[url] = new
                    {
                        status = (int)response.StatusCode,
                        // Show more content for 200s so we can see what data is there
                        bodyPreview = response.StatusCode == System.Net.HttpStatusCode.OK
                            ? (body.Length > 800 ? body[..800] : body)
                            : (body.Length > 100 ? body[..100] : body)
                    };
                }
                catch (Exception ex)
                {
                    results[url] = new { error = ex.Message };
                }
            }

            return Ok(results);
        }

        [HttpGet("probe5")]
        public async Task<ActionResult> ProbePrinter5()
        {
            var results = new Dictionary<string, object>();

            var handler = new System.Net.Http.HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };

            using var http = new System.Net.Http.HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(5);

            // Add Basic auth header explicitly
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes("admin:Epson.nona"));
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            var endpoints = new[]
            {
        "https://192.168.88.93/PRESENTATION/ADVANCED/PASSWORD/TOP",
        "https://192.168.88.93/PRESENTATION/ADVANCED/PRTINFO/TOP",
        "https://192.168.88.93/PRESENTATION/ADVANCED/MEDIASETTING/TOP",
        "https://192.168.88.93/PRESENTATION/ADVANCED/SUPPLYINFO/TOP",
        "https://192.168.88.93/PRESENTATION/ADVANCED/TOP/TOP",
    };

            foreach (var url in endpoints)
            {
                try
                {
                    var response = await http.GetAsync(url);
                    var body = await response.Content.ReadAsStringAsync();
                    results[url] = new
                    {
                        status = (int)response.StatusCode,
                        bodyPreview = body.Length > 1000 ? body[..1000] : body
                    };
                }
                catch (Exception ex)
                {
                    results[url] = new { error = ex.Message };
                }
            }

            return Ok(results);
        }


        [HttpGet("probe-auth")]
        public async Task<ActionResult> ProbeAuth()
        {
            var handler = new System.Net.Http.HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                UseCookies = true,
                CookieContainer = new System.Net.CookieContainer()
            };

            using var http = new System.Net.Http.HttpClient(handler);
            http.Timeout = TimeSpan.FromSeconds(5);

            // Step 1: POST login
            var loginData = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string, string>("INPUTT_USERNAME", "admin"),
        new KeyValuePair<string, string>("INPUTT_PASSWORD", "Epson.nona")
    });

            var loginResponse = await http.PostAsync(
                "https://192.168.88.93/PRESENTATION/ADVANCED/PASSWORD/SET", loginData);

            var loginBody = await loginResponse.Content.ReadAsStringAsync();

            // Step 2: Try status pages with session cookie
            var results = new Dictionary<string, object>
            {
                ["login_status"] = (int)loginResponse.StatusCode,
                ["login_body"] = loginBody.Length > 300 ? loginBody[..300] : loginBody
            };

            var pages = new[]
            {
        "https://192.168.88.93/PRESENTATION/ADVANCED/PRTINFO/TOP",
        "https://192.168.88.93/PRESENTATION/ADVANCED/SUPPLYINFO/TOP",
        "https://192.168.88.93/PRESENTATION/ADVANCED/MEDIASETTING/TOP",
    };

            foreach (var url in pages)
            {
                try
                {
                    var r = await http.GetAsync(url);
                    var body = await r.Content.ReadAsStringAsync();
                    results[url] = new
                    {
                        status = (int)r.StatusCode,
                        body = body.Length > 500 ? body[..500] : body
                    };
                }
                catch (Exception ex)
                {
                    results[url] = new { error = ex.Message };
                }
            }

            return Ok(results);
        }

        public class PrintFileRequest
        {
            public string FilePath { get; set; } = string.Empty;
        }
    }
}
