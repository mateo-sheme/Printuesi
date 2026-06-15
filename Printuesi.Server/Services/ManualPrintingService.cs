using Printuesi.Server.Enums;
using System.Text.RegularExpressions;

namespace Printuesi.Server.Services
{
    public class ManualPrintingService : ManualPrintingService_Interface
    {
        private const int MaxCopiesPerManualJob = 500;

        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;
        private readonly PrinterService_Interface _printerService;
        private readonly ILogger<ManualPrintingService> _logger;

        private readonly string _labelsBasePath;
        private readonly string _driversBasePath;

        public ManualPrintingService(
            IConfiguration config,
            IWebHostEnvironment env,
            PrinterService_Interface printerService,
            ILogger<ManualPrintingService> logger)
        {
            _config = config;
            _env = env;
            _printerService = printerService;
            _logger = logger;

            _labelsBasePath = config["Labels:BasePath"]
                ?? Path.Combine(env.WebRootPath, "labels");

            var configuredDriverBasePath = config["Drivers:BasePath"] ?? "drivers";

            _driversBasePath = Path.IsPathRooted(configuredDriverBasePath)
                ? configuredDriverBasePath
                : Path.Combine(env.WebRootPath, configuredDriverBasePath);
        }

        public async Task<List<ManualLabelResponse>> GetLabelsAsync(
            string? search = null,
            string? clientFolder = null)
        {
            return await Task.Run(() =>
            {
                if (!Directory.Exists(_labelsBasePath))
                    throw new DirectoryNotFoundException($"Labels folder not found: {_labelsBasePath}");

                var normalizedSearch = search?.Trim().ToLowerInvariant();
                var normalizedClient = clientFolder?.Trim().ToLowerInvariant();

                return Directory
                    .EnumerateFiles(_labelsBasePath, "*.pdf", SearchOption.AllDirectories)
                    .Select(BuildLabelResponse)
                    .Where(label =>
                    {
                        var matchesSearch =
                            string.IsNullOrWhiteSpace(normalizedSearch)
                            || label.FileName.ToLowerInvariant().Contains(normalizedSearch)
                            || label.ClientFolder.ToLowerInvariant().Contains(normalizedSearch)
                            || (label.ProductCode ?? "").ToLowerInvariant().Contains(normalizedSearch);

                        var matchesClient =
                            string.IsNullOrWhiteSpace(normalizedClient)
                            || normalizedClient == "all"
                            || label.ClientFolder.ToLowerInvariant() == normalizedClient;

                        return matchesSearch && matchesClient;
                    })
                    .OrderBy(label => label.ClientFolder)
                    .ThenBy(label => label.FileName)
                    .ToList();
            });
        }

        public Task<string> ResolveSafeLabelPathAsync(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                throw new ArgumentException("RelativePath is required.", nameof(relativePath));

            var baseFullPath = EnsureTrailingSlash(Path.GetFullPath(_labelsBasePath));

            var cleanedRelativePath = relativePath
                .Replace("/", Path.DirectorySeparatorChar.ToString())
                .Replace("\\", Path.DirectorySeparatorChar.ToString());

            var fullPath = Path.GetFullPath(Path.Combine(baseFullPath, cleanedRelativePath));

            if (!fullPath.StartsWith(baseFullPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid label path.");

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Label PDF not found: {relativePath}");

            if (!Path.GetExtension(fullPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only PDF label files are allowed.");

            return Task.FromResult(fullPath);
        }

        public async Task<ManualDriverResponse> SendDriverAsync(ManualSendDriverRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var response = new ManualDriverResponse
            {
                Success = false,
                PaperSize = request.PaperSize
            };

            var printerOnline = await _printerService.TestConnectionAsync();
            if (!printerOnline)
            {
                response.Message = "Printer is offline.";
                response.Errors.Add("Printer is offline. Please check the printer connection.");
                return response;
            }

            var driverPath = ResolveDriverPath(request.PaperSize);

            if (string.IsNullOrWhiteSpace(driverPath))
            {
                response.Message = $"Driver file not found for {request.PaperSize}.";
                response.Errors.Add($"Driver file not found for {request.PaperSize} in: {_driversBasePath}");
                return response;
            }

            response.DriverFileName = Path.GetFileName(driverPath);
            response.Message = $"Sending driver {response.DriverFileName}...";

            var sent = await _printerService.SendDriverAsync(driverPath);

            if (!sent)
            {
                response.Message = $"Failed to send driver for {request.PaperSize}.";
                response.Errors.Add($"PrinterService failed to send driver: {response.DriverFileName}");
                return response;
            }

            await Task.Delay(1500);

            response.Success = true;
            response.Message = $"Driver for {request.PaperSize} sent successfully.";
            return response;
        }

        public async Task<ManualPrintResponse> PrintLabelAsync(ManualPrintLabelRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            ValidateCopies(request.Copies);

            var labelPath = await ResolveSafeLabelPathAsync(request.RelativePath);

            var response = new ManualPrintResponse
            {
                Success = false,
                FileName = Path.GetFileName(labelPath),
                RelativePath = request.RelativePath,
                CopiesRequested = request.Copies
            };

            var printerOnline = await _printerService.TestConnectionAsync();
            if (!printerOnline)
            {
                response.Message = "Printer is offline.";
                response.Errors.Add("Printer is offline. Please check the printer connection.");
                return response;
            }

            for (var i = 1; i <= request.Copies; i++)
            {
                try
                {
                    var sent = await _printerService.SendFileLprAsync(labelPath);

                    if (sent)
                    {
                        response.CopiesSent++;
                    }
                    else
                    {
                        response.Errors.Add($"Failed to send copy {i}.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Manual print failed for copy {CopyNumber}", i);
                    response.Errors.Add($"Copy {i} failed: {ex.Message}");
                }
            }

            response.Success = response.CopiesSent == response.CopiesRequested;

            response.Message = response.Success
                ? $"Manual print completed. {response.CopiesSent}/{response.CopiesRequested} copies sent."
                : $"Manual print partially completed. {response.CopiesSent}/{response.CopiesRequested} copies sent.";

            return response;
        }

        public async Task<ManualPrintResponse> PrintLabelWithDriverAsync(ManualPrintWithDriverRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            ValidateCopies(request.Copies);

            var driverResult = await SendDriverAsync(new ManualSendDriverRequest
            {
                PaperSize = request.PaperSize
            });

            if (!driverResult.Success)
            {
                return new ManualPrintResponse
                {
                    Success = false,
                    RelativePath = request.RelativePath,
                    PaperSize = request.PaperSize,
                    CopiesRequested = request.Copies,
                    CopiesSent = 0,
                    Message = driverResult.Message,
                    Errors = driverResult.Errors
                };
            }

            var printResult = await PrintLabelAsync(new ManualPrintLabelRequest
            {
                RelativePath = request.RelativePath,
                Copies = request.Copies
            });

            printResult.PaperSize = request.PaperSize;

            if (printResult.Success)
            {
                printResult.Message =
                    $"Driver sent and manual print completed. {printResult.CopiesSent}/{printResult.CopiesRequested} copies sent.";
            }

            return printResult;
        }

        private ManualLabelResponse BuildLabelResponse(string fullPath)
        {
            var info = new FileInfo(fullPath);

            var relativePath = Path.GetRelativePath(_labelsBasePath, fullPath)
                .Replace("\\", "/");

            var clientFolder = GetClientFolder(relativePath);
            var productCode = ExtractProductCode(info.Name);
            var detectedPaperSize = DetectPaperSize(info.Name, productCode);

            return new ManualLabelResponse
            {
                FileName = info.Name,
                RelativePath = relativePath,
                ClientFolder = clientFolder,
                ProductCode = productCode,
                DetectedPaperSize = detectedPaperSize?.ToString(),
                SizeBytes = info.Length,
                LastModified = info.LastWriteTime,
                PreviewUrl = $"/api/manual-printing/labels/preview?relativePath={Uri.EscapeDataString(relativePath)}"
            };
        }

        private string ResolveDriverPath(PaperSize paperSize)
        {
            var configuredFileName = _config[$"Drivers:Files:{paperSize}"];

            var candidates = new List<string>();

            if (!string.IsNullOrWhiteSpace(configuredFileName))
                candidates.Add(configuredFileName);

            candidates.Add($"{paperSize}.bak");
            candidates.Add($"{paperSize}.bal");

            if (paperSize == PaperSize.Rrethi)
            {
                candidates.Add("Etiketa Rreth.bak");
                candidates.Add("Etiketa Rrethi.bak");
                candidates.Add("Etiketa Rreth.bal");
                candidates.Add("Etiketa Rrethi.bal");
            }
            else
            {
                candidates.Add($"Etiketa {paperSize}.bak");
                candidates.Add($"Etiketa {paperSize}.bal");
            }

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var fullPath = Path.Combine(_driversBasePath, candidate);

                if (File.Exists(fullPath))
                    return fullPath;
            }

            return string.Empty;
        }

        private PaperSize? DetectPaperSize(string fileName, string? productCode)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);

            if (name.Contains("Mini", StringComparison.OrdinalIgnoreCase))
                return PaperSize.Mini;

            if (name.Contains("Rreth", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Rrethi", StringComparison.OrdinalIgnoreCase))
                return PaperSize.Rrethi;

            if (name.Contains("Prodhim", StringComparison.OrdinalIgnoreCase))
                return PaperSize.Prodhim;

            if (name.Contains("Normale", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Normal", StringComparison.OrdinalIgnoreCase))
                return PaperSize.Normale;

            if (!string.IsNullOrWhiteSpace(productCode))
            {
                var fromMap = DetectPaperSizeFromProductCode(productCode);
                if (fromMap.HasValue)
                    return fromMap.Value;
            }

            return null;
        }

        private PaperSize? DetectPaperSizeFromProductCode(string productCode)
        {
            var section = _config.GetSection("ProductSizeMap");

            foreach (var sizeSection in section.GetChildren())
            {
                if (!Enum.TryParse<PaperSize>(sizeSection.Key, ignoreCase: true, out var paperSize))
                    continue;

                var codes = sizeSection.Get<string[]>() ?? Array.Empty<string>();

                if (codes.Any(code => code.Equals(productCode, StringComparison.OrdinalIgnoreCase)))
                    return paperSize;
            }

            return null;
        }

        private string? ExtractProductCode(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var match = Regex.Match(name, @"^([A-Z0-9]+)([\s_\-]|$)", RegexOptions.IgnoreCase);

            if (!match.Success)
                return null;

            return match.Groups[1].Value.ToUpperInvariant();
        }

        private string GetClientFolder(string relativePath)
        {
            var parts = relativePath.Split(
                new[] { '/', '\\' },
                StringSplitOptions.RemoveEmptyEntries);

            return parts.Length > 1 ? parts[0] : "Root";
        }

        private void ValidateCopies(int copies)
        {
            if (copies < 1)
                throw new ArgumentException("Copies must be at least 1.", nameof(copies));

            if (copies > MaxCopiesPerManualJob)
                throw new ArgumentException($"Copies cannot exceed {MaxCopiesPerManualJob} per manual print job.", nameof(copies));
        }

        private string EnsureTrailingSlash(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar))
                return path;

            return path + Path.DirectorySeparatorChar;
        }
    }
}