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
        private List<Question> _questions = new List<Question>();
        private Queue<Question> _questionQueue = new Queue<Question>();
        private List<Question> _incorrectQuestions = new List<Question>();
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

            if (string.IsNullOrWhiteSpace(leerlingNaam))
            {
                var dialog = new ContentDialog
                {
                    Title = "Naam verplicht",
                    Content = "Voer alstublieft een naam in voordat je de quiz start.",
                    CloseButtonText = "Ok",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }

            StartPanel.Visibility = Visibility.Collapsed;
            QuizPanel.Visibility = Visibility.Visible;

            

            EnsureCurrentStudentInDatabase();

            LoadQuestionsFromDatabase();
            LoadQuestionsFromJson("vragen.json");
            ShuffleQuestions();
            PrepareQuestionQueue();
            _totalQuestions = _questionQueue.Count;
            UpdateProgress();
            DisplayQuestion();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            StartPanel.Visibility = Visibility.Visible;
            DocentPanel.Visibility = Visibility.Collapsed;
            QuizPanel.Visibility = Visibility.Collapsed;
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

            // Controleer of de student al bestaat op naam/email
            var existingStudent = context.Students
                .FirstOrDefault(s => s.Name == _currentStudent.Name && s.Email == _currentStudent.Email);

            if (existingStudent != null)
            {
                _currentStudent = existingStudent; // gebruik bestaande student
            }
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

            var score = new Score
            {
                StudentId = _currentStudent.StudentId,
                Points = _scorePoints,
                Percentage = percentage,
                DateTime = DateTime.Now
            };

            using var context = new QuizDbContext();
            context.Scores.Add(score);
            context.SaveChanges();
        }
        #endregion

        #region JSON Import
        private void LoadQuestionsFromJson(string filePath)
        {
            if (!File.Exists(filePath)) return;

            var jsonString = File.ReadAllText(filePath);
            var vragen = JsonSerializer.Deserialize<List<Question>>(jsonString);
            if (vragen != null)
                _questions.AddRange(vragen);
        }
        #endregion

        #region Quiz Logic
        private void LoadQuestionsFromDatabase()
        {
            using var context = new QuizDbContext();
            var vragen = context.Questions
                                .Where(q => q.IsActive)
                                .ToList();

            _questions.AddRange(vragen);
        }

        private void ShuffleQuestions()
        {
            var random = new Random();
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
            MultipleChoicePanel.Children.Clear();

            if (question is MultipleChoiceQuestion mcq)
            {
                AnswerTextBox.Visibility = Visibility.Collapsed;
                foreach (var option in mcq.Options)
                {
                    var btn = new RadioButton
                    {
                        Content = option,
                        GroupName = "Options",
                        Margin = new Thickness(5)
                    };
                    MultipleChoicePanel.Children.Add(btn);
                }
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (_questionQueue.Count == 0) return;

            var question = _questionQueue.Dequeue();
            string givenAnswer = AnswerTextBox.Visibility == Visibility.Visible ?
                                 AnswerTextBox.Text.Trim() :
                                 MultipleChoicePanel.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true)?.Content.ToString() ?? "";

            bool isCorrect = string.Equals(givenAnswer, question.CorrectAnswer, StringComparison.OrdinalIgnoreCase);

            var answer = new Answer
            {
                QuestionId = question.QuestionId,
                StudentId = _currentStudent.StudentId,
                GivenAnswer = givenAnswer,
                IsCorrect = isCorrect,
                DateTime = DateTime.Now
            };
            SaveAnswer(answer);

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
            {
                IncorrectQuestionsListBox.Items.Add($"{q.Text} - Correct: {q.CorrectAnswer}");
            }

            AnswerTextBox.Visibility = Visibility.Collapsed;
            ConfirmButton.Visibility = Visibility.Collapsed;
            MultipleChoicePanel.Children.Clear();
        }
        #endregion

        #region Docent Events
        private void UploadJsonButton_Click(object sender, RoutedEventArgs e)
        {
            string filePath = "vragen.json";
            LoadQuestionsFromJson(filePath);
            PrepareQuestionQueue();
        }

        private void BekijkResultatenButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResultatenListBox.Items.Clear();

                using var context = new QuizDbContext();
                var scores = context.Scores
                    .Include(s => s.Student)
                    .OrderByDescending(s => s.DateTime)
                    .ToList();

                if (scores.Count == 0)
                {
                    ResultatenListBox.Items.Add("Er zijn nog geen resultaten.");
                }
                else
                {
                    foreach (var score in scores)
                    {
                        var studentName = score.Student?.Name ?? "Onbekende student";
                        ResultatenListBox.Items.Add($"{studentName}: {score.Points} punten ({score.Percentage:0.##}%) op {score.DateTime}");
                    }
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Fout",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                _ = dialog.ShowAsync();
            }
        }

        private async void VoegVraagToeButton_Click(object sender, RoutedEventArgs e)
        {
            string vraagText = VraagTextBox.Text.Trim();
            string correctAnswer = CorrectAnswerTextBox.Text.Trim();
            int weight = int.TryParse(WeightTextBox.Text, out var w) ? w : 1;
            string[] options = OptionsTextBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries);

            Question newQuestion;
            if (options.Length > 0)
            {
                newQuestion = new MultipleChoiceQuestion
                {
                    Text = vraagText,
                    CorrectAnswer = correctAnswer,
                    Weight = weight,
                    Options = options.Select(o => o.Trim()).ToList(),
                    IsActive = true
                };
            }
            else
            {
                newQuestion = new Question
                {
                    Text = vraagText,
                    CorrectAnswer = correctAnswer,
                    Weight = weight,
                    IsActive = true
                };
            }

            using var context = new QuizDbContext();
            context.Questions.Add(newQuestion);
            context.SaveChanges();

            _questions.Add(newQuestion);

            VraagTextBox.Text = "";
            CorrectAnswerTextBox.Text = "";
            WeightTextBox.Text = "";
            OptionsTextBox.Text = "";

            var dialog = new ContentDialog
            {
                Title = "Vraag toegevoegd",
                Content = "De vraag is succesvol toegevoegd aan de database!",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }
        #endregion
    }

    public class MultipleChoiceQuestion : Question
    {
        public List<string> Options { get; set; } = new List<string>();
    }
}
