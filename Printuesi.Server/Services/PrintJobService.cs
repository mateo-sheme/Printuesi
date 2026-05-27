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
        private readonly string _labelsBasePath;
        private readonly string _outputBasePath;

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

            _labelsBasePath = config["Labels:BasePath"]
                ?? Path.Combine(env.WebRootPath, "labels");

            _outputBasePath = Path.Combine(env.WebRootPath, "labels_output", "jobs");

            if (!Directory.Exists(_outputBasePath))
                Directory.CreateDirectory(_outputBasePath);
        }

        // ── Step 1: Scan order PDF and create the print job ──────────────────

        public async Task<PrintJobResponse> CreateFromOrderAsync(IFormFile orderPdf, string createdByUserID)
        {
            // 1. Scan the order PDF
            var scanResult = await _orderScan.ScanOrderAsync(orderPdf);

            if (!scanResult.Items.Any())
                throw new InvalidOperationException(
                    "No recognizable products found in the order PDF. " +
                    $"Unrecognized codes: {string.Join(", ", scanResult.UnrecognizedCodes)}");

            // 2. Resolve client from DB by name
            var client = await _db.Clients.FirstOrDefaultAsync(c =>
                c.Name.ToLower() == scanResult.ClientName.ToLower());

            if (client == null)
                throw new InvalidOperationException(
                    $"Client '{scanResult.ClientName}' not found in database. " +
                    "Please create the client first.");

            // 3. Calculate lot number and expiry date (production = today)
            var productionDate = DateTime.UtcNow;
            var lotNumber = LotNumberHelper.Generate(productionDate);

            // 4. Create the PrintJob entity
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

            // 5. Create PrintJobObject for each line item
            var jobObjects = new List<PrintJobObjects>();

            foreach (var item in scanResult.Items)
            {
                // Find the label template for this client + paper size
                var template = await _db.LabelTemplates.FirstOrDefaultAsync(t =>
                    t.ClientID == client.ClientID &&
                    t.PaperSize == item.PaperSize);

                // Calculate expiry date using template offset (default 12 months if no template)
                var offsetMonths = template?.ExpiryOffsetMonths ?? 12;
                var expiryDate = productionDate.AddMonths(offsetMonths);
                var dateFormat = template?.DateFormat ?? "MM/yyyy";
                var expiryFormatted = expiryDate.ToString(dateFormat);

                // Resolve label PDF path
                var labelPath = ResolveLabelPath(client.Name, item.ProductCode, item.PaperSize);

                var obj = new PrintJobObjects
                {
                    PrintJobObjectID = ID_Generator.PrintJobObject(),
                    PrintJobID = job.PrintJobID,
                    LabelID = template?.LabelID ?? string.Empty,
                    Quantity_Requested = item.Quantity,
                    Quantity_Printed = 0,
                    Expiry_Date = expiryFormatted,
                    Lot_Num = lotNumber,
                    Status = Status.Pending
                };

                jobObjects.Add(obj);
                _db.PrintJobObjects.Add(obj);

                _logger.LogInformation(
                    "Added item {Code} × {Qty} | {PaperSize} | Expiry: {Expiry} | Lot: {Lot} | File: {Path}",
                    item.ProductCode, item.Quantity, item.PaperSize, expiryFormatted, lotNumber, labelPath);
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation("PrintJob {JobID} created for client {Client} with {Count} items",
                job.PrintJobID, client.Name, jobObjects.Count);

            return await BuildResponse(job, client, scanResult.Items, scanResult.UnrecognizedCodes);
        }

        // ── Step 2: Get print job ─────────────────────────────────────────────

        public async Task<PrintJobResponse?> GetPrintJobAsync(string printJobID)
        {
            var job = await _db.PrintJobs
                .Include(j => j.PrintJobObjects)
                .Include(j => j.Client)
                .FirstOrDefaultAsync(j => j.PrintJobID == printJobID);

            if (job == null) return null;

            return await BuildResponseFromDb(job);
        }

        // ── Step 3: Confirm paper loaded → fill PDFs → send to printer ────────

        public async Task<PrintGroupResult> ConfirmAndPrintGroupAsync(string printJobID, PaperSize paperSize)
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

            // Get all pending items for this paper size
            // We need to cross-reference with the scanned items to know paper size per object
            // Paper size is on LabelTemplate; fall back to resolving from ProductSizeMap
            var itemsForGroup = job.PrintJobObjects
                .Where(o => o.Status == Status.Pending &&
                            o.LabelTemplate?.PaperSize == paperSize)
                .ToList();

            if (!itemsForGroup.Any())
                throw new InvalidOperationException(
                    $"No pending items found for paper size {paperSize}.");

            var result = new PrintGroupResult { PaperSize = paperSize };

            // Update job status to In_progress
            job.Status = Status.In_progress;
            _db.PrintJobs.Update(job);

            // Group items by quantity so same-copy items print together
            var byQuantity = itemsForGroup.GroupBy(o => o.Quantity_Requested);

            foreach (var group in byQuantity.OrderBy(g => g.Key))
            {
                var qty = group.Key;
                var outputDir = Path.Combine(_outputBasePath, printJobID, paperSize.ToString());
                Directory.CreateDirectory(outputDir);

                foreach (var item in group)
                {
                    try
                    {
                        // Resolve template PDF path from LabelTemplate.PdfPath
                        var templatePath = item.LabelTemplate?.PdfPath;
                        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
                        {
                            // Try resolving from labels folder directly
                            templatePath = ResolveLabelPathFromDb(job.Client.Name, item);
                        }

                        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
                        {
                            _logger.LogError("Label file not found for item {ID}", item.PrintJobObjectID);
                            result.Errors.Add($"Label file not found for item {item.PrintJobObjectID}");
                            result.ItemsFailed++;
                            continue;
                        }

                        var placeholders = new Dictionary<string, string>
                        {
                            { "EXPIRY_DATE", item.Expiry_Date },
                            { "LOT_NUMBER",  item.Lot_Num ?? string.Empty }
                        };

                        var outputPath = Path.Combine(outputDir,
                            $"{item.PrintJobObjectID}_{qty}copies.pdf");

                        // Fill placeholders
                        var filled = await _pdfService.FillPdfPlaceholdersAsync(
                            templatePath, placeholders, outputPath);

                        if (!filled)
                        {
                            result.Errors.Add($"Failed to fill PDF for item {item.PrintJobObjectID}");
                            result.ItemsFailed++;
                            continue;
                        }

                        // Send to printer N times (one per requested copy)
                        for (int copy = 0; copy < qty; copy++)
                        {
                            var sent = await _printerService.SendFileLprAsync(outputPath);
                            if (!sent)
                            {
                                result.Errors.Add($"Failed to send copy {copy + 1} of {item.PrintJobObjectID} to printer");
                            }
                        }

                        // Mark item as completed
                        item.Status = Status.Completed;
                        item.Quantity_Printed = qty;
                        _db.PrintJobObjects.Update(item);
                        result.ItemsPrinted++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing item {ID}", item.PrintJobObjectID);
                        result.Errors.Add($"Error on item {item.PrintJobObjectID}: {ex.Message}");
                        result.ItemsFailed++;
                    }
                }
            }

            // Check if all items across the whole job are now complete
            var allItems = job.PrintJobObjects.ToList();
            var allDone = allItems.All(o =>
                o.Status == Status.Completed || o.Status == Status.Cancelled);

            if (allDone)
            {
                job.Status = Status.Completed;
                job.CompletedAt = DateTime.UtcNow;
                result.JobCompleted = true;
            }
            else
            {
                // Find next paper size group that still has pending items
                var nextGroup = allItems
                    .Where(o => o.Status == Status.Pending)
                    .Select(o => o.LabelTemplate?.PaperSize)
                    .Where(s => s.HasValue)
                    .Select(s => s!.Value)
                    .Distinct()
                    .FirstOrDefault();

                result.NextPaperSize = allDone ? null : nextGroup;
            }

            _db.PrintJobs.Update(job);
            await _db.SaveChangesAsync();

            result.Success = result.ItemsFailed == 0;
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
            return true;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the label PDF path from the NFS/SMB share.
        /// Convention: {basePath}/{ClientName}/{ProductCode} {ClientName} {PaperSize}.pdf
        /// e.g. /mnt/labels/Someg/STK30 Someg Mini.pdf
        /// </summary>
        private string ResolveLabelPath(string clientName, string productCode, PaperSize paperSize)
        {
            var fileName = $"{productCode} {clientName} {paperSize}.pdf";
            return Path.Combine(_labelsBasePath, clientName, fileName);
        }

        private string ResolveLabelPathFromDb(string clientName, PrintJobObjects item)
        {
            // We don't have productCode on PrintJobObjects directly,
            // so derive from the LabelID / template name if possible
            // Fall back to empty — caller handles missing file
            return string.Empty;
        }

        private async Task<PrintJobResponse> BuildResponse(
            PrintJobs job,
            Clients client,
            List<OrderLineItem> scannedItems,
            List<string> unrecognized)
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

            // Group scanned items by paper size for the response
            var groups = scannedItems
                .GroupBy(i => i.PaperSize)
                .Select(g => new PrintGroupResponse
                {
                    PaperSize = g.Key,
                    IsCompleted = false,
                    Items = g.Select(i =>
                    {
                        var labelPath = ResolveLabelPath(client.Name, i.ProductCode, i.PaperSize);
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

            response.Groups = groups;
            return response;
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

            // Group job objects by paper size via their template
            var groups = job.PrintJobObjects
                .GroupBy(o => o.LabelTemplate?.PaperSize ?? PaperSize.Normale)
                .Select(g => new PrintGroupResponse
                {
                    PaperSize = g.Key,
                    IsCompleted = g.All(o => o.Status == Status.Completed),
                    Items = g.Select(o => new PrintJobItemResponse
                    {
                        PrintJobObjectID = o.PrintJobObjectID,
                        PaperSize = o.LabelTemplate?.PaperSize ?? PaperSize.Normale,
                        QuantityRequested = o.Quantity_Requested,
                        QuantityPrinted = o.Quantity_Printed,
                        ExpiryDate = o.Expiry_Date,
                        LotNumber = o.Lot_Num ?? string.Empty,
                        Status = o.Status
                    }).ToList()
                }).ToList();

            response.Groups = groups;
            return response;
        }
    }
}