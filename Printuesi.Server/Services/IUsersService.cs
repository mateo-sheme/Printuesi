using Printuesi.Server.Entities;

namespace Printuesi.Server.Services
{
    public interface IUsersService
    {
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
