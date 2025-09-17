using FrageFejden_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

[ApiController]
[Route("api/teacher/created-classes")]
[Produces("application/json")]
[Authorize] // räcker
public sealed class TeacherCreatedClassesController : ControllerBase
{
    private readonly IClassService _classService;
    public TeacherCreatedClassesController(IClassService classService) => _classService = classService;

    [HttpGet]
    [SwaggerOperation(
        Summary = "Hämtar lärarens egna klasser",
        Description = "Returnerar alla klasser där den inloggade användaren är skapare (CreatedById)."
    )]
    [ProducesResponseType(typeof(IReadOnlyList<MyClassDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MyClassDto>>> GetMyCreatedClasses(CancellationToken ct)
    {
        var teacherId = GetUserIdFromClaims();
        var classes = await _classService.GetMyCreatedClassesAsync(teacherId, ct);
        return Ok(classes);
    }

    [HttpGet("{classId:guid}/students")]
    [SwaggerOperation(
        Summary = "Hämtar elever i en av lärarens klasser",
        Description = "Returnerar 'student'-medlemmar om den inloggade är ägare eller lärare i klassen. Inkluderar FullName."
    )]
    [ProducesResponseType(typeof(IReadOnlyList<MemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> GetStudentsInCreatedClass(Guid classId, CancellationToken ct)
    {
        try
        {
            var teacherId = GetUserIdFromClaims();
            var students = await _classService.GetStudentsForCreatedClassAsync(classId, teacherId, ct);
            return Ok(students);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private Guid GetUserIdFromClaims()
    {
        var s = User.FindFirstValue("sub")
                 ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? User.FindFirstValue("id");
        if (!Guid.TryParse(s, out var id))
            throw new UnauthorizedAccessException("Missing or invalid user id claim.");
        return id;
    }
}
