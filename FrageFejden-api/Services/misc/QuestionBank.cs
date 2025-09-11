using FrageFejden.Entities;
using FrageFejden_api.Entities.Tables;
using System.Text;
using System.Text.Json;

public class QuestionBank
{
    private readonly List<DailyQuestion> _all = new();
    private readonly Random _rng = new();

    public QuestionBank(IWebHostEnvironment env, ILogger<QuestionBank> logger)
    {
        var path = Path.Combine(env.ContentRootPath, "Entities", "Questions.json");
        if (!File.Exists(path))
        {
            logger.LogError("Questions file not found at {Path}", path);
            throw new FileNotFoundException(path);
        }

        // Read raw bytes once
        var bytes = File.ReadAllBytes(path);

        // Try UTF-8 first; if it produced replacement chars (�) or “Ã…/Ã„/Ã–” patterns, fall back to 1252
        string text = DecodeUtf8Or1252(bytes);

        var items = JsonSerializer.Deserialize<List<JsonQuestion>>(text,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        int id = 1;
        foreach (var q in items)
        {
            _all.Add(new DailyQuestion
            {
                Id = id++,
                Category = q.category ?? "",
                QuestionText = q.question ?? "",
                Alternativ = q.alternativ ?? new List<string>(),
                RattSvar = q.ratt_svar ?? ""
            });
        }

        logger.LogInformation("Loaded {Count} questions from {Path}", _all.Count, path);
    }

    // --- helpers ---
    private static string DecodeUtf8Or1252(byte[] bytes)
    {
        // UTF-8 attempt
        var utf8 = Encoding.UTF8.GetString(bytes);

        // If we see replacement chars or classic mojibake markers, use 1252
        if (utf8.Contains('\uFFFD') || utf8.Contains("Ã…") || utf8.Contains("Ã„") || utf8.Contains("Ã–")
            || utf8.Contains("Ã¥") || utf8.Contains("Ã¤") || utf8.Contains("Ã¶") || utf8.Contains("Ã¼"))
        {
            var win1252 = Encoding.GetEncoding(1252);
            return win1252.GetString(bytes);
        }
        return utf8;
    }

    public DailyQuestion GetById(int id) => _all.First(q => q.Id == id);
    public int Count => _all.Count;
    public DailyQuestion GetRandom() => _all[_rng.Next(_all.Count)];

    private class JsonQuestion
    {
        public string? category { get; set; }
        public string? question { get; set; }
        public List<string>? alternativ { get; set; }
        public string? ratt_svar { get; set; }
    }
}
