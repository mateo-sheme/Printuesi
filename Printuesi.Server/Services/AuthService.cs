using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Printuesi.Server.Data;
using Printuesi.Server.Entities;
using Printuesi.Server.Enums;
using Printuesi.Server.Helpers;

namespace Printuesi.Server.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;
        private readonly PasswordHasher<Users> _hasher;

        public AuthService(ApplicationDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
            _hasher = new PasswordHasher<Users>();
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            //Find user by name
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Name.ToLower() == request.Name.ToLower());

            if (user == null) 
                return null;
            //verify password to hashed
            var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if(result != PasswordVerificationResult.Success) return null;

            var token = GenerateToken(user);
            var expiryHours = int.Parse(_config["Jwt:ExpiryHours"] ?? "8");

            return new LoginResponse
            {
                Token = token,
                UserID = user.UserID,
                Name = user.Name,
                Role = user.Role.ToString(),
                ExpiresAt = DateTime.UtcNow.AddHours(expiryHours)
            };
        }

        public async Task<RegisterResponse?> RegisterAsync(RegisterRequest request)
        {
            // Check if user already exists
            var existingUser = await _db.Users
                .FirstOrDefaultAsync(u => u.Name.ToLower() == request.Name.ToLower());

            if (existingUser != null)
            {
                return null;
            }

            // Create new user
            var newUser = new Users
            {
                UserID = ID_Generator.User(),
                Name = request.Name,
                Role = UserRole.User
            };

            // Hash password
            newUser.PasswordHash = _hasher.HashPassword(newUser, request.Password);

            // Add to database
            _db.Users.Add(newUser);
            await _db.SaveChangesAsync();

            return new RegisterResponse
            {
                UserID = newUser.UserID,
                Name = newUser.Name,
                Role = newUser.Role.ToString(),
                Message = "Registration successful"
            };
        }

        public string GenerateToken(Users user)
        {
            var secret = _config["Jwt:Secret"]!;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiryHours = int.Parse(_config["Jwt:ExpiryHours"] ?? "8");

            var claims = new[]
            {
                new Claim("UserID", user.UserID),
                new Claim("name", user.Name),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expiryHours),
                signingCredentials: credentials
               );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        }
    }
