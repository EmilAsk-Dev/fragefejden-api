using FrageFejden.Entities;
using FrageFejden.Entities.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Transactions;

namespace FrageFejden.Data
{
    public class DatabaseSeeder
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly Random _random = new Random();


        private static readonly Guid QuizMathId = Guid.Parse("aaaa1111-aaaa-1111-aaaa-111111111111");
        private static readonly Guid QuizScienceId = Guid.Parse("aaaa2222-aaaa-2222-aaaa-222222222222");

        private static readonly Guid OptH2OId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        private static readonly Guid OptTrueId = Guid.Parse("10000000-0000-0000-0000-000000000002");

        
        private static readonly Guid Duel1Id = Guid.Parse("20000000-0000-0000-0000-000000000001");

        public DatabaseSeeder(AppDbContext context, UserManager<AppUser> userManager, RoleManager<IdentityRole<Guid>> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }



        public async Task SeedAsync(CancellationToken ct = default)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            await SeedIfEmpty<AppUser>(SeedUsersAsync, ct);

            // ⬇️ Classes (WITH subjects) first so Subject rows exist
            await SeedIfEmpty<Class>(SeedClassesAsync, ct);

            // ⬇️ Everything that references subjects can now run
            await SeedIfEmpty<Topic>(SeedTopicsAsync, ct);
            await SeedIfEmpty<Level>(SeedLevelsAsync, ct);

            await SeedIfEmpty<ClassMembership>(SeedClassMembershipsAsync, ct);

            await SeedIfEmpty<Question>(SeedQuestionsAsync, ct);
            await SeedIfEmpty<QuestionOption>(SeedQuestionOptionsAsync, ct);

            await SeedIfEmpty<Quiz>(SeedQuizzesAsync, ct);
            await SeedIfEmpty<QuizQuestion>(SeedQuizQuestionsAsync, ct);

            await SeedIfEmpty<AiTemplate>(SeedAiTemplatesAsync, ct);

            await SeedIfEmpty<Attempt>(SeedAttemptsAsync, ct);
            await SeedIfEmpty<Response>(SeedResponsesAsync, ct);

            await SeedIfEmpty<Duel>(SeedDuelsAsync, ct);
            await SeedIfEmpty<DuelParticipant>(SeedDuelParticipantsAsync, ct);
            await SeedIfEmpty<DuelRound>(SeedDuelRoundsAsync, ct);

            await SeedIfEmpty<UnlockRule>(SeedUnlockRulesAsync, ct);
            await SeedIfEmpty<UserProgress>(SeedUserProgressAsync, ct);

            await SeedIfEmpty<AiGeneration>(SeedAiGenerationsAsync, ct);

            await tx.CommitAsync(ct);
        }



        private async Task SeedIfEmpty<TEntity>(Func<Task> seedFunc, CancellationToken ct = default)
                where TEntity : class
            {
                if (await _context.Set<TEntity>().AnyAsync(ct))
                    return;

                await seedFunc();
                await _context.SaveChangesAsync(ct);
            }


        private async Task SeedClassSubjectsAsync(CancellationToken ct = default)
        {
            // 9B class id from your seed data
            var class9BId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

            // Subject ids from your SeedSubjectsAsync
            var subjectIds = new[]
            {
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), 
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), 
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), 
            };

            var class9B = await _context.Set<Class>()
                .Include(c => c.Subjects)
                .FirstOrDefaultAsync(c => c.Id == class9BId, ct);

            if (class9B is null)
                return; 

            
            var existingIds = class9B.Subjects.Select(s => s.Id).ToHashSet();
            var subjectsToAttach = await _context.Set<Subject>()
                .Where(s => subjectIds.Contains(s.Id) && !existingIds.Contains(s.Id))
                .ToListAsync(ct);

            if (subjectsToAttach.Count == 0)
                return;

            foreach (var subject in subjectsToAttach)
                class9B.Subjects.Add(subject);

            await _context.SaveChangesAsync(ct);
        }

        private async Task SeedUsersAsync()
        {
            // Make sure Identity roles exist (names must match what you use in [Authorize(Roles=...)] )
            // Here we use: "Admin", "Lärare", "Student"
            foreach (var rn in new[] { "Admin", "Lärare", "Student" })
            {
                if (!await _roleManager.RoleExistsAsync(rn))
                    await _roleManager.CreateAsync(new IdentityRole<Guid>(rn));
            }

            var users = new[]
            {
        new AppUser
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserName = "john.teacher@school.edu",
            Email = "john.teacher@school.edu",
            EmailConfirmed = true,
            FullName = "John Smith",
            Role = Role.teacher,
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            experiencePoints = 2500
        },
        new AppUser
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            UserName = "mary.student@school.edu",
            Email = "mary.student@school.edu",
            EmailConfirmed = true,
            FullName = "Mary Johnson",
            Role = Role.student,
            CreatedAt = DateTime.UtcNow.AddMonths(-3),
            experiencePoints = 1200
        },
        new AppUser
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            UserName = "bob.student@school.edu",
            Email = "bob.student@school.edu",
            EmailConfirmed = true,
            FullName = "Bob Wilson",
            Role = Role.student,
            CreatedAt = DateTime.UtcNow.AddMonths(-2),
            experiencePoints = 800
        },
        new AppUser
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            UserName = "admin@school.edu",
            Email = "admin@school.edu",
            EmailConfirmed = true,
            FullName = "Admin User",
            Role = Role.admin,
            CreatedAt = DateTime.UtcNow.AddYears(-1),
            experiencePoints = 5000
        }
    };

            foreach (var user in users)
            {
                // If user already exists, ensure role membership and continue
                var existing = await _userManager.FindByIdAsync(user.Id.ToString());
                if (existing is null)
                {
                    var createRes = await _userManager.CreateAsync(user, "Password123!");
                    if (!createRes.Succeeded)
                        throw new InvalidOperationException($"Failed to create user {user.Email}: " +
                            string.Join(", ", createRes.Errors.Select(e => e.Description)));
                    existing = user;
                }

                var identityRoleName = MapEnumToIdentityRoleName(existing.Role); // "Admin"|"Lärare"|"Student"
                if (!await _userManager.IsInRoleAsync(existing, identityRoleName))
                {
                    var addRoleRes = await _userManager.AddToRoleAsync(existing, identityRoleName);
                    if (!addRoleRes.Succeeded)
                        throw new InvalidOperationException($"Failed to add role {identityRoleName} to {existing.Email}: " +
                            string.Join(", ", addRoleRes.Errors.Select(e => e.Description)));
                }
            }
        }

        private static string MapEnumToIdentityRoleName(Role r) => r switch
        {
            Role.admin => "Admin",
            Role.teacher => "Lärare",
            Role.student => "Student",
            _ => "Student"
        };

        private async Task SeedSubjectsAsync()
        {
            var subjects = new[]
            {
                new Subject
                {
                    Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Name = "Mathematics",
                    Description = "Comprehensive mathematics curriculum",
                    CreatedById = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    CreatedAt = DateTime.UtcNow.AddMonths(-5)
                },
                new Subject
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Name = "Science",
                    Description = "General science topics",
                    CreatedById = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    CreatedAt = DateTime.UtcNow.AddMonths(-4)
                },
                new Subject
                {
                    Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    Name = "History",
                    Description = "World and local history",
                    CreatedById = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    CreatedAt = DateTime.UtcNow.AddMonths(-3)
                }
            };

            _context.Set<Subject>().AddRange(subjects);
        }

        private async Task SeedTopicsAsync()
        {
            var topics = new[]
            {
                
                new Topic
                {
                    Id = Guid.NewGuid(),
                    SubjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Name = "Algebra",
                    Description = "Basic algebraic concepts and equations"
                },
                new Topic
                {
                    Id = Guid.NewGuid(),
                    SubjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Name = "Geometry",
                    Description = "Shapes, angles, and spatial relationships"
                },
                
                new Topic
                {
                    Id = Guid.NewGuid(),
                    SubjectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Name = "Physics",
                    Description = "Laws of motion and energy"
                },
                new Topic
                {
                    Id = Guid.NewGuid(),
                    SubjectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Name = "Biology",
                    Description = "Living organisms and life processes"
                }
            };

            _context.Set<Topic>().AddRange(topics);
        }

        private async Task SeedLevelsAsync()
        {
            var levels = new List<Level>();
            var subjects = new[]
            {
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")
            };

            foreach (var subjectId in subjects)
            {
                for (int i = 1; i <= 5; i++)
                {
                    levels.Add(new Level
                    {
                        Id = Guid.NewGuid(),
                        SubjectId = subjectId,
                        LevelNumber = i,
                        Title = $"Level {i}",
                        MinXpUnlock = (i - 1) * 200
                    });
                }
            }

            _context.Set<Level>().AddRange(levels);
        }

        private async Task SeedClassesAsync()
        {
            var teacherId = Guid.Parse("11111111-1111-1111-1111-111111111111");

            var class9B = new Class
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                Name = "9B",
                GradeLabel = "Grade 9",
                JoinCode = "MATH101",
                CreatedById = teacherId,
                CreatedAt = DateTime.UtcNow.AddMonths(-4),

                // ⬇️ Subjects “inside” the class (aggregate-root style)
                Subjects =
        {
            new Subject
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "Mathematics",
                Description = "Comprehensive mathematics curriculum",
                CreatedById = teacherId,
                CreatedAt = DateTime.UtcNow.AddMonths(-5)
            },
            new Subject
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Name = "Science",
                Description = "General science topics",
                CreatedById = teacherId,
                CreatedAt = DateTime.UtcNow.AddMonths(-4)
            },
            new Subject
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Name = "History",
                Description = "World and local history",
                CreatedById = Guid.Parse("44444444-4444-4444-4444-444444444444"), // admin in your seed
                CreatedAt = DateTime.UtcNow.AddMonths(-3)
            }
        }
            };

            var class9C = new Class
            {
                Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                Name = "9C",
                GradeLabel = "Grade 8",
                JoinCode = "SCI8EX",
                CreatedById = teacherId,
                CreatedAt = DateTime.UtcNow.AddMonths(-3),

                // You can give 9C its own set (or reuse some)
                Subjects =
        {
            // Example: just Science for 9C
            new Subject
            {
                Id = Guid.NewGuid(), // different subject instance for 9C
                Name = "Science",
                Description = "Science for Grade 8",
                CreatedById = teacherId,
                CreatedAt = DateTime.UtcNow.AddMonths(-3)
            }
        }
            };

            _context.Set<Class>().AddRange(class9B, class9C);
        }

        private async Task SeedClassMembershipsAsync()
        {
            var memberships = new[]
            {
                new ClassMembership
                {
                    Id = Guid.NewGuid(),
                    ClassId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    RoleInClass = Role.student,
                    EnrolledAt = DateTime.UtcNow.AddMonths(-3)
                },
                new ClassMembership
                {
                    Id = Guid.NewGuid(),
                    ClassId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    RoleInClass = Role.student,
                    EnrolledAt = DateTime.UtcNow.AddMonths(-2)
                },
                new ClassMembership
                {
                    Id = Guid.NewGuid(),
                    ClassId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                    UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    RoleInClass = Role.student,
                    EnrolledAt = DateTime.UtcNow.AddMonths(-2)
                }
            };

            _context.Set<ClassMembership>().AddRange(memberships);
        }

        private async Task SeedQuestionsAsync()
        {
            var questions = new[]
            {
                new Question
                {
                    Id = Guid.Parse("ffff1111-ffff-1111-ffff-111111111111"),
                    SubjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Type = QuestionType.multiple_choice,
                    Difficulty = Difficulty.easy,
                    Stem = "What is 2 + 2?",
                    Explanation = "Basic addition of two numbers",
                    SourceAi = false,
                    CreatedById = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    ApprovedById = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    CreatedAt = DateTime.UtcNow.AddMonths(-2)
                },
                new Question
                {
                    Id = Guid.Parse("ffff2222-ffff-2222-ffff-222222222222"),
                    SubjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Type = QuestionType.multiple_choice,
                    Difficulty = Difficulty.medium,
                    Stem = "Solve for x: 2x + 5 = 11",
                    Explanation = "Subtract 5 from both sides, then divide by 2",
                    SourceAi = false,
                    CreatedById = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    CreatedAt = DateTime.UtcNow.AddMonths(-1)
                },
                new Question
                {
                    Id = Guid.Parse("ffff3333-ffff-3333-ffff-333333333333"),
                    SubjectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Type = QuestionType.multiple_choice,
                    Difficulty = Difficulty.easy,
                    Stem = "What is the chemical symbol for water?",
                    Explanation = "Water consists of two hydrogen atoms and one oxygen atom",
                    SourceAi = false,
                    CreatedById = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                },
                new Question
                {
                    Id = Guid.Parse("ffff4444-ffff-4444-ffff-444444444444"),
                    SubjectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Type = QuestionType.true_false,
                    Difficulty = Difficulty.medium,
                    Stem = "The Earth orbits around the Sun.",
                    Explanation = "The Earth takes approximately 365.25 days to orbit the Sun",
                    SourceAi = true,
                    CreatedById = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                }
            };

            _context.Set<Question>().AddRange(questions);
        }

        private async Task SeedQuestionOptionsAsync()
        {
            var options = new[]
            {
        
        new QuestionOption { Id = Guid.NewGuid(), QuestionId = Guid.Parse("ffff1111-ffff-1111-ffff-111111111111"), OptionText = "3",  IsCorrect = false, SortOrder = 1 },
        new QuestionOption { Id = Guid.NewGuid(), QuestionId = Guid.Parse("ffff1111-ffff-1111-ffff-111111111111"), OptionText = "4",  IsCorrect = true,  SortOrder = 2 },
        new QuestionOption { Id = Guid.NewGuid(), QuestionId = Guid.Parse("ffff1111-ffff-1111-ffff-111111111111"), OptionText = "5",  IsCorrect = false, SortOrder = 3 },
        new QuestionOption { Id = Guid.NewGuid(), QuestionId = Guid.Parse("ffff1111-ffff-1111-ffff-111111111111"), OptionText = "6",  IsCorrect = false, SortOrder = 4 },

        
        new QuestionOption { Id = Guid.NewGuid(), QuestionId = Guid.Parse("ffff2222-ffff-2222-ffff-222222222222"), OptionText = "2",  IsCorrect = false, SortOrder = 1 },
        new QuestionOption { Id = Guid.NewGuid(), QuestionId = Guid.Parse("ffff2222-ffff-2222-ffff-222222222222"), OptionText = "3",  IsCorrect = true,  SortOrder = 2 },
        new QuestionOption { Id = Guid.NewGuid(), QuestionId = Guid.Parse("ffff2222-ffff-2222-ffff-222222222222"), OptionText = "4",  IsCorrect = false, SortOrder = 3 },
        new QuestionOption { Id = Guid.NewGuid(), QuestionId = Guid.Parse("ffff2222-ffff-2222-ffff-222222222222"), OptionText = "5",  IsCorrect = false, SortOrder = 4 },

        
        new QuestionOption { Id = OptH2OId,    QuestionId = Guid.Parse("ffff3333-ffff-3333-ffff-333333333333"), OptionText = "H2O", IsCorrect = true,  SortOrder = 1 },
        new QuestionOption { Id = Guid.NewGuid(), QuestionId = Guid.Parse("ffff3333-ffff-3333-ffff-333333333333"), OptionText = "CO2", IsCorrect = false, SortOrder = 2 },
        new QuestionOption { Id = Guid.NewGuid(), QuestionId = Guid.Parse("ffff3333-ffff-3333-ffff-333333333333"), OptionText = "NaCl", IsCorrect = false, SortOrder = 3 },
        new QuestionOption { Id = Guid.NewGuid(), QuestionId = Guid.Parse("ffff3333-ffff-3333-ffff-333333333333"), OptionText = "O2",  IsCorrect = false, SortOrder = 4 },

        
        new QuestionOption { Id = OptTrueId,   QuestionId = Guid.Parse("ffff4444-ffff-4444-ffff-444444444444"), OptionText = "True",  IsCorrect = true,  SortOrder = 1 },
        new QuestionOption { Id = Guid.NewGuid(), QuestionId = Guid.Parse("ffff4444-ffff-4444-ffff-444444444444"), OptionText = "False", IsCorrect = false, SortOrder = 2 },
    };

            _context.Set<QuestionOption>().AddRange(options);
        }


        private async Task SeedQuizzesAsync()
        {
            _context.Set<Quiz>().AddRange(
                new Quiz
                {
                    Id = QuizMathId,
                    SubjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    ClassId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    Title = "Basic Math Quiz",
                    Description = "Introduction to basic mathematical concepts",
                    IsPublished = true,
                    CreatedById = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    CreatedAt = DateTime.UtcNow.AddDays(-4)
                },
                new Quiz
                {
                    Id = QuizScienceId,
                    SubjectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    ClassId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                    Title = "Science Fundamentals",
                    Description = "Basic science concepts and principles",
                    IsPublished = true,
                    CreatedById = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                }
            );
        }


        private async Task SeedQuizQuestionsAsync()
        {
            _context.Set<QuizQuestion>().AddRange(
                new QuizQuestion { Id = Guid.NewGuid(), QuizId = QuizMathId, QuestionId = Guid.Parse("ffff1111-ffff-1111-ffff-111111111111"), SortOrder = 1 },
                new QuizQuestion { Id = Guid.NewGuid(), QuizId = QuizMathId, QuestionId = Guid.Parse("ffff2222-ffff-2222-ffff-222222222222"), SortOrder = 2 },
                new QuizQuestion { Id = Guid.NewGuid(), QuizId = QuizScienceId, QuestionId = Guid.Parse("ffff3333-ffff-3333-ffff-333333333333"), SortOrder = 1 },
                new QuizQuestion { Id = Guid.NewGuid(), QuizId = QuizScienceId, QuestionId = Guid.Parse("ffff4444-ffff-4444-ffff-444444444444"), SortOrder = 2 }
            );
        }



        private async Task SeedAiTemplatesAsync()
        {
            var templates = new[]
            {
                new AiTemplate
                {
                    Id = Guid.NewGuid(),
                    SubjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Prompt = "Generate a math question about {topic} with difficulty level {difficulty}",
                    DifficultyMin = Difficulty.easy,
                    DifficultyMax = Difficulty.hard,
                    CreatedById = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    CreatedAt = DateTime.UtcNow.AddDays(-6)
                },
                new AiTemplate
                {
                    Id = Guid.NewGuid(),
                    SubjectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Prompt = "Create a science question about {topic} for students",
                    DifficultyMin = Difficulty.easy,
                    DifficultyMax = Difficulty.medium,
                    CreatedById = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                }
            };

            _context.Set<AiTemplate>().AddRange(templates);
        }

        private async Task SeedAttemptsAsync()
        {
            _context.Set<Attempt>().AddRange(
                new Attempt { Id = Guid.NewGuid(), UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"), QuizId = QuizMathId, StartedAt = DateTime.UtcNow.AddDays(-7), CompletedAt = DateTime.UtcNow.AddDays(-7).AddMinutes(15), Score = 85, XpEarned = 100 },
                new Attempt { Id = Guid.NewGuid(), UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"), QuizId = QuizMathId, StartedAt = DateTime.UtcNow.AddDays(-5), CompletedAt = DateTime.UtcNow.AddDays(-5).AddMinutes(20), Score = 75, XpEarned = 85 },
                new Attempt { Id = Guid.NewGuid(), UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"), QuizId = QuizScienceId, StartedAt = DateTime.UtcNow.AddDays(-3), CompletedAt = DateTime.UtcNow.AddDays(-3).AddMinutes(12), Score = 90, XpEarned = 120 }
            );
        }

        private async Task SeedResponsesAsync()
        {
           
            var firstAttemptId = _context.Set<Attempt>().Local.First().Id;

            _context.Set<Response>().AddRange(
                new Response
                {
                    Id = Guid.NewGuid(),
                    AttemptId = firstAttemptId,
                    QuestionId = Guid.Parse("ffff3333-ffff-3333-ffff-333333333333"),
                    SelectedOptionId = OptH2OId,
                    IsCorrect = true,
                    TimeMs = 8500
                },
                new Response
                {
                    Id = Guid.NewGuid(),
                    AttemptId = firstAttemptId,
                    QuestionId = Guid.Parse("ffff4444-ffff-4444-ffff-444444444444"),
                    SelectedOptionId = OptTrueId,
                    IsCorrect = true,
                    TimeMs = 6200
                }
            );
        }


        private async Task SeedDuelsAsync()
        {
            _context.Set<Duel>().AddRange(
                new Duel
                {
                    Id = Duel1Id,
                    SubjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Status = DuelStatus.completed,
                    BestOf = 3,
                    StartedAt = DateTime.UtcNow.AddDays(-2),
                    EndedAt = DateTime.UtcNow.AddDays(-2).AddMinutes(15),
                    CreatedAt = DateTime.UtcNow.AddDays(-2).AddHours(-1)
                },
                new Duel
                {
                    Id = Guid.NewGuid(),
                    SubjectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Status = DuelStatus.pending,
                    BestOf = 5,
                    CreatedAt = DateTime.UtcNow.AddHours(-2)
                }
            );
        }

        private async Task SeedDuelParticipantsAsync()
        {
            _context.Set<DuelParticipant>().AddRange(
                new DuelParticipant
                {
                    Id = Guid.NewGuid(),
                    DuelId = Duel1Id,
                    UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Score = 2,
                    Result = DuelResult.win
                },
                new DuelParticipant
                {
                    Id = Guid.NewGuid(),
                    DuelId = Duel1Id,
                    UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    InvitedById = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Score = 1,
                    Result = DuelResult.lose
                }
            );
        }

        private async Task SeedDuelRoundsAsync()
        {
            _context.Set<DuelRound>().AddRange(
                new DuelRound { Id = Guid.NewGuid(), DuelId = Duel1Id, RoundNumber = 1, QuestionId = Guid.Parse("ffff1111-ffff-1111-ffff-111111111111"), TimeLimitSeconds = 30 },
                new DuelRound { Id = Guid.NewGuid(), DuelId = Duel1Id, RoundNumber = 2, QuestionId = Guid.Parse("ffff2222-ffff-2222-ffff-222222222222"), TimeLimitSeconds = 45 }
            );
        }

        private async Task SeedUnlockRulesAsync()
        {
            var mathLevels = _context.Set<Level>()
                .Where(l => l.SubjectId == Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))
                .OrderBy(l => l.LevelNumber)
                .ToList();

            if (mathLevels.Count >= 2)
            {
                var unlockRules = new[]
                {
                    new UnlockRule
                    {
                        Id = Guid.NewGuid(),
                        SubjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                        FromLevelId = mathLevels[0].Id,
                        ToLevelId = mathLevels[1].Id,
                        Condition = UnlockConditionType.attempt_count,
                        Threshold = 80
                    }
                };

                _context.Set<UnlockRule>().AddRange(unlockRules);
            }
        }

        private async Task SeedUserProgressAsync()
        {
            var progress = new[]
            {
                new UserProgress
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    SubjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Xp = 450,
                    LastActivity = DateTime.UtcNow.AddHours(-6)
                },
                new UserProgress
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    SubjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Xp = 320,
                    LastActivity = DateTime.UtcNow.AddDays(-1)
                },
                new UserProgress
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    SubjectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    Xp = 280,
                    LastActivity = DateTime.UtcNow.AddHours(-12)
                }
            };

            _context.Set<UserProgress>().AddRange(progress);
        }

        private async Task SeedAiGenerationsAsync()
        {
            var aiTemplate = _context.Set<AiTemplate>().First();

            var generations = new[]
            {
                new AiGeneration
                {
                    Id = Guid.NewGuid(),
                    TemplateId = aiTemplate.Id,
                    QuestionId = Guid.Parse("ffff1111-ffff-1111-ffff-111111111111"),
                    ModelName = "GPT-4",
                    ModelVersion = "gpt-4-0613",
                    Metadata = "{\"temperature\": 0.7, \"max_tokens\": 500}",
                    GeneratedAt = DateTime.UtcNow.AddDays(-10)
                },
                new AiGeneration
                {
                    Id = Guid.NewGuid(),
                    TemplateId = aiTemplate.Id,
                    QuestionId = Guid.Parse("ffff2222-ffff-2222-ffff-222222222222"),
                    ModelName = "Claude-3",
                    ModelVersion = "claude-3-sonnet-20240229",
                    Metadata = "{\"temperature\": 0.5, \"max_tokens\": 750}",
                    GeneratedAt = DateTime.UtcNow.AddDays(-8)
                }
            };

            _context.Set<AiGeneration>().AddRange(generations);
        }
    }


    public static class DatabaseSeederExtensions
    {
        public static async Task SeedDatabaseAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            var seeder = new DatabaseSeeder(context, userManager, roleManager); 
            await seeder.SeedAsync();
        }
    }

}
