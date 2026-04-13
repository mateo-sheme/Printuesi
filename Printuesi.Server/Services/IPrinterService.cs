namespace Printuesi.Server.Services
{
    public interface IPrinterService
    {
        Task<bool> TestConnectionAsync();
        Task<bool> SendFileLprAsync(string filePath);
        Task<bool>SendFileRawAsync(string filePath);
    }
}   
