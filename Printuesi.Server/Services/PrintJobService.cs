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

        // Paper sizes that require a test print before batch
        private static readonly PaperSize[] TestPrintRequired = { PaperSize.Mini, PaperSize.Rrethi };

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
            _driversBasePath = Path.Combine(env.WebRootPath, "drivers");

            Directory.CreateDirectory(_outputBasePath);
            Directory.CreateDirectory(_driversBasePath);
        }

        // ── Step 1: Create job from order PDF ────────────────────────────────

        public async Task<PrintJobResponse> CreateFromOrderAsync(IFormFile orderPdf, string createdByUserID)
        {
            var scanResult = await _orderScan.ScanOrderAsync(orderPdf);

            if (!scanResult.Items.Any())
                throw new InvalidOperationException(
                    "No recognizable products found in the order PDF. " +
                    $"Unrecognized codes: {string.Join(", ", scanResult.UnrecognizedCodes)}");

            var client = await _db.Clients.FirstOrDefaultAsync(c =>
                c.Name.ToLower() == scanResult.ClientName.ToLower());

            if (client == null)
                throw new InvalidOperationException(
                    $"Client '{scanResult.ClientName}' not found in database. " +
                    "Please create the client first.");

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

            var jobObjects = new List<PrintJobObjects>();

            foreach (var item in scanResult.Items)
            {
                var template = await _db.LabelTemplates.FirstOrDefaultAsync(t =>
                    t.ClientID == client.ClientID &&
                    t.PaperSize == item.PaperSize);

                var offsetMonths = template?.ExpiryOffsetMonths ?? 12;
                var expiryDate = productionDate.AddMonths(offsetMonths);
                var dateFormat = template?.DateFormat ?? "MM/yyyy";
                var expiryFormatted = expiryDate.ToString(dateFormat);

                var obj = new PrintJobObjects
                {
                    PrintJobObjectID = ID_Generator.PrintJobObject(),
                    PrintJobID = job.PrintJobID,
                    ProductCode = item.ProductCode,
                    LabelID = template?.LabelID ?? null,
                    Quantity_Requested = item.Quantity,
                    Quantity_Printed = 0,
                    Expiry_Date = expiryFormatted,
                    Lot_Num = lotNumber,
                    Status = Status.Pending
                };

                jobObjects.Add(obj);
                _db.PrintJobObjects.Add(obj);

                _logger.LogInformation(
                    "Added {Code} × {Qty} | {Size} | Expiry: {Expiry} | Lot: {Lot}",
                    item.ProductCode, item.Quantity, item.PaperSize, expiryFormatted, lotNumber);
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation("PrintJob {JobID} created for {Client} with {Count} items",
                job.PrintJobID, client.Name, jobObjects.Count);

            return await BuildResponse(job, client, scanResult.Items, scanResult.UnrecognizedCodes);
        }

        // ── Step 2: Get job ───────────────────────────────────────────────────

        public async Task<PrintJobResponse?> GetPrintJobAsync(string printJobID)
        {
            var job = await _db.PrintJobs
                .Include(j => j.PrintJobObjects)
                .Include(j => j.Client)
                .FirstOrDefaultAsync(j => j.PrintJobID == printJobID);

            if (job == null) return null;
            return await BuildResponseFromDb(job);
        }

        // ── Step 3: Start a paper size group ─────────────────────────────────
        // Logic:
        // 1. Check printer is online
        // 2. Check if paper change needed
        //    - If yes: return NeedsPaperChange = true (frontend prompts user)
        //    - If no: continue
        // 3. Send driver file for this paper size
        // 4. If Mini or Rrethi: send 1 test copy, return NeedsTestConfirm = true
        // 5. If Normale or Prodhim: print all copies directly

        public async Task<PrintGroupResult> StartGroupAsync(string printJobID, PaperSize paperSize)
        {
            var job = await LoadJob(printJobID);
            var result = new PrintGroupResult { PaperSize = paperSize };

            // Check printer is online
            var printerOnline = await _printerService.TestConnectionAsync();
            if (!printerOnline)
            {
                result.Success = false;
                result.Errors.Add("Printer is offline. Please check the connection.");
                return result;
            }

            // Check current paper state
            var printerState = await GetOrCreatePrinterState();
            var needsPaperChange = printerState.CurrentPaperSize != paperSize;

            if (needsPaperChange)
            {
                // Send driver first, then ask user to change paper
                await SendDriver(paperSize, result);

                if (result.Errors.Any())
                    return result; // driver send failed

                // Update printer state
                printerState.CurrentPaperSize = paperSize;
                printerState.LastUpdated = DateTime.UtcNow;
                _db.PrinterState.Update(printerState);
                await _db.SaveChangesAsync();

                result.NeedsPaperChange = true;
                result.Success = true;
                result.Message = $"Please load {paperSize} paper and confirm when ready.";
                return result;
            }

            // Same paper already loaded — proceed directly
            return await ExecuteGroup(job, paperSize, result);
        }

        // ── Step 4a: User confirmed paper changed ────────────────────────────

        public async Task<PrintGroupResult> ConfirmPaperChangeAsync(string printJobID, PaperSize paperSize)
        {
            var job = await LoadJob(printJobID);
            var result = new PrintGroupResult { PaperSize = paperSize };

            // Update printer state
            var printerState = await GetOrCreatePrinterState();
            printerState.CurrentPaperSize = paperSize;
            printerState.LastUpdated = DateTime.UtcNow;
            _db.PrinterState.Update(printerState);
            await _db.SaveChangesAsync();

            return await ExecuteGroup(job, paperSize, result);
        }

        // ── Step 4b: User confirmed test print is ok ─────────────────────────

        public async Task<PrintGroupResult> ConfirmTestPrintAsync(string printJobID, PaperSize paperSize)
        {
            var job = await LoadJob(printJobID);
            var result = new PrintGroupResult { PaperSize = paperSize };

            var itemsForGroup = GetPendingItems(job, paperSize);

            if (!itemsForGroup.Any())
                throw new InvalidOperationException($"No pending items for {paperSize}.");

            var firstItem = itemsForGroup.First();

            foreach (var item in itemsForGroup)
            {
                var outputPath = GetOutputPath(printJobID, paperSize, item.PrintJobObjectID);

                if (!File.Exists(outputPath))
                {
                    result.Errors.Add($"Filled PDF not found for {item.PrintJobObjectID} — re-filling.");

                    var templatePath = ResolveTemplatePath(job.Client.Name, item);
                    if (string.IsNullOrEmpty(templatePath))
                    {
                        result.Errors.Add($"Label file not found for {item.PrintJobObjectID}");
                        result.ItemsFailed++;
                        continue;
                    }

                    var useWhiteText = ClientUsesWhiteText(job.Client.Name);
                    var filled = await _pdfService.FillPdfPlaceholdersAsync(
                        templatePath,
                        new Dictionary<string, string>
                        {
                    { "EXPIRY_DATE", item.Expiry_Date },
                    { "LOT_NUMBER", item.Lot_Num ?? string.Empty }
                        },
                        outputPath,
                        useWhiteText);

                    if (!filled)
                    {
                        result.Errors.Add($"Failed to fill PDF for {item.PrintJobObjectID}");
                        result.ItemsFailed++;
                        continue;
                    }
                }

                // First item: test copy already counts as 1 production copy → send qty - 1
                // All other items: send full quantity
                var copiesToPrint = item.PrintJobObjectID == firstItem.PrintJobObjectID
                    ? item.Quantity_Requested - 1
                    : item.Quantity_Requested;

                for (int i = 0; i < copiesToPrint; i++)
                {
                    var sent = await _printerService.SendFileLprAsync(outputPath);
                    if (!sent)
                        result.Errors.Add($"Failed to send copy {i + 1} of {item.ProductCode}");
                }

                item.Status = Status.Completed;
                item.Quantity_Printed = item.Quantity_Requested;
                _db.PrintJobObjects.Update(item);
                result.ItemsPrinted++;
            }

            await FinalizeJob(job, result);
            result.Success = result.ItemsFailed == 0;
            return result;
        }

        // ── Step 4c: User said test print NOT ok → reprint 1 test copy ───────

        public async Task<PrintGroupResult> ReprintTestAsync(string printJobID, PaperSize paperSize)
        {
            var job = await LoadJob(printJobID);
            var result = new PrintGroupResult { PaperSize = paperSize };

            var itemsForGroup = GetPendingItems(job, paperSize);

            if (!itemsForGroup.Any())
                throw new InvalidOperationException($"No pending items for {paperSize}.");

            // Send 1 copy of each item again
            foreach (var item in itemsForGroup)
            {
                var firstItem = itemsForGroup.First();
                var outputPath = GetOutputPath(printJobID, paperSize, firstItem.PrintJobObjectID);
                var useWhiteText = ClientUsesWhiteText(job.Client.Name);

                if (!File.Exists(outputPath))
                {
                    result.Errors.Add($"Filled PDF not found for reprint — re-filling {item.PrintJobObjectID}");
                    var templatePath = ResolveTemplatePath(job.Client.Name, item);
                    if (string.IsNullOrEmpty(templatePath))
                    {
                        result.ItemsFailed++;
                        continue;
                    }

                    await _pdfService.FillPdfPlaceholdersAsync(
                        templatePath,
                        new Dictionary<string, string>
                        {
                            { "EXPIRY_DATE", item.Expiry_Date },
                            { "LOT_NUMBER", item.Lot_Num ?? string.Empty }
                        },
                        outputPath,
                        useWhiteText);
                }

                var sent = await _printerService.SendFileLprAsync(outputPath);
                if (!sent)
                    result.Errors.Add($"Failed to send retest for {firstItem.PrintJobObjectID}");
                else
                    result.ItemsPrinted++;
            }

            // Still waiting for test confirmation
            result.NeedsTestConfirm = true;
            result.Success = result.ItemsFailed == 0;
            result.Message = "Test copy reprinted. Please confirm alignment is correct, or cancel the job.";
            return result;
        }

        // ── Other methods ─────────────────────────────────────────────────────

        public async Task<List<PrintJobResponse>> GetAllPrintJobsAsync(Status? status = null)
        {
            var query = _db.PrintJobs
                .Include(j => j.PrintJobObjects)
                .Include(j => j.Client)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(j => j.Status == status.Value);

            var jobs = await query.OrderByDescending(j => j.CreatedAt).ToListAsync();
            var responses = new List<PrintJobResponse>();

            foreach (var job in jobs)
                responses.Add(await BuildResponseFromDb(job));

            return responses;
        }

        public async Task<bool> CancelPrintJobAsync(string printJobID)
        {
            var job = await _db.PrintJobs.FirstOrDefaultAsync(j => j.PrintJobID == printJobID);
            if (job == null) return false;

            if (job.Status == Status.Completed)
                throw new InvalidOperationException("Cannot cancel a completed job.");

            job.Status = Status.Cancelled;
            _db.PrintJobs.Update(job);
            await _db.SaveChangesAsync();

            // Clean up output files
            CleanupJobOutput(printJobID);
            return true;
        }

        // ── Core execution ────────────────────────────────────────────────────

        /// <summary>
        /// Executes a group — fills PDFs, sends test or full batch depending on paper size.
        /// </summary>
        private async Task<PrintGroupResult> ExecuteGroup(
            PrintJobs job, PaperSize paperSize, PrintGroupResult result)
        {
            job.Status = Status.In_progress;
            _db.PrintJobs.Update(job);
            await _db.SaveChangesAsync();

            var itemsForGroup = GetPendingItems(job, paperSize);

            if (!itemsForGroup.Any())
                throw new InvalidOperationException($"No pending items for {paperSize}.");

            var outputDir = Path.Combine(_outputBasePath, job.PrintJobID, paperSize.ToString());
            Directory.CreateDirectory(outputDir);

            // Fill all PDFs first
            foreach (var item in itemsForGroup)
            {
                var templatePath = ResolveTemplatePath(job.Client.Name, item);
                if (string.IsNullOrEmpty(templatePath))
                {
                    result.Errors.Add($"Label file not found for {item.ProductCode} ({item.PrintJobObjectID})");
                    result.ItemsFailed++;
                    continue;
                }

                var outputPath = GetOutputPath(job.PrintJobID, paperSize, item.PrintJobObjectID);
                var useWhiteText = ClientUsesWhiteText(job.Client.Name);

                var filled = await _pdfService.FillPdfPlaceholdersAsync(
                    templatePath,
                    new Dictionary<string, string>
                    {
                        { "EXPIRY_DATE", item.Expiry_Date },
                        { "LOT_NUMBER", item.Lot_Num ?? string.Empty }
                    },
                    outputPath,
                    useWhiteText);

                if (!filled)
                {
                    result.Errors.Add($"Failed to fill PDF for {item.ProductCode}");
                    result.ItemsFailed++;
                }
            }

            if (result.ItemsFailed > 0)
            {
                result.Success = false;
                return result;
            }

            // Mini and Rrethi: send 1 test copy per item, wait for confirmation
            if (TestPrintRequired.Contains(paperSize))
            {
                var firstItem = itemsForGroup.First();
                var outputPath = GetOutputPath(job.PrintJobID, paperSize, firstItem.PrintJobObjectID);

                var sent = await _printerService.SendFileLprAsync(outputPath);
                if (!sent)
                    result.Errors.Add($"Failed to send test copy for {firstItem.ProductCode}");
                else
                    result.ItemsPrinted++;

                result.NeedsTestConfirm = true;
                result.Success = result.Errors.Count == 0;
                result.Message = $"Test copy of {firstItem.ProductCode} sent. Please confirm alignment is correct.";
                return result;
            }

            // Normale and Prodhim: print all copies directly
            foreach (var item in itemsForGroup)
            {
                var outputPath = GetOutputPath(job.PrintJobID, paperSize, item.PrintJobObjectID);
                if (!File.Exists(outputPath)) continue;

                for (int copy = 0; copy < item.Quantity_Requested; copy++)
                {
                    var sent = await _printerService.SendFileLprAsync(outputPath);
                    if (!sent)
                        result.Errors.Add($"Failed to send copy {copy + 1} of {item.ProductCode}");
                }

                item.Status = Status.Completed;
                item.Quantity_Printed = item.Quantity_Requested;
                _db.PrintJobObjects.Update(item);
                result.ItemsPrinted++;
            }

            await FinalizeJob(job, result);
            result.Success = result.ItemsFailed == 0;
            return result;
        }

        private async Task SendDriver(PaperSize paperSize, PrintGroupResult result)
        {
            var driverPath = Path.Combine(_driversBasePath, $"{paperSize}.bak");

            if (!File.Exists(driverPath))
            {
                _logger.LogWarning("Driver file not found for {PaperSize} at {Path} — continuing without it",
                    paperSize, driverPath);
                return;
            }

            var sent = await _printerService.SendDriverAsync(driverPath);
            if (!sent)
            {
                result.Errors.Add($"Failed to send driver for {paperSize}.");
                result.Success = false;
            }
            else
            {
                await Task.Delay(1500); // give printer time to apply settings
                _logger.LogInformation("Driver sent for {PaperSize}", paperSize);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

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
                .Where(o => o.Status == Status.Pending &&
                            (o.LabelTemplate?.PaperSize == paperSize ||
                             ResolveItemPaperSize(o) == paperSize))
                .ToList();
        }

        /// <summary>
        /// Resolves paper size for an item — from template first, then from ProductSizeMap via config.
        /// </summary>
        private PaperSize ResolveItemPaperSize(PrintJobObjects item)
        {
            if (item.LabelTemplate != null)
                return item.LabelTemplate.PaperSize;

            // Fall back to config lookup by product code
            var section = _config.GetSection("ProductSizeMap");
            foreach (var sizeSection in section.GetChildren())
            {
                if (!Enum.TryParse<PaperSize>(sizeSection.Key, true, out var paperSize))
                    continue;
                var codes = sizeSection.Get<string[]>() ?? Array.Empty<string>();
                if (codes.Any(c => c.Equals(item.ProductCode, StringComparison.OrdinalIgnoreCase)))
                    return paperSize;
            }

            return PaperSize.Normale; // safe default
        }

        /// <summary>
        /// Resolves the label PDF path.
        /// Priority: LabelTemplate.PdfPath → NFS share convention
        /// </summary>
        private string ResolveTemplatePath(string clientName, PrintJobObjects item)
        {
            var paperSize = ResolveItemPaperSize(item);
            var clientFolder = GetClientFolderName(clientName);
            var fileName = $"{item.ProductCode} {clientFolder.ToUpper()} {paperSize}.pdf";
            var path = Path.Combine(_labelsBasePath, clientFolder, fileName);

            if (File.Exists(path))
                return path;

            _logger.LogWarning("Label file not found at: {Path}", path);
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
                o.Status == Status.Completed || o.Status == Status.Cancelled);

            if (allDone)
            {
                job.Status = Status.Completed;
                job.CompletedAt = DateTime.UtcNow;
                result.JobCompleted = true;
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
                result.Message = nextPaperSize.HasValue
                    ? $"Group done. Next: load {nextPaperSize} paper and call start-group."
                    : null;
            }

            _db.PrintJobs.Update(job);
            await _db.SaveChangesAsync();
        }

        private async Task<PrinterState> GetOrCreatePrinterState()
        {
            var state = await _db.PrinterState.FirstOrDefaultAsync();
            if (state == null)
            {
                state = new PrinterState { Id = 1, CurrentPaperSize = null, LastUpdated = null };
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
                _logger.LogWarning("Failed to clean up output for job {JobID}: {Message}",
                    printJobID, ex.Message);
            }
        }

        // ── Response builders ─────────────────────────────────────────────────

        private async Task<PrintJobResponse> BuildResponse(
            PrintJobs job, Clients client,
            List<OrderLineItem> scannedItems, List<string> unrecognized)
        {
            var response = new PrintJobResponse
            {
                PrintJobID = job.PrintJobID,
                ClientID = client.ClientID,
                ClientName = client.Name,
                Status = job.Status,
                CreatedAt = job.CreatedAt,
                CompletedAt = job.CompletedAt,
                Notes = job.Notes,
                CreatedBy = job.CreatedBy,
                UnrecognizedProducts = unrecognized
            };

            response.Groups = scannedItems
                .GroupBy(i => i.PaperSize)
                .Select(g => new PrintGroupResponse
                {
                    PaperSize = g.Key,
                    IsCompleted = false,
                    Items = g.Select(i =>
                    {
                        var clientFolder = GetClientFolderName(client.Name);
                        var labelPath = Path.Combine(_labelsBasePath, clientFolder,
                            $"{i.ProductCode} {clientFolder.ToUpper()} {i.PaperSize}.pdf");
                        return new PrintJobItemResponse
                        {
                            ProductCode = i.ProductCode,
                            ProductName = i.ProductName,
                            PaperSize = i.PaperSize,
                            QuantityRequested = i.Quantity,
                            QuantityPrinted = 0,
                            Status = Status.Pending,
                            LabelFilePath = labelPath,
                            LabelFileFound = File.Exists(labelPath)
                        };
                    }).ToList()
                }).ToList();

            return response;
        }

        private string GetClientFolderName(string clientName)
        {
            return System.Globalization.CultureInfo.InvariantCulture.TextInfo
                .ToTitleCase(clientName.ToLower());
        }

        private async Task<PrintJobResponse> BuildResponseFromDb(PrintJobs job)
        {
            var response = new PrintJobResponse
            {
                PrintJobID = job.PrintJobID,
                ClientID = job.ClientID,
                ClientName = job.Client?.Name ?? string.Empty,
                Status = job.Status,
                CreatedAt = job.CreatedAt,
                CompletedAt = job.CompletedAt,
                Notes = job.Notes,
                CreatedBy = job.CreatedBy
            };

            response.Groups = job.PrintJobObjects
                .GroupBy(o => ResolveItemPaperSize(o))
                .Select(g => new PrintGroupResponse
                {
                    PaperSize = g.Key,
                    IsCompleted = g.All(o => o.Status == Status.Completed),
                    Items = g.Select(o => new PrintJobItemResponse
                    {
                        PrintJobObjectID = o.PrintJobObjectID,
                        ProductCode = o.ProductCode,
                        PaperSize = ResolveItemPaperSize(o),
                        QuantityRequested = o.Quantity_Requested,
                        QuantityPrinted = o.Quantity_Printed,
                        ExpiryDate = o.Expiry_Date,
                        LotNumber = o.Lot_Num ?? string.Empty,
                        Status = o.Status
                    }).ToList()
                }).ToList();

            return response;
        }
    }
}