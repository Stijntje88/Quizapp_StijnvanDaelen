using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Quizapp_StijnvanDaelen.Models;

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

        private void LeerlingButton_Click(object sender, RoutedEventArgs e)
        {
            StartPanel.Visibility = Visibility.Collapsed;
            QuizPanel.Visibility = Visibility.Visible;

            // Nieuwe student
            _currentStudent = new Student { Name = "Leerling", Email = "leerling@example.com" };
            EnsureCurrentStudentInDatabase();

            // Vragen ophalen uit database
            LoadQuestionsFromDatabase();

            // Eventueel JSON-bestand ook nog toevoegen
            string filePath = "vragen.json";
            LoadQuestionsFromJson(filePath);

            ShuffleQuestions();
            PrepareQuestionQueue();
            _totalQuestions = _questionQueue.Count;
            UpdateProgress();
            DisplayQuestion();
        }

        /// <summary>
        /// Laad alle actieve vragen uit de database
        /// </summary>
        private void LoadQuestionsFromDatabase()
        {
            using var context = new QuizDbContext();
            var vragen = context.Questions
                                .Where(q => q.IsActive)
                                .ToList();

            _questions.AddRange(vragen);
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
            var existingStudent = context.Students.FirstOrDefault(s => s.Email == _currentStudent.Email);
            if (existingStudent != null)
            {
                _currentStudent = existingStudent;
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

        #region JSON Import/Export
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
            ResultatenListBox.Items.Clear();
            using var context = new QuizDbContext();
            var scores = context.Scores.OrderByDescending(s => s.DateTime).ToList();
            foreach (var score in scores)
            {
                ResultatenListBox.Items.Add($"{score.Student.Name}: {score.Points} punten ({score.Percentage:0.##}%) op {score.DateTime}");
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
                XamlRoot = this.Content.XamlRoot // Dit is belangrijk in WinUI 3
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
