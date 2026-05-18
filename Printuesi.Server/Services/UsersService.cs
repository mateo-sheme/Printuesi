using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Printuesi.Server.Data;
using Printuesi.Server.Entities;
using Printuesi.Server.Enums;
using Printuesi.Server.Helpers;

namespace Printuesi.Server.Services
{
    public class UsersService : UsersService_Interface
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
            // Validation
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Name is required.", nameof(request.Name));
            if (string.IsNullOrWhiteSpace(request.Password))
                throw new ArgumentException("Password is required.", nameof(request.Password));
            if (string.IsNullOrWhiteSpace(request.Role))
                throw new ArgumentException("Role is required.", nameof(request.Role));

            // Check if user already exists
            var existingUser = await _db.Users
                .FirstOrDefaultAsync(u => u.Name.ToLower() == request.Name.ToLower());

            if (existingUser != null)
                throw new InvalidOperationException($"A user with the name '{request.Name}' already exists.");

            // Create new user
            var user = new Users
            {
                UserID = ID_Generator.User(),
                Name = request.Name,
                Role = Enum.Parse<UserRole>(request.Role, true),
                CreatedAt = DateTime.UtcNow
            };

            // Hash and set password
            user.PasswordHash = _hasher.HashPassword(user, request.Password);

            // Add to database
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return new UserResponse
            {
                UserID = user.UserID,
                Name = user.Name,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<UserResponse?> GetUserAsync(string userID)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(userID))
                throw new ArgumentException("UserID is required.", nameof(userID));

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserID == userID);

            if (user == null)
                return null;

            return new UserResponse
            {
                UserID = user.UserID,
                Name = user.Name,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<List<UserResponse>> GetAllUsersAsync()
        {
            var users = await _db.Users.ToListAsync();

            return users.Select(u => new UserResponse
            {
                UserID = u.UserID,
                Name = u.Name,
                Role = u.Role.ToString(),
                CreatedAt = u.CreatedAt
            }).ToList();
        }

        public async Task<UserResponse?> UpdateUserAsync(string userID, UpdateUserRequest request)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(userID))
                throw new ArgumentException("UserID is required.", nameof(userID));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Name) && string.IsNullOrWhiteSpace(request.Role) && request.Password == null)
                throw new ArgumentException("At least one field must be provided for update.");

            // Get user
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserID == userID);
            if (user == null)
                return null;

            // Check if new name already exists (if changing name)
            if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.ToLower() != user.Name.ToLower())
            {
                var nameExists = await _db.Users
                    .AnyAsync(u => u.Name.ToLower() == request.Name.ToLower() && u.UserID != userID);
                if (nameExists)
                    throw new InvalidOperationException($"A user with the name '{request.Name}' already exists.");

                user.Name = request.Name;
            }

            // Update role if provided
            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                user.Role = Enum.Parse<UserRole>(request.Role, true);
            }

            // Update password if provided
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                user.PasswordHash = _hasher.HashPassword(user, request.Password);
            }

            // Save changes
            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            return new UserResponse
            {
                UserID = user.UserID,
                Name = user.Name,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<bool> DeleteUserAsync(string userID)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(userID))
                throw new ArgumentException("UserID is required.", nameof(userID));

            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserID == userID);
            if (user == null)
                return false;

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            return true;
        }
    }
}
