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

        // ── CLASSES ───────────────────────────────────────────────────────────────
        private static readonly Guid Class8A = Guid.Parse("11111111-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid Class9B = Guid.Parse("22222222-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid Class9C = Guid.Parse("33333333-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid Class10D = Guid.Parse("44444444-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // ── SUBJECTS (GLOBAL) ─────────────────────────────────────────────────────
        private static readonly Guid SubjectMath = Guid.Parse("aaaaaaaa-1111-1111-1111-aaaaaaaaaaaa");
        private static readonly Guid SubjectScience = Guid.Parse("aaaaaaaa-2222-2222-2222-aaaaaaaaaaaa");
        private static readonly Guid SubjectHistory = Guid.Parse("aaaaaaaa-3333-3333-3333-aaaaaaaaaaaa");

        // ── TOPICS (2 per subject) ────────────────────────────────────────────────
        private static readonly Guid TopicAlgebra = Guid.Parse("bbbbbbbb-1111-1111-1111-bbbbbbbbbbbb");
        private static readonly Guid TopicGeometry = Guid.Parse("bbbbbbbb-2222-2222-2222-bbbbbbbbbbbb");
        private static readonly Guid TopicPhysics = Guid.Parse("bbbbbbbb-3333-3333-3333-bbbbbbbbbbbb");
        private static readonly Guid TopicBiology = Guid.Parse("bbbbbbbb-4444-4444-4444-bbbbbbbbbbbb");
        private static readonly Guid TopicWW2 = Guid.Parse("bbbbbbbb-5555-5555-5555-bbbbbbbbbbbb");
        private static readonly Guid TopicAncients = Guid.Parse("bbbbbbbb-6666-6666-6666-bbbbbbbbbbbb");

        // ── In-memory maps to wire data cleanly ───────────────────────────────────
        private readonly Dictionary<Guid, List<Level>> _levelsByTopic = new(); // TopicId -> Levels(1..5)
        private readonly Dictionary<(Guid topicId, int level), List<Guid>> _questionIdsByLevel = new(); // (Topic, Level) -> Q IDs

        // Quiz per (topic, level, class)
        private readonly Dictionary<(Guid topicId, int level, Guid classId), Guid> _quizByLevelClass = new();

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

            // 1) Users & roles
            await SeedIfEmpty<AppUser>(SeedUsersAsync, ct);

            // 2) Classes (NO subjects here yet)
            await SeedIfEmpty<Class>(SeedClassesAsync, ct);

            // 3) Global subjects (upsert)
            await EnsureSubjectsAsync();
            await _context.SaveChangesAsync(ct);

            // 4) Link subjects to each class (many-to-many) → 3 subjects per class
            await SeedClassSubjectLinksAsync();

            // 5) Topics (2 per subject) → FK ok (subjects exist)
            await SeedIfEmpty<Topic>(SeedTopicsAsync, ct);

            // 6) Levels (5 per topic) with study text aligned to quizzes
            await SeedIfEmpty<Level>(SeedLevelsWithStudyTextAsync, ct);

            // 7) Class memberships
            await SeedIfEmpty<ClassMembership>(SeedClassMembershipsAsync, ct);

            // 8) Questions + options (3 per level, all topics/levels)
            await SeedIfEmpty<Question>(SeedQuestionsAsync, ct);
            await SeedIfEmpty<QuestionOption>(SeedQuestionOptionsAsync, ct);

            // 9) Quizzes: one per (topic, level, class) → published
            await SeedIfEmpty<Quiz>(SeedQuizzesAsync, ct);

            // 10) Attach questions to every quiz for that (topic, level)
            await SeedIfEmpty<QuizQuestion>(SeedQuizQuestionsAsync, ct);

            await tx.CommitAsync(ct);
        }

        private async Task SeedIfEmpty<TEntity>(Func<Task> seedFunc, CancellationToken ct = default) where TEntity : class
        {
            if (await _context.Set<TEntity>().AnyAsync(ct)) return;
            await seedFunc();
            await _context.SaveChangesAsync(ct);
        }

        // ───────────────────────── USERS & ROLES ──────────────────────────────────
        private async Task SeedUsersAsync()
        {
            foreach (var rn in new[] { "admin", "teacher", "student" })
                if (!await _roleManager.RoleExistsAsync(rn))
                    await _roleManager.CreateAsync(new IdentityRole<Guid>(rn));

            var users = new[]
            {
                // Teachers
                NewUser("tina.teacher@school.edu",  "Tina Larsson",   Role.teacher, "55555555-1111-1111-1111-555555555551"),
                NewUser("olof.teacher@school.edu",  "Olof Berg",      Role.teacher, "55555555-2222-2222-2222-555555555552"),
                NewUser("maria.teacher@school.edu", "Maria Sund",     Role.teacher, "55555555-3333-3333-3333-555555555553"),
                // Admin
                NewUser("admin@school.edu",         "Admin User",     Role.admin,   "99999999-9999-9999-9999-999999999999"),
                // Students (12, min 3 per class)
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
                        throw new InvalidOperationException($"Failed to create user {u.Email}: " +
                            string.Join(", ", res.Errors.Select(e => e.Description)));
                    existing = u;
                }

                var roleName = MapEnumToIdentityRoleName(existing.Role);
                if (!await _userManager.IsInRoleAsync(existing, roleName))
                {
                    var addRoleRes = await _userManager.AddToRoleAsync(existing, roleName);
                    if (!addRoleRes.Succeeded)
                        throw new InvalidOperationException($"Failed to add role {roleName} to {existing.Email}: " +
                            string.Join(", ", addRoleRes.Errors.Select(e => e.Description)));
                }
            }

            static AppUser NewUser(string email, string name, Role role, string id) =>
                new AppUser
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

        private static string MapEnumToIdentityRoleName(Role r) => r switch
        {
            Role.admin => "admin",
            Role.teacher => "teacher",
            Role.student => "student",
            _ => "student"
        };

        // ───────────────────────── CLASSES (no subjects yet) ──────────────────────────────
        private async Task SeedClassesAsync()
        {
            var tina = Guid.Parse("55555555-1111-1111-1111-555555555551");
            var olof = Guid.Parse("55555555-2222-2222-2222-555555555552");
            var maria = Guid.Parse("55555555-3333-3333-3333-555555555553");

            _context.Set<Class>().AddRange(
                new Class { Id = Class8A, Name = "8A", GradeLabel = "Åk 8", JoinCode = "JOIN8A", CreatedById = tina, CreatedAt = DateTime.UtcNow.AddMonths(-2) },
                new Class { Id = Class9B, Name = "9B", GradeLabel = "Åk 9", JoinCode = "JOIN9B", CreatedById = olof, CreatedAt = DateTime.UtcNow.AddMonths(-2) },
                new Class { Id = Class9C, Name = "9C", GradeLabel = "Åk 9", JoinCode = "JOIN9C", CreatedById = olof, CreatedAt = DateTime.UtcNow.AddMonths(-2) },
                new Class { Id = Class10D, Name = "10D", GradeLabel = "Åk 10", JoinCode = "JOIN10D", CreatedById = maria, CreatedAt = DateTime.UtcNow.AddMonths(-2) }
            );
        }

        // ───────────────────────── SUBJECTS (global upsert) ───────────────────────────────
        private async Task EnsureSubjectsAsync()
        {
            var now = DateTime.UtcNow;
            async Task Upsert(Guid id, string name, string desc, string icon, Guid createdBy)
            {
                var s = await _context.Set<Subject>().FindAsync(id);
                if (s == null)
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
                    s.Name = name;
                    s.Description = desc;
                    s.IconUrl = icon;
                }
            }

            var tina = Guid.Parse("55555555-1111-1111-1111-555555555551");
            var olof = Guid.Parse("55555555-2222-2222-2222-555555555552");
            var maria = Guid.Parse("55555555-3333-3333-3333-555555555553");

            await Upsert(SubjectMath, "Matematik", "Algebra och geometri", "icons/math.png", tina);
            await Upsert(SubjectScience, "NO", "Fysik och biologi", "icons/science.png", olof);
            await Upsert(SubjectHistory, "Historia", "Världshistoria", "icons/history.png", maria);
        }

        // ── Link the 3 subjects to every class (many-to-many without duplicate tracking) ──
        private async Task SeedClassSubjectLinksAsync()
        {
            var classes = await _context.Set<Class>()
                .Where(c => new[] { Class8A, Class9B, Class9C, Class10D }.Contains(c.Id))
                .Include(c => c.Subjects)
                .ToListAsync();

            var math = await _context.Set<Subject>().FindAsync(SubjectMath);
            var science = await _context.Set<Subject>().FindAsync(SubjectScience);
            var history = await _context.Set<Subject>().FindAsync(SubjectHistory);

            foreach (var cls in classes)
            {
                if (cls.Subjects == null) cls.Subjects = new List<Subject>();
                if (!cls.Subjects.Any(s => s.Id == SubjectMath)) cls.Subjects.Add(math!);
                if (!cls.Subjects.Any(s => s.Id == SubjectScience)) cls.Subjects.Add(science!);
                if (!cls.Subjects.Any(s => s.Id == SubjectHistory)) cls.Subjects.Add(history!);
            }

            await _context.SaveChangesAsync();
        }

        // ───────────────────────────── TOPICS (2 per subject) ─────────────────────────────
        private async Task SeedTopicsAsync()
        {
            _context.Set<Topic>().AddRange(
                new Topic { Id = TopicAlgebra, SubjectId = SubjectMath, Name = "Algebra", Description = "Uttryck, ekvationer, faktorisering", SortOrder = 0 },
                new Topic { Id = TopicGeometry, SubjectId = SubjectMath, Name = "Geometri", Description = "Vinklar, area, volym", SortOrder = 1 },

                new Topic { Id = TopicPhysics, SubjectId = SubjectScience, Name = "Fysik", Description = "Krafter, rörelse, energi", SortOrder = 0 },
                new Topic { Id = TopicBiology, SubjectId = SubjectScience, Name = "Biologi", Description = "Cellen, genetik, system", SortOrder = 1 },

                new Topic { Id = TopicWW2, SubjectId = SubjectHistory, Name = "Andra världskriget", Description = "Europa & Stillahavet 1939–45", SortOrder = 0 },
                new Topic { Id = TopicAncients, SubjectId = SubjectHistory, Name = "Antikens civilisationer", Description = "Mesopotamien, Egypten, Grekland, Rom", SortOrder = 1 }
            );
        }

        // ───────────────────────── LEVELS (5 per topic) + STUDY TEXT ──────────────────────
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
                for (int i = 1; i <= 5; i++)
                {
                    levels.Add(new Level
                    {
                        Id = Guid.NewGuid(),
                        TopicId = topicId,
                        LevelNumber = i,
                        Title = $"{topicName} – Nivå {i}",
                        MinXpUnlock = (i - 1) * 220,
                        StudyText = StudyText(topicName, i)
                    });
                }
                _levelsByTopic[topicId] = levels;
                _context.Set<Level>().AddRange(levels);
            }

            static string StudyText(string topic, int level) => topic switch
            {
                "Algebra" => level switch
                {
                    1 => "Termer, koefficienter, enkla ekvationer (x+3=7).",
                    2 => "Fördelningslagen, förenkling, parenteser 2(x+3).",
                    3 => "Faktorisering, potenser, system av ekvationer.",
                    4 => "Kvadratiska ekvationer, rötter, olikheter.",
                    5 => "Funktioner, polynom, rationella uttryck.",
                    _ => ""
                },
                "Geometri" => level switch
                {
                    1 => "Triangeltyper, vinkelsumma 180°, area rektangel.",
                    2 => "Pythagoras, cirkelomkrets 2πr, area triangel.",
                    3 => "Likformighet, parallellvinklar, kubvolym.",
                    4 => "Koner/cylindrar, likformighetsskala, cirkelarea.",
                    5 => "Geometriska bevis, trigbas (sin, cos, tan) enkla fall.",
                    _ => ""
                },
                "Fysik" => level switch
                {
                    1 => "Tröghetslagen, v=s/t, energi: potentiell/kinetisk.",
                    2 => "F=ma, Ep=mgh, acceleration.",
                    3 => "p=mv, arbete W=F·s, effekt P=W/t.",
                    4 => "Impuls, krafttid-diagram, friktion.",
                    5 => "Enkla kretsar (V=IR), energiomvandlingar.",
                    _ => ""
                },
                "Biologi" => level switch
                {
                    1 => "Cellens delar, fotosyntes.",
                    2 => "DNA, mitos, cellandning.",
                    3 => "Nervsystem, immunsystem, enzymer.",
                    4 => "Genetik: alleler, dominant/recessiv, Punnett.",
                    5 => "Ekologi: näringskedjor, kretslopp, biomer.",
                    _ => ""
                },
                "Andra världskriget" => level switch
                {
                    1 => "Krigets start 1939, axelmakter/allierade, blitzkrieg.",
                    2 => "Barbarossa, D-dagen, Förintelsen.",
                    3 => "Stillahavet, Hiroshima/Nagasaki, FN.",
                    4 => "Ekonomi & propaganda, frontavsnitt, krigsmateriel.",
                    5 => "Efterspel: Nürnberg, järnridå, avkolonisering.",
                    _ => ""
                },
                "Antikens civilisationer" => level switch
                {
                    1 => "Mesopotamien, hieroglyfer, grekiska stadsstater.",
                    2 => "Hammurabi, romerska republiken/kejsardömet, pyramider.",
                    3 => "Alexander den store, romerska vägar, atensk demokrati.",
                    4 => "Hellenism, biblioteket i Alexandria, imperieförvaltning.",
                    5 => "Rättssystem, handel, kulturarv i modern tid.",
                    _ => ""
                },
                _ => ""
            };
        }

        // ───────────────────────── CLASS MEMBERSHIPS ─────────────────────────────────────
        private async Task SeedClassMembershipsAsync()
        {
            var tina = Guid.Parse("55555555-1111-1111-1111-555555555551");
            var olof = Guid.Parse("55555555-2222-2222-2222-555555555552");
            var maria = Guid.Parse("55555555-3333-3333-3333-555555555553");

            _context.Set<ClassMembership>().AddRange(
                // Teachers
                CM(Class8A, tina, Role.teacher),
                CM(Class9B, olof, Role.teacher),
                CM(Class9C, olof, Role.teacher),
                CM(Class10D, maria, Role.teacher),

                // Students
                CM(Class8A, Guid.Parse("66666666-0001-0001-0001-666666666661")),
                CM(Class8A, Guid.Parse("66666666-0002-0002-0002-666666666662")),
                CM(Class8A, Guid.Parse("66666666-0003-0003-0003-666666666663")),

                CM(Class9B, Guid.Parse("66666666-1001-1001-1001-666666666671")),
                CM(Class9B, Guid.Parse("66666666-1002-1002-1002-666666666672")),
                CM(Class9B, Guid.Parse("66666666-1003-1003-1003-666666666673")),

                CM(Class9C, Guid.Parse("66666666-2001-2001-2001-666666666681")),
                CM(Class9C, Guid.Parse("66666666-2002-2002-2002-666666666682")),
                CM(Class9C, Guid.Parse("66666666-2003-2003-2003-666666666683")),

                CM(Class10D, Guid.Parse("66666666-3001-3001-3001-666666666691")),
                CM(Class10D, Guid.Parse("66666666-3002-3002-3002-666666666692")),
                CM(Class10D, Guid.Parse("66666666-3003-3003-3003-666666666693"))
            );

            static ClassMembership CM(Guid classId, Guid userId, Role roleInClass = Role.student) =>
                new ClassMembership
                {
                    Id = Guid.NewGuid(),
                    ClassId = classId,
                    UserId = userId,
                    RoleInClass = roleInClass,
                    EnrolledAt = DateTime.UtcNow.AddDays(-20)
                };
        }

        // ───────────────────────── QUESTIONS (3 per level) ────────────────────────────────
        private async Task SeedQuestionsAsync()
        {
            void Add(Guid subjectId, Guid topicId, int level, string stem, string explanation,
                string a, bool ac, string b, bool bc, string c, bool cc, string d, bool dc)
            {
                var q = new Question
                {
                    Id = Guid.NewGuid(),
                    SubjectId = subjectId,
                    Type = QuestionType.multiple_choice,
                    Difficulty = level <= 2 ? Difficulty.easy : (level <= 4 ? Difficulty.medium : Difficulty.hard),
                    Stem = stem,
                    Explanation = explanation,
                    SourceAi = false,
                    CreatedById = Guid.Parse("55555555-1111-1111-1111-555555555551"),
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                };
                _questions.Add(q);

                var opts = new (string txt, bool ok)[]
                {
                    (a, ac), (b, bc), (c, cc), (d, dc)
                };
                int sort = 1;
                foreach (var (txt, ok) in opts)
                {
                    _options.Add(new QuestionOption
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = q.Id,
                        OptionText = txt,
                        IsCorrect = ok,
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

            // Algebra (L1–L5)
            Add(SubjectMath, TopicAlgebra, 1, "Koefficienten i 7x − 2 är…", "Koefficienten är talet framför x.",
                "7", true, "−2", false, "x", false, "5", false);
            Add(SubjectMath, TopicAlgebra, 1, "Lös x + 5 = 12", "Subtrahera 5 från båda sidor: x=7.",
                "7", true, "12", false, "5", false, "–7", false);
            Add(SubjectMath, TopicAlgebra, 1, "Vad betyder 3x?", "Tre gånger x.",
                "x + x + x", true, "x − x − x", false, "x/3", false, "3 + x", false);

            Add(SubjectMath, TopicAlgebra, 2, "Förenkla 2(x+4)", "Fördelningslagen: 2x+8.",
                "2x + 8", true, "2x + 4", false, "x + 8", false, "x + 4", false);
            Add(SubjectMath, TopicAlgebra, 2, "Lös 3x − 6 = 9", "Lägg till 6, dela med 3: x=5.",
                "5", true, "1", false, "−5", false, "3", false);
            Add(SubjectMath, TopicAlgebra, 2, "Förenkla 4x − x + 2x", "4x − x + 2x = 5x.",
                "5x", true, "7x", false, "3x", false, "x", false);

            Add(SubjectMath, TopicAlgebra, 3, "Faktorisera x² − 16", "Konjugat: (x−4)(x+4).",
                "(x−4)(x+4)", true, "(x−8)(x+2)", false, "x(x−16)", false, "Ej faktoriserbar", false);
            Add(SubjectMath, TopicAlgebra, 3, "Lös systemet: x+y=6; x−y=2", "Addera: 2x=8 → x=4, y=2.",
                "x=4, y=2", true, "x=2, y=4", false, "x=6, y=2", false, "x=3, y=3", false);
            Add(SubjectMath, TopicAlgebra, 3, "Beräkna 2³·2²", "Adderar exponenter: 2⁵=32.",
                "32", true, "16", false, "8", false, "64", false);

            Add(SubjectMath, TopicAlgebra, 4, "Lös x² − 5x + 6 = 0", "Faktorisera (x−2)(x−3)=0 → x=2,3.",
                "x=2 eller x=3", true, "x=−2 eller −3", false, "x=6", false, "x=0", false);
            Add(SubjectMath, TopicAlgebra, 4, "Lös olikheten 2x + 1 > 7", "2x>6 → x>3.",
                "x > 3", true, "x ≥ 3", false, "x < 3", false, "x ≤ 3", false);
            Add(SubjectMath, TopicAlgebra, 4, "Förenkla (x+2)(x+3)", "x²+5x+6.",
                "x² + 5x + 6", true, "x² + 6x + 5", false, "x² + 2x + 3", false, "x² + 3x + 2", false);

            Add(SubjectMath, TopicAlgebra, 5, "Förenkla (x²−9)/(x−3), x≠3", "Faktorisera täljare: (x−3)(x+3)/(x−3)=x+3.",
                "x + 3", true, "x − 3", false, "x² − 3", false, "3x", false);
            Add(SubjectMath, TopicAlgebra, 5, "Vilken är lutningen i y=4x−1?", "Koef framför x = 4.",
                "4", true, "−1", false, "1/4", false, "0", false);
            Add(SubjectMath, TopicAlgebra, 5, "Förenkla (2x)/(x) för x≠0", "2x/x=2.",
                "2", true, "x", false, "1/2", false, "0", false);

            // Geometry (L1–L5)
            Add(SubjectMath, TopicGeometry, 1, "Vinkelsumman i triangel är…", "180°.",
                "180°", true, "90°", false, "270°", false, "360°", false);
            Add(SubjectMath, TopicGeometry, 1, "Area rektangel b=8, h=5", "A=b·h=40.",
                "40", true, "13", false, "20", false, "80", false);
            Add(SubjectMath, TopicGeometry, 1, "Triangel med 90° vinkel?", "Rätvinklig triangel.",
                "Rätvinklig", true, "Likbent", false, "Liksidig", false, "Trubbvinklig", false);

            Add(SubjectMath, TopicGeometry, 2, "Pythagoras: kateter 6 & 8 → hyp?", "√(36+64)=10.",
                "10", true, "12", false, "14", false, "8", false);
            Add(SubjectMath, TopicGeometry, 2, "Omkrets cirkel r=5 (≈)", "2πr ≈ 31,4.",
                "≈ 31,4", true, "≈ 15,7", false, "≈ 25", false, "≈ 10", false);
            Add(SubjectMath, TopicGeometry, 2, "Area triangel b=12, h=7", "b·h/2=42.",
                "42", true, "84", false, "19", false, "12", false);

            Add(SubjectMath, TopicGeometry, 3, "Likformiga trianglar har…", "Lika vinklar, proportionella sidor.",
                "Lika vinklar & proportionella sidor", true, "Alltid samma area", false, "Alltid kongruenta", false, "Olika vinklar", false);
            Add(SubjectMath, TopicGeometry, 3, "Alternatvinklar uppstår vid…", "Transversal över parallella linjer.",
                "Transversal & parallella linjer", true, "Två skärande linjer", false, "Triangel", false, "Cirkel", false);
            Add(SubjectMath, TopicGeometry, 3, "Volym kub med sida 5", "5³=125.",
                "125", true, "25", false, "75", false, "100", false);

            Add(SubjectMath, TopicGeometry, 4, "Volym cylinder r=3, h=10 (≈)", "πr²h ≈ 282,7.",
                "≈ 282,7", true, "≈ 188,5", false, "≈ 90", false, "≈ 314", false);
            Add(SubjectMath, TopicGeometry, 4, "Skalfaktor 1:2 ger area…", "Area skalar med kvadraten: 1:4.",
                "1:4", true, "1:2", false, "2:1", false, "1:8", false);
            Add(SubjectMath, TopicGeometry, 4, "Area cirkel r=6 (≈)", "πr² ≈ 113,1.",
                "≈ 113,1", true, "≈ 36", false, "≈ 72", false, "≈ 144", false);

            Add(SubjectMath, TopicGeometry, 5, "sin(30°) är…", "1/2.",
                "1/2", true, "√2/2", false, "√3/2", false, "0", false);
            Add(SubjectMath, TopicGeometry, 5, "cos(60°) är…", "1/2.",
                "1/2", true, "√3/2", false, "0", false, "1", false);
            Add(SubjectMath, TopicGeometry, 5, "tan(45°) är…", "1.",
                "1", true, "0", false, "√3/3", false, "2", false);

            // Physics (L1–L5)
            Add(SubjectScience, TopicPhysics, 1, "Newtons första lag kallas…", "Tröghetslagen.",
                "Tröghetslagen", true, "Gravitationslagen", false, "Hookes lag", false, "Ohms lag", false);
            Add(SubjectScience, TopicPhysics, 1, "v=s/t betyder…", "Hastighet = sträcka / tid.",
                "Hastighet = sträcka / tid", true, "Kraft / massa", false, "Energi / tid", false, "Sträcka · tid", false);
            Add(SubjectScience, TopicPhysics, 1, "Energi högt upp är…", "Potentiell energi.",
                "Potentiell", true, "Kinetisk", false, "Termisk", false, "Elektrisk", false);

            Add(SubjectScience, TopicPhysics, 2, "F=ma är…", "Newtons andra lag.",
                "Newtons andra lag", true, "Ohms lag", false, "Arbetslagen", false, "Boyles lag", false);
            Add(SubjectScience, TopicPhysics, 2, "Potentiell energi:", "Ep=mgh.",
                "Ep=mgh", true, "Ep=1/2 mv²", false, "Ep=F·s", false, "Ep=p·V", false);
            Add(SubjectScience, TopicPhysics, 2, "Enhet för acceleration", "m/s².",
                "m/s²", true, "m/s", false, "N", false, "J", false);

            Add(SubjectScience, TopicPhysics, 3, "Rörelsemängd p:", "p=mv.",
                "p=mv", true, "p=m/a", false, "p=ma", false, "p=m+v", false);
            Add(SubjectScience, TopicPhysics, 3, "Arbete W:", "W=F·s.",
                "W=F·s", true, "W=P·t", false, "W=m·g", false, "W=Q·V", false);
            Add(SubjectScience, TopicPhysics, 3, "Effekt P:", "P=W/t.",
                "P=W/t", true, "P=F·s", false, "P=mv", false, "P=mgh", false);

            Add(SubjectScience, TopicPhysics, 4, "Impuls definieras som…", "Krafts verkan under tid: I=F·Δt.",
                "I=F·Δt", true, "I=mgh", false, "I=W/t", false, "I=mv²", false);
            Add(SubjectScience, TopicPhysics, 4, "Friktion…", "Motsätter rörelse mellan ytor.",
                "Motsätter rörelse", true, "Skapar alltid rörelse", false, "Är alltid noll", false, "Beror inte på ytor", false);
            Add(SubjectScience, TopicPhysics, 4, "Krafttid-diagrammets area är…", "Impuls.",
                "Impuls", true, "Arbete", false, "Energi", false, "Effekt", false);

            Add(SubjectScience, TopicPhysics, 5, "Ohms lag:", "V=IR.",
                "V=IR", true, "P=W/t", false, "F=ma", false, "E=mc²", false);
            Add(SubjectScience, TopicPhysics, 5, "Enhet för resistans", "Ohm (Ω).",
                "Ohm (Ω)", true, "Volt (V)", false, "Ampere (A)", false, "Joule (J)", false);
            Add(SubjectScience, TopicPhysics, 5, "Elektrisk effekt i likström:", "P=VI.",
                "P=VI", true, "P=V/I", false, "P=I/R", false, "P=V²I", false);

            // Biology (L1–L5)
            Add(SubjectScience, TopicBiology, 1, "Cellkärnan innehåller…", "DNA.",
                "DNA", true, "ATP", false, "Glukos", false, "Cellvätska", false);
            Add(SubjectScience, TopicBiology, 1, "Fotosyntes producerar…", "Glukos & O₂.",
                "Glukos & syre", true, "CO₂ & H₂O", false, "Protein", false, "Fett", false);
            Add(SubjectScience, TopicBiology, 1, "Cellmembran reglerar…", "Transport in/ut.",
                "Transport in/ut", true, "DNA-syntes", false, "ATP-produktion", false, "Proteinsyntes", false);

            Add(SubjectScience, TopicBiology, 2, "DNA bär…", "Genetisk information.",
                "Genetisk information", true, "Syre", false, "Avfall", false, "Aminosyror", false);
            Add(SubjectScience, TopicBiology, 2, "Mitos ger…", "Två identiska dotterceller.",
                "Två identiska dotterceller", true, "Fyra könsceller", false, "En jättekcell", false, "Ingen delning", false);
            Add(SubjectScience, TopicBiology, 2, "Cellandning frigör…", "Energi ur glukos.",
                "Energi ur glukos", true, "DNA ur protein", false, "Syre ur vatten", false, "Fett ur CO₂", false);

            Add(SubjectScience, TopicBiology, 3, "Nervcell kallas…", "Neuron.",
                "Neuron", true, "Makrofag", false, "Osteoblast", false, "Erytrocyt", false);
            Add(SubjectScience, TopicBiology, 3, "Antikroppar görs av…", "B-celler.",
                "B-celler", true, "Neuron", false, "Muskelfibrer", false, "Hudceller", false);
            Add(SubjectScience, TopicBiology, 3, "Enzymer…", "Sänker aktiveringsenergin.",
                "Sänker aktiveringsenergi", true, "Höjer temperaturen", false, "Är alltid kolhydrater", false, "Skapar DNA", false);

            Add(SubjectScience, TopicBiology, 4, "Dominant allel…", "Överröstar recessiv i fenotyp.",
                "Syns vid heterozygoti", true, "Syns aldrig", false, "Är alltid skadlig", false, "Är könsbunden", false);
            Add(SubjectScience, TopicBiology, 4, "Korsning Aa × aa ger dominanta fenotyper…", "≈ 50%.",
                "50%", true, "0%", false, "25%", false, "75%", false);
            Add(SubjectScience, TopicBiology, 4, "Punnett-ruta används för…", "Förutse nedärvning.",
                "Förutse nedärvning", true, "Mäta blodtryck", false, "Bestämma pH", false, "Mäta puls", false);

            Add(SubjectScience, TopicBiology, 5, "Näringskedja visar…", "Energi/ämnesflöde mellan organismer.",
                "Energi/ämnesflöde", true, "Väderprognos", false, "Seismik", false, "Astrologi", false);
            Add(SubjectScience, TopicBiology, 5, "Kvävecykeln innefattar…", "Fixering, nitrifikation, denitrifikation.",
                "Fixering & (de)nitrifikation", true, "Fotosyntes", false, "Sublimering", false, "Fermentering", false);
            Add(SubjectScience, TopicBiology, 5, "Biomer är…", "Stora ekosystemtyper (t.ex. taiga).",
                "Stora ekosystemtyper", true, "Cellorganeller", false, "Atomslag", false, "Bakteriesläkten", false);

            // WW2 (L1–L5)
            Add(SubjectHistory, TopicWW2, 1, "Kriget startade…", "1939 med invasionen av Polen.",
                "1939", true, "1914", false, "1941", false, "1945", false);
            Add(SubjectHistory, TopicWW2, 1, "Axelmakter:", "Tyskland, Italien, Japan.",
                "Tyskland, Italien, Japan", true, "USA, UK, Sovjet", false, "Sverige, Norge, Danmark", false, "Spanien, Schweiz, Portugal", false);
            Add(SubjectHistory, TopicWW2, 1, "Blitzkrieg:", "Snabb, koncentrerad anfallstaktik.",
                "Snabbt koncentrerat anfall", true, "Ubåtskrig", false, "Atomkrig", false, "Luftbro", false);

            Add(SubjectHistory, TopicWW2, 2, "Operation Barbarossa:", "Tysklands anfall mot Sovjet 1941.",
                "Anfallet mot Sovjet 1941", true, "D-dagen", false, "Pearl Harbor", false, "Nürnberg", false);
            Add(SubjectHistory, TopicWW2, 2, "D-dagen:", "Allierad landstigning i Normandie 1944.",
                "Normandie 1944", true, "Polen 1939", false, "Berlin 1945", false, "Moskva 1941", false);
            Add(SubjectHistory, TopicWW2, 2, "Förintelsen:", "Systematisk förföljelse/mord på judar m.fl.",
                "Systematisk förföljelse/mord", true, "Militärkupp", false, "Kärnvapenprogram", false, "Evakuering", false);

            Add(SubjectHistory, TopicWW2, 3, "Atombomber:", "Hiroshima & Nagasaki 1945.",
                "Hiroshima/Nagasaki", true, "Tokyo/Osaka", false, "Seoul/Pusan", false, "Peking/Nanjing", false);
            Add(SubjectHistory, TopicWW2, 3, "FN grundas…", "1945.",
                "1945", true, "1939", false, "1919", false, "1950", false);
            Add(SubjectHistory, TopicWW2, 3, "Europa kapitulerar…", "Maj 1945.",
                "Maj 1945", true, "Juni 1944", false, "Sep 1943", false, "Aug 1946", false);

            Add(SubjectHistory, TopicWW2, 4, "Propaganda syftade till…", "Påverka opinion, moral, rekrytering.",
                "Påverka opinion/moral", true, "Sänka industrin", false, "Stoppa radio", false, "Enbart censur", false);
            Add(SubjectHistory, TopicWW2, 4, "Krigsekonomi innebar…", "Ransonering & omställning av industrin.",
                "Ransonering & omställning", true, "Skattesänkningar", false, "Importstopp av allt", false, "Endast lyxproduktion", false);
            Add(SubjectHistory, TopicWW2, 4, "Vilket var ett frontavsnitt?", "Östfronten.",
                "Östfronten", true, "Nordfronten 1812", false, "Västindien", false, "Sydpolen", false);

            Add(SubjectHistory, TopicWW2, 5, "Nürnbergrättegångarna…", "Prövade nazistledare efter kriget.",
                "Prövade nazistledare", true, "Grundade NATO", false, "Fredsavtal i Versailles", false, "Skapade FN", false);
            Add(SubjectHistory, TopicWW2, 5, "Järnridån syftar på…", "Politisk/ideologisk gräns i Europa.",
                "Politisk/ideologisk gräns", true, "Fysisk mur i Berlin 1945", false, "Himalaya", false, "Maginotlinjen 1918", false);
            Add(SubjectHistory, TopicWW2, 5, "Avkolonisering tog fart p.g.a…", "Försvagad kolonialmakt & självständighetsrörelser.",
                "Försvagning & rörelser", true, "Mer kolonial expansion", false, "Industrialisering i kolonierna 1700", false, "Religiösa edikt", false);

            // Ancients (L1–L5)
            Add(SubjectHistory, TopicAncients, 1, "Mesopotamien låg mellan…", "Eufrat & Tigris.",
                "Eufrat & Tigris", true, "Nilen & Niger", false, "Donau & Rhen", false, "Ganges & Brahmaputra", false);
            Add(SubjectHistory, TopicAncients, 1, "Egyptiskt skriftspråk:", "Hieroglyfer.",
                "Hieroglyfer", true, "Kilskrift", false, "Latin", false, "Arameiska", false);
            Add(SubjectHistory, TopicAncients, 1, "Grekisk stadsstat:", "Polis.",
                "Polis", true, "Agora", false, "Forum", false, "Akropolis", false);

            Add(SubjectHistory, TopicAncients, 2, "Hammurabis lagar:", "Tidigt skriven lagkodex.",
                "Skriven lagkodex", true, "Skattelag 1800-t", false, "Militärmanual", false, "Religionsbok", false);
            Add(SubjectHistory, TopicAncients, 2, "Romerska republiken ersattes av…", "Kejsardömet.",
                "Kejsardömet", true, "Teokrati", false, "Absolut demokrati", false, "Feodalism", false);
            Add(SubjectHistory, TopicAncients, 2, "Pyramiderna var främst…", "Faraoners gravar.",
                "Faraoners gravar", true, "Fästningar", false, "Tempel", false, "Lagerhus", false);

            Add(SubjectHistory, TopicAncients, 3, "Alexander den store…", "Spred hellenismen.",
                "Spred hellenismen", true, "Byggde kinesiska muren", false, "Uppfann alfabetet", false, "Grundade Rom", false);
            Add(SubjectHistory, TopicAncients, 3, "Romerska vägar gav…", "Snabb truppförflyttning & handel.",
                "Snabb förflyttning & handel", true, "Irrigation", false, "Kraftproduktion", false, "Kanalbyggen", false);
            Add(SubjectHistory, TopicAncients, 3, "Atensk demokrati:", "Direkt medborgardeltagande.",
                "Direkt deltagande", true, "Absolut monarki", false, "Teokrati", false, "Oligarki", false);

            Add(SubjectHistory, TopicAncients, 4, "Hellenismen är…", "Blandning av grekisk & östlig kultur.",
                "Kulturell blandning", true, "Romersk skattelag", false, "Egyptisk religion", false, "Persisk militärdoktrin", false);
            Add(SubjectHistory, TopicAncients, 4, "Alexandria berömt för…", "Biblioteket & forskningen.",
                "Biblioteket", true, "Kolonier i Amerika", false, "Silkesvägar", false, "Porcelainsfartyg", false);
            Add(SubjectHistory, TopicAncients, 4, "Imperieförvaltning krävde…", "Skatt, infrastruktur, administration.",
                "Skatt & administration", true, "Endast armé", false, "Slaveriets upphörande", false, "Guldbaserad valuta", false);

            Add(SubjectHistory, TopicAncients, 5, "Rättssystemens arv:", "Påverkar moderna lagar.",
                "Påverkar moderna lagar", true, "Endast religiöst", false, "Helt försvunnet", false, "Enbart muntligt", false);
            Add(SubjectHistory, TopicAncients, 5, "Handelns betydelse:", "Spred varor, idéer, teknik.",
                "Spred varor/idéer", true, "Isolerade städer", false, "Minskade språk", false, "Stoppade kulturutbyte", false);
            Add(SubjectHistory, TopicAncients, 5, "Kulturarv syns i…", "Språk, arkitektur, styrning.",
                "Språk/arkitektur/styrning", true, "Endast musik", false, "Enbart jordbruk", false, "Bara astronomi", false);

            _context.Set<Question>().AddRange(_questions);
        }

        private async Task SeedQuestionOptionsAsync()
        {
            _context.Set<QuestionOption>().AddRange(_options);
        }

        // ───────────────────────── QUIZZES (per topic, level, class) ──────────────────────
        private async Task SeedQuizzesAsync()
        {
            var classTargets = new[] { Class8A, Class9B, Class9C, Class10D };

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
                        _quizByLevelClass[(topicId, level.LevelNumber, cls)] = qzId;

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
            var quizQuestions = new List<QuizQuestion>();
            var classTargets = new[] { Class8A, Class9B, Class9C, Class10D };

            foreach (var ((topicId, level), qIds) in _questionIdsByLevel)
            {
                foreach (var cls in classTargets)
                {
                    if (!_quizByLevelClass.TryGetValue((topicId, level, cls), out var quizId))
                        continue;

                    int sort = 1;
                    foreach (var qId in qIds)
                    {
                        quizQuestions.Add(new QuizQuestion
                        {
                            Id = Guid.NewGuid(),
                            QuizId = quizId,
                            QuestionId = qId,
                            SortOrder = sort++
                        });
                    }
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
