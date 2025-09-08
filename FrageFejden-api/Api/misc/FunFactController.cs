using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FrageFejden_api.Controllers;

public class FunFacts
{
    public List<string> fun_facts { get; set; } = new();
}

[ApiController]
[Route("api/[controller]")]
public class FunFactController : ControllerBase
{
    private readonly string _jsonPath;
    private readonly Random _rng = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public FunFactController(IWebHostEnvironment env)
    {
        
        _jsonPath = Path.Combine(env.ContentRootPath, "Entities", "fun_facts_sv.json");
    }

    [HttpGet]
    public IActionResult GetRandom()
    {
        if (!System.IO.File.Exists(_jsonPath))
        {
            return NotFound(new { error = $"Filen saknas: {_jsonPath}" });
        }

        FunFacts? allFacts;
        try
        {
            var json = System.IO.File.ReadAllText(_jsonPath);
            allFacts = JsonSerializer.Deserialize<FunFacts>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message, title: "Kunde inte läsa fun_facts_sv.json");
        }

        if (allFacts is null || allFacts.fun_facts.Count == 0)
        {
            return NotFound(new { error = "Inga fun facts hittades i filen." });
        }

        var fact = allFacts.fun_facts[_rng.Next(allFacts.fun_facts.Count)];
        return Ok(new { fact });
    }
}
