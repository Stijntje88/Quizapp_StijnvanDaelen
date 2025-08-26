using Microsoft.EntityFrameworkCore;
using Quizapp_StijnvanDaelen.Models;

public class QuizDbContext : DbContext
{
    public DbSet<Question> Questions { get; set; }
    public DbSet<Answer> Answers { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Score> Scores { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Vul hier je eigen databasegegevens in
        var connectionString = "Server=localhost;Database=quizapp;User=root;Password=;";
        optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
    }
}
