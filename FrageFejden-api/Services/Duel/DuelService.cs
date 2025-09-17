using FrageFejden.Entities;
using FrageFejden.Entities.Enums;
using FrageFejden.Services.Interfaces;
using FrageFejden_api.Entities.Tables;
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
            if (bestOf <= 0) bestOf = 5;
            if (bestOf % 2 == 0) bestOf += 1;

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

        public async Task<bool> StartDuelAsync(Guid duelId)
        {
            try
            {
                var duel = await GetDuelByIdAsync(duelId);
                if (duel == null) return false;

                if (duel.Participants.Count < 2 || duel.Status != DuelStatus.active)
                    return false;

                duel.StartedAt ??= DateTime.UtcNow;

                if (!duel.Rounds.Any())
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
            var duel = await GetDuelByIdAsync(duelId) ?? throw new ArgumentException("Duel not found");

            
            var quizQuestions = _context.Set<QuizQuestion>()
                .AsNoTracking()
                .Where(qq => _context.Set<Quiz>()
                    .Any(qz => qz.Id == qq.QuizId &&
                               qz.IsPublished &&
                               qz.SubjectId == duel.SubjectId));

           
            if (duel.LevelId.HasValue)
            {
                quizQuestions = quizQuestions.Where(qq => _context.Set<Quiz>()
                    .Any(qz => qz.Id == qq.QuizId && qz.LevelId == duel.LevelId));
            }

            var pool = _context.Set<Question>()
                .AsNoTracking()
                .Where(q => quizQuestions.Select(qq => qq.QuestionId).Distinct().Contains(q.Id));

            
            var usedIds = _context.Set<DuelRound>().Where(r => r.DuelId == duelId).Select(r => r.QuestionId);
            var available = pool.Where(q => !usedIds.Contains(q.Id));

            var availableCount = await available.CountAsync();
            if (availableCount == 0)
                throw new InvalidOperationException("No available questions left for this duel.");

            var skip = Random.Shared.Next(availableCount);

            var picked = await available
                .OrderBy(q => q.Id) 
                .Skip(skip)
                .Take(1)
                .Select(q => new
                {
                    q.Id,
                    q.Stem,
                    Options = q.Options
                        .OrderBy(o => o.SortOrder)
                        .Select(o => new { o.OptionText, o.IsCorrect })
                        .ToList()
                })
                .FirstAsync();

            var snapOptions = picked.Options.Select(o => o.OptionText).ToList();
            var correctIndex = picked.Options.FindIndex(o => o.IsCorrect);

            var round = new DuelRound
            {
                Id = Guid.NewGuid(),
                DuelId = duelId,
                RoundNumber = roundNumber,
                QuestionId = picked.Id,
                TimeLimitSeconds = 30,
                TextSnapshot = picked.Stem,
                AlternativesSnapshot = snapOptions,
                CorrectIndexSnapshot = correctIndex,
                StartedAt = DateTime.UtcNow
            };

            _context.DuelRounds.Add(round);
            await _context.SaveChangesAsync();
            return round;
        }

        public async Task<bool> SubmitRoundAnswerAsync(Guid duelId, Guid userId, Guid questionId, Guid? selectedOptionId, int timeMs)
        {
            var duel = await GetDuelByIdAsync(duelId);
            if (duel == null || duel.Status != DuelStatus.active) return false;
            if (!duel.Participants.Any(p => p.UserId == userId)) return false;

            var round = duel.Rounds
                .OrderByDescending(r => r.RoundNumber)
                .FirstOrDefault(r => r.EndedAt == null);
            if (round == null || round.QuestionId != questionId) return false;
            if (round.EndedAt != null) return false;
            if (round.Answers.Any(a => a.UserId == userId)) return false;

            
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

           
            var totalPlayers = duel.Participants.Count;
            if (round.Answers.Count + 1 >= totalPlayers)
            {
                await _context.SaveChangesAsync();

                round = await _context.DuelRounds
                    .Include(r => r.Answers)
                    .Include(r => r.Duel).ThenInclude(d => d.Participants)
                    .FirstAsync(r => r.Id == round.Id);

                round.EndedAt = DateTime.UtcNow;

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

                var needed = (duel.BestOf + 1) / 2;
                var top = duel.Participants.Max(p => p.Score);
                if (top >= needed || duel.Rounds.Count >= duel.BestOf)
                    await CompleteDuelAsync(duelId);
                else
                    await CreateDuelRoundAsync(duelId, round.RoundNumber + 1);
            }

            await _context.SaveChangesAsync();
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
