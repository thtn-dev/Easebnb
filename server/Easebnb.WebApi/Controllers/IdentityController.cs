using Microsoft.AspNetCore.Mvc;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Interfaces;

namespace Easebnb.WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class IdentityController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request, "");
        return Ok(result.Value);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return Ok(result.Value);
    }
}