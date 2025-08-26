using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quizapp_StijnvanDaelen.Models
{
    public class Question
    {
        public int QuestionId { get; set; } // Primaire sleutel
        public string Text { get; set; } = null!; // De vraagtekst
        public string CorrectAnswer { get; set; } = null!; // Correct antwoord
        public int Weight { get; set; } // Gewicht van de vraag voor scoreberekening
        public bool IsActive { get; set; } = true; // Actief of niet
    }

}
