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

        public async Task<List<UserResponse>> GetAllUsersAsync()
        {
            var users = await _db.Users
                .OrderBy(u => u.Name)
                .ToListAsync();

            return users.Select(MapToResponse).ToList();
        }

        public async Task<UserResponse?> GetUserAsync(string userID)
        {
            if (string.IsNullOrWhiteSpace(userID))
                throw new ArgumentException("UserID is required.", nameof(userID));

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.UserID == userID);

            if (user == null)
                return null;

            return MapToResponse(user);
        }

        public async Task<UserResponse> CreateUserAsync(CreateUserRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var username = GetRequestedUsername(request.Username, request.Name);
            var password = request.Password?.Trim() ?? string.Empty;
            var role = ParseRole(request.Role);

            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username is required.");

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is required.");

            var existingUser = await _db.Users
                .FirstOrDefaultAsync(u => u.Name.ToLower() == username.ToLower());

            if (existingUser != null)
                throw new InvalidOperationException($"A user with the name '{username}' already exists.");

            var user = new Users
            {
                UserID = ID_Generator.User(),
                Name = username,
                Role = role,
                CreatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _hasher.HashPassword(user, password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return MapToResponse(user);
        }

        public async Task<UserResponse?> UpdateUserAsync(string userID, UpdateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(userID))
                throw new ArgumentException("UserID is required.", nameof(userID));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.UserID == userID);

            if (user == null)
                return null;

            var username = GetRequestedUsername(request.Username, request.Name);

            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username is required.");

            var newRole = ParseRole(request.Role);

            var nameExists = await _db.Users
                .AnyAsync(u =>
                    u.UserID != userID &&
                    u.Name.ToLower() == username.ToLower());

            if (nameExists)
                throw new InvalidOperationException($"A user with the name '{username}' already exists.");

            if (user.Role == UserRole.Admin && newRole != UserRole.Admin)
            {
                var adminCount = await _db.Users
                    .CountAsync(u => u.Role == UserRole.Admin);

                if (adminCount <= 1)
                    throw new InvalidOperationException("Cannot remove Admin role from the last admin user.");
            }

            user.Name = username;
            user.Role = newRole;

            // Compatibility: if old UI sends password inside update, handle it.
            // New UI should use /change-password instead.
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                user.PasswordHash = _hasher.HashPassword(user, request.Password);
            }

            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            return MapToResponse(user);
        }

        public async Task<bool> ChangePasswordAsync(string userID, ChangePasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(userID))
                throw new ArgumentException("UserID is required.", nameof(userID));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var password = request.Password?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is required.");

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.UserID == userID);

            if (user == null)
                return false;

            user.PasswordHash = _hasher.HashPassword(user, password);

            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteUserAsync(string userID)
        {
            if (string.IsNullOrWhiteSpace(userID))
                throw new ArgumentException("UserID is required.", nameof(userID));

            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.UserID == userID);

            if (user == null)
                return false;

            if (user.Role == UserRole.Admin)
            {
                var adminCount = await _db.Users
                    .CountAsync(u => u.Role == UserRole.Admin);

                if (adminCount <= 1)
                    throw new InvalidOperationException("Cannot delete the last admin user.");
            }

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();

            return true;
        }

        private static UserResponse MapToResponse(Users user)
        {
            return new UserResponse
            {
                UserID = user.UserID,
                Username = user.Name,
                Name = user.Name,
                Role = user.Role.ToString(),
                IsActive = true,
                CreatedAt = user.CreatedAt
            };
        }

        private static string GetRequestedUsername(string username, string name)
        {
            if (!string.IsNullOrWhiteSpace(username))
                return username.Trim();

            if (!string.IsNullOrWhiteSpace(name))
                return name.Trim();

            return string.Empty;
        }

        private static UserRole ParseRole(string? roleText)
        {
            if (string.IsNullOrWhiteSpace(roleText))
                return UserRole.User;

            if (!Enum.TryParse<UserRole>(roleText, ignoreCase: true, out var role))
                throw new ArgumentException($"Invalid role '{roleText}'. Allowed roles are User and Admin.");

            if (role != UserRole.User && role != UserRole.Admin)
                throw new ArgumentException($"Invalid role '{roleText}'. Allowed roles are User and Admin.");

            return role;
        }
    }
}