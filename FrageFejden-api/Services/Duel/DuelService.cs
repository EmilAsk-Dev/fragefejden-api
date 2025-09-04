
using FrageFejden.Entities;
using FrageFejden.Entities.Enums;
using FrageFejden.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FrageFejden.Services
{
    public class DuelService : IDuelService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DuelService> _logger;

        public DuelService(AppDbContext context, ILogger<DuelService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Duel> CreateDuelAsync(Guid initiatorId, Guid subjectId, Guid? levelId = null, int bestOf = 5)
        {
            try
            {
                if (!await CanUserCreateDuelAsync(initiatorId, subjectId))
                {
                    throw new UnauthorizedAccessException("User cannot create duel for this subject");
                }

                var duel = new Duel
                {
                    Id = Guid.NewGuid(),
                    SubjectId = subjectId,
                    LevelId = levelId,
                    Status = DuelStatus.pending,
                    BestOf = bestOf
                };

                _context.Duels.Add(duel);


                var initiatorParticipant = new DuelParticipant
                {
                    Id = Guid.NewGuid(),
                    DuelId = duel.Id,
                    UserId = initiatorId,
                    Score = 0
                };

                _context.Set<DuelParticipant>().Add(initiatorParticipant);
                await _context.SaveChangesAsync();

                return duel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating duel for user {UserId} in subject {SubjectId}", initiatorId, subjectId);
                throw;
            }
        }

        public async Task<DuelParticipant> InviteToQuelAsync(Guid duelId, Guid inviterId, Guid inviteeId)
        {
            try
            {
                var duel = await GetDuelByIdAsync(duelId);
                if (duel == null)
                    throw new ArgumentException("Duel not found");

                if (duel.Status != DuelStatus.pending)
                    throw new InvalidOperationException("Cannot invite to non-pending duel");


                var inviterParticipant = duel.Participants.FirstOrDefault(p => p.UserId == inviterId);
                if (inviterParticipant == null)
                    throw new UnauthorizedAccessException("User is not a participant in this duel");


                if (duel.Participants.Any(p => p.UserId == inviteeId))
                    throw new InvalidOperationException("User is already a participant in this duel");


                if (!await AreUsersInSameClassAsync(inviterId, inviteeId))
                    throw new InvalidOperationException("Users are not in the same class");

                var inviteeParticipant = new DuelParticipant
                {
                    Id = Guid.NewGuid(),
                    DuelId = duelId,
                    UserId = inviteeId,
                    InvitedById = inviterId,
                    Score = 0
                };

                _context.Set<DuelParticipant>().Add(inviteeParticipant);
                await _context.SaveChangesAsync();

                return inviteeParticipant;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inviting user {InviteeId} to duel {DuelId}", inviteeId, duelId);
                throw;
            }
        }

        public async Task<bool> AcceptDuelInvitationAsync(Guid duelId, Guid userId)
        {
            try
            {
                var duel = await GetDuelByIdAsync(duelId);
                if (duel == null) return false;

                var participant = duel.Participants.FirstOrDefault(p => p.UserId == userId);
                if (participant == null || participant.InvitedById == null) return false;


                if (duel.Participants.Count == 2)
                {
                    duel.Status = DuelStatus.active;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting duel invitation for user {UserId} in duel {DuelId}", userId, duelId);
                return false;
            }
        }

        public async Task<bool> DeclineDuelInvitationAsync(Guid duelId, Guid userId)
        {
            try
            {
                var duel = await GetDuelByIdAsync(duelId);
                if (duel == null) return false;

                var participant = duel.Participants.FirstOrDefault(p => p.UserId == userId);
                if (participant == null) return false;

                _context.Set<DuelParticipant>().Remove(participant);


                if (!duel.Participants.Any(p => p.InvitedById != null))
                {
                    duel.Status = DuelStatus.pending;
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error declining duel invitation for user {UserId} in duel {DuelId}", userId, duelId);
                return false;
            }
        }

        public async Task<bool> StartDuelAsync(Guid duelId)
        {
            try
            {
                var duel = await GetDuelByIdAsync(duelId);
                if (duel == null || duel.Status != DuelStatus.active) return false;

                duel.Status = DuelStatus.active;
                duel.StartedAt = DateTime.UtcNow;


                await CreateDuelRoundAsync(duelId, 1);

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting duel {DuelId}", duelId);
                return false;
            }
        }

        public async Task<DuelRound> CreateDuelRoundAsync(Guid duelId, int roundNumber)
        {
            try
            {
                var duel = await GetDuelByIdAsync(duelId);
                if (duel == null) throw new ArgumentException("Duel not found");


                var query = _context.Questions
                    .Where(q => q.SubjectId == duel.SubjectId);

                if (duel.LevelId.HasValue)
                {
                    var level = await _context.Set<Level>()
                        .FirstOrDefaultAsync(l => l.Id == duel.LevelId);
                    if (level != null)
                    {

                        query = query.Where(q => (int)q.Difficulty <= level.LevelNumber);
                    }
                }

                var question = await query.OrderBy(q => Guid.NewGuid()).FirstOrDefaultAsync();
                if (question == null) throw new InvalidOperationException("No questions available for this duel");

                var round = new DuelRound
                {
                    Id = Guid.NewGuid(),
                    DuelId = duelId,
                    RoundNumber = roundNumber,
                    QuestionId = question.Id,
                    TimeLimitSeconds = 30
                };

                _context.Set<DuelRound>().Add(round);
                await _context.SaveChangesAsync();

                return round;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating round {RoundNumber} for duel {DuelId}", roundNumber, duelId);
                throw;
            }
        }

        public async Task<bool> SubmitRoundAnswerAsync(Guid duelId, Guid userId, Guid questionId, Guid? selectedOptionId, int timeMs)
        {
            try
            {
                var duel = await GetDuelByIdAsync(duelId);
                if (duel == null || duel.Status != DuelStatus.active) return false;

                var participant = duel.Participants.FirstOrDefault(p => p.UserId == userId);
                if (participant == null) return false;


                bool isCorrect = false;
                if (selectedOptionId.HasValue)
                {
                    var option = await _context.Set<QuestionOption>()
                        .FirstOrDefaultAsync(o => o.Id == selectedOptionId && o.QuestionId == questionId);
                    isCorrect = option?.IsCorrect ?? false;
                }

                if (isCorrect)
                {
                    participant.Score++;
                }


                var maxPossibleRounds = duel.BestOf;
                var currentRound = duel.Rounds.Count;
                var requiredWins = (duel.BestOf / 2) + 1;

                if (participant.Score >= requiredWins || currentRound >= maxPossibleRounds)
                {
                    await CompleteDuelAsync(duelId);
                }
                else
                {

                    var allAnswersSubmitted = true;
                    if (allAnswersSubmitted)
                    {
                        await CreateDuelRoundAsync(duelId, currentRound + 1);
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting answer for user {UserId} in duel {DuelId}", userId, duelId);
                return false;
            }
        }

        public async Task<bool> CompleteDuelAsync(Guid duelId)
        {
            try
            {
                var duel = await GetDuelByIdAsync(duelId);
                if (duel == null) return false;

                duel.Status = DuelStatus.completed;
                duel.EndedAt = DateTime.UtcNow;

                // Determine results
                var sortedParticipants = duel.Participants.OrderByDescending(p => p.Score).ToList();

                if (sortedParticipants.Count >= 2)
                {
                    if (sortedParticipants[0].Score > sortedParticipants[1].Score)
                    {
                        sortedParticipants[0].Result = DuelResult.win;
                        sortedParticipants[1].Result = DuelResult.lose;
                    }
                    else
                    {
                        // It's a draw
                        foreach (var participant in sortedParticipants)
                        {
                            participant.Result = DuelResult.draw;
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing duel {DuelId}", duelId);
                return false;
            }
        }

        public async Task<Duel?> GetDuelByIdAsync(Guid duelId)
        {
            return await _context.Duels
                .Include(d => d.Subject)
                .Include(d => d.Level)
                .Include(d => d.Participants)
                    .ThenInclude(p => p.User)
                .Include(d => d.Rounds)
                    .ThenInclude(r => r.Question)
                        .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(d => d.Id == duelId);
        }

        public async Task<List<Duel>> GetUserDuelsAsync(Guid userId, DuelStatus? status = null)
        {
            var query = _context.Duels
                .Include(d => d.Subject)
                .Include(d => d.Level)
                .Include(d => d.Participants)
                    .ThenInclude(p => p.User)
                .Where(d => d.Participants.Any(p => p.UserId == userId));

            if (status.HasValue)
            {
                query = query.Where(d => d.Status == status);
            }

            return await query
                .OrderByDescending(d => d.StartedAt ?? d.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Duel>> GetPendingInvitationsAsync(Guid userId)
        {
            return await _context.Duels
                .Include(d => d.Subject)
                .Include(d => d.Level)
                .Include(d => d.Participants)
                    .ThenInclude(p => p.User)
                .Where(d => d.Status == DuelStatus.pending &&
                           d.Participants.Any(p => p.UserId == userId && p.InvitedById != null))
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<DuelParticipant>> GetClassmatesForDuelAsync(Guid userId, Guid subjectId)
        {

            var userClasses = await _context.Set<ClassMembership>()
                .Where(cm => cm.UserId == userId)
                .Select(cm => cm.ClassId)
                .ToListAsync();


            var classmates = await _context.Set<ClassMembership>()
                .Include(cm => cm.User)
                .Where(cm => userClasses.Contains(cm.ClassId) && cm.UserId != userId)
                .Select(cm => new DuelParticipant
                {
                    UserId = cm.UserId,
                    User = cm.User
                })
                .Distinct()
                .ToListAsync();

            return classmates;
        }

        public async Task<bool> CanUserCreateDuelAsync(Guid userId, Guid subjectId)
        {

            var hasProgress = await _context.Set<UserProgress>()
                .AnyAsync(up => up.UserId == userId && up.SubjectId == subjectId);

            if (hasProgress) return true;


            var userClasses = await _context.Set<ClassMembership>()
                .Where(cm => cm.UserId == userId)
                .Select(cm => cm.ClassId)
                .ToListAsync();

            var hasClassAccess = await _context.Set<Quiz>()
                .AnyAsync(q => q.SubjectId == subjectId &&
                              userClasses.Contains(q.ClassId.Value));

            return hasClassAccess;
        }

        public async Task<bool> AreUsersInSameClassAsync(Guid userId1, Guid userId2)
        {
            var user1Classes = await _context.Set<ClassMembership>()
                .Where(cm => cm.UserId == userId1)
                .Select(cm => cm.ClassId)
                .ToListAsync();

            var user2Classes = await _context.Set<ClassMembership>()
                .Where(cm => cm.UserId == userId2)
                .Select(cm => cm.ClassId)
                .ToListAsync();

            return user1Classes.Intersect(user2Classes).Any();
        }

        public async Task<DuelStats> GetUserDuelStatsAsync(Guid userId, Guid? subjectId = null)
        {
            var query = _context.Set<DuelParticipant>()
                .Include(dp => dp.Duel)
                .Where(dp => dp.UserId == userId && dp.Duel.Status == DuelStatus.completed);

            if (subjectId.HasValue)
            {
                query = query.Where(dp => dp.Duel.SubjectId == subjectId);
            }

            var participations = await query.ToListAsync();

            var wins = participations.Count(p => p.Result == DuelResult.win);
            var losses = participations.Count(p => p.Result == DuelResult.lose);
            var draws = participations.Count(p => p.Result == DuelResult.draw);
            var total = participations.Count;

            var winRate = total > 0 ? (double)wins / total : 0.0;


            var recentParticipations = participations
                .OrderByDescending(p => p.Duel.EndedAt)
                .ToList();

            int currentStreak = 0;
            foreach (var participation in recentParticipations)
            {
                if (participation.Result == DuelResult.win)
                    currentStreak++;
                else
                    break;
            }

            return new DuelStats
            {
                TotalDuels = total,
                Wins = wins,
                Losses = losses,
                Draws = draws,
                WinRate = winRate,
                CurrentStreak = currentStreak,
                BestStreak = currentStreak
            };
        }
    }
}