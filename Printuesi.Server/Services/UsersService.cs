using Microsoft.AspNetCore.Identity;
using Printuesi.Server.Data;
using Printuesi.Server.Entities;
using Printuesi.Server.Enums;
using Printuesi.Server.Helpers;

namespace Printuesi.Server.Services
{
    public class UsersService : IUsersService
    {
        private readonly ApplicationDbContext _db;
        private readonly PasswordHasher<Users> _hasher;

        public UsersService(ApplicationDbContext db)
        {
            _db = db;
            _hasher = new PasswordHasher<Users>();
        }

        public async Task<UserResponse> CreateUserAsync(CreateUserRequest request)
        {
            //Find user by name
            var user = new Users
            {
                UserID = ID_Generator.User(),
                Name = request.Name,
                Role = Enum.Parse<UserRole>(request.Role, true),
                CreatedAt = DateTime.UtcNow
            };

            return new UserResponse
            {
                UserID = user.UserID,
                Name = user.Name,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt
            };
        }
    }
}
