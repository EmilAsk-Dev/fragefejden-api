using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FrageFejden_api.Services;
using System.Security.Claims;

[ApiController]
[Route("api/Class/me")]
[Produces("application/json")]
public sealed class MyClassesController : ControllerBase
{
    private readonly IClassService _svc;
    public MyClassesController(IClassService svc) => _svc = svc;

    private Guid UserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var g)
            ? g : throw new UnauthorizedAccessException();

    [HttpGet, Authorize]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => Ok(await _svc.GetMyClassesAsync(UserId(), ct));
}
