using Microsoft.EntityFrameworkCore;
using FrancProject.Models;
using FrancProject.Models.Analytics;

namespace FrancProject.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options) { }


        public DbSet<User> Users { get; set; }
        public DbSet<Faculty> Faculties { get; set; }
        public DbSet<Major> Majors { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<Answer> Answers { get; set; }
        public DbSet<EvaluateQuestion> EvaluateQuestions { get; set; }
        public DbSet<EvaluationReport> EvaluationReports { get; set; }
        public DbSet<EvaluationReportSkill> EvaluationReportSkills { get; set; }
        public DbSet<MockInterview> MockInterviews { get; set; }
        public DbSet<SDSSection> SDSSections { get; set; }
        public DbSet<SDSQuestion> SDSQuestions { get; set; }
        public DbSet<SDSAnswerOption> SDSAnswerOptions { get; set; }
        public DbSet<SDSResponse> SDSResponses { get; set; }
        public DbSet<SDSResult> SDSResults { get; set; }
        public DbSet<FileRecord> Files { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<JobComparison> JobComparisons { get; set; }
        public DbSet<JobComparisonAnswer> JobComparisonAnswers { get; set; }
        public DbSet<JobComparisonCriterion> JobComparisonCriteria { get; set; }
        public DbSet<JobPost> JobPosts { get; set; }
        public DbSet<JobSearchCache> JobSearchCaches { get; set; }
        public DbSet<JobSearchResult> JobSearchResults { get; set; }
        public DbSet<MajorSkillsCache> MajorSkillsCaches { get; set; }
        public DbSet<GameLevel> GameLevels { get; set; }
        public DbSet<GameQuestion> GameQuestions { get; set; }
        public DbSet<GameSession> GameSessions { get; set; }
        public DbSet<GameSessionAnswer> GameSessionAnswers { get; set; }
        public DbSet<UserGameProgress> UserGameProgresses { get; set; }
        public DbSet<ActivityEvent> ActivityEvents { get; set; }
        public DbSet<JobMatchingSearch> JobMatchingSearches { get; set; }









        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --------------------------
            // USER
            // --------------------------
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // --------------------------
            // ANSWERS (User activity → delete with user)
            // --------------------------
            modelBuilder.Entity<Answer>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);   // CHANGED: delete answers when user is deleted

            modelBuilder.Entity<Answer>()
                .HasOne(a => a.Question)
                .WithMany()
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);  // KEEP QUESTIONS (admin content)

            modelBuilder.Entity<Answer>()
                .HasOne(a => a.EvaluationReport)
                .WithMany(e => e.Answers)
                .HasForeignKey(a => a.EvaluationReportId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Answer>()
                .HasOne(a => a.MockInterview)
                .WithMany(m => m.Answers)
                .HasForeignKey(a => a.MockInterviewId)
                .OnDelete(DeleteBehavior.SetNull);   // MockInterview can stay even if answer is deleted

            // --------------------------
            // EVALUATION REPORTS (user-related → delete with user)
            // --------------------------
            modelBuilder.Entity<EvaluationReport>()
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EvaluationReportSkill>()
                .HasOne(s => s.EvaluationReport)
                .WithMany(r => r.SkillScores)
                .HasForeignKey(s => s.EvaluationReportId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EvaluationReportSkill>()
                .HasIndex(s => new { s.EvaluationReportId, s.SkillCode })
                .IsUnique();

            modelBuilder.Entity<EvaluateQuestion>()
                .HasOne(e => e.Answer)
                .WithMany()
                .HasForeignKey(e => e.AnswerId)
                .OnDelete(DeleteBehavior.Cascade);   // Delete evaluations when answers deleted

            modelBuilder.Entity<EvaluateQuestion>()
                .HasOne(e => e.Evaluator)
                .WithMany()
                .HasForeignKey(e => e.EvaluatorId)
                .OnDelete(DeleteBehavior.Restrict);  // Evaluator (admin) should not be deleted

            // --------------------------
            // QUESTIONS (admin content → DO NOT DELETE)
            // --------------------------
            modelBuilder.Entity<Question>()
                .HasOne(q => q.Major)
                .WithMany(m => m.Questions)
                .HasForeignKey(q => q.MajorId)
                .OnDelete(DeleteBehavior.Restrict);  // Majors stay

            // --------------------------
            // SDS SYSTEM (user activity → delete with user)
            // --------------------------
            modelBuilder.Entity<SDSResponse>()
                .HasIndex(r => new { r.UserId, r.QuestionId, r.AttemptNumber })
                .IsUnique();

            modelBuilder.Entity<SDSResult>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);   // Delete SDS results when user deleted

            modelBuilder.Entity<SDSResponse>()
                .HasOne(r => r.SDSResult)
                .WithMany(res => res.Responses)
                .HasForeignKey(r => r.SDSResultId)
                .OnDelete(DeleteBehavior.SetNull);   // SDS responses remain but detached

            modelBuilder.Entity<SDSQuestion>()
                .HasMany(q => q.AnswerOptions)
                .WithOne(o => o.Question)
                .HasForeignKey(o => o.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);   // Admin content

            modelBuilder.Entity<SDSSection>()
                .HasMany(s => s.Questions)
                .WithOne(q => q.Section)
                .HasForeignKey(q => q.SectionId)
                .OnDelete(DeleteBehavior.Cascade);   // Admin content



            // User → JobComparisons (delete user => delete comparisons)
            modelBuilder.Entity<JobComparison>()
                .HasOne(j => j.User)
                .WithMany(u => u.JobComparisons)
                .HasForeignKey(j => j.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // JobComparison → Answers (delete comparison => delete answers)
            modelBuilder.Entity<JobComparisonAnswer>()
                .HasOne(a => a.JobComparison)
                .WithMany(j => j.Answers)
                .HasForeignKey(a => a.JobComparisonId)
                .OnDelete(DeleteBehavior.Cascade);

            // Criterion → Answers (admin deletes criterion? answers should go)
            modelBuilder.Entity<JobComparisonAnswer>()
                .HasOne<JobComparisonCriterion>()
                .WithMany()
                .HasForeignKey(a => a.CriterionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<JobPost>()
    .HasIndex(j => j.LinkedinUrl)
    .IsUnique();

            // Unique SearchKey (provided by Flask)
            modelBuilder.Entity<JobSearchCache>()
                .HasIndex(s => s.SearchKey)
                .IsUnique();

            // Composite Key for mapping table
            modelBuilder.Entity<JobSearchResult>()
                .HasKey(sr => new { sr.SearchId, sr.JobPostId });

            // Relationship: SearchCache → JobSearchResult
            modelBuilder.Entity<JobSearchResult>()
                .HasOne(sr => sr.Search)
                .WithMany(s => s.Results)
                .HasForeignKey(sr => sr.SearchId)
                .OnDelete(DeleteBehavior.Cascade);
            // When search expires → delete mappings

            // Relationship: JobPost → JobSearchResult
            modelBuilder.Entity<JobSearchResult>()
                .HasOne(sr => sr.JobPost)
                .WithMany(j => j.SearchResults)
                .HasForeignKey(sr => sr.JobPostId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MajorSkillsCache>(entity =>
            {
                entity.ToTable("MajorSkillsCaches");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.SearchKey)
                    .IsRequired()
                    .HasMaxLength(450);

                entity.HasIndex(e => e.SearchKey)
                    .IsUnique();

                entity.Property(e => e.Faculty)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Major)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Level)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Country)
                    .HasMaxLength(100);

                entity.Property(e => e.ResultJson)
                    .IsRequired();

                entity.Property(e => e.PromptVersion)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.ModelName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.CreatedAt)
                    .IsRequired();

                entity.Property(e => e.UpdatedAt)
                    .IsRequired();
            });

            // --------------------------
            // GAME QUIZ
            // --------------------------
            modelBuilder.Entity<GameLevel>(entity =>
            {
                entity.ToTable("GameLevels");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.BadgeName).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.LevelNumber).IsUnique();
                entity.HasData(
                    new GameLevel { Id = 1, LevelNumber = 1, Name = "Level 1", BadgeName = "Bronze", PassScore = 7, IsActive = true },
                    new GameLevel { Id = 2, LevelNumber = 2, Name = "Level 2", BadgeName = "Silver", PassScore = 7, IsActive = true },
                    new GameLevel { Id = 3, LevelNumber = 3, Name = "Level 3", BadgeName = "Gold", PassScore = 7, IsActive = true },
                    new GameLevel { Id = 4, LevelNumber = 4, Name = "Level 4", BadgeName = "Platinum", PassScore = 7, IsActive = true },
                    new GameLevel { Id = 5, LevelNumber = 5, Name = "Level 5", BadgeName = "Diamond", PassScore = 7, IsActive = true });
            });

            modelBuilder.Entity<GameQuestion>(entity =>
            {
                entity.ToTable("GameQuestions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.QuestionText).IsRequired();
                entity.Property(e => e.OptionA).IsRequired().HasMaxLength(500);
                entity.Property(e => e.OptionB).IsRequired().HasMaxLength(500);
                entity.Property(e => e.OptionC).IsRequired().HasMaxLength(500);
                entity.Property(e => e.OptionD).IsRequired().HasMaxLength(500);
                entity.Property(e => e.CorrectOption).IsRequired().HasMaxLength(1);
                entity.Property(e => e.Hint).HasMaxLength(1000);
                entity.HasOne(e => e.Level)
                    .WithMany(l => l.Questions)
                    .HasForeignKey(e => e.LevelId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => new { e.LevelId, e.IsActive });
            });

            modelBuilder.Entity<GameSession>(entity =>
            {
                entity.ToTable("GameSessions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Level)
                    .WithMany(l => l.Sessions)
                    .HasForeignKey(e => e.LevelId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("IX_GameSessions_UserId");
                entity.HasIndex(e => e.UserId)
                    .IsUnique()
                    .HasFilter("[Status] = 'InProgress'")
                    .HasDatabaseName("IX_GameSessions_UserId_InProgress_Unique");
            });

            modelBuilder.Entity<GameSessionAnswer>(entity =>
            {
                entity.ToTable("GameSessionAnswers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SelectedOption).HasMaxLength(1);
                entity.HasIndex(e => new { e.SessionId, e.QuestionOrder }).IsUnique();
                entity.HasOne(e => e.Session)
                    .WithMany(s => s.Answers)
                    .HasForeignKey(e => e.SessionId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Question)
                    .WithMany(q => q.SessionAnswers)
                    .HasForeignKey(e => e.QuestionId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<UserGameProgress>(entity =>
            {
                entity.ToTable("UserGameProgresses");
                entity.HasKey(e => e.Id);
                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.UserId).IsUnique();
            });

            modelBuilder.Entity<ActivityEvent>(entity =>
            {
                entity.ToTable("ActivityEvents");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ServiceKey).IsRequired().HasMaxLength(32);
                entity.Property(e => e.ActivityType).IsRequired().HasMaxLength(64);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
                entity.Property(e => e.ResultSummary).HasMaxLength(256);
                entity.Property(e => e.EntityId).IsRequired().HasMaxLength(64);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.OccurredAt);
                entity.HasIndex(e => new { e.UserId, e.OccurredAt });
                entity.HasIndex(e => new { e.ServiceKey, e.OccurredAt });
                entity.HasIndex(e => new { e.ServiceKey, e.EntityId, e.ActivityType }).IsUnique();
            });

            modelBuilder.Entity<JobMatchingSearch>(entity =>
            {
                entity.ToTable("JobMatchingSearches");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SearchType).IsRequired().HasMaxLength(32);
                entity.Property(e => e.Faculty).HasMaxLength(200);
                entity.Property(e => e.Major).HasMaxLength(200);
                entity.Property(e => e.Country).HasMaxLength(100);
                entity.Property(e => e.QueryText).HasMaxLength(500);
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => e.SearchedAt);
                entity.HasIndex(e => e.UserId);
            });

        }

    }
}
