using Printuesi.Server.Enums;
using Printuesi.Server.Helpers;
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
        private readonly PdfService_Interface _pdfService;
        private readonly string _outputBasePath;

        public ManualPrintingService(
    IConfiguration config,
    IWebHostEnvironment env,
    PrinterService_Interface printerService,
    PdfService_Interface pdfService,
    ILogger<ManualPrintingService> logger)
        {
            _config = config;
            _env = env;
            _printerService = printerService;
            _pdfService = pdfService;
            _logger = logger;

            _labelsBasePath = config["Labels:BasePath"]
                ?? Path.Combine(env.WebRootPath, "labels");

            var configuredDriverBasePath = config["Drivers:BasePath"] ?? "drivers";

            _driversBasePath = Path.IsPathRooted(configuredDriverBasePath)
                ? configuredDriverBasePath
                : Path.Combine(env.WebRootPath, configuredDriverBasePath);

            _outputBasePath = Path.Combine(env.WebRootPath, "labels_output", "manual");
            Directory.CreateDirectory(_outputBasePath);
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

            _logger.LogInformation(
                "ManualPrintingService.SendDriverAsync started. PaperSize: {PaperSize}, DriversBasePath: {DriversBasePath}",
                request.PaperSize,
                _driversBasePath);

            var response = new ManualDriverResponse
            {
                Success = false,
                PaperSize = request.PaperSize
            };

            var printerOnline = await _printerService.TestConnectionAsync();

            _logger.LogInformation("Printer online check result: {PrinterOnline}", printerOnline);

            if (!printerOnline)
            {
                response.Message = "Printer is offline.";
                response.Errors.Add("Printer is offline. Please check the printer connection.");
                return response;
            }

            var driverPath = ResolveDriverPath(request.PaperSize);

            _logger.LogInformation(
                "Resolved driver path for {PaperSize}: {DriverPath}",
                request.PaperSize,
                string.IsNullOrWhiteSpace(driverPath) ? "(not found)" : driverPath);

            if (string.IsNullOrWhiteSpace(driverPath))
            {
                response.Message = $"Driver file not found for {request.PaperSize}.";
                response.Errors.Add($"Driver file not found for {request.PaperSize} in: {_driversBasePath}");
                return response;
            }

            response.DriverFileName = Path.GetFileName(driverPath);
            response.Message = $"Sending driver {response.DriverFileName}...";

            var sent = await _printerService.SendDriverAsync(driverPath);

            _logger.LogInformation(
                "PrinterService.SendDriverAsync result for {DriverFileName}: {Sent}",
                response.DriverFileName,
                sent);

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
		private string GetManualOutputPath(string templatePath)
		{
			var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(templatePath);

			var safeName = new string(fileNameWithoutExtension.Select(ch =>
				char.IsLetterOrDigit(ch) ||
				ch == '-' ||
				ch == '_'
					? ch
					: '_').ToArray());

			// Local date folder: labels_output/manual/2026-06-19/
			var dayFolderName = DateTime.Now.ToString("yyyy-MM-dd");
			var dayFolderPath = Path.Combine(_outputBasePath, dayFolderName);

			Directory.CreateDirectory(dayFolderPath);

			var outputName =
				$"{safeName}_{DateTime.Now:HHmmssfff}_{Guid.NewGuid():N}.pdf";

			return Path.Combine(dayFolderPath, outputName);
		}

		private bool ClientUsesWhiteText(string clientName)
        {
            var whiteTextClients = _config.GetSection("WhiteTextClients")
                .Get<string[]>() ?? Array.Empty<string>();

            return whiteTextClients.Any(c =>
                c.Equals(clientName, StringComparison.OrdinalIgnoreCase));
        }
        public async Task<ManualPrintResponse> PrintLabelAsync(ManualPrintLabelRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            ValidateCopies(request.Copies);

            if (string.IsNullOrWhiteSpace(request.RelativePath))
                throw new ArgumentException("RelativePath is required.", nameof(request.RelativePath));

            var templatePath = await ResolveSafeLabelPathAsync(request.RelativePath);

            var paperSize =
                request.PaperSize ??
                DetectPaperSize(Path.GetFileName(templatePath), ExtractProductCode(Path.GetFileName(templatePath))) ??
                PaperSize.Normale;

            var productionDate = DateTime.UtcNow;

            var dateFormat = string.IsNullOrWhiteSpace(request.DateFormat)
                ? "dd/MM/yyyy"
                : request.DateFormat.Trim();

            var expiryOffsetMonths = request.ExpiryOffsetMonths.GetValueOrDefault(12);

            var expiryDate = !string.IsNullOrWhiteSpace(request.ExpiryDate)
                ? request.ExpiryDate.Trim()
                : productionDate.AddMonths(expiryOffsetMonths).ToString(dateFormat);

            var lotNumber = !string.IsNullOrWhiteSpace(request.LotNumber)
                ? request.LotNumber.Trim()
                : LotNumberHelper.Generate(productionDate);

            var kg = request.Kg?.Trim() ?? string.Empty;

            var outputPath = GetManualOutputPath(templatePath);

            var placeholders = new Dictionary<string, string>
            {
                { "EXP", expiryDate },
                { "LOT", lotNumber }
            };

            if (!string.IsNullOrWhiteSpace(kg))
                placeholders["KG"] = kg;

            var clientFolder = GetClientFolder(request.RelativePath);
            var useWhiteText = ClientUsesWhiteText(clientFolder);

            var response = new ManualPrintResponse
            {
                Success = false,
                FileName = Path.GetFileName(templatePath),
                RelativePath = request.RelativePath,
                PaperSize = paperSize,
                CopiesRequested = request.Copies,
                CopiesSent = 0,
                ExpiryDate = expiryDate,
                LotNumber = lotNumber,
                Kg = kg
            };

            _logger.LogInformation(
                "Manual print preparing filled PDF. Template: {Template}, Output: {Output}, PaperSize: {PaperSize}, Copies: {Copies}, Expiry: {Expiry}, Lot: {Lot}, KG: {Kg}",
                templatePath,
                outputPath,
                paperSize,
                request.Copies,
                expiryDate,
                lotNumber,
                kg);

            var filled = await _pdfService.FillPdfPlaceholdersAsync(
                templatePath,
                placeholders,
                outputPath,
                useWhiteText);

            if (!filled)
            {
                response.Message = "Failed to fill PDF placeholders.";
                response.Errors.Add("PDF placeholder replacement failed.");
                return response;
            }

            var printed = await _printerService.PrintPdfWithCupsAsync(
                outputPath,
                request.Copies,
                paperSize);

            if (!printed)
            {
                response.Message = "Filled PDF was created, but CUPS printing failed.";
                response.Errors.Add("CUPS print failed. Check backend logs and Linux VM CUPS logs.");
                return response;
            }

            response.Success = true;
            response.CopiesSent = request.Copies;
            response.Message = $"Manual print completed through CUPS. {request.Copies}/{request.Copies} copies sent.";

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
                Copies = request.Copies,
                PaperSize = request.PaperSize,
                Kg = request.Kg,
                ExpiryDate = request.ExpiryDate,
                LotNumber = request.LotNumber,
                DateFormat = request.DateFormat,
                ExpiryOffsetMonths = request.ExpiryOffsetMonths
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

		public async Task<ManualTestLabelResponse> TestLabelAsync(ManualTestLabelRequest request)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));

			if (string.IsNullOrWhiteSpace(request.RelativePath))
				throw new ArgumentException("RelativePath is required.", nameof(request.RelativePath));

			var templatePath = await ResolveSafeLabelPathAsync(request.RelativePath);

			var paperSize =
				request.PaperSize ??
				DetectPaperSize(Path.GetFileName(templatePath), ExtractProductCode(Path.GetFileName(templatePath))) ??
				PaperSize.Normale;

			var productionDate = DateTime.UtcNow;

			var dateFormat = string.IsNullOrWhiteSpace(request.DateFormat)
				? "dd/MM/yyyy"
				: request.DateFormat.Trim();

			var expiryOffsetMonths = request.ExpiryOffsetMonths.GetValueOrDefault(12);

			var expiryDate = !string.IsNullOrWhiteSpace(request.ExpiryDate)
				? request.ExpiryDate.Trim()
				: productionDate.AddMonths(expiryOffsetMonths).ToString(dateFormat);

			var lotNumber = !string.IsNullOrWhiteSpace(request.LotNumber)
				? request.LotNumber.Trim()
				: LotNumberHelper.Generate(productionDate);

			var kg = request.Kg?.Trim() ?? string.Empty;

			var outputPath = GetManualOutputPath(templatePath);

			var placeholders = new Dictionary<string, string>
	{
		{ "EXP", expiryDate },
		{ "LOT", lotNumber }
	};

			if (!string.IsNullOrWhiteSpace(kg))
				placeholders["KG"] = kg;

			var clientFolder = GetClientFolder(request.RelativePath);
			var useWhiteText = ClientUsesWhiteText(clientFolder);

			var response = new ManualTestLabelResponse
			{
				Success = false,
				FileName = Path.GetFileName(templatePath),
				TemplateRelativePath = request.RelativePath,
				PaperSize = paperSize,
				ExpiryDate = expiryDate,
				LotNumber = lotNumber,
				Kg = kg
			};

			_logger.LogInformation(
				"Admin label test preparing PDF. Template: {Template}, Output: {Output}, PaperSize: {PaperSize}, Expiry: {Expiry}, Lot: {Lot}, KG: {Kg}",
				templatePath,
				outputPath,
				paperSize,
				expiryDate,
				lotNumber,
				kg);

			var filled = await _pdfService.FillPdfPlaceholdersAsync(
				templatePath,
				placeholders,
				outputPath,
				useWhiteText);

			if (!filled)
			{
				response.Message = "Template missing required placeholders: {{EXP}} and/or {{LOT}}. Test PDF was not generated.";
				response.Errors.Add("Required placeholder not found in PDF template.");
				return response;
			}

			var generatedRelativePath = Path
				.GetRelativePath(_outputBasePath, outputPath)
				.Replace('\\', '/');

			response.Success = true;
			response.GeneratedRelativePath = generatedRelativePath;
			response.PreviewUrl =
				$"/api/manual-printing/test-label/preview?relativePath={Uri.EscapeDataString(generatedRelativePath)}";
			response.Message = "Test label generated successfully. No print was sent.";

			return response;
		}

		public Task<string> ResolveSafeGeneratedOutputPathAsync(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("relativePath is required.", nameof(relativePath));

			var normalizedRelative = relativePath
				.Replace('\\', Path.DirectorySeparatorChar)
				.Replace('/', Path.DirectorySeparatorChar);

			var basePath = Path.GetFullPath(_outputBasePath);
			var fullPath = Path.GetFullPath(Path.Combine(basePath, normalizedRelative));

			if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
				throw new InvalidOperationException("Invalid generated file path.");

			if (!File.Exists(fullPath))
				throw new FileNotFoundException($"Generated test PDF not found: {relativePath}");

			return Task.FromResult(fullPath);
		}
	}
}