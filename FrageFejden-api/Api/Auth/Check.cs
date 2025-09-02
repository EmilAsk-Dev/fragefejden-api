using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    [SwaggerOperation(
        Summary = "Kontrollerar om API:t är igång.",
        Description = "Returnerar en enkel bekräftelse på att API:t är igång tillsammans med serverns namn och aktuell tid."
    )]
    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping() => Ok(new
    {
        ok = true,
        name = typeof(SystemController).Assembly.GetName().Name,
        time = DateTimeOffset.UtcNow
    });
}
