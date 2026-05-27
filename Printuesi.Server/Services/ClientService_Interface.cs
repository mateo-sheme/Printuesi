namespace Printuesi.Server.Services
{
    public interface ClientService_Interface
    {
        Task<ClientResponse> CreateClientAsync(CreateClientRequest request);
        Task<ClientResponse?> GetClientAsync(string ClientID);
        Task<List<ClientResponse>> GetAllClientsAsync();
        Task<ClientResponse?> UpdateClientAsync(string ClientID, UpdateClientRequest request);
        Task<bool> DeleteClientAsync(string ClientID);
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
}
