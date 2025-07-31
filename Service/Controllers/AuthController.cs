using BusinessLogic.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Service.Controllers;

public class AuthController(IAuthProvider _authProvider) : ControllerBase
{
    [HttpGet]
    [Route("login")]
    public async Task<IActionResult> LoginUser([FromQuery] string email, [FromQuery] string password, [FromQuery] string userName)
    {
        try
        {
            var authUser = await _authProvider.AuthorizeUser(email, password, userName);
            return Ok(authUser);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    
    [HttpPost]
    [Route("register")]
    public async Task<IActionResult> RegisterUser(string email, string password, string userName)
    {
        try
        {
            var newUser = await _authProvider.RegisterUser(email, password, userName);
            return Ok(newUser);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}