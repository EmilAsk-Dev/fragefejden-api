using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/daily")]
public class DailyController : ControllerBase
{
    private readonly DailyQuestionService _svc;
    private readonly QuestionBank _bank;

    public DailyController(DailyQuestionService svc, QuestionBank bank)
    {
        _svc = svc; _bank = bank;
    }

    
    private string GetUserId() =>
        HttpContext.User?.Identity?.Name
        ?? Request.Headers["X-UserId"].FirstOrDefault()
        ?? "demo-user";
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetToday()
    {
        var userId = GetUserId();
        var (row, q) = await _svc.GetOrCreateTodayAsync(userId);
        return Ok(new
        {
            date = row.Date.ToString(),
            questionId = q.Id,
            category = q.Category,
            question = q.QuestionText,
            alternativ = q.Alternativ,
            answered = row.Answered
        });
    }

    public record AnswerDto(string answer);
    [Authorize]
    [HttpPost("answer")]
    public async Task<IActionResult> Answer([FromBody] AnswerDto dto)
    {
        var userId = GetUserId();
        var (accepted, correct, message) = await _svc.SubmitAnswerAsync(userId, dto.answer);
        return accepted ? Ok(new { correct, message }) : BadRequest(new { correct, message });
    }

    [Authorize]
    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var userId = GetUserId();
        var stats = await _svc.GetStatsAsync(userId);
        return Ok(stats);
    }
}
