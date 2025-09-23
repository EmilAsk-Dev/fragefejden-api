using FrageFejden.Entities;
using FrageFejden.Entities.Enums;
using FrageFejden.Services.Interfaces;
using FrageFejden_api.Entities.Tables;
using Microsoft.EntityFrameworkCore;
using System.Data;

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

        // -------------------- CREATE / INVITES --------------------

        public async Task<Duel> CreateDuelAsync(Guid initiatorId, Guid subjectId, Guid? levelId = null, int bestOf = 5)
        {
            if (bestOf <= 0) bestOf = 5;
            if (bestOf % 2 == 0) bestOf += 1; // best-of must be odd

            var subjectExists = await _context.Set<Subject>()
                .AsNoTracking()
                .AnyAsync(s => s.Id == subjectId);
            if (!subjectExists) throw new ArgumentException("Subject not found", nameof(subjectId));

            if (levelId.HasValue)
            {
                var levelValid = await _context.Set<Level>()
                    .AnyAsync(l => l.Id == levelId.Value &&
                                   _context.Set<Topic>().Any(t => t.Id == l.TopicId && t.SubjectId == subjectId));
                if (!levelValid)
                    throw new ArgumentException("Level does not belong to subject", nameof(levelId));
            }

            if (!await CanUserCreateDuelAsync(initiatorId, subjectId))
                throw new UnauthorizedAccessException("User cannot create duel for this subject");

            var duel = new Duel
            {
                Id = Guid.NewGuid(),
                SubjectId = subjectId,
                LevelId = levelId,
                Status = DuelStatus.pending,
                BestOf = bestOf,
                CreatedAt = DateTime.UtcNow
            };

            _context.Duels.Add(duel);
            _context.Set<DuelParticipant>().Add(new DuelParticipant
            {
                Id = Guid.NewGuid(),
                DuelId = duel.Id,
                UserId = initiatorId,
                Score = 0
            });

            await _context.SaveChangesAsync();
            return duel;
        }

        public async Task<DuelParticipant> InviteToQuelAsync(Guid duelId, Guid inviterId, Guid inviteeId)
        {
            try
            {
                var duel = await GetDuelByIdAsync(duelId) ?? throw new ArgumentException("Duel not found");

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

        // -------------------- START / ROUNDS --------------------

        /// <summary>
        /// Build the complete question pool: all published quizzes that either
        /// belong directly to the subject OR belong to any level under any topic of that subject.
        /// If a duel.LevelId is specified, we restrict to that level.
        /// </summary>
        private async Task<List<Question>> BuildQuestionPoolAsync(Duel duel)
        {
            // Topics in the subject
            var topicIds = await _context.Set<Topic>()
                .Where(t => t.SubjectId == duel.SubjectId)
                .Select(t => t.Id)
                .ToListAsync();

            // Levels under those topics
            var levelIdsUnderSubject = await _context.Set<Level>()
                .Where(l => topicIds.Contains(l.TopicId))
                .Select(l => l.Id)
                .ToListAsync();

            // All published quizzes that belong to the subject OR belong to those levels
            var quizQuery = _context.Set<Quiz>()
                .AsNoTracking()
                .Where(q => q.IsPublished &&
                           (q.SubjectId == duel.SubjectId ||
                           (q.LevelId != null && levelIdsUnderSubject.Contains(q.LevelId.Value))));

            // If duel has a specific LevelId, restrict further
            if (duel.LevelId.HasValue)
            {
                var lvl = duel.LevelId.Value;
                quizQuery = quizQuery.Where(q => q.LevelId == lvl);
            }

            var quizIds = await quizQuery.Select(q => q.Id).ToListAsync();

            // Question ids that appear in those quizzes
            var questionIds = await _context.Set<QuizQuestion>()
                .Where(qq => quizIds.Contains(qq.QuizId))
                .Select(qq => qq.QuestionId)
                .Distinct()
                .ToListAsync();

            // Load questions+options
            var questions = await _context.Set<Question>()
                .Where(q => questionIds.Contains(q.Id))
                .Include(q => q.Options)
                .ToListAsync();

            return questions;
        }

        public async Task<bool> StartDuelAsync(Guid duelId)
        {
            try
            {
                var duel = await GetDuelByIdAsync(duelId);
                if (duel == null) return false;

                if (duel.Participants.Count < 2 || duel.Status != DuelStatus.active)
                    return false;

                duel.StartedAt ??= DateTime.UtcNow;

                // Pre-generate all rounds up front if needed
                if (!duel.Rounds.Any())
                {
                    var pool = await BuildQuestionPoolAsync(duel);
                    if (pool.Count == 0)
                        throw new InvalidOperationException("No available questions for this duel/subject.");

                    var take = Math.Min(duel.BestOf, pool.Count);

                    // Shuffle and pick take items
                    var selected = pool
                        .OrderBy(_ => Guid.NewGuid())
                        .Take(take)
                        .ToList();

                    var roundsToAdd = new List<DuelRound>();

                    for (int i = 0; i < selected.Count; i++)
                    {
                        var q = selected[i];
                        var opts = q.Options.OrderBy(o => o.SortOrder).ToList();

                        var snapOptions = opts.Select(o => o.OptionText).ToList();
                        var correctIndex = opts.FindIndex(o => o.IsCorrect);

                        roundsToAdd.Add(new DuelRound
                        {
                            Id = Guid.NewGuid(),
                            DuelId = duelId,
                            RoundNumber = i + 1,
                            QuestionId = q.Id,
                            TimeLimitSeconds = 30,
                            TextSnapshot = q.Stem,
                            AlternativesSnapshot = snapOptions,
                            CorrectIndexSnapshot = correctIndex,
                            StartedAt = DateTime.MinValue,
                            EndedAt = null
                        });
                    }

                    _context.DuelRounds.AddRange(roundsToAdd);
                }

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
            var duel = await GetDuelByIdAsync(duelId) ?? throw new ArgumentException("Duel not found");

            var pool = await BuildQuestionPoolAsync(duel);

            // Exclude already-used questions
            var usedIds = await _context.Set<DuelRound>()
                .Where(r => r.DuelId == duelId)
                .Select(r => r.QuestionId)
                .ToListAsync();

            var available = pool.Where(q => !usedIds.Contains(q.Id)).ToList();
            if (available.Count == 0)
                throw new InvalidOperationException("No available questions left for this duel.");

            var qPicked = available.OrderBy(_ => Guid.NewGuid()).First();
            var opts = qPicked.Options.OrderBy(o => o.SortOrder).ToList();
            var snapOptions = opts.Select(o => o.OptionText).ToList();
            var correctIndex = opts.FindIndex(o => o.IsCorrect);

            var round = new DuelRound
            {
                Id = Guid.NewGuid(),
                DuelId = duelId,
                RoundNumber = roundNumber,
                QuestionId = qPicked.Id,
                TimeLimitSeconds = 30,
                TextSnapshot = qPicked.Stem,
                AlternativesSnapshot = snapOptions,
                CorrectIndexSnapshot = correctIndex,
                StartedAt = DateTime.UtcNow
            };

            _context.DuelRounds.Add(round);
            await _context.SaveChangesAsync();
            return round;
        }

        // -------------------- ANSWERS / COMPLETE --------------------

        public async Task<bool> SubmitRoundAnswerAsync(
            Guid duelId, Guid userId, Guid questionId, Guid? selectedOptionId, int timeMs)
        {
            // SERIALIZABLE (or REPEATABLE READ) to prevent race conditions
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var duel = await _context.Duels
                .Include(d => d.Participants)
                .FirstOrDefaultAsync(d => d.Id == duelId);

            if (duel == null || duel.Status != DuelStatus.active)
                return false;

            if (!duel.Participants.Any(p => p.UserId == userId))
                return false;

            // Bind to the round by duel + questionId and ensure it's still open
            var round = await _context.DuelRounds
                .Include(r => r.Answers)
                .FirstOrDefaultAsync(r =>
                    r.DuelId == duelId &&
                    r.QuestionId == questionId &&
                    r.EndedAt == null);

            if (round == null)
                return false;

            // Prevent duplicate answers by the same user
            if (round.Answers.Any(a => a.UserId == userId))
                return false;

            // Map selected option to the snapshot index
            int selectedIndex = -1;
            if (selectedOptionId.HasValue)
            {
                var optText = await _context.Set<QuestionOption>()
                    .Where(o => o.Id == selectedOptionId && o.QuestionId == questionId)
                    .Select(o => o.OptionText)
                    .FirstOrDefaultAsync();

                if (optText != null)
                    selectedIndex = round.AlternativesSnapshot.FindIndex(t => t == optText);
            }

            var isCorrect = selectedIndex == round.CorrectIndexSnapshot;

            _context.Set<DuelAnswer>().Add(new DuelAnswer
            {
                Id = Guid.NewGuid(),
                DuelRoundId = round.Id,
                UserId = userId,
                SelectedIndex = Math.Max(-1, selectedIndex),
                IsCorrect = isCorrect,
                TimeMs = Math.Max(0, timeMs),
                AnsweredAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            // Re-count from DB to avoid stale in-memory counts
            var totalPlayers = duel.Participants.Count;
            var answeredCount = await _context.Set<DuelAnswer>()
                .CountAsync(a => a.DuelRoundId == round.Id);

            // If this submit completed the round, finalize and maybe complete the duel
            if (answeredCount >= totalPlayers)
            {
                // Reload with answers & participants for scoring
                round = await _context.DuelRounds
                    .Include(r => r.Answers)
                    .Include(r => r.Duel).ThenInclude(d => d.Participants)
                    .FirstAsync(r => r.Id == round.Id);

                round.EndedAt = DateTime.UtcNow;

                // Score: if multiple correct, the fastest gets the point
                var correct = round.Answers.Where(a => a.IsCorrect).ToList();
                if (correct.Count == 1)
                {
                    duel.Participants.First(p => p.UserId == correct[0].UserId).Score += 1;
                }
                else if (correct.Count > 1)
                {
                    var fastest = correct.OrderBy(a => a.TimeMs).First();
                    duel.Participants.First(p => p.UserId == fastest.UserId).Score += 1;
                }

                // Compute “rounds played” = rounds that ended
                var playedCount = await _context.Set<DuelRound>()
                    .CountAsync(r => r.DuelId == duelId && r.EndedAt != null);

                // Best-of threshold
                var needed = (duel.BestOf + 1) / 2;
                var top = duel.Participants.Max(p => p.Score);

                if (top >= needed || playedCount >= duel.BestOf)
                {
                    await CompleteDuelAsync(duelId);
                }
                else
                {
                    // If you did NOT pre-create all rounds, you could create the next here:
                    // await CreateDuelRoundAsync(duelId, round.RoundNumber + 1);
                    // With pre-generated rounds, do nothing.
                }

                await _context.SaveChangesAsync();
            }

            await tx.CommitAsync();
            return true;
        }

        public async Task<bool> CompleteDuelAsync(Guid duelId)
        {
            try
            {
                var duel = await GetDuelByIdAsync(duelId);
                if (duel == null) return false;

                duel.Status = DuelStatus.completed;
                duel.EndedAt = DateTime.UtcNow;

                var sorted = duel.Participants.OrderByDescending(p => p.Score).ToList();
                if (sorted.Count >= 2)
                {
                    if (sorted[0].Score > sorted[1].Score)
                    {
                        sorted[0].Result = DuelResult.win;
                        sorted[1].Result = DuelResult.lose;
                    }
                    else
                    {
                        foreach (var p in sorted) p.Result = DuelResult.draw;
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

        // -------------------- QUERIES --------------------

        public async Task<Duel?> GetDuelByIdAsync(Guid duelId)
        {
            return await _context.Duels
                .Include(d => d.Subject)
                .Include(d => d.Level)
                .Include(d => d.Participants).ThenInclude(p => p.User)
                .Include(d => d.Rounds).ThenInclude(r => r.Question).ThenInclude(q => q.Options)
                .Include(d => d.Rounds).ThenInclude(r => r.Answers)
                .FirstOrDefaultAsync(d => d.Id == duelId);
        }

        public async Task<List<Duel>> GetUserDuelsAsync(Guid userId, DuelStatus? status = null)
        {
            var query = _context.Duels
                .Include(d => d.Subject)
                .Include(d => d.Level)
                .Include(d => d.Participants).ThenInclude(p => p.User)
                .Where(d => d.Participants.Any(p => p.UserId == userId));

            if (status.HasValue) query = query.Where(d => d.Status == status);

            return await query
                .OrderByDescending(d => d.StartedAt ?? d.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Duel>> GetPendingInvitationsAsync(Guid userId)
        {
            return await _context.Duels
                .Include(d => d.Subject)
                .Include(d => d.Level)
                .Include(d => d.Participants).ThenInclude(p => p.User)
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
            var subjectExists = await _context.Set<Subject>()
                .AsNoTracking()
                .AnyAsync(s => s.Id == subjectId);
            if (!subjectExists) return false;

            var userClasses = await _context.Set<ClassMembership>()
                .Where(cm => cm.UserId == userId)
                .Select(cm => cm.ClassId)
                .ToListAsync();

            if (userClasses.Count > 0)
            {
                var subjectInUserClass = await _context.Set<Subject>()
                    .AnyAsync(s => s.Id == subjectId && s.ClassId.HasValue && userClasses.Contains(s.ClassId.Value));
                if (subjectInUserClass) return true;
            }

            var hasAnyProgress = await _context.Set<UserProgress>()
                .AnyAsync(up => up.UserId == userId && up.SubjectId == subjectId);
            if (hasAnyProgress) return true;

            var allLevels = await GetAllLevelIdsForSubject(subjectId);
            if (allLevels.Count == 0) return true;

            var hasAllLevels = await HasCompletedOrUnlockedAllLevels(userId, allLevels);
            return hasAllLevels;
        }

        private async Task<List<Guid>> GetAllLevelIdsForSubject(Guid subjectId)
        {
            return await _context.Set<Level>()
                .Where(l => _context.Set<Topic>().Where(t => t.SubjectId == subjectId).Select(t => t.Id).Contains(l.TopicId))
                .Select(l => l.Id)
                .ToListAsync();
        }

        private async Task<bool> HasCompletedOrUnlockedAllLevels(Guid userId, List<Guid> levelIds)
        {
            if (levelIds.Count == 0) return true;

            var readLevels = await _context.Set<UserProgress>()
                .Where(up => up.UserId == userId && up.LevelId != null && levelIds.Contains(up.LevelId.Value) && up.HasReadStudyText == true)
                .Select(up => up.LevelId!.Value)
                .Distinct()
                .ToListAsync();

            var remaining = levelIds.Except(readLevels).ToList();
            if (remaining.Count == 0) return true;

            var completedViaQuizzes = await _context.Set<Attempt>()
                .Where(a => a.UserId == userId && a.CompletedAt != null && a.Quiz.LevelId != null && remaining.Contains(a.Quiz.LevelId.Value))
                .Select(a => a.Quiz.LevelId!.Value)
                .Distinct()
                .ToListAsync();

            remaining = remaining.Except(completedViaQuizzes).ToList();
            return remaining.Count == 0;
        }

        public async Task<bool> AreUsersInSameClassAsync(Guid userId1, Guid userId2)
        {
            var user1Classes = await _context.Set<ClassMembership>().Where(cm => cm.UserId == userId1).Select(cm => cm.ClassId).ToListAsync();
            var user2Classes = await _context.Set<ClassMembership>().Where(cm => cm.UserId == userId2).Select(cm => cm.ClassId).ToListAsync();
            return user1Classes.Intersect(user2Classes).Any();
        }

        public async Task<DuelStats> GetUserDuelStatsAsync(Guid userId, Guid? subjectId = null)
        {
            var query = _context.Set<DuelParticipant>()
                .Include(dp => dp.Duel)
                .Where(dp => dp.UserId == userId && dp.Duel.Status == DuelStatus.completed);

            if (subjectId.HasValue) query = query.Where(dp => dp.Duel.SubjectId == subjectId);

            var participations = await query.ToListAsync();

            var wins = participations.Count(p => p.Result == DuelResult.win);
            var losses = participations.Count(p => p.Result == DuelResult.lose);
            var draws = participations.Count(p => p.Result == DuelResult.draw);
            var total = participations.Count;

            var winRate = total > 0 ? (double)wins / total : 0.0;

            var recent = participations.OrderByDescending(p => p.Duel.EndedAt).ToList();
            int currentStreak = 0;
            foreach (var p in recent)
            {
                if (p.Result == DuelResult.win) currentStreak++;
                else break;
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
