namespace Printuesi.Server.Services
{
    public interface PdfService_Interface
    {
        /// <summary>
        /// Fill PDF template placeholders with actual values and save to output path
        /// </summary>
        /// <param name="templatePath">Path to the template PDF file</param>
        /// <param name="placeholders">Dictionary of {{PLACEHOLDER}} keys and their replacement values</param>
        /// <param name="outputPath">Path where the filled PDF will be saved</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> FillPdfPlaceholdersAsync(
            string templatePath,
            Dictionary<string, string> placeholders,
            string outputPath);
    }
}