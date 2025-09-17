using FrageFejden.DTOs.Duel;
using FrageFejden.Entities;
using FrageFejden.Entities.Enums;
using FrageFejden.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FrageFejden.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DuelController : ControllerBase
    {
        private readonly IDuelService _duelService;
        private readonly ILogger<DuelController> _logger;

        public DuelController(IDuelService duelService, ILogger<DuelController> logger)
        {
            _duelService = duelService;
            _logger = logger;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.Parse(userIdClaim!);
        }

        [HttpPost]
        public async Task<ActionResult<DuelDto>> CreateDuel([FromBody] CreateDuelRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var duel = await _duelService.CreateDuelAsync(userId, request.SubjectId, request.LevelId, request.BestOf);

                // ✅ RELOAD WITH NAVIGATIONS
                var full = await _duelService.GetDuelByIdAsync(duel.Id);
                if (full == null) return NotFound("Duel not found after creation");

                var duelDto = MapDuelToDto(full, userId);
                return Ok(duelDto);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid("You don't have access to create duels for this subject");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating duel");
                return StatusCode(500, "An error occurred while creating the duel");
            }
        }

        [HttpPost("invite")]
        public async Task<ActionResult> InviteToDuel([FromBody] InviteToDuelRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _duelService.InviteToQuelAsync(request.DuelId, userId, request.InviteeId);
                return Ok(new { message = "Invitation sent successfully" });
            }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inviting user to duel");
                return StatusCode(500, "An error occurred while sending the invitation");
            }
        }

        [HttpPost("accept")]
        public async Task<ActionResult> AcceptDuelInvitation([FromBody] DuelActionRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var ok = await _duelService.AcceptDuelInvitationAsync(request.DuelId, userId);
                if (!ok) return BadRequest("Unable to accept invitation");
                return Ok(new { message = "Invitation accepted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting duel invitation");
                return StatusCode(500, "An error occurred while accepting the invitation");
            }
        }

        [HttpPost("decline")]
        public async Task<ActionResult> DeclineDuelInvitation([FromBody] DuelActionRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var ok = await _duelService.DeclineDuelInvitationAsync(request.DuelId, userId);
                if (!ok) return BadRequest("Unable to decline invitation");
                return Ok(new { message = "Invitation declined successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error declining duel invitation");
                return StatusCode(500, "An error occurred while declining the invitation");
            }
        }

        [HttpPost("{duelId}/start")]
        public async Task<ActionResult> StartDuel(Guid duelId)
        {
            try
            {
                var ok = await _duelService.StartDuelAsync(duelId);
                if (!ok) return BadRequest("Unable to start duel");
                return Ok(new { message = "Duel started successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting duel");
                return StatusCode(500, "An error occurred while starting the duel");
            }
        }

        [HttpPost("answer")]
        public async Task<ActionResult> SubmitAnswer([FromBody] SubmitDuelAnswerRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var ok = await _duelService.SubmitRoundAnswerAsync(
                    request.DuelId, userId, request.QuestionId, request.SelectedOptionId, request.TimeMs);

                if (!ok) return BadRequest("Unable to submit answer");
                return Ok(new { message = "Answer submitted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting duel answer");
                return StatusCode(500, "An error occurred while submitting the answer");
            }
        }

        [HttpGet("{duelId}")]
        public async Task<ActionResult<DuelDto>> GetDuel(Guid duelId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var duel = await _duelService.GetDuelByIdAsync(duelId);
                if (duel == null) return NotFound("Duel not found");
                if (!duel.Participants.Any(p => p.UserId == userId)) return Forbid("You are not a participant in this duel");
                return Ok(MapDuelToDto(duel, userId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting duel");
                return StatusCode(500, "An error occurred while retrieving the duel");
            }
        }

        [HttpGet]
        public async Task<ActionResult<List<DuelDto>>> GetUserDuels([FromQuery] DuelStatus? status = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var duels = await _duelService.GetUserDuelsAsync(userId, status);
                return Ok(duels.Select(d => MapDuelToDto(d, userId)).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user duels");
                return StatusCode(500, "An error occurred while retrieving duels");
            }
        }

        [HttpGet("invitations")]
        public async Task<ActionResult<List<DuelInvitationDto>>> GetPendingInvitations()
        {
            try
            {
                var userId = GetCurrentUserId();
                var duels = await _duelService.GetPendingInvitationsAsync(userId);

                var invitations = duels.Select(d => new DuelInvitationDto
                {
                    Id = d.Id,
                    Subject = new SubjectDto { Id = d.Subject.Id, Name = d.Subject.Name, Description = d.Subject.Description },
                    Level = d.Level != null ? new LevelDto { Id = d.Level.Id, LevelNumber = d.Level.LevelNumber, Title = d.Level.Title, MinXpUnlock = d.Level.MinXpUnlock } : null,
                    InvitedBy = new UserDto
                    {
                        Id = d.Participants.First(p => p.InvitedById == null).User.Id,
                        FullName = d.Participants.First(p => p.InvitedById == null).User.FullName,
                        AvatarUrl = d.Participants.First(p => p.InvitedById == null).User.AvatarUrl
                    },
                    BestOf = d.BestOf,
                    CreatedAt = d.CreatedAt
                }).ToList();

                return Ok(invitations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending invitations");
                return StatusCode(500, "An error occurred while retrieving invitations");
            }
        }

        [HttpGet("classmates/{subjectId}")]
        public async Task<ActionResult<List<ClassmateDto>>> GetClassmatesForDuel(Guid subjectId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var participants = await _duelService.GetClassmatesForDuelAsync(userId, subjectId);

                var classmates = participants.Select(p => new ClassmateDto
                {
                    Id = p.User.Id,
                    FullName = p.User.FullName,
                    AvatarUrl = p.User.AvatarUrl,
                    IsAvailable = true
                }).ToList();

                return Ok(classmates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting classmates for duel");
                return StatusCode(500, "An error occurred while retrieving classmates");
            }
        }

        [HttpGet("stats")]
        public async Task<ActionResult<DuelStatsDto>> GetDuelStats([FromQuery] Guid? subjectId = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var stats = await _duelService.GetUserDuelStatsAsync(userId, subjectId);

                return Ok(new DuelStatsDto
                {
                    TotalDuels = stats.TotalDuels,
                    Wins = stats.Wins,
                    Losses = stats.Losses,
                    Draws = stats.Draws,
                    WinRate = stats.WinRate,
                    CurrentStreak = stats.CurrentStreak,
                    BestStreak = stats.BestStreak
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting duel stats");
                return StatusCode(500, "An error occurred while retrieving statistics");
            }
        }

        [HttpPost("{duelId}/complete")]
        public async Task<ActionResult> CompleteDuel(Guid duelId)
        {
            try
            {
                var ok = await _duelService.CompleteDuelAsync(duelId);
                if (!ok) return BadRequest("Unable to complete duel");
                return Ok(new { message = "Duel completed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing duel");
                return StatusCode(500, "An error occurred while completing the duel");
            }
        }

        private static QuestionDto BuildQuestionDtoFromSnapshot(DuelRound r)
        {
            var q = r.Question; 
            var altTexts = (r.AlternativesSnapshot != null && r.AlternativesSnapshot.Count > 0)
                ? r.AlternativesSnapshot
                : (q?.Options?.OrderBy(o => o.SortOrder).Select(o => o.OptionText).ToList() ?? new List<string>());

            return new QuestionDto
            {
                Id = r.QuestionId,
                Type = q?.Type ?? default,          
                Difficulty = q?.Difficulty ?? default,
                Stem = r.TextSnapshot ?? q?.Stem ?? string.Empty,
                Explanation = q?.Explanation,
                Options = altTexts.Select((t, i) => new QuestionOptionDto
                {
                    Id = Guid.Empty,                
                    OptionText = t,
                    SortOrder = i
                }).ToList()
            };
        }

        private DuelDto MapDuelToDto(Duel duel, Guid currentUserId)
        {
            
            var subjectDto = (duel.Subject != null)
                ? new SubjectDto { Id = duel.Subject.Id, Name = duel.Subject.Name, Description = duel.Subject.Description }
                : new SubjectDto { Id = duel.SubjectId, Name = "(Ämne)", Description = null };

            var levelDto = (duel.Level != null)
                ? new LevelDto { Id = duel.Level.Id, LevelNumber = duel.Level.LevelNumber, Title = duel.Level.Title, MinXpUnlock = duel.Level.MinXpUnlock }
                : null;

            var participants = (duel.Participants ?? Enumerable.Empty<DuelParticipant>()).Select(p => new DuelParticipantDto
            {
                Id = p.Id,
                User = (p.User != null)
                    ? new UserDto { Id = p.User.Id, FullName = p.User.FullName, AvatarUrl = p.User.AvatarUrl }
                    : new UserDto { Id = p.UserId, FullName = "(Okänd)", AvatarUrl = null },
                InvitedBy = (p.InvitedBy != null)
                    ? new UserDto { Id = p.InvitedBy.Id, FullName = p.InvitedBy.FullName, AvatarUrl = p.InvitedBy.AvatarUrl }
                    : null,
                Score = p.Score,
                Result = p.Result,
                IsCurrentUser = p.UserId == currentUserId
            }).ToList();

            var roundsOrdered = (duel.Rounds ?? Enumerable.Empty<DuelRound>())
                .OrderBy(r => r.RoundNumber)
                .ToList();

            var rounds = roundsOrdered.Select(r => new DuelRoundDto
            {
                Id = r.Id,
                RoundNumber = r.RoundNumber,
                Question = BuildQuestionDtoFromSnapshot(r),
                TimeLimitSeconds = r.TimeLimitSeconds
            }).ToList();

            var currentRound = duel.Status == DuelStatus.active
                ? roundsOrdered
                    .OrderByDescending(r => r.RoundNumber)
                    .Select(r => new DuelRoundDto
                    {
                        Id = r.Id,
                        RoundNumber = r.RoundNumber,
                        Question = BuildQuestionDtoFromSnapshot(r),
                        TimeLimitSeconds = r.TimeLimitSeconds
                    })
                    .FirstOrDefault()
                : null;

            return new DuelDto
            {
                Id = duel.Id,
                Subject = subjectDto,
                Level = levelDto,
                Status = duel.Status,
                BestOf = duel.BestOf,
                StartedAt = duel.StartedAt,
                EndedAt = duel.EndedAt,
                Participants = participants,
                Rounds = rounds,
                CurrentRound = currentRound
            };
        }
    }
}
