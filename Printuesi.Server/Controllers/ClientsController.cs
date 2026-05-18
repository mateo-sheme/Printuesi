using Microsoft.AspNetCore.Mvc;
using Printuesi.Server.Services;

namespace Printuesi.Server.Controllers
{
    [ApiController]
    [Route("api/clients")]
    public class ClientsController : ControllerBase
    {
        private readonly ClientService_Interface _clientService;

        public ClientsController(ClientService_Interface clientService)
        {
            _clientService = clientService;
        }

        /// <summary>
        /// Create a new client
        /// </summary>
        [HttpPost("create")]
        public async Task<ActionResult<ClientResponse>> CreateClient([FromBody] CreateClientRequest request)
        {
            try
            {
                var result = await _clientService.CreateClientAsync(request);
                return CreatedAtAction(nameof(GetClient), new { clientID = result.ClientID }, result);
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
                return StatusCode(500, new { message = "An error occurred while creating the client.", error = ex.Message });
            }
        }

        /// <summary>
        /// Get client by ID
        /// </summary>
        [HttpGet("{clientID}")]
        public async Task<ActionResult<ClientResponse>> GetClient(string clientID)
        {
            try
            {
                var result = await _clientService.GetClientAsync(clientID);
                if (result == null)
                    return NotFound(new { message = $"Client with ID '{clientID}' not found." });

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving the client.", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all clients
        /// </summary>
        [HttpGet("")]
        public async Task<ActionResult<List<ClientResponse>>> GetAllClients()
        {
            try
            {
                var result = await _clientService.GetAllClientsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving clients.", error = ex.Message });
            }
        }

        /// <summary>
        /// Update client details
        /// </summary>
        [HttpPut("{clientID}")]
        public async Task<ActionResult<ClientResponse>> UpdateClient(string clientID, [FromBody] UpdateClientRequest request)
        {
            try
            {
                var result = await _clientService.UpdateClientAsync(clientID, request);
                if (result == null)
                    return NotFound(new { message = $"Client with ID '{clientID}' not found." });

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
                return StatusCode(500, new { message = "An error occurred while updating the client.", error = ex.Message });
            }
        }

        /// <summary>
        /// Delete client
        /// </summary>
        [HttpDelete("{clientID}")]
        public async Task<ActionResult> DeleteClient(string clientID)
        {
            try
            {
                var result = await _clientService.DeleteClientAsync(clientID);
                if (!result)
                    return NotFound(new { message = $"Client with ID '{clientID}' not found." });

                return Ok(new { message = "Client deleted successfully." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the client.", error = ex.Message });
            }
        }
    }
}
