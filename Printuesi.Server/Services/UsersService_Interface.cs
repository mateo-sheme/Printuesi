using Printuesi.Server.Entities;

namespace Printuesi.Server.Services
{
    public interface UsersService_Interface
    {
        Task<UserResponse> CreateUserAsync(CreateUserRequest request);
        Task<UserResponse?> GetUserAsync(string userID);
        Task<List<UserResponse>> GetAllUsersAsync();
        Task<UserResponse?> UpdateUserAsync(string userID, UpdateUserRequest request);
        Task<bool> DeleteUserAsync(string userID);
    }

    public class CreateUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class UpdateUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string Role { get; set; } = string.Empty;
    }

    public class DeleteUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Password { get; set; } = string.Empty;
    }

    public class UserResponse
    {
        public string UserID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

}
