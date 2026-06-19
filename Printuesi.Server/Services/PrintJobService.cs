using Microsoft.EntityFrameworkCore;
using Printuesi.Server.Data;
using Printuesi.Server.Entities;
using Printuesi.Server.Enums;
using Printuesi.Server.Helpers;

namespace Printuesi.Server.Services
{
    public class PrintJobService : PrintJobService_Interface
    {
        private readonly ApplicationDbContext _db;
        private readonly OrderScanService_Interface _orderScan;
        private readonly PdfService_Interface _pdfService;
        private readonly PrinterService_Interface _printerService;
        private readonly IConfiguration _config;
        private readonly ILogger<PrintJobService> _logger;
        private readonly IWebHostEnvironment _env;

        private readonly string _labelsBasePath;
        private readonly string _outputBasePath;
        private readonly string _driversBasePath;

        // Mini and Rrethi are problematic, so test print is mandatory for them.
        // Normale and Prodhim can also test, but it is optional.
        private static readonly PaperSize[] TestPrintRequired =
        {
            PaperSize.Mini,
            PaperSize.Rrethi
        };

        public PrintJobService(
            ApplicationDbContext db,
            OrderScanService_Interface orderScan,
            PdfService_Interface pdfService,
            PrinterService_Interface printerService,
            IConfiguration config,
            ILogger<PrintJobService> logger,
            IWebHostEnvironment env)
        {
            _db = db;
            _orderScan = orderScan;
            _pdfService = pdfService;
            _printerService = printerService;
            _config = config;
            _logger = logger;
            _env = env;

            _labelsBasePath = config["Labels:BasePath"]
                ?? Path.Combine(env.WebRootPath, "labels");

            _outputBasePath = Path.Combine(env.WebRootPath, "labels_output");

            var configuredDriverBasePath = config["Drivers:BasePath"] ?? "drivers";

            _driversBasePath = Path.IsPathRooted(configuredDriverBasePath)
                ? configuredDriverBasePath
                : Path.Combine(env.WebRootPath, configuredDriverBasePath);

            Directory.CreateDirectory(_outputBasePath);
            Directory.CreateDirectory(_driversBasePath);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Step 1: Create job from order PDF
        // ─────────────────────────────────────────────────────────────────────

        public async Task<PrintJobResponse> CreateFromOrderAsync(IFormFile orderPdf, string createdByUserID)
        {
            var scanResult = await _orderScan.ScanOrderAsync(orderPdf);

            if (!scanResult.Items.Any())
            {
                throw new InvalidOperationException(
                    "No recognizable products found in the order PDF. " +
                    $"Unrecognized codes: {string.Join(", ", scanResult.UnrecognizedCodes)}");
            }

            var client = await _db.Clients.FirstOrDefaultAsync(c =>
                c.Name.ToLower() == scanResult.ClientName.ToLower());

            if (client == null)
            {
                throw new InvalidOperationException(
                    $"Client '{scanResult.ClientName}' not found in database. " +
                    "Please create the client first.");
            }

            var productionDate = DateTime.UtcNow;
            var lotNumber = LotNumberHelper.Generate(productionDate);

            var job = new PrintJobs
            {
                PrintJobID = ID_Generator.PrintJob(),
                ClientID = client.ClientID,
                Status = Status.Pending,
                CreatedAt = productionDate,
                CompletedAt = null,
                CreatedBy = createdByUserID,
                Notes = $"Order scanned: {orderPdf.FileName}"
            };

            _db.PrintJobs.Add(job);

            foreach (var item in scanResult.Items)
            {
                var template = await _db.LabelTemplates.FirstOrDefaultAsync(t =>
                    t.ClientID == client.ClientID &&
                    t.PaperSize == item.PaperSize);

                var offsetMonths = template?.ExpiryOffsetMonths > 0
                    ? template.ExpiryOffsetMonths
                    : 12;

                var expiryDate = productionDate.AddMonths(offsetMonths);

                var dateFormat = string.IsNullOrWhiteSpace(template?.DateFormat)
                    ? "dd/MM/yyyy"
                    : template.DateFormat;

                var expiryFormatted = expiryDate.ToString(dateFormat);

                var obj = new PrintJobObjects
                {
                    PrintJobObjectID = ID_Generator.PrintJobObject(),
                    PrintJobID = job.PrintJobID,
                    ProductCode = item.ProductCode,
                    LabelID = template?.LabelID,
                    Quantity_Requested = item.Quantity,
                    Quantity_Printed = 0,
                    EXP = expiryFormatted,
                    Lot_Num = lotNumber,
                    Status = Status.Pending
                };

                _db.PrintJobObjects.Add(obj);

                _logger.LogInformation(
                    "Added {Code} × {Qty} | {Size} | Expiry: {Expiry} | Lot: {Lot}",
                    item.ProductCode,
                    item.Quantity,
                    item.PaperSize,
                    expiryFormatted,
                    lotNumber);
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "PrintJob {JobID} created for {Client}",
                job.PrintJobID,
                client.Name);

            var savedJob = await _db.PrintJobs
                .Include(j => j.PrintJobObjects)
                    .ThenInclude(o => o.LabelTemplate)
                .Include(j => j.Client)
                .FirstOrDefaultAsync(j => j.PrintJobID == job.PrintJobID);

            if (savedJob == null)
                throw new InvalidOperationException($"Created print job '{job.PrintJobID}' could not be reloaded.");

            var response = await BuildResponseFromDb(savedJob);
            response.UnrecognizedProducts = scanResult.UnrecognizedCodes;

            return response;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Step 2: Get job
        // ─────────────────────────────────────────────────────────────────────

        public async Task<PrintJobResponse?> GetPrintJobAsync(string printJobID)
        {
            var job = await _db.PrintJobs
                .Include(j => j.PrintJobObjects)
                    .ThenInclude(o => o.LabelTemplate)
                .Include(j => j.Client)
                .FirstOrDefaultAsync(j => j.PrintJobID == printJobID);

            if (job == null)
                return null;

            return await BuildResponseFromDb(job);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Step 3: User confirms paper changed
        // This sends the driver and stores current paper state.
        // It does NOT print.
        // ─────────────────────────────────────────────────────────────────────

        public async Task<PrintGroupResult> ConfirmPaperChangeAsync(string printJobID, PaperSize paperSize)
        {
            var job = await LoadJob(printJobID);

            var result = new PrintGroupResult
            {
                PaperSize = paperSize
            };

            var printerOnline = await _printerService.TestConnectionAsync();

            if (!printerOnline)
            {
                result.Success = false;
                result.Errors.Add("Printer is offline. Please check the connection.");
                result.Message = "Printer is offline.";
                return result;
            }

            await SendDriver(paperSize, result);

            if (result.Errors.Any())
            {
                result.Success = false;
                result.Message = $"Failed to prepare {paperSize} paper.";
                return result;
            }

            var printerState = await GetOrCreatePrinterState();
            printerState.CurrentPaperSize = paperSize;
            printerState.LastUpdated = DateTime.UtcNow;

            job.Status = Status.In_progress;

            _db.PrinterState.Update(printerState);
            _db.PrintJobs.Update(job);

            // If the paper is confirmed again, old test result should no longer count.
            ClearTestPrinted(printJobID, paperSize);

            await _db.SaveChangesAsync();

            result.Success = true;
            result.NeedsPaperChange = false;
            result.NeedsTestConfirm = TestPrintRequired.Contains(paperSize);

            result.Message = TestPrintRequired.Contains(paperSize)
                ? $"{paperSize} paper confirmed. Driver sent. Test print is required before starting the group."
                : $"{paperSize} paper confirmed. Driver sent. Test print is optional; you may start the group.";

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Step 4: Optional/mandatory test print
        // Mini/Rrethi: mandatory before StartGroup
        // Normale/Prodhim: optional
        // Test print does NOT count as production.
        // ─────────────────────────────────────────────────────────────────────

        public async Task<PrintGroupResult> PrintTestAsync(string printJobID, PaperSize paperSize, string? kg = null)
        {
            var job = await LoadJob(printJobID);

            var result = new PrintGroupResult
            {
                PaperSize = paperSize
            };

            if (!ValidateKgIfRequired(job, kg, result))
                return result;

            var printerOnline = await _printerService.TestConnectionAsync();

            if (!printerOnline)
            {
                result.Success = false;
                result.Errors.Add("Printer is offline. Please check the connection.");
                result.Message = "Printer is offline.";
                return result;
            }

            var printerState = await GetOrCreatePrinterState();

            if (printerState.CurrentPaperSize != paperSize)
            {
                result.Success = false;
                result.NeedsPaperChange = true;
                result.Message = $"Please confirm {paperSize} paper first.";
                return result;
            }

            var itemsForGroup = GetPendingItems(job, paperSize);

            if (!itemsForGroup.Any())
                throw new InvalidOperationException($"No pending items for {paperSize}.");

            var firstItem = itemsForGroup.First();

            var prepared = await EnsureFilledPdfAsync(job, paperSize, firstItem, result, kg);

            if (!prepared)
                return result;

            var outputPath = GetOutputPath(job.PrintJobID, paperSize, firstItem.PrintJobObjectID);

            var sent = await _printerService.SendFileLprAsync(outputPath);

            if (!sent)
            {
                result.Success = false;
                result.ItemsFailed++;
                result.Errors.Add($"Failed to send test copy for {firstItem.ProductCode}");
                result.Message = "Test print failed.";
                return result;
            }

            MarkTestPrinted(printJobID, paperSize);

            job.Status = Status.In_progress;
            _db.PrintJobs.Update(job);
            await _db.SaveChangesAsync();

            result.Success = true;
            result.ItemsPrinted = 1;
            result.NeedsPaperChange = false;
            result.NeedsTestConfirm = false;

            result.Message = TestPrintRequired.Contains(paperSize)
                ? $"Test label sent for {firstItem.ProductCode}. If alignment is good, click 'Nis grupin'."
                : $"Optional test label sent for {firstItem.ProductCode}. You may now start the group.";

            return result;
        }

        public async Task<PrintGroupResult> ReprintTestAsync(string printJobID, PaperSize paperSize, string? kg = null)
        {
            var result = await PrintTestAsync(printJobID, paperSize, kg);

            if (result.Success)
                result.Message = "Test label reprinted. If alignment is good, click 'Nis grupin'.";

            return result;
        }

        // Kept for compatibility with your existing interface/controller.
        // New frontend should use StartGroupAsync after test is printed.
        public async Task<PrintGroupResult> ConfirmTestPrintAsync(string printJobID, PaperSize paperSize, string? kg = null)
        {
            return await StartGroupAsync(printJobID, paperSize, kg);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Step 5: Start production group
        // Mini/Rrethi require a previous successful test.
        // Normale/Prodhim can start directly after paper confirmation.
        // ─────────────────────────────────────────────────────────────────────

        public async Task<PrintGroupResult> StartGroupAsync(string printJobID, PaperSize paperSize, string? kg = null)
        {
            var job = await LoadJob(printJobID);

            var result = new PrintGroupResult
            {
                PaperSize = paperSize
            };

            if (!ValidateKgIfRequired(job, kg, result))
                return result;

            var printerOnline = await _printerService.TestConnectionAsync();

            if (!printerOnline)
            {
                result.Success = false;
                result.Errors.Add("Printer is offline. Please check the connection.");
                result.Message = "Printer is offline.";
                return result;
            }

            var printerState = await GetOrCreatePrinterState();

            if (printerState.CurrentPaperSize != paperSize)
            {
                result.Success = false;
                result.NeedsPaperChange = true;
                result.Message = $"Please load {paperSize} paper and click 'Kam ndërruar letrën' first.";
                return result;
            }

            if (TestPrintRequired.Contains(paperSize) && !HasTestPrinted(printJobID, paperSize))
            {
                result.Success = false;
                result.NeedsTestConfirm = true;
                result.Message = $"{paperSize} requires a test print before starting the group.";
                return result;
            }

            return await ExecuteGroup(job, paperSize, result, kg);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Other methods
        // ─────────────────────────────────────────────────────────────────────

        public async Task<List<PrintJobResponse>> GetAllPrintJobsAsync(Status? status = null)
        {
            var query = _db.PrintJobs
                .Include(j => j.PrintJobObjects)
                    .ThenInclude(o => o.LabelTemplate)
                .Include(j => j.Client)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(j => j.Status == status.Value);

            var jobs = await query
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            var responses = new List<PrintJobResponse>();

            foreach (var job in jobs)
                responses.Add(await BuildResponseFromDb(job));

            return responses;
        }

        public async Task<bool> CancelPrintJobAsync(string printJobID)
        {
            var job = await _db.PrintJobs.FirstOrDefaultAsync(j => j.PrintJobID == printJobID);

            if (job == null)
                return false;

            if (job.Status == Status.Completed)
                throw new InvalidOperationException("Cannot cancel a completed job.");

            job.Status = Status.Cancelled;

            _db.PrintJobs.Update(job);
            await _db.SaveChangesAsync();

            CleanupJobOutput(printJobID);

            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Core execution
        // ─────────────────────────────────────────────────────────────────────

        private async Task<PrintGroupResult> ExecuteGroup(
            PrintJobs job,
            PaperSize paperSize,
            PrintGroupResult result,
            string? kg)
        {
            job.Status = Status.In_progress;
            _db.PrintJobs.Update(job);
            await _db.SaveChangesAsync();

            var itemsForGroup = GetPendingItems(job, paperSize);

            if (!itemsForGroup.Any())
                throw new InvalidOperationException($"No pending items for {paperSize}.");

            Directory.CreateDirectory(Path.Combine(_outputBasePath, job.PrintJobID, paperSize.ToString()));

            foreach (var item in itemsForGroup)
            {
                var prepared = await EnsureFilledPdfAsync(job, paperSize, item, result, kg);

                if (!prepared)
                {
                    result.ItemsFailed++;
                    continue;
                }

                var outputPath = GetOutputPath(job.PrintJobID, paperSize, item.PrintJobObjectID);

                var copiesRemaining = Math.Max(0, item.Quantity_Requested - item.Quantity_Printed);
                var sentCopies = 0;

                for (var copy = 0; copy < copiesRemaining; copy++)
                {
                    var sent = await _printerService.SendFileLprAsync(outputPath);

                    if (sent)
                    {
                        sentCopies++;
                        result.ItemsPrinted++;
                    }
                    else
                    {
                        result.Errors.Add($"Failed to send copy {copy + 1} of {item.ProductCode}");
                    }
                }

                item.Quantity_Printed += sentCopies;

                if (item.Quantity_Printed >= item.Quantity_Requested)
                {
                    item.Quantity_Printed = item.Quantity_Requested;
                    item.Status = Status.Completed;
                }

                _db.PrintJobObjects.Update(item);
                await _db.SaveChangesAsync();
            }

            await FinalizeJob(job, result);

            result.Success = result.ItemsFailed == 0 && !result.Errors.Any();

            if (result.Success && string.IsNullOrWhiteSpace(result.Message))
                result.Message = "Group printed successfully.";

            return result;
        }

        private async Task<bool> EnsureFilledPdfAsync(
        PrintJobs job,
        PaperSize paperSize,
        PrintJobObjects item,
        PrintGroupResult result,
        string? kg = null)
        {
            var outputPath = GetOutputPath(job.PrintJobID, paperSize, item.PrintJobObjectID);

            var normalizedKg = NormalizeKg(kg);

            // If KG is provided, regenerate the temporary PDF.
            // This prevents printing an old cached file with the wrong KG.
            if (File.Exists(outputPath) && string.IsNullOrWhiteSpace(normalizedKg))
                return true;

            if (File.Exists(outputPath) && !string.IsNullOrWhiteSpace(normalizedKg))
                File.Delete(outputPath);

            if (File.Exists(outputPath))
                return true;

            var templatePath = ResolveTemplatePath(job.Client.Name, item);

            if (string.IsNullOrEmpty(templatePath))
            {
                result.Success = false;
                result.Errors.Add($"Label file not found for {item.ProductCode} ({item.PrintJobObjectID})");
                result.Message = $"Label file not found for {item.ProductCode}.";
                return false;
            }

            var useWhiteText = ClientUsesWhiteText(job.Client.Name);

            var filled = await _pdfService.FillPdfPlaceholdersAsync(
                templatePath,
                new Dictionary<string, string>
                {
                    { "EXP", item.EXP },
                    { "LOT", item.Lot_Num ?? string.Empty }
                },
                outputPath,
                useWhiteText);

            if (!filled)
            {
                result.Success = false;
                result.Errors.Add($"Failed to fill PDF for {item.ProductCode}");
                result.Message = $"Failed to prepare label for {item.ProductCode}.";
                return false;
            }

            return true;
        }

        private async Task SendDriver(PaperSize paperSize, PrintGroupResult result)
        {
            var driverPath = ResolveDriverPath(paperSize);

            if (string.IsNullOrWhiteSpace(driverPath))
            {
                result.Success = false;
                result.Errors.Add($"Driver file not found for {paperSize} in '{_driversBasePath}'.");
                _logger.LogWarning("Driver file not found for {PaperSize} in {Path}", paperSize, _driversBasePath);
                return;
            }

            var sent = await _printerService.SendDriverAsync(driverPath);

            if (!sent)
            {
                result.Success = false;
                result.Errors.Add($"Failed to send driver for {paperSize}.");
                return;
            }

            await Task.Delay(1500);

            _logger.LogInformation("Driver sent for {PaperSize}: {Driver}", paperSize, driverPath);
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

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private async Task<PrintJobs> LoadJob(string printJobID)
        {
            var job = await _db.PrintJobs
                .Include(j => j.PrintJobObjects)
                    .ThenInclude(o => o.LabelTemplate)
                .Include(j => j.Client)
                .FirstOrDefaultAsync(j => j.PrintJobID == printJobID);

            if (job == null)
                throw new InvalidOperationException($"Print job '{printJobID}' not found.");

            if (job.Status == Status.Completed || job.Status == Status.Cancelled)
                throw new InvalidOperationException($"Print job is already {job.Status}.");

            return job;
        }

        private bool ClientUsesWhiteText(string clientName)
        {
            var whiteTextClients = _config.GetSection("WhiteTextClients")
                .Get<string[]>() ?? Array.Empty<string>();

            return whiteTextClients.Any(c =>
                c.Equals(clientName, StringComparison.OrdinalIgnoreCase));
        }

        private List<PrintJobObjects> GetPendingItems(PrintJobs job, PaperSize paperSize)
        {
            return job.PrintJobObjects
                .Where(o =>
                    o.Status == Status.Pending &&
                    ResolveItemPaperSize(o) == paperSize)
                .ToList();
        }

        private PaperSize ResolveItemPaperSize(PrintJobObjects item)
        {
            if (item.LabelTemplate != null)
                return item.LabelTemplate.PaperSize;

            var section = _config.GetSection("ProductSizeMap");

            foreach (var sizeSection in section.GetChildren())
            {
                if (!Enum.TryParse<PaperSize>(sizeSection.Key, ignoreCase: true, out var paperSize))
                    continue;

                var codes = sizeSection.Get<string[]>() ?? Array.Empty<string>();

                if (codes.Any(c => c.Equals(item.ProductCode, StringComparison.OrdinalIgnoreCase)))
                    return paperSize;
            }

            return PaperSize.Normale;
        }

        private string ResolveTemplatePath(string clientName, PrintJobObjects item)
        {
            var paperSize = ResolveItemPaperSize(item);

            if (item.LabelTemplate != null &&
                !string.IsNullOrWhiteSpace(item.LabelTemplate.PdfPath))
            {
                var templatePath = item.LabelTemplate.PdfPath;

                if (!Path.IsPathRooted(templatePath))
                    templatePath = Path.Combine(_labelsBasePath, templatePath);

                if (File.Exists(templatePath))
                    return templatePath;
            }

            var clientFolder = GetClientFolderName(clientName);

            var candidates = new[]
            {
                Path.Combine(_labelsBasePath, clientFolder, $"{item.ProductCode} {clientFolder} {paperSize}.pdf"),
                Path.Combine(_labelsBasePath, clientFolder, $"{item.ProductCode} {clientFolder.ToUpper()} {paperSize}.pdf"),
                Path.Combine(_labelsBasePath, clientFolder, $"{item.ProductCode} {clientFolder.ToLower()} {paperSize}.pdf")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                    return path;
            }

            _logger.LogWarning(
                "Label file not found for {Code}. Tried: {Paths}",
                item.ProductCode,
                string.Join(" | ", candidates));

            return string.Empty;
        }

        private string GetOutputPath(string printJobID, PaperSize paperSize, string itemID)
        {
            var outputDir = Path.Combine(_outputBasePath, printJobID, paperSize.ToString());

            Directory.CreateDirectory(outputDir);

            return Path.Combine(outputDir, $"{itemID}.pdf");
        }

        private async Task FinalizeJob(PrintJobs job, PrintGroupResult result)
        {
            var allItems = job.PrintJobObjects.ToList();

            var allDone = allItems.All(o =>
                o.Status == Status.Completed ||
                o.Status == Status.Cancelled);

            if (allDone)
            {
                job.Status = Status.Completed;
                job.CompletedAt = DateTime.UtcNow;
                result.JobCompleted = true;
                result.NextPaperSize = null;
                result.Message = "Print job completed.";

                CleanupJobOutput(job.PrintJobID);

                _logger.LogInformation("PrintJob {JobID} completed", job.PrintJobID);
            }
            else
            {
                var nextPaperSize = allItems
                    .Where(o => o.Status == Status.Pending)
                    .Select(o => (PaperSize?)ResolveItemPaperSize(o))
                    .Distinct()
                    .FirstOrDefault();

                result.NextPaperSize = nextPaperSize;

                if (nextPaperSize.HasValue)
                {
                    result.Message = $"Group done. Next: load {nextPaperSize} paper and click 'Kam ndërruar letrën'.";
                }
            }

            _db.PrintJobs.Update(job);
            await _db.SaveChangesAsync();
        }

        private async Task<PrinterState> GetOrCreatePrinterState()
        {
            var state = await _db.PrinterState.FirstOrDefaultAsync();

            if (state == null)
            {
                state = new PrinterState
                {
                    Id = 1,
                    CurrentPaperSize = null,
                    LastUpdated = null
                };

                _db.PrinterState.Add(state);
                await _db.SaveChangesAsync();
            }

            return state;
        }

        private void CleanupJobOutput(string printJobID)
        {
            try
            {
                var jobDir = Path.Combine(_outputBasePath, printJobID);

                if (Directory.Exists(jobDir))
                    Directory.Delete(jobDir, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Failed to clean up output for job {JobID}: {Message}",
                    printJobID,
                    ex.Message);
            }
        }

        private string GetTestMarkerPath(string printJobID, PaperSize paperSize)
        {
            var outputDir = Path.Combine(_outputBasePath, printJobID, paperSize.ToString());

            Directory.CreateDirectory(outputDir);

            return Path.Combine(outputDir, "_test_printed.marker");
        }

        private bool HasTestPrinted(string printJobID, PaperSize paperSize)
        {
            return File.Exists(GetTestMarkerPath(printJobID, paperSize));
        }

        private void MarkTestPrinted(string printJobID, PaperSize paperSize)
        {
            var markerPath = GetTestMarkerPath(printJobID, paperSize);

            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
        }

        private void ClearTestPrinted(string printJobID, PaperSize paperSize)
        {
            var markerPath = GetTestMarkerPath(printJobID, paperSize);

            if (File.Exists(markerPath))
                File.Delete(markerPath);
        }

        private string GetClientFolderName(string clientName)
        {
            return System.Globalization.CultureInfo.InvariantCulture.TextInfo
                .ToTitleCase(clientName.ToLower());
        }

        private bool ClientRequiresKg(string clientName)
        {
            var kgClients = _config.GetSection("VariableKgClients")
                .Get<string[]>() ?? Array.Empty<string>();

            return kgClients.Any(c =>
                c.Equals(clientName, StringComparison.OrdinalIgnoreCase));
        }

        private List<string> GetKgOptions()
        {
            return _config.GetSection("KgOptions")
                .Get<string[]>()?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList()
                ?? new List<string> { "0.5 kg", "1 kg", "2 kg" };
        }

        private string? NormalizeKg(string? kg)
        {
            if (string.IsNullOrWhiteSpace(kg))
                return null;

            return kg.Trim();
        }

        private bool ValidateKgIfRequired(PrintJobs job, string? kg, PrintGroupResult result)
        {
            var clientName = job.Client?.Name ?? string.Empty;

            if (!ClientRequiresKg(clientName))
                return true;

            var normalizedKg = NormalizeKg(kg);

            if (string.IsNullOrWhiteSpace(normalizedKg))
            {
                result.Success = false;
                result.Message = $"Client '{clientName}' requires KG selection.";
                result.Errors.Add("KG value is required for this client.");
                return false;
            }

            var allowed = GetKgOptions();

            if (!allowed.Any(x => x.Equals(normalizedKg, StringComparison.OrdinalIgnoreCase)))
            {
                result.Success = false;
                result.Message = $"Invalid KG value: {normalizedKg}.";
                result.Errors.Add($"Allowed KG values: {string.Join(", ", allowed)}");
                return false;
            }

            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Response builder
        // ─────────────────────────────────────────────────────────────────────

        private async Task<PrintJobResponse> BuildResponseFromDb(PrintJobs job)
        {
            var clientName = job.Client?.Name ?? string.Empty;

            var response = new PrintJobResponse
            {
                PrintJobID = job.PrintJobID,
                ClientID = job.ClientID,
                ClientName = clientName,
                Status = job.Status,
                CreatedAt = job.CreatedAt,
                CompletedAt = job.CompletedAt,
                Notes = job.Notes,
                CreatedBy = job.CreatedBy,
                RequiresKg = ClientRequiresKg(clientName),
                KgOptions = GetKgOptions()
            };

            response.Groups = job.PrintJobObjects
                .GroupBy(o => ResolveItemPaperSize(o))
                .Select(g => new PrintGroupResponse
                {
                    PaperSize = g.Key,
                    IsCompleted = g.All(o => o.Status == Status.Completed),
                    Items = g.Select(o =>
                    {
                        var paperSize = ResolveItemPaperSize(o);
                        var labelPath = ResolveTemplatePath(clientName, o);

                        return new PrintJobItemResponse
                        {
                            PrintJobObjectID = o.PrintJobObjectID,
                            ProductCode = o.ProductCode,
                            ProductName = o.ProductCode,
                            PaperSize = paperSize,
                            QuantityRequested = o.Quantity_Requested,
                            QuantityPrinted = o.Quantity_Printed,
                            ExpiryDate = o.EXP,
                            LotNumber = o.Lot_Num ?? string.Empty,
                            Status = o.Status,
                            LabelFilePath = labelPath,
                            LabelFileFound = !string.IsNullOrWhiteSpace(labelPath) && File.Exists(labelPath)
                        };
                    }).ToList()
                }).ToList();

            return await Task.FromResult(response);
        }
    }
}