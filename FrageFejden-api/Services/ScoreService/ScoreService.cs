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
        public string FullName { get; set; }
        public int TotalExp { get; set; }
    }

    // Lägger till exp för en användare inom ett specifikt ämne och uppdaterar totalpoäng i users-tabellen
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

        // Uppdatera användarens totalpoäng i users-tabellen
        var user = await _db.Users.FindAsync(userId);
        if (user != null)
        {
            user.experiencePoints += exp; 
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
            .ToListAsync();

    // Hämta namn från Users-tabellen
    var userIds = scores.Select(s => s.UserId).ToList();
    var users = await _db.Users
        .Where(u => userIds.Contains(u.Id))
        .ToDictionaryAsync(u => u.Id, u => u.FullName);

    var result = scores
        .Select(s => new SubjectScoreDto
        {
            UserId = s.UserId,
            TotalExp = s.TotalExp,
            FullName = users.TryGetValue(s.UserId, out var name) ? name : "Okänd"
        })
        .OrderByDescending(s => s.TotalExp)
        .ToList();

    return result;
    }



}