using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Printuesi.Server.Services;

namespace Printuesi.Server.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly UsersService_Interface _usersService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            UsersService_Interface usersService,
            ILogger<UsersController> logger)
        {
            _usersService = usersService;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<ActionResult<List<UserResponse>>> GetAllUsers()
        {
            try
            {
                var result = await _usersService.GetAllUsersAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users");

                return StatusCode(500, new
                {
                    message = "Error loading users.",
                    error = ex.Message
                });
            }
        }

        [HttpGet("{userID}")]
        public async Task<ActionResult<UserResponse>> GetUser(string userID)
        {
            try
            {
                var result = await _usersService.GetUserAsync(userID);

                if (result == null)
                    return NotFound(new { message = $"User '{userID}' not found." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user {UserID}", userID);

                return StatusCode(500, new
                {
                    message = "Error loading user.",
                    error = ex.Message
                });
            }
        }

        [HttpPost("")]
        public async Task<ActionResult<UserResponse>> CreateUser(
            [FromBody] CreateUserRequest request)
        {
            try
            {
                var result = await _usersService.CreateUserAsync(request);

                return CreatedAtAction(
                    nameof(GetUser),
                    new { userID = result.UserID },
                    result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");

                return StatusCode(500, new
                {
                    message = "Error creating user.",
                    error = ex.Message
                });
            }
        }

        [HttpPut("{userID}")]
        public async Task<ActionResult<UserResponse>> UpdateUser(
            string userID,
            [FromBody] UpdateUserRequest request)
        {
            try
            {
                var result = await _usersService.UpdateUserAsync(userID, request);

                if (result == null)
                    return NotFound(new { message = $"User '{userID}' not found." });

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserID}", userID);

                return StatusCode(500, new
                {
                    message = "Error updating user.",
                    error = ex.Message
                });
            }
        }

        [HttpPost("{userID}/change-password")]
        public async Task<ActionResult> ChangePassword(
            string userID,
            [FromBody] ChangePasswordRequest request)
        {
            try
            {
                var result = await _usersService.ChangePasswordAsync(userID, request);

                if (!result)
                    return NotFound(new { message = $"User '{userID}' not found." });

                return Ok(new { message = "Password changed." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserID}", userID);

                return StatusCode(500, new
                {
                    message = "Error changing password.",
                    error = ex.Message
                });
            }
        }

        [HttpDelete("{userID}")]
        public async Task<ActionResult> DeleteUser(string userID)
        {
            try
            {
                var result = await _usersService.DeleteUserAsync(userID);

                if (!result)
                    return NotFound(new { message = $"User '{userID}' not found." });

                return Ok(new { message = "User deleted." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserID}", userID);

                return StatusCode(500, new
                {
                    message = "Error deleting user.",
                    error = ex.Message
                });
            }
        }
    }
}