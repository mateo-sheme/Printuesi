using Printuesi.Server.Enums;

namespace Printuesi.Server.Services
{
    public interface UsersService_Interface
    {
        Task<List<UserResponse>> GetAllUsersAsync();
        Task<UserResponse?> GetUserAsync(string userID);
        Task<UserResponse> CreateUserAsync(CreateUserRequest request);
        Task<UserResponse?> UpdateUserAsync(string userID, UpdateUserRequest request);
        Task<bool> ChangePasswordAsync(string userID, ChangePasswordRequest request);
        Task<bool> DeleteUserAsync(string userID);
    }

    public class CreateUserRequest
    {
        // New frontend field
        public string Username { get; set; } = string.Empty;

        // Old backend/entity field fallback
        public string Name { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
    }

    public class UpdateUserRequest
    {
        // New frontend field
        public string Username { get; set; } = string.Empty;

        // Old backend/entity field fallback
        public string Name { get; set; } = string.Empty;

        public string Role { get; set; } = "User";

        // Kept for old compatibility, but password should normally use ChangePasswordRequest
        public string? Password { get; set; }

        // Your current Users entity probably does not have IsActive.
        // We accept it from frontend but ignore it in service for now.
        public bool IsActive { get; set; } = true;
    }

    public class ChangePasswordRequest
    {
        public string Password { get; set; } = string.Empty;
    }

    public class UserResponse
    {
        public string UserID { get; set; } = string.Empty;

        // New frontend field
        public string Username { get; set; } = string.Empty;

        // Old display field
        public string Name { get; set; } = string.Empty;

        public string Role { get; set; } = "User";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}