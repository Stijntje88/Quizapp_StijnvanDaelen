using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Quizapp_StijnvanDaelen.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Quizapp_StijnvanDaelen
{
    public sealed partial class MainWindow : Window
    {
        private QuizDbContext _context = new QuizDbContext();
        private Student _currentStudent;
        private List<Question> _questions = new List<Question>();
        private Queue<Question> _questionQueue = new Queue<Question>();
        private int _scorePoints = 0;
        private int _totalQuestions = 0;

        public MainWindow()
        {
            this.InitializeComponent();

            _currentStudent = new Student { Name = "Stijn", Email = "stijn@example.com" };

            LoadQuestionsFromFile("vragen.json"); // JSON import
            ShuffleQuestions();
            PrepareQuestionQueue();

            _totalQuestions = _questionQueue.Count;
            UpdateProgress();

            if (_questionQueue.Count > 0)
                DisplayQuestion();
            else
                QuestionTextBlock.Text = "Geen vragen beschikbaar!";
        }

        private void LoadQuestionsFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return;

            var json = File.ReadAllText(filePath);
            var loadedQuestions = JsonSerializer.Deserialize<List<Question>>(json);
            if (loadedQuestions != null) _questions.AddRange(loadedQuestions);

            foreach (var q in _questions)
            {
                if (!_context.Questions.Any(dbQ => dbQ.Text == q.Text))
                    _context.Questions.Add(q);
            }
            _context.SaveChanges();
        }

        private void ShuffleQuestions()
        {
            var random = new Random();
            _questions = _questions.OrderBy(q => random.Next()).ToList();
        }

        private void PrepareQuestionQueue()
        {
            foreach (var q in _questions.Where(q => q.IsActive))
                _questionQueue.Enqueue(q);
        }

        private void DisplayQuestion()
        {
            if (_questionQueue.Count == 0) { ShowFinalScore(); return; }

            var question = _questionQueue.Peek();
            QuestionTextBlock.Text = question.Text;
            FeedbackTextBlock.Text = "";
            FeedbackTextBlock.Foreground = new SolidColorBrush(Colors.Black);

            AnswerTextBox.Text = "";
            AnswerTextBox.Visibility = Visibility.Visible;
            ConfirmButton.Visibility = Visibility.Visible;

            UpdateProgress();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (_questionQueue.Count == 0) return;

            var question = _questionQueue.Dequeue();
            string givenAnswer = AnswerTextBox.Text.Trim();
            bool isCorrect = string.Equals(givenAnswer, question.CorrectAnswer, StringComparison.OrdinalIgnoreCase);

            // Feedback met kleur
            if (isCorrect)
            {
                FeedbackTextBlock.Text = "Correct!";
                FeedbackTextBlock.Foreground = new SolidColorBrush(Colors.Green);
                _scorePoints += question.Weight;
            }
            else
            {
                FeedbackTextBlock.Text = $"Fout! Correct antwoord: {question.CorrectAnswer}";
                FeedbackTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                _questionQueue.Enqueue(question); // opnieuw later tonen
            }

            // Opslaan antwoord
            var answer = new Answer
            {
                Question = question,
                Student = _currentStudent,
                GivenAnswer = givenAnswer,
                IsCorrect = isCorrect
            };
            _context.Answers.Add(answer);
            _context.SaveChanges();

            DisplayQuestion();
        }

        private void ShowFinalScore()
        {
            double totalWeight = _questions.Sum(q => q.Weight);
            double percentage = totalWeight > 0 ? (_scorePoints / totalWeight) * 100 : 0;

            var score = new Score
            {
                Student = _currentStudent,
                Points = _scorePoints,
                Percentage = percentage
            };
            _context.Scores.Add(score);
            _context.SaveChanges();

            QuestionTextBlock.Text = "Quiz voltooid!";
            AnswerTextBox.Visibility = Visibility.Collapsed;
            ConfirmButton.Visibility = Visibility.Collapsed;
            FeedbackTextBlock.Text = "";

            ScoreTextBlock.Text = $"Score: {_scorePoints} punten ({percentage:0.##}%)";
            ProgressBar.Value = 100;
            ProgressTextBlock.Text = "Voltooid!";
        }

        private void UpdateProgress()
        {
            int answered = _totalQuestions - _questionQueue.Count;
            double progressPercent = _totalQuestions > 0 ? (answered * 100.0 / _totalQuestions) : 0;
            ProgressBar.Value = progressPercent;
            ProgressTextBlock.Text = $"Voortgang: {answered} van {_totalQuestions} vragen ({progressPercent:0.##}%)";
        }
    }
}
