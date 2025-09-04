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

        /// <summary>
        /// Create a new duel
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<DuelDto>> CreateDuel([FromBody] CreateDuelRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var duel = await _duelService.CreateDuelAsync(userId, request.SubjectId, request.LevelId, request.BestOf);

                var duelDto = MapDuelToDto(duel, userId);
                return Ok(duelDto);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid("You don't have access to create duels for this subject");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating duel");
                return StatusCode(500, "An error occurred while creating the duel");
            }
        }

        /// <summary>
        /// Invite a classmate to a duel
        /// </summary>
        [HttpPost("invite")]
        public async Task<ActionResult> InviteToDuel([FromBody] InviteToDuelRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _duelService.InviteToQuelAsync(request.DuelId, userId, request.InviteeId);

                return Ok(new { message = "Invitation sent successfully" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inviting user to duel");
                return StatusCode(500, "An error occurred while sending the invitation");
            }
        }

        /// <summary>
        /// Accept a duel invitation
        /// </summary>
        [HttpPost("accept")]
        public async Task<ActionResult> AcceptDuelInvitation([FromBody] DuelActionRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var success = await _duelService.AcceptDuelInvitationAsync(request.DuelId, userId);

                if (!success)
                    return BadRequest("Unable to accept invitation");

                return Ok(new { message = "Invitation accepted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting duel invitation");
                return StatusCode(500, "An error occurred while accepting the invitation");
            }
        }

        /// <summary>
        /// Decline a duel invitation
        /// </summary>
        [HttpPost("decline")]
        public async Task<ActionResult> DeclineDuelInvitation([FromBody] DuelActionRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var success = await _duelService.DeclineDuelInvitationAsync(request.DuelId, userId);

                if (!success)
                    return BadRequest("Unable to decline invitation");

                return Ok(new { message = "Invitation declined successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error declining duel invitation");
                return StatusCode(500, "An error occurred while declining the invitation");
            }
        }

        /// <summary>
        /// Start a ready duel
        /// </summary>
        [HttpPost("{duelId}/start")]
        public async Task<ActionResult> StartDuel(Guid duelId)
        {
            try
            {
                var success = await _duelService.StartDuelAsync(duelId);

                if (!success)
                    return BadRequest("Unable to start duel");

                return Ok(new { message = "Duel started successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting duel");
                return StatusCode(500, "An error occurred while starting the duel");
            }
        }

        /// <summary>
        /// Submit an answer for the current round
        /// </summary>
        [HttpPost("answer")]
        public async Task<ActionResult> SubmitAnswer([FromBody] SubmitDuelAnswerRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var success = await _duelService.SubmitRoundAnswerAsync(
                    request.DuelId, userId, request.QuestionId, request.SelectedOptionId, request.TimeMs);

                if (!success)
                    return BadRequest("Unable to submit answer");

                return Ok(new { message = "Answer submitted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting duel answer");
                return StatusCode(500, "An error occurred while submitting the answer");
            }
        }

        /// <summary>
        /// Get a specific duel by ID
        /// </summary>
        [HttpGet("{duelId}")]
        public async Task<ActionResult<DuelDto>> GetDuel(Guid duelId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var duel = await _duelService.GetDuelByIdAsync(duelId);

                if (duel == null)
                    return NotFound("Duel not found");

                // Check if user is a participant
                if (!duel.Participants.Any(p => p.UserId == userId))
                    return Forbid("You are not a participant in this duel");

                var duelDto = MapDuelToDto(duel, userId);
                return Ok(duelDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting duel");
                return StatusCode(500, "An error occurred while retrieving the duel");
            }
        }

        /// <summary>
        /// Get user's duels with optional status filter
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<DuelDto>>> GetUserDuels([FromQuery] DuelStatus? status = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var duels = await _duelService.GetUserDuelsAsync(userId, status);

                var duelDtos = duels.Select(d => MapDuelToDto(d, userId)).ToList();
                return Ok(duelDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user duels");
                return StatusCode(500, "An error occurred while retrieving duels");
            }
        }

        /// <summary>
        /// Get pending duel invitations for the current user
        /// </summary>
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
                    Subject = new SubjectDto
                    {
                        Id = d.Subject.Id,
                        Name = d.Subject.Name,
                        Description = d.Subject.Description
                    },
                    Level = d.Level != null ? new LevelDto
                    {
                        Id = d.Level.Id,
                        LevelNumber = d.Level.LevelNumber,
                        Title = d.Level.Title,
                        MinXpUnlock = d.Level.MinXpUnlock
                    } : null,
                    InvitedBy = new UserDto
                    {
                        Id = d.Participants.First(p => p.InvitedById == null).User.Id,
                        FullName = d.Participants.First(p => p.InvitedById == null).User.FullName,
                        AvatarUrl = d.Participants.First(p => p.InvitedById == null).User.AvatarUrl
                    },
                    BestOf = d.BestOf,
                    CreatedAt = d.StartedAt ?? DateTime.UtcNow
                }).ToList();

                return Ok(invitations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending invitations");
                return StatusCode(500, "An error occurred while retrieving invitations");
            }
        }

        /// <summary>
        /// Get classmates available for dueling in a specific subject
        /// </summary>
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
                    IsAvailable = true // You might want to add logic to check if they're currently in a duel
                }).ToList();

                return Ok(classmates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting classmates for duel");
                return StatusCode(500, "An error occurred while retrieving classmates");
            }
        }

        /// <summary>
        /// Get user's duel statistics
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<DuelStatsDto>> GetDuelStats([FromQuery] Guid? subjectId = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                var stats = await _duelService.GetUserDuelStatsAsync(userId, subjectId);

                var statsDto = new DuelStatsDto
                {
                    TotalDuels = stats.TotalDuels,
                    Wins = stats.Wins,
                    Losses = stats.Losses,
                    Draws = stats.Draws,
                    WinRate = stats.WinRate,
                    CurrentStreak = stats.CurrentStreak,
                    BestStreak = stats.BestStreak
                };

                return Ok(statsDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting duel stats");
                return StatusCode(500, "An error occurred while retrieving statistics");
            }
        }

        /// <summary>
        /// Complete a duel (admin or system use)
        /// </summary>
        [HttpPost("{duelId}/complete")]
        public async Task<ActionResult> CompleteDuel(Guid duelId)
        {
            try
            {
                var success = await _duelService.CompleteDuelAsync(duelId);

                if (!success)
                    return BadRequest("Unable to complete duel");

                return Ok(new { message = "Duel completed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing duel");
                return StatusCode(500, "An error occurred while completing the duel");
            }
        }

        private DuelDto MapDuelToDto(Duel duel, Guid currentUserId)
        {
            return new DuelDto
            {
                Id = duel.Id,
                Subject = new SubjectDto
                {
                    Id = duel.Subject.Id,
                    Name = duel.Subject.Name,
                    Description = duel.Subject.Description
                },
                Level = duel.Level != null ? new LevelDto
                {
                    Id = duel.Level.Id,
                    LevelNumber = duel.Level.LevelNumber,
                    Title = duel.Level.Title,
                    MinXpUnlock = duel.Level.MinXpUnlock
                } : null,
                Status = duel.Status,
                BestOf = duel.BestOf,
                StartedAt = duel.StartedAt,
                EndedAt = duel.EndedAt,
                Participants = duel.Participants.Select(p => new DuelParticipantDto
                {
                    Id = p.Id,
                    User = new UserDto
                    {
                        Id = p.User.Id,
                        FullName = p.User.FullName,
                        AvatarUrl = p.User.AvatarUrl
                    },
                    InvitedBy = p.InvitedBy != null ? new UserDto
                    {
                        Id = p.InvitedBy.Id,
                        FullName = p.InvitedBy.FullName,
                        AvatarUrl = p.InvitedBy.AvatarUrl
                    } : null,
                    Score = p.Score,
                    Result = p.Result,
                    IsCurrentUser = p.UserId == currentUserId
                }).ToList(),
                Rounds = duel.Rounds.OrderBy(r => r.RoundNumber).Select(r => new DuelRoundDto
                {
                    Id = r.Id,
                    RoundNumber = r.RoundNumber,
                    Question = new QuestionDto
                    {
                        Id = r.Question.Id,
                        Type = r.Question.Type,
                        Difficulty = r.Question.Difficulty,
                        Stem = r.Question.Stem,
                        Explanation = r.Question.Explanation,
                        Options = r.Question.Options.OrderBy(o => o.SortOrder).Select(o => new QuestionOptionDto
                        {
                            Id = o.Id,
                            OptionText = o.OptionText,
                            SortOrder = o.SortOrder
                        }).ToList()
                    },
                    TimeLimitSeconds = r.TimeLimitSeconds
                }).ToList(),
                CurrentRound = duel.Status == DuelStatus.active ?
                    duel.Rounds.OrderByDescending(r => r.RoundNumber).Select(r => new DuelRoundDto
                    {
                        Id = r.Id,
                        RoundNumber = r.RoundNumber,
                        Question = new QuestionDto
                        {
                            Id = r.Question.Id,
                            Type = r.Question.Type,
                            Difficulty = r.Question.Difficulty,
                            Stem = r.Question.Stem,
                            Options = r.Question.Options.OrderBy(o => o.SortOrder).Select(o => new QuestionOptionDto
                            {
                                Id = o.Id,
                                OptionText = o.OptionText,
                                SortOrder = o.SortOrder
                            }).ToList()
                        },
                        TimeLimitSeconds = r.TimeLimitSeconds
                    }).FirstOrDefault() : null
            };
        }
    }
}