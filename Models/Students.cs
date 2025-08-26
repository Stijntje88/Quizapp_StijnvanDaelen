using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace Quizapp_StijnvanDaelen.Models
{
    public class Student
    {
        public int StudentId { get; set; } // Primaire sleutel
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;

        public ICollection<Answer> Answers { get; set; } = new List<Answer>();
        public ICollection<Score> Scores { get; set; } = new List<Score>();
    }

}
