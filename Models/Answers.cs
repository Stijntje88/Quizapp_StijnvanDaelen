using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quizapp_StijnvanDaelen.Models
{
    public class Answer
    {
        public int AnswerId { get; set; } // Primaire sleutel
        public int QuestionId { get; set; } // Buitenlandse sleutel naar Question
        public Question Question { get; set; } = null!;

        public int StudentId { get; set; } // Buitenlandse sleutel naar Student
        public Student Student { get; set; } = null!;

        public string GivenAnswer { get; set; } = null!; // Antwoord van de student
        public bool IsCorrect { get; set; } // Juist/fout
        public DateTime DateTime { get; set; } = DateTime.Now; // Tijd van antwoord
    }

}
