using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FrageFejden_api.Services;
using System.Security.Claims;
using Swashbuckle.AspNetCore.Annotations;

[ApiController]
[Route("api/Class")] // keep existing path; use "api/classes" if you want to modernize
[Produces("application/json")]
public sealed class ClassesController : ControllerBase
{
    private readonly IClassService _svc;
    public ClassesController(IClassService svc) => _svc = svc;

    private Guid UserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var g)
            ? g : throw new UnauthorizedAccessException();

    [SwaggerOperation(
        Summary = "Hämtar alla klasser",
        Description = "Returnerar en paginerad lista över alla klasser som den autentiserade användaren har tillgång till."
    )]
    [HttpGet, Authorize]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var (items, total) = await _svc.ListAsync(page, pageSize, ct);
        return Ok(new { total, page, pageSize, items });
    }

    [SwaggerOperation(
        Summary = "Hämtar en klass efter ID",
        Description = "Returnerar information om en specifik klass baserat på dess ID."
    )]
    [HttpGet("{id:guid}"), Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => (await _svc.GetByIdAsync(id, ct)) is { } cls ? Ok(cls) : NotFound();


    [SwaggerOperation(
        Summary = "Skapar en ny klass",
        Description = "Skapar en ny klass med angivet namn, betygsetikett och beskrivning. Den autentiserade användaren kan välja att bli lärare i den nya klassen."
    )]
    [HttpPost, Authorize]
    public async Task<IActionResult> Create([FromBody] CreateClassRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required.");
        var cls = await _svc.CreateAsync(UserId(), req, ct);
        return CreatedAtAction(nameof(GetById), new { id = cls.Id }, cls);
    }

    [SwaggerOperation(
        Summary = "Uppdaterar en klass",
        Description = "Uppdaterar informationen för en specifik klass baserat på dess ID. Endast lärare kan utföra denna åtgärd."
    )]
    [HttpPut("{id:guid}"), Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClassRequest req, CancellationToken ct)
    {
        try { return await _svc.UpdateAsync(id, UserId(), req, ct) ? NoContent() : NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [SwaggerOperation(
        Summary = "Tar bort en klass",
        Description = "Tar bort en specifik klass baserat på dess ID. Endast lärare kan utföra denna åtgärd."
    )]
    [HttpDelete("{id:guid}"), Authorize]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { return await _svc.DeleteAsync(id, UserId(), ct) ? NoContent() : NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
