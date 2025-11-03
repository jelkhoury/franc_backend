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




        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Answer>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Answer>()
                .HasOne(a => a.Question)
                .WithMany()
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Answer>()
                .HasOne(a => a.EvaluationReport)
                .WithMany(e => e.Answers)
                .HasForeignKey(a => a.EvaluationReportId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EvaluateQuestion>()
                .HasOne(e => e.Answer)
                .WithMany()
                .HasForeignKey(e => e.AnswerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EvaluateQuestion>()
                .HasOne(e => e.Evaluator)
                .WithMany()
                .HasForeignKey(e => e.EvaluatorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EvaluationReport>()
                .HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Question>()
                .HasOne(q => q.Major)
                .WithMany(m => m.Questions)
                .HasForeignKey(q => q.MajorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Answer>()
                .HasOne(a => a.MockInterview)
                .WithMany(m => m.Answers)
                .HasForeignKey(a => a.MockInterviewId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<SDSResponse>()
    .HasIndex(r => new { r.UserId, r.QuestionId, r.AttemptNumber })
    .IsUnique();


            modelBuilder.Entity<SDSQuestion>()
                .HasMany(q => q.AnswerOptions)
                .WithOne(o => o.Question)
                .HasForeignKey(o => o.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SDSSection>()
                .HasMany(s => s.Questions)
                .WithOne(q => q.Section)
                .HasForeignKey(q => q.SectionId)
                .OnDelete(DeleteBehavior.Cascade);


        }
    }
}
