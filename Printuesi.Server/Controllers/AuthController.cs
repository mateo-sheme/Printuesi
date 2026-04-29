using Microsoft.AspNetCore.Mvc;
using Printuesi.Server.Services;

namespace Project.Server.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);

            if (result == null)
            {
                return Unauthorized(new { message = "Invadil name or password" });
                // returns: { token, userId, name, role, expiresAt }
            }
            return Ok(result);
        }
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return Ok(new { message = "Logged out" });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);

            if (result == null)
            {
                return BadRequest(new { message = "Registration failed" });
            }

            return Ok(result);
        }
    }
}


