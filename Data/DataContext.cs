using Microsoft.EntityFrameworkCore;
using FrancProject.Models;

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
        public DbSet<MockInterview> MockInterviews { get; set; }
        public DbSet<SDSSection> SDSSections { get; set; }
        public DbSet<SDSQuestion> SDSQuestions { get; set; }
        public DbSet<SDSAnswerOption> SDSAnswerOptions { get; set; }
        public DbSet<SDSResponse> SDSResponses { get; set; }
        public DbSet<SDSResult> SDSResults { get; set; }





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
        }

    }
}
