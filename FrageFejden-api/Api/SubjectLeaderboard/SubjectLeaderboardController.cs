using Microsoft.AspNetCore.Mvc;


[ApiController]
[Route("api/[controller]")]
public class SubjectLeaderboardController : ControllerBase
{
    private readonly ScoreService _scoreService;

    public SubjectLeaderboardController(ScoreService scoreService)
    {
        _scoreService = scoreService;
    }
    //Get /Subject/{subjectId}/scores
    //endpoint för att hämta leaderboard för ett specifikt ämne
    [HttpGet("{subjectId}/scores")]
    public async Task<IActionResult> GetSubjectScores(Guid subjectId)
    {
        var scores = await _scoreService.GetSubjectLeaderboardAsync(subjectId);
        return Ok(scores);
    }

    //Get /Subject/user/{userId}/points/{subjectId}
    //endpoint för att hämta en användares poäng inom ett specifikt ämne
    [HttpGet("user/{userId}/points/{subjectId}")]
    public async Task<IActionResult> GetUserSubjectPoints(Guid userId, Guid subjectId)
    {
        var score = await _scoreService.GetSubjectExperienceAsync(userId, subjectId);
        return Ok(new { score });
    }
}