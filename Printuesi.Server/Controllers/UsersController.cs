using Microsoft.AspNetCore.Mvc;
using Printuesi.Server.Services;

namespace Printuesi.Server.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly UsersService_Interface _usersService;

        public UsersController(UsersService_Interface usersService)
        {
            _usersService = usersService;
        }

        /// <summary>
        /// Create a new user
        /// </summary>
        [HttpPost("create")]
        public async Task<ActionResult<UserResponse>> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                var result = await _usersService.CreateUserAsync(request);
                return CreatedAtAction(nameof(GetUser), new { userID = result.UserID }, result);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new { message = ex.Message });
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
                return StatusCode(500, new { message = "An error occurred while creating the user.", error = ex.Message });
            }
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        [HttpGet("{userID}")]
        public async Task<ActionResult<UserResponse>> GetUser(string userID)
        {
            try
            {
                var result = await _usersService.GetUserAsync(userID);
                if (result == null)
                    return NotFound(new { message = $"User with ID '{userID}' not found." });

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the user.", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all users
        /// </summary>
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
                return StatusCode(500, new { message = "An error occurred while retrieving users.", error = ex.Message });
            }
        }

        /// <summary>
        /// Update user details
        /// </summary>
        [HttpPut("{userID}")]
        public async Task<ActionResult<UserResponse>> UpdateUser(string userID, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var result = await _usersService.UpdateUserAsync(userID, request);
                if (result == null)
                    return NotFound(new { message = $"User with ID '{userID}' not found." });

                return Ok(result);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(new { message = ex.Message });
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
                return StatusCode(500, new { message = "An error occurred while updating the user.", error = ex.Message });
            }
        }

        /// <summary>
        /// Delete user
        /// </summary>
        [HttpDelete("{userID}")]
        public async Task<ActionResult> DeleteUser(string userID)
        {
            try
            {
                var result = await _usersService.DeleteUserAsync(userID);
                if (!result)
                    return NotFound(new { message = $"User with ID '{userID}' not found." });

                return Ok(new { message = "User deleted successfully." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the user.", error = ex.Message });
            }
        }
    }
}
