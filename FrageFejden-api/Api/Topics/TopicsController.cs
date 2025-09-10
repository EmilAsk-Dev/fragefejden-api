using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FrageFejden.Entities;
using FrageFejden.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using FrageFejden_api.Api;

namespace FrageFejden.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Tags("Topics")]
    public class TopicsController : ControllerBase
    {
        private readonly ITopicService _topics;
        private readonly AppDbContext _db;

        public TopicsController(ITopicService topics, AppDbContext db)
        {
            _topics = topics;
            _db = db;
        }

        // ===== DTOs (local) =====
        public class TopicDto
        {
            public Guid Id { get; set; }
            public Guid SubjectId { get; set; }
            public string Name { get; set; } = null!;
            public string? Description { get; set; }
            public int SortOrder { get; set; }
            public int LevelCount { get; set; }
        }

        public class TopicCreateDto
        {
            public Guid SubjectId { get; set; }
            public string Name { get; set; } = null!;
            public string? Description { get; set; }
            public int? SortOrder { get; set; }
        }

        public class TopicUpdateDto
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
            public int? SortOrder { get; set; }
        }

        public class LevelCreateDto
        {
            public int LevelNumber { get; set; }
            public string? Title { get; set; }
            public int MinXpUnlock { get; set; } = 0;
        }

        public class LevelUpdateDto
        {
            public int? LevelNumber { get; set; }
            public string? Title { get; set; }
            public int? MinXpUnlock { get; set; }
        }

        // ======================
        // Topic CRUD / queries
        // ======================

        [HttpGet("{topicId:guid}")]
        [SwaggerOperation(Summary = "Get a topic by id")]
        [ProducesResponseType(typeof(TopicDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTopic(Guid topicId)
        {
            if (!UserIsIn("admin", "teacher", "student")) return Forbid();

            var topic = await _topics.GetTopicAsync(topicId);
            return topic is null ? NotFound() : Ok(ToDto(topic));
        }

        [HttpGet("by-subject/{subjectId:guid}")]
        [SwaggerOperation(Summary = "List topics for a subject")]
        [ProducesResponseType(typeof(IEnumerable<TopicSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTopicsForSubject(Guid subjectId)
        {
            if (!UserIsIn("admin", "teacher", "student")) return Forbid();

            var items = await _topics.GetTopicsForSubjectAsync(subjectId);
            return Ok(items);
        }

        [HttpPost]
        [SwaggerOperation(Summary = "Create a topic under a subject")]
        [ProducesResponseType(typeof(TopicDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateTopic([FromBody] TopicCreateDto body)
        {
            if (!UserIsIn("admin", "teacher")) return Forbid();
            if (body is null) return BadRequest("Body required.");
            if (body.SubjectId == Guid.Empty) return BadRequest("SubjectId required.");

            var created = await _topics.CreateTopicAsync(body.SubjectId, body.Name, body.Description, body.SortOrder);
            return CreatedAtAction(nameof(GetTopic), new { topicId = created.Id }, ToDto(created));
        }

        [HttpPut("{topicId:guid}")]
        [SwaggerOperation(Summary = "Update a topic")]
        [ProducesResponseType(typeof(TopicDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateTopic(Guid topicId, [FromBody] TopicUpdateDto body)
        {
            if (!UserIsIn("admin", "teacher")) return Forbid();
            if (body is null) return BadRequest("Body required.");

            var ok = await _topics.UpdateTopicAsync(topicId, body.Name, body.Description, body.SortOrder);
            if (!ok) return NotFound();

            var updated = await _topics.GetTopicAsync(topicId);
            return Ok(ToDto(updated!));
        }

        [HttpDelete("{topicId:guid}")]
        [SwaggerOperation(Summary = "Delete a topic (levels are cascade-deleted)")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteTopic(Guid topicId)
        {
            if (!UserIsIn("admin", "teacher")) return Forbid();

            var ok = await _topics.DeleteTopicAsync(topicId);
            return ok ? NoContent() : NotFound();
        }

        // ======================
        // Levels under a Topic
        // ======================

        [HttpGet("{topicId:guid}/levels")]
        [SwaggerOperation(Summary = "List levels for a topic")]
        [ProducesResponseType(typeof(IEnumerable<LevelDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLevels(Guid topicId)
        {
            if (!UserIsIn("admin", "teacher", "student")) return Forbid();

            var levels = await _topics.GetLevelsForTopicAsync(topicId);
            return Ok(levels);
        }

        [HttpPost("{topicId:guid}/levels")]
        [SwaggerOperation(Summary = "Create a level in a topic")]
        [ProducesResponseType(typeof(LevelDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateLevel(Guid topicId, [FromBody] LevelCreateDto body)
        {
            if (!UserIsIn("admin", "teacher")) return Forbid();
            if (body is null) return BadRequest("Body required.");

            var created = await _topics.CreateLevelAsync(topicId, body.LevelNumber, body.Title, body.MinXpUnlock);

            var dto = new LevelDto
            {
                LevelId = created.Id,
                TopicId = created.TopicId,
                LevelNumber = created.LevelNumber,
                Title = created.Title,
                MinXpUnlock = created.MinXpUnlock
            };

            return CreatedAtAction(nameof(GetLevels), new { topicId }, dto);
        }

        [HttpPut("levels/{levelId:guid}")]
        [SwaggerOperation(Summary = "Update a level")]
        [ProducesResponseType(typeof(LevelDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateLevel(Guid levelId, [FromBody] LevelUpdateDto body)
        {
            if (!UserIsIn("admin", "teacher")) return Forbid();
            if (body is null) return BadRequest("Body required.");

            var ok = await _topics.UpdateLevelAsync(levelId, body.LevelNumber, body.Title, body.MinXpUnlock);
            if (!ok) return NotFound();

            // Reload level via its topic for a consistent DTO
            var topicId = await FindLevelTopicId(levelId);
            if (topicId is null) return NotFound();

            var levels = await _topics.GetLevelsForTopicAsync(topicId.Value);
            var level = levels.FirstOrDefault(l => l.LevelId == levelId);
            return level is null ? NotFound() : Ok(level);
        }

        [HttpDelete("levels/{levelId:guid}")]
        [SwaggerOperation(Summary = "Delete a level")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteLevel(Guid levelId)
        {
            if (!UserIsIn("admin", "teacher")) return Forbid();

            var ok = await _topics.DeleteLevelAsync(levelId);
            return ok ? NoContent() : NotFound();
        }

        // ======================
        // Progress & Quizzes
        // ======================

        [HttpGet("{topicId:guid}/progress")]
        [SwaggerOperation(Summary = "Get level progress for a topic (current user)")]
        [ProducesResponseType(typeof(TopicProgressDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTopicProgress(Guid topicId)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();
            if (!UserIsIn("admin", "teacher", "student")) return Forbid();

            var result = await _topics.GetTopicProgressAsync(topicId, userId.Value);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("{topicId:guid}/quizzes")]
        [SwaggerOperation(Summary = "List quizzes scoped to a topic")]
        [ProducesResponseType(typeof(IEnumerable<Quiz>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTopicQuizzes(Guid topicId, [FromQuery] bool onlyPublished = true)
        {
            if (!UserIsIn("admin", "teacher", "student")) return Forbid();

            var items = await _topics.GetTopicQuizzesAsync(topicId, onlyPublished);
            return Ok(items);
        }

        // ===== helpers =====
        private static TopicDto ToDto(Topic t) => new TopicDto
        {
            Id = t.Id,
            SubjectId = t.SubjectId,
            Name = t.Name,
            Description = t.Description,
            SortOrder = t.SortOrder,
            LevelCount = t.Levels?.Count ?? 0
        };

        private Guid? GetUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }

        private bool UserIsIn(params string[] rolesAllowed)
        {
            var userRoles = GetRoleValues(User);
            return userRoles.Intersect(rolesAllowed.Select(r => r.ToLowerInvariant())).Any();
        }

        private static IEnumerable<string> GetRoleValues(ClaimsPrincipal user)
        {
            var claims = new List<string>();
            claims.AddRange(user.FindAll(ClaimTypes.Role).Select(c => c.Value));
            claims.AddRange(user.FindAll("role").Select(c => c.Value));
            claims.AddRange(user.FindAll("roles").Select(c => c.Value));

            return claims
                .SelectMany(v => v.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(v => v.Trim().ToLowerInvariant())
                .Distinct();
        }

        private async Task<Guid?> FindLevelTopicId(Guid levelId)
        {
            return await _db.Levels
                .Where(l => l.Id == levelId)
                .Select(l => (Guid?)l.TopicId)
                .FirstOrDefaultAsync();
        }
    }
}
