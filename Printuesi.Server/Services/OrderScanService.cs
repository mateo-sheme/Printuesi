using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using Printuesi.Server.Enums;
using System.Text.RegularExpressions;

namespace Printuesi.Server.Services
{
    public class OrderScanService : OrderScanService_Interface
    {
        private readonly ILogger<OrderScanService> _logger;
        private readonly IConfiguration _config;

        private readonly Dictionary<string, PaperSize> _productSizeMap;

        public OrderScanService(ILogger<OrderScanService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _productSizeMap = BuildProductSizeMap();
        }

        public async Task<OrderScanResult> ScanOrderAsync(IFormFile orderPdf)
        {
            if (orderPdf == null || orderPdf.Length == 0)
                throw new ArgumentException("Order PDF is required.");

            var tempPath = Path.Combine(Path.GetTempPath(), $"order_{Guid.NewGuid()}.pdf");
            try
            {
                await using (var fs = new FileStream(tempPath, FileMode.Create))
                    await orderPdf.CopyToAsync(fs);

                return await Task.Run(() => ParsePdf(tempPath));
            }
            finally
            {
                try { File.Delete(tempPath); } catch { }
            }
        }

        private OrderScanResult ParsePdf(string pdfPath)
        {
            var result = new OrderScanResult();
            var fullText = ExtractText(pdfPath);

            _logger.LogInformation("Extracted order PDF text:\n{Text}", fullText);

            result.ClientNameRaw = ExtractClientName(fullText);
            result.ClientName = NormalizeClientName(result.ClientNameRaw);
            result.OrderDate = DateTime.UtcNow;
            result.Items = ExtractLineItems(fullText, result);

            _logger.LogInformation("Parsed order — Client: '{Client}', Items: {Count}",
                result.ClientName, result.Items.Count);

            return result;
        }

        private string ExtractText(string pdfPath)
        {
            using var reader = new PdfReader(pdfPath);
            using var doc = new PdfDocument(reader);

            var sb = new System.Text.StringBuilder();
            for (int i = 1; i <= doc.GetNumberOfPages(); i++)
                sb.AppendLine(PdfTextExtractor.GetTextFromPage(doc.GetPage(i)));

            return sb.ToString();
        }

        /// <summary>
        /// Grabs the first line after "Marrësi:" (handles both ë and e in case of encoding variation).
        /// "Minerva Yzeiri(SOMEG), RR." → extracts "SOMEG" from parentheses.
        /// Falls back to everything before the first comma if no parentheses.
        /// </summary>
        private string ExtractClientName(string text)
        {
            // iText reads PDF columns left-to-right so "Marrësi:" and "Dërguesi:" 
            // end up on the same line. Client name is always 2 lines after Dërguesi:.
            // Most reliable: find name in parentheses anywhere e.g. "Minerva Yzeiri(SOMEG)"
            var parenMatch = Regex.Match(text, @"\(([A-Z][A-Z0-9 ]+)\)");
            if (parenMatch.Success)
            {
                var name = parenMatch.Groups[1].Value.Trim();
                _logger.LogInformation("Client name from parentheses: {Name}", name);
                return name;
            }

            // Fallback: line immediately after Dërguesi:\n{sender}\n → that's the client name
            var m = Regex.Match(text, @"D[eë]rguesi:\s*\n.+?\n(.+)");
            if (m.Success)
            {
                var firstLine = m.Groups[1].Value.Trim();
                var name = firstLine.Split(',').First().Trim();
                _logger.LogInformation("Client name from fallback: {Name}", name);
                return name;
            }

            _logger.LogWarning("Could not extract client name. Full text:\n{Text}", text);
            return string.Empty;
        }

        /// <summary>
        /// Normalizes to Title Case and strips legal suffixes.
        /// "SOMEG" → "Someg"
        /// </summary>
        private string NormalizeClientName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;

            var cleaned = Regex.Replace(raw,
                @"\b(sh\.?p\.?k\.?|s\.?r\.?l\.?|llc|ltd)\b", "",
                RegexOptions.IgnoreCase).Trim();

            return System.Globalization.CultureInfo.InvariantCulture.TextInfo
                .ToTitleCase(cleaned.ToLower());
        }

        /// <summary>
        /// Extracts line items from the order table.
        /// Handles both "Copë" and "Cope" in case of encoding variation.
        /// Format: "1 [STK30] Stika te pjekura can PET mini 60 g Copë 4"
        /// </summary>
        private List<OrderLineItem> ExtractLineItems(string text, OrderScanResult result)
        {
            var items = new List<OrderLineItem>();

            var linePattern = new Regex(
                @"^\s*\d+\s+\[([A-Z0-9]+)\]\s+(.+?)\s+Cop[eë]\s+(\d+)\s*$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);

            foreach (Match m in linePattern.Matches(text))
            {
                var code = m.Groups[1].Value.Trim().ToUpper();
                var description = m.Groups[2].Value.Trim();
                var qty = int.Parse(m.Groups[3].Value);

                if (!_productSizeMap.TryGetValue(code, out var paperSize))
                {
                    _logger.LogWarning("Product code '{Code}' not in ProductSizeMap — skipping", code);
                    result.UnrecognizedCodes.Add(code);
                    continue;
                }

                items.Add(new OrderLineItem
                {
                    ProductCode = code,
                    ProductName = description,
                    Quantity = qty,
                    PaperSize = paperSize,
                    HasRrethi = false
                });

                _logger.LogInformation("  → {Code} x{Qty} [{Size}]", code, qty, paperSize);
            }

            return items;
        }

        private Dictionary<string, PaperSize> BuildProductSizeMap()
        {
            var map = new Dictionary<string, PaperSize>(StringComparer.OrdinalIgnoreCase);

            var section = _config.GetSection("ProductSizeMap");
            foreach (var sizeSection in section.GetChildren())
            {
                if (!Enum.TryParse<PaperSize>(sizeSection.Key, true, out var paperSize))
                {
                    _logger.LogWarning("Unknown PaperSize in config: {Key}", sizeSection.Key);
                    continue;
                }

                var codes = sizeSection.Get<string[]>() ?? Array.Empty<string>();
                foreach (var code in codes)
                    map[code.Trim().ToUpper()] = paperSize;
            }

            _logger.LogInformation("ProductSizeMap loaded: {Count} product codes", map.Count);
            return map;
        }
    }
}