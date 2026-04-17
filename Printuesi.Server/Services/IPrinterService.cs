namespace Printuesi.Server.Services
{
    public interface IPrinterService
    {

        // kjo eshte koka dmth service mund te bej kete kete kete
        Task<bool> TestConnectionAsync();
        Task<bool> SendFileLprAsync(string filePath);
        Task<bool>SendFileRawAsync(string filePath);
    }
}   
