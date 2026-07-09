using Printuesi.Server.Enums;

namespace Printuesi.Server.Services
{
    public interface ClientService_Interface
    {
        Task<ClientResponse> CreateClientAsync(CreateClientRequest request);
        Task<ClientResponse?> GetClientAsync(string ClientID);
        Task<List<ClientResponse>> GetAllClientsAsync();
        Task<ClientResponse?> UpdateClientAsync(string ClientID, UpdateClientRequest request);
        Task<bool> DeleteClientAsync(string ClientID);
        Task<List<ClientPrintJobHistoryResponse>> GetClientPrintJobsAsync(
        string clientID,
        Status? status = null);
    }

    public class CreateClientRequest
    {
        public string NIPT { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateClientRequest
    {
        public string NIPT { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class ClientResponse
    {
        public string ClientID { get; set; } = string.Empty;
        public string NIPT { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class ClientPrintJobHistoryResponse
    {
        public string PrintJobID { get; set; } = string.Empty;
        public string ClientID { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;

        public Status Status { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public int TotalRequested { get; set; }
        public int TotalPrinted { get; set; }

        public int GroupsCount { get; set; }
        public List<string> PaperSizes { get; set; } = new();

        public string CreatedBy { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}

