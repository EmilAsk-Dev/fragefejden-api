using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping() => Ok(new
    {
        ok = true,
        name = typeof(SystemController).Assembly.GetName().Name,
        time = DateTimeOffset.UtcNow
    });
}
