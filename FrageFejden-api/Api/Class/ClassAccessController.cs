using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FrageFejden_api.Services;
using System.Security.Claims;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel;


[ApiController]
[Route("api/Class")]
[Produces("application/json")]
public sealed class ClassAccessController : ControllerBase
{
    private readonly IClassService _svc;
    public ClassAccessController(IClassService svc) => _svc = svc;


    private Guid UserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var g)
            ? g : throw new UnauthorizedAccessException();


    [SwaggerOperation(
        Summary = "Get classes the user has joined",
        Description = "Returns a list of classes the authenticated user has joined."
    )]
    [HttpPost("join"), Authorize]
    public async Task<IActionResult> Join([FromBody] JoinRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.JoinCode)) return BadRequest("JoinCode is required.");
        try { var dto = await _svc.JoinByCodeAsync(UserId(), req.JoinCode, ct); return dto is null ? NotFound("Invalid join code.") : Ok(dto); }
        catch (InvalidOperationException e) { return Conflict(e.Message); }
    }

    [SwaggerOperation(
        Summary = "Lämna en klass",
        Description = "Låter den inloggade användaren lämna en klass baserat på klassens ID."
    )]
    [HttpDelete("{id:guid}/leave"), Authorize]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
        => (await _svc.LeaveAsync(UserId(), id, ct)) ? NoContent() : NotFound();

    [SwaggerOperation(
        Summary = "Återgenererar en join-kod för en klass",
        Description = "Återgenererar och returnerar en ny join-kod för den angivna klassen. Endast lärare kan utföra denna åtgärd."
    )]
    [HttpPost("{id:guid}/regen-joincode"), Authorize]
    public async Task<IActionResult> RegenerateJoinCode(Guid id, CancellationToken ct)
    {
        try { var res = await _svc.RegenerateJoinCodeAsync(id, UserId(), ct); return res is null ? NotFound() : Ok(new { res.Value.Id, res.Value.JoinCode }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
