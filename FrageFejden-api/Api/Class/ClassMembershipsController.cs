using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FrageFejden.Entities.Enums;
using FrageFejden_api.Services;
using System.Security.Claims;
using Swashbuckle.AspNetCore.Annotations;

[ApiController]
[Route("api/Class/{id:guid}/members")]
[Produces("application/json")]
public sealed class ClassMembershipsController : ControllerBase
{
    private readonly IClassService _svc;
    public ClassMembershipsController(IClassService svc) => _svc = svc;

    private Guid UserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var g)
            ? g : throw new UnauthorizedAccessException();
    [SwaggerOperation(
        Summary = "Hämtar medlemmar i en klass",
        Description = "Returnerar en lista över medlemmar i den angivna klassen. Endast lärare kan utföra denna åtgärd."
    )]
    [HttpGet, Authorize]
    public async Task<IActionResult> List(Guid id, CancellationToken ct)
    {
        try { return Ok(await _svc.GetMembersAsync(id, UserId(), ct)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    public sealed record AddMemberRequest(Guid UserId, Role RoleInClass);

    [SwaggerOperation(
        Summary = "Lägger till en medlem i en klass",
        Description = "Lägger till en användare som medlem i den angivna klassen med en specifik roll. Endast lärare kan utföra denna åtgärd."
    )]
    [HttpPost, Authorize]
    public async Task<IActionResult> Add(Guid id, [FromBody] AddMemberRequest req, CancellationToken ct)
    {
        try { return await _svc.AddMemberAsync(id, UserId(), req.UserId, req.RoleInClass, ct) ? Ok() : Conflict("User already in class."); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [SwaggerOperation(
        Summary = "Tar bort en medlem från en klass",
        Description = "Tar bort en användare från den angivna klassen baserat på användarens ID. Endast lärare kan utföra denna åtgärd."
    )]
    [HttpDelete("{userId:guid}"), Authorize]
    public async Task<IActionResult> Remove(Guid id, Guid userId, CancellationToken ct)
    {
        try { return await _svc.RemoveMemberAsync(id, UserId(), userId, ct) ? NoContent() : NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    
}
