using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Quizapp_StijnvanDaelen.Models;
using Microsoft.EntityFrameworkCore;

namespace Quizapp_StijnvanDaelen
{
    public sealed partial class MainWindow : Window
    {
        private List<Question> _questions = new();
        private Queue<Question> _questionQueue = new();
        private List<Question> _incorrectQuestions = new();
        private int _scorePoints = 0;
        private int _totalQuestions = 0;

        private Student _currentStudent;

        public MainWindow()
        {
            this.InitializeComponent();
            ShowStartScreen();
        }

        #region Startscherm
        private void ShowStartScreen()
        {
            StartPanel.Visibility = Visibility.Visible;
            QuizPanel.Visibility = Visibility.Collapsed;
            DocentPanel.Visibility = Visibility.Collapsed;
        }

        private async void LeerlingButton_Click(object sender, RoutedEventArgs e)
        {
            string leerlingNaam = NaamTextBox.Text.Trim();
            string leerlingEmail = EmailTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(leerlingNaam) || string.IsNullOrWhiteSpace(leerlingEmail))
            {
                await ShowDialogAsync("Naam en e-mail verplicht",
                    "Voer alstublieft zowel je naam als e-mail in voordat je de quiz start.");
                return;
            }

            StartPanel.Visibility = Visibility.Collapsed;
            QuizPanel.Visibility = Visibility.Visible;

            _currentStudent = new Student { Name = leerlingNaam, Email = leerlingEmail };
            EnsureCurrentStudentInDatabase();

            _questions.Clear();
            LoadQuestionsFromDatabase();
            LoadQuestionsFromJson("vragen.json");

            ShuffleQuestions();
            PrepareQuestionQueue();

            _totalQuestions = _questionQueue.Count;
            UpdateProgress();
            DisplayQuestion();
            //ShowFinalScore();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ResetQuiz();
            ShowStartScreen();
        }


        private void DocentButton_Click(object sender, RoutedEventArgs e)
        {
            StartPanel.Visibility = Visibility.Collapsed;
            DocentPanel.Visibility = Visibility.Visible;
        }
        #endregion

        #region Database
        private void EnsureCurrentStudentInDatabase()
        {
            using var context = new QuizDbContext();

            var existingStudent = context.Students
                .FirstOrDefault(s => s.Name == _currentStudent.Name && s.Email == _currentStudent.Email);

            if (existingStudent != null)
                _currentStudent = existingStudent;
            else
            {
                context.Students.Add(_currentStudent);
                context.SaveChanges();
            }
        }

        private void SaveAnswer(Answer answer)
        {
            using var context = new QuizDbContext();
            context.Answers.Add(answer);
            context.SaveChanges();
        }

        private void SaveScore()
        {
            EnsureCurrentStudentInDatabase();

            double totalWeight = _questions.Sum(q => q.Weight);
            double percentage = totalWeight > 0 ? (_scorePoints / totalWeight) * 100 : 0;

            using var context = new QuizDbContext();
            context.Scores.Add(new Score
            {
                StudentId = _currentStudent.StudentId,
                Points = _scorePoints,
                Percentage = percentage,
                DateTime = DateTime.Now
            });
            context.SaveChanges();
        }
        #endregion

        #region JSON Import
        private void LoadQuestionsFromJson(string filePath)
        {
            if (!File.Exists(filePath)) return;

            string jsonString = File.ReadAllText(filePath);
            var vragen = JsonSerializer.Deserialize<List<Question>>(jsonString);
            if (vragen != null)
                _questions.AddRange(vragen);
        }
        #endregion

        #region Quiz Logic
        private void LoadQuestionsFromDatabase()
        {
            using var context = new QuizDbContext();
            _questions.AddRange(context.Questions.Where(q => q.IsActive).ToList());
        }

        private void ShuffleQuestions()
        {
            Random random = new();
            _questions = _questions.OrderBy(q => random.Next()).ToList();
        }

        private void PrepareQuestionQueue()
        {
            _questionQueue.Clear();
            foreach (var q in _questions)
                _questionQueue.Enqueue(q);
        }

        private void DisplayQuestion()
        {
            if (_questionQueue.Count == 0)
            {
                if (_incorrectQuestions.Count > 0)
                {
                    foreach (var q in _incorrectQuestions)
                        _questionQueue.Enqueue(q);
                    _incorrectQuestions.Clear();
                }
                else
                {
                    ShowFinalScore();
                    return;
                }
            }

            var question = _questionQueue.Peek();
            QuestionTextBlock.Text = question.Text;
            FeedbackTextBlock.Text = "";
            FeedbackTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black);

            AnswerTextBox.Visibility = Visibility.Visible;
            AnswerTextBox.Text = "";
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (_questionQueue.Count == 0) return;

            var question = _questionQueue.Dequeue();
            string givenAnswer = AnswerTextBox.Text.Trim();

            bool isCorrect = string.Equals(givenAnswer, question.CorrectAnswer, StringComparison.OrdinalIgnoreCase);

            SaveAnswer(new Answer
            {
                QuestionId = question.QuestionId,
                StudentId = _currentStudent.StudentId,
                GivenAnswer = givenAnswer,
                IsCorrect = isCorrect,
                DateTime = DateTime.Now
            });

            if (isCorrect)
            {
                FeedbackTextBlock.Text = "Correct!";
                FeedbackTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Green);
                _scorePoints += question.Weight;
            }
            else
            {
                FeedbackTextBlock.Text = $"Fout! Correct antwoord: {question.CorrectAnswer}";
                FeedbackTextBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                _incorrectQuestions.Add(question);
            }

            UpdateProgress();
            DisplayQuestion();
        }

        private void UpdateProgress()
        {
            int answered = _totalQuestions - _questionQueue.Count;
            double progressPercent = _totalQuestions > 0 ? (answered * 100.0 / _totalQuestions) : 0;
            ProgressBar.Value = progressPercent;
            ProgressTextBlock.Text = $"Voortgang: {answered} van {_totalQuestions} vragen ({progressPercent:0.##}%)";
        }

        private void ShowFinalScore()
        {
            double totalWeight = _questions.Sum(q => q.Weight);
            double percentage = totalWeight > 0 ? (_scorePoints / totalWeight) * 100 : 0;

            ScoreTextBlock.Text = $"Score: {_scorePoints} punten ({percentage:0.##}%)";
            SaveScore();

            IncorrectQuestionsListBox.Items.Clear();
            foreach (var q in _incorrectQuestions)
                IncorrectQuestionsListBox.Items.Add($"{q.Text} - Correct: {q.CorrectAnswer}");

            AnswerTextBox.Visibility = Visibility.Collapsed;
            ConfirmButton.Visibility = Visibility.Collapsed;
        }
        #endregion
        private void ResetQuiz()
        {
            _questions.Clear();
            _questionQueue.Clear();
            _incorrectQuestions.Clear();

            _scorePoints = 0;
            _totalQuestions = 0;

            // Reset UI
            ProgressBar.Value = 0;
            ProgressTextBlock.Text = "Voortgang: 0 van 0 vragen (0%)";

            ScoreTextBlock.Text = "";
            AnswerTextBox.Text = "";
            AnswerTextBox.Visibility = Visibility.Visible;
            ConfirmButton.Visibility = Visibility.Visible;
            FeedbackTextBlock.Text = "";
            IncorrectQuestionsListBox.Items.Clear();
        }


        #region Docent Events
        private void UploadJsonButton_Click(object sender, RoutedEventArgs e)
        {
            LoadQuestionsFromJson("vragen.json");
            PrepareQuestionQueue();
        }

        private void BekijkResultatenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResultatenListBox.Items.Clear();
                using var context = new QuizDbContext();

                var scores = context.Scores.Include(s => s.Student)
                                           .OrderByDescending(s => s.DateTime)
                                           .ToList();

                if (scores.Count == 0)
                    ResultatenListBox.Items.Add("Er zijn nog geen resultaten.");
                else
                {
                    foreach (var score in scores)
                    {
                        string studentName = score.Student?.Name ?? "Onbekende student";
                        ResultatenListBox.Items.Add($"{studentName}: {score.Points} punten ({score.Percentage:0.##}%) op {score.DateTime:g}");
                    }
                }
            }
            catch (Exception ex)
            {
                _ = ShowDialogAsync("Fout", ex.Message);
            }
        }

        private async void VoegVraagToeButton_Click(object sender, RoutedEventArgs e)
        {
            string vraagText = VraagTextBox.Text.Trim();
            string correctAnswer = CorrectAnswerTextBox.Text.Trim();
            int weight = int.TryParse(WeightTextBox.Text, out var w) ? w : 1;

            if (string.IsNullOrWhiteSpace(vraagText) || string.IsNullOrWhiteSpace(correctAnswer))
            {
                await ShowDialogAsync("Fout", "Vul zowel een vraag als een correct antwoord in.");
                return;
            }

            var newQuestion = new Question
            {
                Text = vraagText,
                CorrectAnswer = correctAnswer,
                Weight = weight,
                IsActive = true
            };

            using var context = new QuizDbContext();
            context.Questions.Add(newQuestion);
            context.SaveChanges();

            _questions.Add(newQuestion);

            VraagTextBox.Text = "";
            CorrectAnswerTextBox.Text = "";
            WeightTextBox.Text = "";

            await ShowDialogAsync("Vraag toegevoegd", "De vraag is succesvol toegevoegd aan de database!");
        }
        #endregion

        #region Helpers
        private async System.Threading.Tasks.Task ShowDialogAsync(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        #endregion
    }
}
