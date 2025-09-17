using Microsoft.EntityFrameworkCore;

public class ScoreService
{
    private readonly AppDbContext _db;

    public ScoreService(AppDbContext db)
    {
        _db = db;
    }

    public class SubjectScoreDto
    {
        public Guid UserId { get; set; }
        public int TotalExp { get; set; }
    }

    public async Task AddExpAsync(Guid userId, Guid subjectId, int exp)
    {
        var entry = new ScoreEntry
        {
            UserId = userId,
            SubjectId = subjectId,
            exp = exp,
            CreatedAt = DateTime.UtcNow
        };

        _db.ScoreEntries.Add(entry);

        //Uppdatera användarens totala poäng i usertabellen
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            var totalExp = await _db.ScoreEntries
                .Where(s => s.UserId == userId)
                .SumAsync(s => s.exp);
        }


        await _db.SaveChangesAsync();
    }

    // Hämtar total exp för en användare inom ett specifikt ämne
    public async Task<int> GetSubjectExperienceAsync(Guid userId, Guid subjectId)
    {
        return await _db.ScoreEntries
            .Where(s => s.UserId == userId && s.SubjectId == subjectId)
            .SumAsync(s => s.exp);
    }

    //Skapar en leaderboard för ett specifikt ämne
public async Task<List<SubjectScoreDto>> GetSubjectLeaderboardAsync(Guid subjectId)
    {
        var scores = await _db.ScoreEntries
            .Where(s => s.SubjectId == subjectId)
            .GroupBy(s => s.UserId)
            .Select(g => new SubjectScoreDto
            {
                UserId = g.Key,
                TotalExp = g.Sum(x => x.exp)
            })
            .OrderByDescending(x => x.TotalExp)
            .ToListAsync();

        return scores;
    }



}