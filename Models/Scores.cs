using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quizapp_StijnvanDaelen.Models
{
    public class Score
    {
        public int ScoreId { get; set; } // Primaire sleutel
        public int StudentId { get; set; } // Buitenlandse sleutel naar Student
        public Student Student { get; set; } = null!;

        public int Points { get; set; } // Punten totaal
        public double Percentage { get; set; } // Percentage correct
        public DateTime DateTime { get; set; } = DateTime.Now;
    }

}
