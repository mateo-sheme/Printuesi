using Printuesi.Server.Enums;

namespace Printuesi.Server.Services
{
    public interface PrinterService_Interface
    {
        Task<bool> TestConnectionAsync();
        Task<bool> SendFileLprAsync(string filePath);
        Task<bool> SendFileRawAsync(string filePath);
        Task<bool> SendDriverAsync(string driverPath);
        Task<bool> PrintPdfWithCupsAsync(string pdfPath, int copies, PaperSize paperSize);
    }
}