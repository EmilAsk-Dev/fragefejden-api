using FrageFejden_api.Entities.Tables;
using Microsoft.EntityFrameworkCore;
using TimeZoneConverter;

public class DailyQuestionService
{
    private readonly AppDbContext _db;
    private readonly QuestionBank _bank;
    private readonly TimeZoneInfo _tz = SafeStockholm();

    public DailyQuestionService(AppDbContext db, QuestionBank bank)
    {
        _db = db; _bank = bank;
    }

    private static TimeZoneInfo SafeStockholm()
    {
        try { return TimeZoneConverter.TZConvert.GetTimeZoneInfo("Europe/Stockholm"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); }
    }

    private DateOnly TodayStockholm()
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _tz);
        return DateOnly.FromDateTime(local.Date);
    }

    private DateTimeOffset NowLocal() => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _tz);

    public async Task<(UserDailyQuestion row, DailyQuestion q)> GetOrCreateTodayAsync(string userId)
    {
        var today = TodayStockholm();

        // ✅ Query by columns (no composite PK required)
        var row = await _db.UserDailyQuestions
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Date == today);

        if (row is null)
        {
            var q = _bank.GetRandom();
            row = new UserDailyQuestion
            {
                
                UserId = userId,
                Date = today,
                QuestionId = q.Id,
                CreatedAt = NowLocal()
            };

            _db.UserDailyQuestions.Add(row);
            await _db.SaveChangesAsync();
            return (row, q);
        }

        return (row, _bank.GetById(row.QuestionId));
    }

    public async Task<(bool accepted, bool correct, string message)> SubmitAnswerAsync(string userId, string answer)
    {
        var today = TodayStockholm();

        
        var row = await _db.UserDailyQuestions
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Date == today);

        if (row is null) return (false, false, "Ingen dagens fråga. Hämta först.");

        if (row.Answered)
            return (false, row.Correct ?? false, "Du har redan svarat idag.");

        var q = _bank.GetById(row.QuestionId);
        var correct = string.Equals(answer?.Trim(), q.RattSvar, StringComparison.OrdinalIgnoreCase);

        row.Answered = true;
        row.Correct = correct;
        row.AnswerGiven = answer;
        row.AnsweredAt = NowLocal();

        await _db.SaveChangesAsync();

        return (true, correct, correct ? "Rätt svar!" : $"Fel svar. Rätt svar är: {q.RattSvar}");
    }

    public async Task<UserDailyStats> GetStatsAsync(string userId)
    {
        var answeredDays = await _db.UserDailyQuestions
            .Where(x => x.UserId == userId && x.Answered)
            .Select(x => x.Date)
            .OrderBy(d => d)
            .ToListAsync();

        var total = answeredDays.Count;
        var today = TodayStockholm();
        var last = total > 0 ? answeredDays[^1] : (DateOnly?)null;

        
        int current = 0;
        if (last.HasValue && (last == today || last == today.AddDays(-1)))
        {
            current = 1;
            var expected = last.Value;
            for (int i = answeredDays.Count - 2; i >= 0; i--)
            {
                if (answeredDays[i] == expected.AddDays(-1))
                {
                    current++;
                    expected = answeredDays[i];
                }
                else break;
            }
        }

        
        int longest = 0, run = 0;
        DateOnly? prev = null;
        foreach (var d in answeredDays)
        {
            if (prev is null || d == prev.Value.AddDays(1))
            {
                run++;
            }
            else
            {
                longest = Math.Max(longest, run);
                run = 1;
            }
            prev = d;
        }
        longest = Math.Max(longest, run);


        // Veckomål mån–fre
        const int WEEK_GOAL = 5;

        static DayOfWeek DayOfWeekFor(DateOnly d)
            => d.ToDateTime(TimeOnly.MinValue).DayOfWeek;

        static bool IsWeekday(DateOnly d)
        {
            var dow = DayOfWeekFor(d);
            return dow != DayOfWeek.Saturday && dow != DayOfWeek.Sunday;
        }

        // hitta måndag denna vecka
        int offsetToMonday = ((int)DayOfWeekFor(today) + 6) % 7;
        var monday = today.AddDays(-offsetToMonday);
        var weekEndExclusive = monday.AddDays(7);

        var weekAnswered = answeredDays
            .Where(d => d >= monday && d < weekEndExclusive)
            .Where(IsWeekday)
            .Distinct()
            .Count();

        return new UserDailyStats
        {
            TotalAnswered = total,
            CurrentStreak = current,
            LongestStreak = longest,
            LastAnsweredDate = last,

            // returnera veckomål
            WeekAnswered = weekAnswered,  
            WeekGoal = WEEK_GOAL
        };
    }

    public class UserDailyStats
    {
        public int TotalAnswered { get; set; }
        public int CurrentStreak { get; set; }
        public int LongestStreak { get; set; }
        public DateOnly? LastAnsweredDate { get; set; }

        public int WeekAnswered { get; set; }   // hur många denna vecka (mån–fre)
        public int WeekGoal { get; set; } = 5;  // standardmål per vecka = 5
    }
}
