using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FrageFejden_api.Services;
using System.Security.Claims;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;


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

    [HttpDelete("{id:guid}/leave"), Authorize]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
        => (await _svc.LeaveAsync(UserId(), id, ct)) ? NoContent() : NotFound();

    [HttpGet("validate-joincode/{joinCode}"), AllowAnonymous]
    [SwaggerOperation(
    Summary = "Kollar om joinkoden är giltig "
    )]
    public async Task<ActionResult> ValidateJoinCode(string joinCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
            return BadRequest(new { Message = "JoinCode is required." });

        try
        {
            var info = await _svc.FindClassByJoinCodeAsync(joinCode, ct);

            return info is null
                ? NotFound(new { IsValid = false, Message = "JoinCode not found or invalid." })
                : Ok(new { IsValid = true, ClassId = info.Value.Id, ClassName = info.Value.Name });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { Message = "An error occurred while validating join code.", Exception = ex.Message });
        }
    }

}
