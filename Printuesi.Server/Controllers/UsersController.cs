using Microsoft.AspNetCore.Mvc;
using Printuesi.Server.Services;

namespace Printuesi.Server.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class UsersController : Controller
    {
        private readonly IUsersService _authService;

        public UsersController(IUsersService authService)
        {
            _authService = authService;
        }
    }
}
