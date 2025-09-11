using System;
using FrageFejden.Entities;
using FrageFejden_api.Entities.Tables;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserDailyQuestion> UserDailyQuestions => Set<UserDailyQuestion>();

    public DbSet<Class> Classes { get; set; }
    public DbSet<ClassMembership> ClassMemberships { get; set; }

    public DbSet<Subject> Subjects { get; set; }
    public DbSet<Topic> Topics { get; set; }
    public DbSet<Level> Levels { get; set; }

    public DbSet<Quiz> Quizzes { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<QuestionOption> QuestionOptions { get; set; }
    public DbSet<QuizQuestion> QuizQuestions { get; set; }

    public DbSet<AiTemplate> AiTemplates { get; set; }
    public DbSet<AiGeneration> AiGenerations { get; set; }

    public DbSet<Attempt> Attempts { get; set; }
    public DbSet<Response> Responses { get; set; }

    public DbSet<Duel> Duels { get; set; }
    public DbSet<DuelParticipant> DuelParticipants { get; set; }
    public DbSet<DuelRound> DuelRounds { get; set; }

    public DbSet<UnlockRule> UnlockRules { get; set; }
    public DbSet<UserProgress> UserProgresses { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ===== AppUser =====
        builder.Entity<AppUser>(entity =>
        {
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // ===== Class / ClassMembership =====
        builder.Entity<Class>(entity =>
        {
            entity.HasOne(c => c.CreatedBy)
                .WithMany(u => u.ClassesCreated)
                .HasForeignKey(c => c.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ClassMembership>(entity =>
        {
            entity.HasOne(cm => cm.User)
                .WithMany(u => u.ClassMemberships)
                .HasForeignKey(cm => cm.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cm => cm.Class)
                .WithMany(c => c.Memberships)
                .HasForeignKey(cm => cm.ClassId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== Subject =====
        builder.Entity<Subject>(entity =>
        {
            entity.HasOne(s => s.CreatedBy)
                .WithMany(u => u.SubjectsCreated)
                .HasForeignKey(s => s.CreatedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(s => s.Name);
        });

        // ===== Topic (sub-course in a Subject) =====
        builder.Entity<Topic>(entity =>
        {
            entity.HasOne(t => t.Subject)
                .WithMany(s => s.Topics)
                .HasForeignKey(t => t.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(t => new { t.SubjectId, t.SortOrder });
            entity.HasIndex(t => new { t.SubjectId, t.Name });
        });

        // ===== Level (ONLY under Topic; NO SubjectId) =====
        builder.Entity<Level>(entity =>
        {
            entity.HasOne(l => l.Topic)
                .WithMany(t => t.Levels)
                .HasForeignKey(l => l.TopicId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(l => new { l.TopicId, l.LevelNumber });
        });


        // ===== Quiz (Subject-general, optional Topic/Level) =====
        builder.Entity<Quiz>(entity =>
        {
            entity.HasOne(q => q.Subject)
                .WithMany(s => s.Quizzes)
                .HasForeignKey(q => q.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔧 change: NO ACTION to avoid multiple cascade paths via Topic
            entity.HasOne(q => q.Topic)
                .WithMany(t => t.Quizzes)
                .HasForeignKey(q => q.TopicId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(q => q.Level)
                .WithMany(l => l.Quizzes)
                .HasForeignKey(q => q.LevelId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(q => q.Class)
                .WithMany(c => c.Quizzes)
                .HasForeignKey(q => q.ClassId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(q => q.CreatedBy)
                .WithMany()
                .HasForeignKey(q => q.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(q => new { q.SubjectId, q.TopicId, q.LevelId, q.IsPublished });
        });

        // ===== Question =====
        builder.Entity<Question>(entity =>
        {
            entity.HasOne(q => q.Subject)
                .WithMany(s => s.Questions)
                .HasForeignKey(q => q.SubjectId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(q => q.Topic)
                .WithMany(t => t.Questions)
                .HasForeignKey(q => q.TopicId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(q => q.CreatedBy)
                .WithMany()
                .HasForeignKey(q => q.CreatedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(q => q.ApprovedBy)
                .WithMany()
                .HasForeignKey(q => q.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(q => new { q.SubjectId, q.TopicId, q.Difficulty });
        });

        // ===== QuestionOption =====
        builder.Entity<QuestionOption>(entity =>
        {
            entity.HasOne(qo => qo.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(qo => qo.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== QuizQuestion =====
        builder.Entity<QuizQuestion>(entity =>
        {
            entity.HasOne(qq => qq.Quiz)
                .WithMany(q => q.QuizQuestions)
                .HasForeignKey(qq => qq.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(qq => qq.Question)
                .WithMany(q => q.QuizQuestions)
                .HasForeignKey(qq => qq.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(qq => new { qq.QuizId, qq.QuestionId });
        });

        // ===== AI =====
        builder.Entity<AiTemplate>(entity =>
        {
            entity.HasOne(at => at.CreatedBy)
                .WithMany()
                .HasForeignKey(at => at.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(at => at.Subject)
                .WithMany(s => s.AiTemplates)
                .HasForeignKey(at => at.SubjectId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(at => at.Topic)
                .WithMany(t => t.AiTemplates)
                .HasForeignKey(at => at.TopicId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AiGeneration>(entity =>
        {
            entity.HasOne(ag => ag.Template)
                .WithMany(t => t.Generations)
                .HasForeignKey(ag => ag.TemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(ag => ag.Question)
                .WithMany()
                .HasForeignKey(ag => ag.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ===== Attempts / Responses =====
        builder.Entity<Attempt>(entity =>
        {
            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Quiz)
                .WithMany(q => q.Attempts)
                .HasForeignKey(a => a.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.LevelAtTime)
                .WithMany()
                .HasForeignKey(a => a.LevelIdAtTime)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Response>(entity =>
        {
            entity.HasOne(r => r.Attempt)
                .WithMany(a => a.Responses)
                .HasForeignKey(r => r.AttemptId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.Question)
                .WithMany()
                .HasForeignKey(r => r.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.SelectedOption)
                .WithMany()
                .HasForeignKey(r => r.SelectedOptionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ===== Duels =====
        builder.Entity<Duel>(entity =>
        {
            entity.HasOne(d => d.Subject)
                .WithMany(s => s.Duels)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Level)
                .WithMany()
                .HasForeignKey(d => d.LevelId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<DuelParticipant>(entity =>
        {
            entity.HasOne(dp => dp.User)
                .WithMany()
                .HasForeignKey(dp => dp.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(dp => dp.InvitedBy)
                .WithMany()
                .HasForeignKey(dp => dp.InvitedById)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(dp => dp.Duel)
                .WithMany(d => d.Participants)
                .HasForeignKey(dp => dp.DuelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DuelRound>(entity =>
        {
            entity.HasOne(dr => dr.Duel)
                .WithMany(d => d.Rounds)
                .HasForeignKey(dr => dr.DuelId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(dr => dr.Question)
                .WithMany()
                .HasForeignKey(dr => dr.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ===== UserProgress (Subject required; optional Topic/Level) =====
        builder.Entity<UserProgress>(entity =>
        {
            entity.HasOne(up => up.User)
                .WithMany()
                .HasForeignKey(up => up.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(up => up.Subject)
                .WithMany(s => s.UserProgresses)
                .HasForeignKey(up => up.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            // 🔧 change: NO ACTION to avoid double path Topic->(direct) and Topic->Level->UserProgress
            entity.HasOne(up => up.Topic)
                .WithMany()
                .HasForeignKey(up => up.TopicId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(up => up.Level)
                .WithMany()
                .HasForeignKey(up => up.LevelId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(up => new { up.UserId, up.SubjectId, up.TopicId, up.LevelId });
        });

        // ===== Unlock rules (between levels) =====
        builder.Entity<UnlockRule>(entity =>
        {
            entity.HasOne(ur => ur.Subject)
                .WithMany(s => s.UnlockRules)
                .HasForeignKey(ur => ur.SubjectId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ur => ur.FromLevel)
                .WithMany()
                .HasForeignKey(ur => ur.FromLevelId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(ur => ur.ToLevel)
                .WithMany()
                .HasForeignKey(ur => ur.ToLevelId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
