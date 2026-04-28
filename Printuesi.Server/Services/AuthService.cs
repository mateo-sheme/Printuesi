using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Printuesi.Server.Data;
using Printuesi.Server.Entities;

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
                    UserID = user.UserID.ToString(),
                    Name = user.Name,
                    Role = user.Role.ToString(),
                    ExpiresAt = DateTime.UtcNow.AddHours(expiryHours)
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
                    new Claim("UserID", user.UserID.ToString()),
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
