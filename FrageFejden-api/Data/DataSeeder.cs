using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FrageFejden.Entities;
using FrageFejden.Entities.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FrageFejden.Data
{
    public class DatabaseSeeder
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;

        // ── Fasta ID:n för klasser ────────────────────────────────────────────────────────
        private static readonly Guid Class8A = Guid.Parse("11111111-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid Class9B = Guid.Parse("22222222-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid Class9C = Guid.Parse("33333333-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid Class10D = Guid.Parse("44444444-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // ── Fasta ID:n för ämnen (globala, skapas separat – inte i Class-grafen) ─────────
        private static readonly Guid SubjectMath = Guid.Parse("aaaaaaaa-1111-1111-1111-aaaaaaaaaaaa");
        private static readonly Guid SubjectScience = Guid.Parse("aaaaaaaa-2222-2222-2222-aaaaaaaaaaaa");
        private static readonly Guid SubjectHistory = Guid.Parse("aaaaaaaa-3333-3333-3333-aaaaaaaaaaaa");

        // ── Fasta ID:n för topics ────────────────────────────────────────────────────────
        private static readonly Guid TopicAlgebra = Guid.Parse("bbbbbbbb-1111-1111-1111-bbbbbbbbbbbb");
        private static readonly Guid TopicGeometry = Guid.Parse("bbbbbbbb-2222-2222-2222-bbbbbbbbbbbb");
        private static readonly Guid TopicPhysics = Guid.Parse("bbbbbbbb-3333-3333-3333-bbbbbbbbbbbb");
        private static readonly Guid TopicBiology = Guid.Parse("bbbbbbbb-4444-4444-4444-bbbbbbbbbbbb");
        private static readonly Guid TopicWW2 = Guid.Parse("bbbbbbbb-5555-5555-5555-bbbbbbbbbbbb");
        private static readonly Guid TopicAncients = Guid.Parse("bbbbbbbb-6666-6666-6666-bbbbbbbbbbbb");

        // ── In-memory kartor för att länka ihop allt efter hand ──────────────────────────
        private readonly Dictionary<Guid, List<Level>> _levelsByTopic = new();                 // TopicId -> Levels
        private readonly Dictionary<(Guid topicId, int level), Guid> _quizByLevel = new();     // (TopicId, LevelNumber) -> QuizId
        private readonly Dictionary<(Guid topicId, int level), List<Guid>> _questionIdsByLevel = new(); // (TopicId, LevelNumber) -> QuestionIds

        private readonly List<Question> _questions = new();
        private readonly List<QuestionOption> _options = new();
        private readonly List<Quiz> _quizzes = new();

        public DatabaseSeeder(AppDbContext context, UserManager<AppUser> userManager, RoleManager<IdentityRole<Guid>> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task SeedAsync(CancellationToken ct = default)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            // 1) Användare och roller
            await SeedIfEmpty<AppUser>(SeedUsersAsync, ct);

            // 2) Klasser (OBS: inga Subjects här – de skapas globalt i nästa steg)
            await SeedIfEmpty<Class>(SeedClassesAsync, ct);

            // 3) Globala Subjects (Med fasta ID:n; ingen dubblett, ingen tracking-konflikt)
            await EnsureSubjectsAsync();
            await _context.SaveChangesAsync(ct);

            // 4) Topics som refererar Subjects (FK OK, ämnen finns)
            await SeedIfEmpty<Topic>(SeedTopicsAsync, ct);

            // 5) Levels med studietext per topic
            await SeedIfEmpty<Level>(SeedLevelsWithStudyTextAsync, ct);

            // 6) Medlemskap i klasser
            await SeedIfEmpty<ClassMembership>(SeedClassMembershipsAsync, ct);

            // 7) Frågor + alternativ (per topic/level, riktiga förklaringar)
            await SeedIfEmpty<Question>(SeedQuestionsAsync, ct);
            await SeedIfEmpty<QuestionOption>(SeedQuestionOptionsAsync, ct);

            // 8) Ett quiz per (topic, level), publicerat till två klasser
            await SeedIfEmpty<Quiz>(SeedQuizzesAsync, ct);

            // 9) Länka frågor till respektive quiz (efter att quiz finns)
            await SeedIfEmpty<QuizQuestion>(SeedQuizQuestionsAsync, ct);

            await tx.CommitAsync(ct);
        }

        private async Task SeedIfEmpty<TEntity>(Func<Task> seedFunc, CancellationToken ct = default) where TEntity : class
        {
            if (await _context.Set<TEntity>().AnyAsync(ct)) return;
            await seedFunc();
            await _context.SaveChangesAsync(ct);
        }

        // ─────────────────────────────── USERS & ROLES ────────────────────────────────────
        private async Task SeedUsersAsync()
        {
            foreach (var rn in new[] { "admin", "teacher", "student" })
                if (!await _roleManager.RoleExistsAsync(rn))
                    await _roleManager.CreateAsync(new IdentityRole<Guid>(rn));

            var users = new[]
            {
                // Lärare (3 st)
                NewUser("tina.teacher@school.edu",  "Tina Larsson",   Role.teacher, "55555555-1111-1111-1111-555555555551"),
                NewUser("olof.teacher@school.edu",  "Olof Berg",      Role.teacher, "55555555-2222-2222-2222-555555555552"),
                NewUser("maria.teacher@school.edu", "Maria Sund",     Role.teacher, "55555555-3333-3333-3333-555555555553"),

                // Admin
                NewUser("admin@school.edu",         "Admin Användare", Role.admin,   "99999999-9999-9999-9999-999999999999"),

                // Elever (12 st – minst 3 i varje klass)
                NewUser("eva.8a@school.edu",   "Eva Karlsson",      Role.student, "66666666-0001-0001-0001-666666666661"),
                NewUser("ahmed.8a@school.edu", "Ahmed Ali",         Role.student, "66666666-0002-0002-0002-666666666662"),
                NewUser("lisa.8a@school.edu",  "Lisa Norén",        Role.student, "66666666-0003-0003-0003-666666666663"),

                NewUser("jon.9b@school.edu",   "Jon Persson",       Role.student, "66666666-1001-1001-1001-666666666671"),
                NewUser("mia.9b@school.edu",   "Mia Östberg",       Role.student, "66666666-1002-1002-1002-666666666672"),
                NewUser("leo.9b@school.edu",   "Leo Olsson",        Role.student, "66666666-1003-1003-1003-666666666673"),

                NewUser("nina.9c@school.edu",  "Nina Holm",         Role.student, "66666666-2001-2001-2001-666666666681"),
                NewUser("vik.9c@school.edu",   "Viktor Pettersson", Role.student, "66666666-2002-2002-2002-666666666682"),
                NewUser("sam.9c@school.edu",   "Sam Tran",          Role.student, "66666666-2003-2003-2003-666666666683"),

                NewUser("edvin.10d@school.edu","Edvin Åkesson",     Role.student, "66666666-3001-3001-3001-666666666691"),
                NewUser("sofia.10d@school.edu","Sofia Bergström",   Role.student, "66666666-3002-3002-3002-666666666692"),
                NewUser("maya.10d@school.edu", "Maya Widell",       Role.student, "66666666-3003-3003-3003-666666666693"),
            };

            foreach (var u in users)
            {
                var existing = await _userManager.FindByIdAsync(u.Id.ToString());
                if (existing is null)
                {
                    var res = await _userManager.CreateAsync(u, "Password123!");
                    if (!res.Succeeded)
                        throw new InvalidOperationException($"Kunde inte skapa användare {u.Email}: " +
                            string.Join(", ", res.Errors.Select(e => e.Description)));
                    existing = u;
                }

                var roleName = MapEnumToIdentityRoleName(existing.Role);
                if (!await _userManager.IsInRoleAsync(existing, roleName))
                {
                    var addRoleRes = await _userManager.AddToRoleAsync(existing, roleName);
                    if (!addRoleRes.Succeeded)
                        throw new InvalidOperationException($"Kunde inte lägga till roll {roleName} till {existing.Email}: " +
                            string.Join(", ", addRoleRes.Errors.Select(e => e.Description)));
                }
            }

            static AppUser NewUser(string email, string name, Role role, string id)
            {
                return new AppUser
                {
                    Id = Guid.Parse(id),
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = name,
                    Role = role,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    experiencePoints = role == Role.student ? 300 : (role == Role.teacher ? 1200 : 5000)
                };
            }
        }

        private static string MapEnumToIdentityRoleName(Role r) => r switch
        {
            Role.admin => "admin",
            Role.teacher => "teacher",
            Role.student => "student",
            _ => "student"
        };

        // ───────────────────────────────── CLASSES ────────────────────────────────────────
        // Viktigt: inga Subjects här (för att undvika dubblering/track-konflikter). 
        private async Task SeedClassesAsync()
        {
            var teacherTina = Guid.Parse("55555555-1111-1111-1111-555555555551");
            var teacherOlof = Guid.Parse("55555555-2222-2222-2222-555555555552");
            var teacherMaria = Guid.Parse("55555555-3333-3333-3333-555555555553");

            _context.Set<Class>().AddRange(
                new Class { Id = Class8A, Name = "8A", GradeLabel = "Åk 8", JoinCode = "JOIN8A", CreatedById = teacherTina, CreatedAt = DateTime.UtcNow.AddMonths(-2) },
                new Class { Id = Class9B, Name = "9B", GradeLabel = "Åk 9", JoinCode = "JOIN9B", CreatedById = teacherOlof, CreatedAt = DateTime.UtcNow.AddMonths(-2) },
                new Class { Id = Class9C, Name = "9C", GradeLabel = "Åk 9", JoinCode = "JOIN9C", CreatedById = teacherOlof, CreatedAt = DateTime.UtcNow.AddMonths(-2) },
                new Class { Id = Class10D, Name = "10D", GradeLabel = "Åk 10", JoinCode = "JOIN10D", CreatedById = teacherMaria, CreatedAt = DateTime.UtcNow.AddMonths(-2) }
            );
        }

        // ─────────────────────────────── SUBJECTS (globalt) ───────────────────────────────
        // Skapas alltid/”upsertas” så FK till Topics är säkra, utan dubbletter/konflikter.
        private async Task EnsureSubjectsAsync()
        {
            var now = DateTime.UtcNow;

            async Task EnsureOne(Guid id, string name, string desc, string icon, Guid createdBy)
            {
                var existing = await _context.Set<Subject>().FindAsync(id);
                if (existing == null)
                {
                    _context.Set<Subject>().Add(new Subject
                    {
                        Id = id,
                        Name = name,
                        Description = desc,
                        IconUrl = icon,
                        CreatedById = createdBy,
                        CreatedAt = now
                    });
                }
                else
                {
                    // valfritt: uppdatera metadata
                    existing.Name = name;
                    existing.Description = desc;
                    existing.IconUrl = icon;
                }
            }

            var tina = Guid.Parse("55555555-1111-1111-1111-555555555551");
            var olof = Guid.Parse("55555555-2222-2222-2222-555555555552");
            var maria = Guid.Parse("55555555-3333-3333-3333-555555555553");

            await EnsureOne(SubjectMath, "Matematik", "Algebra och geometri", "icons/math.png", tina);
            await EnsureOne(SubjectScience, "NO", "Fysik och biologi", "icons/science.png", olof);
            await EnsureOne(SubjectHistory, "Historia", "Världshistoria", "icons/history.png", maria);
        }

        // ─────────────────────────────────── TOPICS ───────────────────────────────────────
        private async Task SeedTopicsAsync()
        {
            _context.Set<Topic>().AddRange(
                new Topic { Id = TopicAlgebra, SubjectId = SubjectMath, Name = "Algebra", Description = "Uttryck, ekvationer, faktorisering", SortOrder = 0 },
                new Topic { Id = TopicGeometry, SubjectId = SubjectMath, Name = "Geometri", Description = "Former, vinklar, area och volym", SortOrder = 1 },

                new Topic { Id = TopicPhysics, SubjectId = SubjectScience, Name = "Fysik", Description = "Krafter, rörelse, energi", SortOrder = 0 },
                new Topic { Id = TopicBiology, SubjectId = SubjectScience, Name = "Biologi", Description = "Cellen, genetik, system", SortOrder = 1 },

                new Topic { Id = TopicWW2, SubjectId = SubjectHistory, Name = "Andra världskriget", Description = "Europa, Stillahavet, Förintelsen, 1939–1945", SortOrder = 0 },
                new Topic { Id = TopicAncients, SubjectId = SubjectHistory, Name = "Antikens civilisationer", Description = "Mesopotamien, Egypten, Grekland, Rom", SortOrder = 1 }
            );
        }

        // ───────────────────────────── LEVELS + STUDIETEXTER ─────────────────────────────
        private async Task SeedLevelsWithStudyTextAsync()
        {
            foreach (var (topicId, topicName) in new[]
            {
                (TopicAlgebra,  "Algebra"),
                (TopicGeometry, "Geometri"),
                (TopicPhysics,  "Fysik"),
                (TopicBiology,  "Biologi"),
                (TopicWW2,      "Andra världskriget"),
                (TopicAncients, "Antikens civilisationer")
            })
            {
                var levels = new List<Level>();
                for (int i = 1; i <= 3; i++)
                {
                    levels.Add(new Level
                    {
                        Id = Guid.NewGuid(),
                        TopicId = topicId,
                        LevelNumber = i,
                        Title = $"{topicName} – Nivå {i}",
                        MinXpUnlock = (i - 1) * 200,
                        StudyText = BuildStudyText(topicName, i)
                    });
                }
                _levelsByTopic[topicId] = levels;
                _context.Set<Level>().AddRange(levels);
            }

            static string BuildStudyText(string topic, int level) => topic switch
            {
                "Algebra" => level switch
                {
                    1 => "Nivå 1: termer, koefficienter, enkla ekvationer (x + 3 = 7).",
                    2 => "Nivå 2: fördelningslagen, förenkling, ekvationer med parenteser (2(x+3)=14).",
                    3 => "Nivå 3: faktorisering, potenser, enkla ekvationssystem.",
                    _ => ""
                },
                "Geometri" => level switch
                {
                    1 => "Nivå 1: triangeltyper, vinkelsumma 180°, area rektangel (b·h).",
                    2 => "Nivå 2: Pythagoras, cirkelomkrets (2πr), area triangel (b·h/2).",
                    3 => "Nivå 3: likformighet, vinklar vid parallella linjer, volym kub.",
                    _ => ""
                },
                "Fysik" => level switch
                {
                    1 => "Nivå 1: tröghetslagen, hastighet v=s/t, energiformer.",
                    2 => "Nivå 2: acceleration, F=ma, potentiell energi Ep=mgh.",
                    3 => "Nivå 3: rörelsemängd p=mv, arbete W=F·s, effekt P=W/t.",
                    _ => ""
                },
                "Biologi" => level switch
                {
                    1 => "Nivå 1: cellens delar, fotosyntesen (6CO₂ + 6H₂O → C₆H₁₂O₆ + 6O₂).",
                    2 => "Nivå 2: DNA, mitos, cellandning (glukos + O₂ → CO₂ + H₂O + energi).",
                    3 => "Nivå 3: nervsystem, immunsystem, enzymer.",
                    _ => ""
                },
                "Andra världskriget" => level switch
                {
                    1 => "Nivå 1: krigsstart 1939, axelmakter/allierade, blitzkrieg.",
                    2 => "Nivå 2: Barbarossa, D-dagen, Förintelsen.",
                    3 => "Nivå 3: Stillahavskriget, Hiroshima/Nagasaki, FN 1945.",
                    _ => ""
                },
                "Antikens civilisationer" => level switch
                {
                    1 => "Nivå 1: Mesopotamiens floder, hieroglyfer, grekiska stadsstater.",
                    2 => "Nivå 2: Romerska republiken/kejsardömet, Hammurabis lagar, pyramider.",
                    3 => "Nivå 3: Alexander den store, romerska vägar, atensk demokrati.",
                    _ => ""
                },
                _ => ""
            };
        }

        // ─────────────────────────────── CLASS MEMBERSHIPS ────────────────────────────────
        private async Task SeedClassMembershipsAsync()
        {
            var tina = Guid.Parse("55555555-1111-1111-1111-555555555551");
            var olof = Guid.Parse("55555555-2222-2222-2222-555555555552");
            var maria = Guid.Parse("55555555-3333-3333-3333-555555555553");

            var m = new List<ClassMembership>
            {
                // Lärare
                CM(Class8A,  tina,  Role.teacher),
                CM(Class9B,  olof,  Role.teacher),
                CM(Class9C,  olof,  Role.teacher),
                CM(Class10D, maria, Role.teacher),

                // Elever: minst 3 per klass
                CM(Class8A,  Guid.Parse("66666666-0001-0001-0001-666666666661")),
                CM(Class8A,  Guid.Parse("66666666-0002-0002-0002-666666666662")),
                CM(Class8A,  Guid.Parse("66666666-0003-0003-0003-666666666663")),

                CM(Class9B,  Guid.Parse("66666666-1001-1001-1001-666666666671")),
                CM(Class9B,  Guid.Parse("66666666-1002-1002-1002-666666666672")),
                CM(Class9B,  Guid.Parse("66666666-1003-1003-1003-666666666673")),

                CM(Class9C,  Guid.Parse("66666666-2001-2001-2001-666666666681")),
                CM(Class9C,  Guid.Parse("66666666-2002-2002-0002-666666666682".Replace("0002","2002"))), // safe literal
                CM(Class9C,  Guid.Parse("66666666-2003-2003-2003-666666666683")),

                CM(Class10D, Guid.Parse("66666666-3001-3001-3001-666666666691")),
                CM(Class10D, Guid.Parse("66666666-3002-3002-3002-666666666692")),
                CM(Class10D, Guid.Parse("66666666-3003-3003-3003-666666666693")),
            };

            _context.Set<ClassMembership>().AddRange(m);

            static ClassMembership CM(Guid classId, Guid userId, Role roleInClass = Role.student)
                => new ClassMembership { Id = Guid.NewGuid(), ClassId = classId, UserId = userId, RoleInClass = roleInClass, EnrolledAt = DateTime.UtcNow.AddDays(-20) };
        }

        // ───────────────────────────── QUESTIONS (riktiga) ────────────────────────────────
        private async Task SeedQuestionsAsync()
        {
            // Hjälpare som registrerar frågor + kopplar nivå (i minneskartan)
            void Q(Guid subjectId, Guid topicId, int level, string stem, string explanation, params (string text, bool correct)[] options)
            {
                var q = new Question
                {
                    Id = Guid.NewGuid(),
                    SubjectId = subjectId,
                    Type = QuestionType.multiple_choice,
                    Difficulty = level == 1 ? Difficulty.easy : (level == 2 ? Difficulty.medium : Difficulty.hard),
                    Stem = stem,
                    Explanation = explanation,
                    SourceAi = false,
                    CreatedById = Guid.Parse("55555555-1111-1111-1111-555555555551"),
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                };
                _questions.Add(q);

                int sort = 1;
                foreach (var (text, correct) in options)
                {
                    _options.Add(new QuestionOption
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = q.Id,
                        OptionText = text,
                        IsCorrect = correct,
                        SortOrder = sort++
                    });
                }

                if (!_questionIdsByLevel.TryGetValue((topicId, level), out var list))
                {
                    list = new List<Guid>();
                    _questionIdsByLevel[(topicId, level)] = list;
                }
                list.Add(q.Id);
            }

            // --- Algebra ---
            Q(SubjectMath, TopicAlgebra, 1, "Vilken är koefficienten i uttrycket 5x + 3?", "Koefficienten är talet som multiplicerar variabeln.",
                ("5", true), ("x", false), ("3", false), ("8", false));
            Q(SubjectMath, TopicAlgebra, 1, "Lös ekvationen x + 3 = 7.", "Subtrahera 3 från båda sidor: x = 4.",
                ("4", true), ("3", false), ("7", false), ("-4", false));
            Q(SubjectMath, TopicAlgebra, 1, "Vad betyder uttrycket 2x?", "Det är två gånger variabeln x.",
                ("x + x", true), ("x - x", false), ("x/2", false), ("2 + x", false));

            Q(SubjectMath, TopicAlgebra, 2, "Förenkla 2(x + 3).", "Fördelningslagen: 2x + 6.",
                ("2x + 6", true), ("x + 6", false), ("2x + 3", false), ("x + 9", false));
            Q(SubjectMath, TopicAlgebra, 2, "Lös 2x - 4 = 10.", "Addera 4 och dela med 2: x = 7.",
                ("7", true), ("3", false), ("-7", false), ("14", false));
            Q(SubjectMath, TopicAlgebra, 2, "Förenkla 3x + 2x - x.", "3x + 2x - x = 4x.",
                ("4x", true), ("6x", false), ("5x", false), ("2x", false));

            Q(SubjectMath, TopicAlgebra, 3, "Faktorisera uttrycket x^2 - 9.", "Konjugat: (x-3)(x+3).",
                ("(x-3)(x+3)", true), ("(x-9)(x+9)", false), ("x(x-9)", false), ("(x-1)(x+9)", false));
            Q(SubjectMath, TopicAlgebra, 3, "Lös systemet: x + y = 5 och x - y = 1.", "Addera: 2x=6 ⇒ x=3, y=2.",
                ("x=3, y=2", true), ("x=2, y=3", false), ("x=4, y=1", false), ("x=1, y=4", false));
            Q(SubjectMath, TopicAlgebra, 3, "Värdet av 2^3 · 2^2 är?", "Exponenter adderas: 2^5 = 32.",
                ("32", true), ("64", false), ("16", false), ("8", false));

            // --- Geometri ---
            Q(SubjectMath, TopicGeometry, 1, "Vinkelsumman i en triangel är?", "Alltid 180°.",
                ("180°", true), ("90°", false), ("270°", false), ("360°", false));
            Q(SubjectMath, TopicGeometry, 1, "Arean av rektangel med bas 6 och höjd 3?", "A=b·h=18.",
                ("18", true), ("9", false), ("12", false), ("24", false));
            Q(SubjectMath, TopicGeometry, 1, "Vilken triangel har en 90° vinkel?", "Rätvinklig triangel.",
                ("Rätvinklig", true), ("Likbent", false), ("Liksidig", false), ("Trubbvinklig", false));

            Q(SubjectMath, TopicGeometry, 2, "Pythagoras: kateter 3 och 4 ⇒ hypotenusa?", "√(3²+4²)=5.",
                ("5", true), ("6", false), ("7", false), ("4", false));
            Q(SubjectMath, TopicGeometry, 2, "Omkrets av cirkel med r=7?", "2πr ≈ 44.",
                ("≈ 44", true), ("≈ 22", false), ("≈ 14", false), ("≈ 88", false));
            Q(SubjectMath, TopicGeometry, 2, "Area av triangel b=10, h=4?", "(b·h)/2=20.",
                ("20", true), ("40", false), ("10", false), ("14", false));

            Q(SubjectMath, TopicGeometry, 3, "Likformiga trianglar har…", "Lika vinklar, proportionella sidor.",
                ("Lika vinklar och proportionella sidor", true), ("Lika sidor men olika vinklar", false), ("Alltid samma area", false), ("Alltid samma omkrets", false));
            Q(SubjectMath, TopicGeometry, 3, "Alternatvinklar uppstår när…", "Två parallella linjer skärs av transversal.",
                ("En transversal skär två parallella linjer", true), ("Två icke-parallella linjer möts", false), ("En triangel ritas", false), ("En cirkel ritas", false));
            Q(SubjectMath, TopicGeometry, 3, "Volym av kub med sida 4?", "4³=64.",
                ("64", true), ("16", false), ("32", false), ("48", false));

            // --- Fysik ---
            Q(SubjectScience, TopicPhysics, 1, "Newtons första lag kallas…", "Tröghetslagen.",
                ("Tröghetslagen", true), ("Gravitationslagen", false), ("Termodynamikens nollte", false), ("Hookes lag", false));
            Q(SubjectScience, TopicPhysics, 1, "Hastighet v definieras som…", "Sträcka per tid (v=s/t).",
                ("Sträcka per tid", true), ("Tid per sträcka", false), ("Kraft per massa", false), ("Energi per tid", false));
            Q(SubjectScience, TopicPhysics, 1, "Vilken energi har ett föremål högt upp?", "Potentiell energi.",
                ("Potentiell", true), ("Kinetisk", false), ("Värme", false), ("El", false));

            Q(SubjectScience, TopicPhysics, 2, "Newtons andra lag är…", "F=ma.",
                ("F=ma", true), ("E=mc²", false), ("p=mv", false), ("V=IR", false));
            Q(SubjectScience, TopicPhysics, 2, "Potentiell energi uttrycks…", "Ep=mgh.",
                ("Ep=mgh", true), ("Ep=1/2mv²", false), ("Ep=F·s", false), ("Ep=p·V", false));
            Q(SubjectScience, TopicPhysics, 2, "Acceleration mäts i…", "m/s².",
                ("m/s²", true), ("m/s", false), ("N", false), ("kg", false));

            Q(SubjectScience, TopicPhysics, 3, "Rörelsemängd p definieras som…", "p=mv.",
                ("p=mv", true), ("p=m/a", false), ("p=ma", false), ("p=m+v", false));
            Q(SubjectScience, TopicPhysics, 3, "Arbete W definieras som…", "W=F·s.",
                ("W=F·s", true), ("W=P·t", false), ("W=m·g", false), ("W=Q·V", false));
            Q(SubjectScience, TopicPhysics, 3, "Effekt P är…", "Arbete per tid (P=W/t).",
                ("P=W/t", true), ("P=F·s", false), ("P=mv", false), ("P=mgh", false));

            // --- Biologi ---
            Q(SubjectScience, TopicBiology, 1, "Cellkärnan innehåller…", "DNA.",
                ("DNA", true), ("ATP", false), ("Ribosomer", false), ("Klorofyll", false));
            Q(SubjectScience, TopicBiology, 1, "Fotosyntesen producerar…", "Glukos och syre.",
                ("Glukos och syre", true), ("Koldioxid och vatten", false), ("Protein", false), ("Lipider", false));
            Q(SubjectScience, TopicBiology, 1, "Cellmembranets roll är…", "Reglera transport in/ut.",
                ("Reglera transport", true), ("Skapa ATP", false), ("Bryta ner gifter", false), ("Lagra DNA", false));

            Q(SubjectScience, TopicBiology, 2, "DNA bär…", "Genetisk information.",
                ("Genetisk information", true), ("Syre", false), ("Avfall", false), ("Aminosyror", false));
            Q(SubjectScience, TopicBiology, 2, "Mitos ger…", "Två identiska dotterceller.",
                ("Två identiska dotterceller", true), ("Fyra könsceller", false), ("En större cell", false), ("Ingen förändring", false));
            Q(SubjectScience, TopicBiology, 2, "Cellandning frigör…", "Energi ur glukos.",
                ("Energi ur glukos", true), ("Syre ur vatten", false), ("Glukos ur syre", false), ("DNA ur proteiner", false));

            Q(SubjectScience, TopicBiology, 3, "Nervsystemets grundcell heter…", "Neuron.",
                ("Neuron", true), ("Hemoglobin", false), ("Myosin", false), ("Makrofag", false));
            Q(SubjectScience, TopicBiology, 3, "Antikroppar produceras av…", "B-celler.",
                ("B-celler", true), ("Neuron", false), ("Osteoblaster", false), ("Erytrocyter", false));
            Q(SubjectScience, TopicBiology, 3, "Enzymer…", "Sänker aktiveringsenergin.",
                ("Sänker aktiveringsenergi", true), ("Höjer temperaturen", false), ("Ger laddning", false), ("Är alltid kolhydrater", false));

            // --- Andra världskriget ---
            Q(SubjectHistory, TopicWW2, 1, "Kriget började år…", "1939.",
                ("1939", true), ("1914", false), ("1941", false), ("1945", false));
            Q(SubjectHistory, TopicWW2, 1, "Axelmakterna var bl.a.…", "Tyskland, Italien, Japan.",
                ("Tyskland, Italien, Japan", true), ("USA, UK, Sovjet", false), ("Sverige, Norge, Danmark", false), ("Spanien, Portugal, Schweiz", false));
            Q(SubjectHistory, TopicWW2, 1, "Blitzkrieg syftar på…", "Snabbt, koncentrerat anfall.",
                ("Snabbt koncentrerat anfall", true), ("Ubåtskrig", false), ("Artilleribombning", false), ("Luftbro", false));

            Q(SubjectHistory, TopicWW2, 2, "Operation Barbarossa var…", "Tysklands anfall mot Sovjet 1941.",
                ("Anfallet mot Sovjet 1941", true), ("Invasion av Polen", false), ("D-dagen", false), ("Fredsavtal", false));
            Q(SubjectHistory, TopicWW2, 2, "D-dagen (Normandie) var…", "Allierad landstigning 1944.",
                ("Landstigning i Normandie 1944", true), ("Bombning av London", false), ("Japans kapitulation", false), ("Stalingrad", false));
            Q(SubjectHistory, TopicWW2, 2, "Förintelsen avser…", "Systematisk förföljelse/mord.",
                ("Systematisk förföljelse/mord", true), ("Militärkupp", false), ("Kärnvapenprogram", false), ("Evakuering", false));

            Q(SubjectHistory, TopicWW2, 3, "Atombomber fälldes över…", "Hiroshima och Nagasaki 1945.",
                ("Hiroshima & Nagasaki", true), ("Tokyo & Osaka", false), ("Seoul & Pusan", false), ("Peking & Nanjing", false));
            Q(SubjectHistory, TopicWW2, 3, "FN grundades…", "1945.",
                ("1945", true), ("1939", false), ("1919", false), ("1950", false));
            Q(SubjectHistory, TopicWW2, 3, "Kriget i Europa slutade…", "Maj 1945.",
                ("Maj 1945", true), ("Juni 1944", false), ("Sept 1943", false), ("Aug 1946", false));

            // --- Antiken ---
            Q(SubjectHistory, TopicAncients, 1, "Mesopotamien låg mellan…", "Eufrat och Tigris.",
                ("Eufrat & Tigris", true), ("Nilen & Niger", false), ("Donau & Rhen", false), ("Ganges & Brahmaputra", false));
            Q(SubjectHistory, TopicAncients, 1, "Egyptiskt skriftspråk heter…", "Hieroglyfer.",
                ("Hieroglyfer", true), ("Kilskrift", false), ("Latin", false), ("Arabiska", false));
            Q(SubjectHistory, TopicAncients, 1, "Grekisk stadsstat kallas…", "Polis.",
                ("Polis", true), ("Agora", false), ("Forum", false), ("Akropolis", false));

            Q(SubjectHistory, TopicAncients, 2, "Hammurabis lagar var…", "En tidig skriven lagkodex.",
                ("Skriven lagkodex", true), ("Skattelag", false), ("Militärhandbok", false), ("Religionsbok", false));
            Q(SubjectHistory, TopicAncients, 2, "Republiken ersattes av…", "Kejsardömet.",
                ("Kejsardömet", true), ("Demokrati", false), ("Teokrati", false), ("Feodalism", false));
            Q(SubjectHistory, TopicAncients, 2, "Pyramidernas syfte var…", "Gravar åt faraoner.",
                ("Faraoners gravar", true), ("Fästningar", false), ("Tempel", false), ("Lagerhus", false));

            Q(SubjectHistory, TopicAncients, 3, "Alexander den store…", "Spred grekisk kultur (hellenism).",
                ("Spred hellenismen", true), ("Byggde kinesiska muren", false), ("Uppfann alfabetet", false), ("Grundade Rom", false));
            Q(SubjectHistory, TopicAncients, 3, "Romerska vägar möjliggjorde…", "Snabb truppförflyttning & handel.",
                ("Snabb förflyttning & handel", true), ("Irrigation", false), ("Kraftproduktion", false), ("Kanalbyggen", false));
            Q(SubjectHistory, TopicAncients, 3, "Atensk demokrati byggde på…", "Medborgarnas direkta deltagande.",
                ("Direkt deltagande", true), ("Absolut monarki", false), ("Teokrati", false), ("Oligarki", false));

            _context.Set<Question>().AddRange(_questions);
        }

        private async Task SeedQuestionOptionsAsync()
        {
            _context.Set<QuestionOption>().AddRange(_options);
        }

        // ─────────────────────────────── QUIZZES (per Level) ───────────────────────────────
        private async Task SeedQuizzesAsync()
        {
            // Publicera samma innehåll till två olika klasser för "mycket data"
            var classTargets = new[] { Class8A, Class9B };

            foreach (var (topicId, topicName, subjectId) in new[]
            {
                (TopicAlgebra,  "Algebra",  SubjectMath),
                (TopicGeometry, "Geometri", SubjectMath),
                (TopicPhysics,  "Fysik",    SubjectScience),
                (TopicBiology,  "Biologi",  SubjectScience),
                (TopicWW2,      "Andra världskriget", SubjectHistory),
                (TopicAncients, "Antikens civilisationer", SubjectHistory)
            })
            {
                if (!_levelsByTopic.TryGetValue(topicId, out var levels)) continue;

                foreach (var level in levels)
                {
                    foreach (var cls in classTargets)
                    {
                        var qzId = Guid.NewGuid();
                        _quizByLevel[(topicId, level.LevelNumber)] = qzId;

                        _quizzes.Add(new Quiz
                        {
                            Id = qzId,
                            SubjectId = subjectId,
                            TopicId = topicId,
                            LevelId = level.Id,
                            ClassId = cls,
                            Title = $"{topicName} – Nivå {level.LevelNumber} Quiz",
                            Description = $"Quiz kopplat till studietexten för {topicName}, nivå {level.LevelNumber}.",
                            IsPublished = true,
                            CreatedById = Guid.Parse("55555555-1111-1111-1111-555555555551"),
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            _context.Set<Quiz>().AddRange(_quizzes);
        }

        private async Task SeedQuizQuestionsAsync()
        {
            // Koppla alla frågor per (topic, level) till rätt quizId
            var quizQuestions = new List<QuizQuestion>();

            foreach (var kv in _questionIdsByLevel)
            {
                var key = kv.Key;                // (topicId, levelNumber)
                var qIds = kv.Value;

                if (!_quizByLevel.TryGetValue(key, out var quizId)) continue;

                int order = 1;
                foreach (var qId in qIds)
                {
                    quizQuestions.Add(new QuizQuestion
                    {
                        Id = Guid.NewGuid(),
                        QuizId = quizId,
                        QuestionId = qId,
                        SortOrder = order++
                    });
                }
            }

            _context.Set<QuizQuestion>().AddRange(quizQuestions);
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
