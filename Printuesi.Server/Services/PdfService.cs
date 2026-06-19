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
            _fontPath = System.IO.Path.Combine(env.WebRootPath, "fonts", "ACUMINVARIABLECONCEPT.OTF");
        }

        private PdfFont LoadFont()
        {
            if (!File.Exists(_fontPath))
                throw new FileNotFoundException($"Required font not found at: {_fontPath}");

            var font = PdfFontFactory.CreateFont(_fontPath, PdfEncodings.IDENTITY_H);

            _logger.LogInformation("Loaded custom font from {FontPath}", _fontPath);

            return font;
        }

        public async Task<bool> FillPdfPlaceholdersAsync(
            string templatePath,
            Dictionary<string, string> placeholders,
            string outputPath,
            bool useWhiteText = false)
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

                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                var success = await Task.Run(() =>
                {
                    var replacedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    using var reader = new PdfReader(templatePath);
                    using var writer = new PdfWriter(outputPath);
                    using var pdfDoc = new PdfDocument(reader, writer);

                    var font = LoadFont();

                    for (var pageNum = 1; pageNum <= pdfDoc.GetNumberOfPages(); pageNum++)
                    {
                        var page = pdfDoc.GetPage(pageNum);
                        var textLocations = ExtractMergedTextLocations(page);

                        foreach (var placeholder in placeholders)
                        {
                            if (replacedKeys.Contains(placeholder.Key))
                                continue;

                            var key = $"{{{{{placeholder.Key}}}}}"; // {{EXP}}, {{LOT}}, {{KG}}

                            var replaced = ReplaceTextInPdf(
                                page,
                                key,
                                placeholder.Value,
                                textLocations,
                                font,
                                useWhiteText);

                            if (replaced)
                                replacedKeys.Add(placeholder.Key);
                        }
                    }

                    var missing = placeholders.Keys
                        .Where(key => !replacedKeys.Contains(key))
                        .Select(key => $"{{{{{key}}}}}")
                        .ToList();

                    if (missing.Any())
                    {
                        _logger.LogError(
                            "PDF template is missing required placeholder(s): {Missing}. Template: {TemplatePath}",
                            string.Join(", ", missing),
                            templatePath);

                        return false;
                    }

                    _logger.LogInformation("PDF successfully filled and saved to {OutputPath}", outputPath);
                    return true;
                });

                if (!success)
                {
                    try
                    {
                        if (File.Exists(outputPath))
                            File.Delete(outputPath);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(
                            deleteEx,
                            "Failed to delete invalid output PDF: {OutputPath}",
                            outputPath);
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filling PDF placeholders");
                return false;
            }
        }

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

                var sameLine = Math.Abs(current.Y - loc.Y) < 3.0f;
                var adjacent = loc.X <= current.X + current.Width + 8f;

                if (sameLine && adjacent)
                {
                    current.Text += loc.Text;
                    current.Width = Math.Max(
                        current.Width,
                        (loc.X + loc.Width) - current.X);

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

        private static string NormalizePdfText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return new string(value
                    .Where(ch => !char.IsControl(ch))
                    .ToArray())
                .Replace(" ", "")
                .Trim();
        }

        private bool ReplaceTextInPdf(
    PdfPage page,
    string placeholder,
    string value,
    List<TextLocationData> textLocations,
    PdfFont font,
    bool useWhiteText = false)
        {
            var normalizedPlaceholder = NormalizePdfText(placeholder);

            var location = textLocations.FirstOrDefault(l =>
                NormalizePdfText(l.Text).Contains(normalizedPlaceholder));

            if (location == null)
            {
                _logger.LogWarning("Placeholder '{Placeholder}' not found in PDF", placeholder);

                foreach (var loc in textLocations)
                {
                    _logger.LogWarning(
                        "PDF text chunk seen: [{Text}] at X:{X:F1}, Y:{Y:F1}",
                        loc.Text,
                        loc.X,
                        loc.Y);
                }

                return false;
            }

            var fontSize = location.FontSize > 0 ? location.FontSize : 10f;

            var drawX = location.X;
            var drawY = location.Y;

            var rawIndex = location.Text.IndexOf(placeholder, StringComparison.Ordinal);

            if (rawIndex > 0 && location.Text.Length > 0 && location.Width > 0)
            {
                var ratio = rawIndex / (float)location.Text.Length;
                drawX = location.X + (location.Width * ratio);
            }

            // IMPORTANT:
            // Write AFTER existing Illustrator artwork, so the replacement appears on top.
            var canvas = new PdfCanvas(
                page.NewContentStreamAfter(),
                page.GetResources(),
                page.GetDocument());

            var color = useWhiteText ? ColorConstants.WHITE : ColorConstants.BLACK;

            canvas.BeginText()
                  .SetFontAndSize(font, fontSize)
                  .SetFillColor(color)
                  .MoveText(drawX, drawY)
                  .ShowText(value)
                  .EndText();

            _logger.LogInformation(
                "Replaced '{Placeholder}' with '{Value}' at ({X:F1}, {Y:F1}) size {Size:F1}. Source chunk: [{SourceText}]",
                placeholder,
                value,
                drawX,
                drawY,
                fontSize,
                location.Text);

            return true;
        }

        private class CustomTextExtractionStrategy : ITextExtractionStrategy
        {
            private readonly List<TextLocationData> _locations = new();

            public string GetResultantText()
            {
                return string.Join(" ", _locations.Select(t => t.Text));
            }

            public List<TextLocationData> GetTextLocations()
            {
                return _locations;
            }

            public void EventOccurred(IEventData data, EventType type)
            {
                if (type == EventType.RENDER_TEXT)
                    RenderText((TextRenderInfo)data);
            }

            public ICollection<EventType> GetSupportedEvents()
            {
                return new HashSet<EventType> { EventType.RENDER_TEXT };
            }

            private void RenderText(TextRenderInfo renderInfo)
            {
                var text = renderInfo.GetText();

                if (string.IsNullOrEmpty(text))
                    return;

                var baseline = renderInfo.GetBaseline();

                var fontSize =
                    renderInfo.GetFontSize() *
                    renderInfo.GetTextMatrix().Get(Matrix.I11);

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

            public TextLocationData Clone()
            {
                return (TextLocationData)MemberwiseClone();
            }
        }
    }
}