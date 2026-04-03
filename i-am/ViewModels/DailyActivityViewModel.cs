using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Models;
using i_am.Services;

namespace i_am.ViewModels
{
    // Model pojedynczego kafelka odpowiedzi
    public partial class OptionItem : ObservableObject
    {
        public QuestionOption Option { get; set; } = new();

        [ObservableProperty]
        private bool isSelected;
    }

    // Model pojedynczego pytania w formularzu
    public partial class AnswerFormItem : ObservableObject
    {
        public QuestionTemplate Question { get; set; } = new();
        public ObservableCollection<OptionItem> SelectableOptions { get; } = new();

        [ObservableProperty]
        private string openText = string.Empty;

        public bool IsClosed => Question.Type == "Closed";
        public bool IsOpen => Question.Type == "Open";

        // Tekst podpowiadający limit
        public string SelectionHint => Question.MaxSelections > 1 ? $"(Wybierz do {Question.MaxSelections} opcji)" : "(Wybierz 1 opcję)";

        [RelayCommand]
        private void ToggleOption(OptionItem? item)
        {
            if (item == null) return;

            if (item.IsSelected)
            {
                // Odznaczanie
                item.IsSelected = false;
            }
            else
            {
                // Zaznaczanie
                var currentlySelected = SelectableOptions.Count(o => o.IsSelected);

                if (Question.MaxSelections == 1)
                {
                    // Zachowanie RadioButton: odznacz wszystkie inne, zaznacz ten
                    foreach (var opt in SelectableOptions) opt.IsSelected = false;
                    item.IsSelected = true;
                }
                else if (currentlySelected < Question.MaxSelections)
                {
                    // Jest jeszcze miejsce w limicie
                    item.IsSelected = true;
                }
                else
                {
                    // Limit osiągnięty
                    Shell.Current.DisplayAlert("Limit", $"Możesz wybrać maksymalnie {Question.MaxSelections} opcji.", "OK");
                }
            }
        }
    }

    public partial class DailyActivityViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private string _myUid = string.Empty;

        [ObservableProperty] private bool isLoading = true;
        [ObservableProperty] private bool hasAlreadySubmitted;

        public ObservableCollection<AnswerFormItem> FormItems { get; } = new();

        public DailyActivityViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public async Task InitializeAsync()
        {
            IsLoading = true;
            _myUid = _firestoreService.GetCurrentUserId() ?? string.Empty;

            if (string.IsNullOrEmpty(_myUid)) return;

            string reportingDate = _firestoreService.GetReportingDateString();

            HasAlreadySubmitted = await _firestoreService.HasSubmittedDailyResponseAsync(_myUid, reportingDate);

            if (!HasAlreadySubmitted)
            {
                await LoadAndFilterQuestionsAsync();
            }

            IsLoading = false;
        }

        private async Task LoadAndFilterQuestionsAsync()
        {
            var allQuestions = await _firestoreService.GetQuestionTemplatesAsync(_myUid);
            var finalQuestions = new List<QuestionTemplate>();

            // 1. Zawsze bierzemy pytania codzienne (IsRandomPool == false)
            finalQuestions.AddRange(allQuestions.Where(q => !q.IsRandomPool));

            // 2. Losujemy po 1 pytaniu z puli (Używamy seeda daty, by losowanie było stałe w danej dobie!)
            var randomSeed = DateTime.Now.DayOfYear + DateTime.Now.Year;
            var random = new Random(randomSeed);

            var randomClosed = allQuestions.Where(q => q.IsRandomPool && q.Type == "Closed")
                                           .OrderBy(x => random.Next()).FirstOrDefault();
            if (randomClosed != null) finalQuestions.Add(randomClosed);

            var randomOpen = allQuestions.Where(q => q.IsRandomPool && q.Type == "Open")
                                         .OrderBy(x => random.Next()).FirstOrDefault();
            if (randomOpen != null) finalQuestions.Add(randomOpen);

            // 3. Budujemy modele do widoku (sortując po OrderIndex, żeby zachować logikę)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FormItems.Clear();
                foreach (var q in finalQuestions.OrderBy(x => x.OrderIndex))
                {
                    var formItem = new AnswerFormItem { Question = q };
                    if (q.Type == "Closed" && q.Options != null)
                    {
                        foreach (var opt in q.Options)
                        {
                            formItem.SelectableOptions.Add(new OptionItem { Option = opt, IsSelected = false });
                        }
                    }
                    FormItems.Add(formItem);
                }
            });
        }

        [RelayCommand]
        private async Task SubmitAnswersAsync()
        {
            // Walidacja: czy odpowiedziano na WSZYSTKIE zamknięte pytania?
            if (FormItems.Any(item => item.IsClosed && !item.SelectableOptions.Any(o => o.IsSelected)))
            {
                await Shell.Current.DisplayAlert("Uwaga", "Proszę wybrać odpowiedź we wszystkich zamkniętych pytaniach.", "OK");
                return;
            }

            try
            {
                IsLoading = true;

                var response = new DailyResponse
                {
                    Id = _firestoreService.GetReportingDateString(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    TotalScore = 0
                };

                foreach (var item in FormItems)
                {
                    var answer = new GivenAnswer
                    {
                        QuestionId = item.Question.Id,
                        QuestionText = item.Question.Text,
                        OpenTextResponse = item.OpenText ?? string.Empty
                    };

                    // Jeśli zamknięte, zlicz punkty i sformatuj odpowiedzi po przecinku
                    if (item.IsClosed)
                    {
                        var selected = item.SelectableOptions.Where(o => o.IsSelected).ToList();
                        answer.SelectedOptionText = string.Join(", ", selected.Select(s => s.Option.Text));
                        answer.PointsAwarded = selected.Sum(s => s.Option.Points);
                    }

                    response.Answers.Add(answer);
                    response.TotalScore += answer.PointsAwarded;
                }

                // Logika diagnozowania na podstawie Twoich wytycznych
                if (response.TotalScore <= -3) response.EvaluationStatus = "Sugeruje zaburzenie (Krytyczne)";
                else if (response.TotalScore <= -2) response.EvaluationStatus = "Sugeruje stan zaniżony (Ostrzeżenie)";
                else if (response.TotalScore <= -1) response.EvaluationStatus = "Niewspierające doznania";
                else response.EvaluationStatus = "W normie";

                await _firestoreService.SaveDailyResponseAsync(_myUid, response);

                HasAlreadySubmitted = true;
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Błąd", $"Wystąpił problem: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}