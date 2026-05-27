using iText.IO.Font;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace Printuesi.Server.Services
{
    public class PdfService : PdfService_Interface
    {
        private readonly ILogger<PdfService> _logger;
        private readonly string _fontPath;

        public PdfService(ILogger<PdfService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            // Resolves to wwwroot/fonts/ACUMINVARIABLECONCEPT.OTF
            _fontPath = System.IO.Path.Combine(env.WebRootPath, "fonts", "ACUMINVARIABLECONCEPT.OTF");
        }

        public async Task<bool> FillPdfPlaceholdersAsync(
            string templatePath,
            Dictionary<string, string> placeholders,
            string outputPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(templatePath))
                    throw new ArgumentException("Template path is required.", nameof(templatePath));
                if (!File.Exists(templatePath))
                    throw new FileNotFoundException($"PDF template not found: {templatePath}");
                if (placeholders == null || placeholders.Count == 0)
                    throw new ArgumentException("No placeholders provided.", nameof(placeholders));
                if (string.IsNullOrWhiteSpace(outputPath))
                    throw new ArgumentException("Output path is required.", nameof(outputPath));

                var outputDir = System.IO.Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir) && !System.IO.Directory.Exists(outputDir))
                    System.IO.Directory.CreateDirectory(outputDir);

                return await Task.Run(() =>
                {
                    using var reader = new PdfReader(templatePath);
                    using var writer = new PdfWriter(outputPath);
                    using var pdfDoc = new PdfDocument(reader, writer);

                    // Load custom font once, reuse across all pages
                    var font = LoadFont();

                    for (int pageNum = 1; pageNum <= pdfDoc.GetNumberOfPages(); pageNum++)
                    {
                        var page = pdfDoc.GetPage(pageNum);
                        // Merge fragmented text chunks so {{PLACEHOLDER}} is found as one unit
                        var textLocations = ExtractMergedTextLocations(page);

                        foreach (var placeholder in placeholders)
                        {
                            var key = $"{{{{{placeholder.Key}}}}}"; // e.g. {{EXPIRY_DATE}}
                            ReplaceTextInPdf(page, key, placeholder.Value, textLocations, font);
                        }
                    }

                    _logger.LogInformation("PDF successfully filled and saved to {OutputPath}", outputPath);
                    return true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filling PDF placeholders");
                return false;
            }
        }

        /// <summary>
        /// Merges adjacent text fragments so that a placeholder like {{EXPIRY_DATE}},
        /// which iText splits into many small chunks, is found as one unit.
        /// </summary>
        private List<TextLocationData> ExtractMergedTextLocations(PdfPage page)
        {
            var strategy = new CustomTextExtractionStrategy();
            PdfTextExtractor.GetTextFromPage(page, strategy);

            var rawLocations = strategy.GetTextLocations();
            var merged = new List<TextLocationData>();

            TextLocationData? current = null;

            foreach (var loc in rawLocations.OrderBy(l => l.Y).ThenBy(l => l.X))
            {
                if (current == null)
                {
                    current = loc.Clone();
                    continue;
                }

                bool sameLine = Math.Abs(current.Y - loc.Y) < 1.0f;
                bool adjacent = loc.X <= current.X + current.Width + 2f; // 2pt gap tolerance

                if (sameLine && adjacent && current.FontName == loc.FontName)
                {
                    current.Text += loc.Text;
                    current.Width = (loc.X + loc.Width) - current.X;
                    current.FontSize = Math.Max(current.FontSize, loc.FontSize);
                }
                else
                {
                    merged.Add(current);
                    current = loc.Clone();
                }
            }

            if (current != null)
                merged.Add(current);

            return merged;
        }

        private void ReplaceTextInPdf(
            PdfPage page,
            string placeholder,
            string value,
            List<TextLocationData> textLocations,
            PdfFont font)
        {
            var location = textLocations.FirstOrDefault(l => l.Text.Contains(placeholder));
            if (location == null)
            {
                _logger.LogWarning("Placeholder '{Placeholder}' not found in PDF", placeholder);
                return;
            }

            var fontSize = location.FontSize > 0 ? location.FontSize : 10f;
            var canvas = new PdfCanvas(page);

            // Write replacement value in black using the custom font at the placeholder's position
            canvas.BeginText()
                  .SetFontAndSize(font, fontSize)
                  .SetFillColor(ColorConstants.BLACK)
                  .MoveText(location.X, location.Y)
                  .ShowText(value)
                  .EndText();

            _logger.LogInformation(
                "Replaced '{Placeholder}' with '{Value}' at ({X:F1}, {Y:F1}) size {Size:F1}",
                placeholder, value, location.X, location.Y, fontSize);
        }

        private PdfFont LoadFont()
        {
            if (!File.Exists(_fontPath))
                throw new FileNotFoundException($"Required font not found at: {_fontPath}");

            var font = PdfFontFactory.CreateFont(_fontPath, PdfEncodings.IDENTITY_H);

            _logger.LogInformation("Loaded custom font from {FontPath}", _fontPath);
            return font;
        }

        // ── Inner types ──────────────────────────────────────────────────────────

        private class CustomTextExtractionStrategy : ITextExtractionStrategy
        {
            private readonly List<TextLocationData> _locations = new();

            public string GetResultantText() =>
                string.Join(" ", _locations.Select(t => t.Text));

            public List<TextLocationData> GetTextLocations() => _locations;

            // IEventListener implementation — routes PDF events into RenderText
            public void EventOccurred(IEventData data, EventType type)
            {
                if (type == EventType.RENDER_TEXT)
                    RenderText((TextRenderInfo)data);
            }

            public ICollection<EventType> GetSupportedEvents() =>
                new HashSet<EventType> { EventType.RENDER_TEXT };

            private void RenderText(TextRenderInfo renderInfo)
            {
                var text = renderInfo.GetText();
                if (string.IsNullOrEmpty(text)) return;

                var baseline = renderInfo.GetBaseline();
                // Multiply by matrix scale to get actual rendered size, not the unscaled value
                var fontSize = renderInfo.GetFontSize()
                               * renderInfo.GetTextMatrix().Get(Matrix.I11);

                _locations.Add(new TextLocationData
                {
                    Text = text,
                    X = baseline.GetStartPoint().Get(0),
                    Y = baseline.GetStartPoint().Get(1),
                    FontSize = Math.Abs(fontSize),
                    FontName = renderInfo.GetFont()?.GetFontProgram()
                                   ?.GetFontNames()?.GetFontName() ?? "Unknown",
                    Width = renderInfo.GetSingleSpaceWidth() * text.Length,
                    Height = renderInfo.GetAscentLine().GetStartPoint().Get(1)
                               - renderInfo.GetDescentLine().GetStartPoint().Get(1)
                });
            }
        }

        private class TextLocationData
        {
            public string Text { get; set; } = string.Empty;
            public float X { get; set; }
            public float Y { get; set; }
            public float FontSize { get; set; }
            public string FontName { get; set; } = string.Empty;
            public float Width { get; set; }
            public float Height { get; set; }

            public TextLocationData Clone() => (TextLocationData)MemberwiseClone();
        }
    }
}