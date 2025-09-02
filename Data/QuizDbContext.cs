using Microsoft.EntityFrameworkCore;
using Quizapp_StijnvanDaelen.Models;

namespace Quizapp_StijnvanDaelen
{
    public class QuizDbContext : DbContext
    {
        public DbSet<Question> Questions { get; set; }
        public DbSet<Answer> Answers { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Score> Scores { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(
                "server=localhost;" +
                "port=3306;" +
                "user=root;" +
                "password=;" +
                "Database=QuizeApp",
                ServerVersion.Parse("8.0.30")
            );
        }
    }
}

