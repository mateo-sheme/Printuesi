using Microsoft.EntityFrameworkCore;
using Printuesi.Server.Data;
using Printuesi.Server.Entities;
using Printuesi.Server.Enums;
using Printuesi.Server.Helpers;

namespace Printuesi.Server.Services
{
    public class ClientService : ClientService_Interface
    {
        private readonly ApplicationDbContext _db;

        public ClientService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<ClientResponse> CreateClientAsync(CreateClientRequest request)
        {
            // Validation
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Client name is required.", nameof(request.Name));
            if (string.IsNullOrWhiteSpace(request.NIPT))
                throw new ArgumentException("NIPT is required.", nameof(request.NIPT));

            // Check if client already exists by NIPT
            var existingClient = await _db.Clients
                .FirstOrDefaultAsync(c => c.NIPT.ToLower() == request.NIPT.ToLower());

            if (existingClient != null)
                throw new InvalidOperationException($"A client with NIPT '{request.NIPT}' already exists.");

            // Create new client
            var client = new Clients
            {
                ClientID = ID_Generator.Client(),
                NIPT = request.NIPT,
                Name = request.Name,
                CreatedAt = DateTime.UtcNow
            };

            // Add to database
            _db.Clients.Add(client);
            await _db.SaveChangesAsync();

            return new ClientResponse
            {
                ClientID = client.ClientID,
                NIPT = client.NIPT,
                Name = client.Name,
                CreatedAt = client.CreatedAt
            };
        }

        public async Task<ClientResponse?> GetClientAsync(string ClientID)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(ClientID))
                throw new ArgumentException("ClientID is required.", nameof(ClientID));

            var client = await _db.Clients.FirstOrDefaultAsync(c => c.ClientID == ClientID);

            if (client == null)
                return null;

            return new ClientResponse
            {
                ClientID = client.ClientID,
                NIPT = client.NIPT,
                Name = client.Name,
                CreatedAt = client.CreatedAt
            };
        }

        public async Task<List<ClientResponse>> GetAllClientsAsync()
        {
            var clients = await _db.Clients.ToListAsync();

            return clients.Select(c => new ClientResponse
            {
                ClientID = c.ClientID,
                NIPT = c.NIPT,
                Name = c.Name,
                CreatedAt = c.CreatedAt
            }).ToList();
        }

        public async Task<ClientResponse?> UpdateClientAsync(string ClientID, UpdateClientRequest request)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(ClientID))
                throw new ArgumentException("ClientID is required.", nameof(ClientID));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Name) && string.IsNullOrWhiteSpace(request.NIPT))
                throw new ArgumentException("At least one field must be provided for update.");

            // Get client
            var client = await _db.Clients.FirstOrDefaultAsync(c => c.ClientID == ClientID);
            if (client == null)
                return null;

            // Check if new NIPT already exists (if changing NIPT)
            if (!string.IsNullOrWhiteSpace(request.NIPT) && request.NIPT.ToLower() != client.NIPT.ToLower())
            {
                var niptExists = await _db.Clients
                    .AnyAsync(c => c.NIPT.ToLower() == request.NIPT.ToLower() && c.ClientID != ClientID);
                if (niptExists)
                    throw new InvalidOperationException($"A client with NIPT '{request.NIPT}' already exists.");

                client.NIPT = request.NIPT;
            }

            // Update name if provided
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                client.Name = request.Name;
            }

            // Save changes
            _db.Clients.Update(client);
            await _db.SaveChangesAsync();

            return new ClientResponse
            {
                ClientID = client.ClientID,
                NIPT = client.NIPT,
                Name = client.Name,
                CreatedAt = client.CreatedAt
            };
        }

        public async Task<bool> DeleteClientAsync(string ClientID)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(ClientID))
                throw new ArgumentException("ClientID is required.", nameof(ClientID));

            var client = await _db.Clients.FirstOrDefaultAsync(c => c.ClientID == ClientID);
            if (client == null)
                return false;

            _db.Clients.Remove(client);
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<List<ClientPrintJobHistoryResponse>> GetClientPrintJobsAsync(
    string clientID,
    Status? status = null)
        {
            var clientExists = await _db.Clients.AnyAsync(c => c.ClientID == clientID);

            if (!clientExists)
                throw new InvalidOperationException($"Client '{clientID}' not found.");

            var query = _db.PrintJobs
                .Include(j => j.Client)
                .Include(j => j.PrintJobObjects)
                    .ThenInclude(o => o.LabelTemplate)
                .Where(j => j.ClientID == clientID)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(j => j.Status == status.Value);

            var jobs = await query
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            return jobs.Select(job =>
            {
                var paperSizes = job.PrintJobObjects
                    .Select(o => o.LabelTemplate != null
                        ? o.LabelTemplate.PaperSize.ToString()
                        : "Unknown")
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                return new ClientPrintJobHistoryResponse
                {
                    PrintJobID = job.PrintJobID,
                    ClientID = job.ClientID,
                    ClientName = job.Client?.Name ?? string.Empty,
                    Status = job.Status,
                    CreatedAt = job.CreatedAt,
                    CompletedAt = job.CompletedAt,
                    TotalRequested = job.PrintJobObjects.Sum(o => o.Quantity_Requested),
                    TotalPrinted = job.PrintJobObjects.Sum(o => o.Quantity_Printed),
                    GroupsCount = paperSizes.Count,
                    PaperSizes = paperSizes,
                    CreatedBy = job.CreatedBy,
                    Notes = job.Notes
                };
            }).ToList();
        }
    }
}
