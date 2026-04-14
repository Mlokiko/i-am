using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Models;
using i_am.Services;

namespace i_am.ViewModels
{
    public partial class OptionItem : ObservableObject
    {
        public QuestionOption Option { get; set; } = new();
        public AnswerFormItem? Parent { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BgColor))]
        [NotifyPropertyChangedFor(nameof(BorderColor))]
        [NotifyPropertyChangedFor(nameof(TxtColor))]
        [NotifyPropertyChangedFor(nameof(TextFontAttributes))]
        private bool isSelected;

        private bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

        public Color BgColor => IsSelected
            ? (IsDark ? Color.FromArgb("#356AAB") : Color.FromArgb("#E8F0FE"))
            : (IsDark ? Color.FromArgb("#2C2F36") : Color.FromArgb("#FFFFFF"));

        public Color BorderColor => IsSelected
            ? Color.FromArgb("#4A90E2")
            : (IsDark ? Color.FromArgb("#404040") : Color.FromArgb("#C8C8C8"));

        public Color TxtColor => IsSelected
            ? (IsDark ? Colors.White : Color.FromArgb("#4A90E2"))
            : (IsDark ? Colors.White : Colors.Black);

        public FontAttributes TextFontAttributes => IsSelected ? FontAttributes.Bold : FontAttributes.None;

        [RelayCommand]
        private void Toggle()
        {
            Parent?.ToggleOption(this);
        }
    }

    public partial class AnswerFormItem : ObservableObject
    {
        public QuestionTemplate Question { get; set; } = new();
        public ObservableCollection<OptionItem> SelectableOptions { get; } = new();

        [ObservableProperty]
        private string openText = string.Empty;

        public bool IsClosed => Question.Type == "Closed";
        public bool IsOpen => Question.Type == "Open";
        public string SelectionHint => Question.MaxSelections > 1 ? $"(Wybierz do {Question.MaxSelections} opcji)" : "(Wybierz 1 opcję)";

        private bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;
        public Color TitleColor => IsDark ? Colors.White : Colors.Black;
        public Color HintColor => IsDark ? Color.FromArgb("#356AAB") : Color.FromArgb("#4A90E2");
        public Color EditorBgColor => IsDark ? Color.FromArgb("#2C2F36") : Color.FromArgb("#FFFFFF");
        public Color EditorBorderColor => IsDark ? Color.FromArgb("#404040") : Color.FromArgb("#C8C8C8");

        public void ToggleOption(OptionItem? item)
        {
            if (item == null) return;

            if (item.IsSelected)
            {
                item.IsSelected = false;
            }
            else
            {
                var currentlySelected = SelectableOptions.Count(o => o.IsSelected);

                if (Question.MaxSelections == 1)
                {
                    foreach (var opt in SelectableOptions) opt.IsSelected = false;
                    item.IsSelected = true;
                }
                else if (currentlySelected < Question.MaxSelections)
                {
                    item.IsSelected = true;
                }
                else
                {
                    Shell.Current.DisplayAlert("Limit", $"Możesz wybrać maksymalnie {Question.MaxSelections} opcji.", "OK");
                }
            }
        }
    }

    public partial class DailyActivityViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private string _myUid = string.Empty;

        private FileResult? _frontPhoto;
        private FileResult? _rearPhoto;

        [ObservableProperty] private ImageSource? frontPhotoPreview;
        [ObservableProperty] private bool isFrontPhotoCaptured;

        [ObservableProperty] private ImageSource? rearPhotoPreview;
        [ObservableProperty] private bool isRearPhotoCaptured;

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
            _myUid = Preferences.Default.Get("UserId", string.Empty);

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

            finalQuestions.AddRange(allQuestions.Where(q => !q.IsRandomPool));

            var randomSeed = DateTime.Now.DayOfYear + DateTime.Now.Year;
            var random = new Random(randomSeed);

            var randomClosed = allQuestions.Where(q => q.IsRandomPool && q.Type == "Closed")
                                           .OrderBy(x => random.Next()).FirstOrDefault();
            if (randomClosed != null) finalQuestions.Add(randomClosed);

            var randomOpen = allQuestions.Where(q => q.IsRandomPool && q.Type == "Open")
                                         .OrderBy(x => random.Next()).FirstOrDefault();
            if (randomOpen != null) finalQuestions.Add(randomOpen);

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
                            formItem.SelectableOptions.Add(new OptionItem { Option = opt, IsSelected = false, Parent = formItem });
                        }
                    }
                    FormItems.Add(formItem);
                }
            });
        }

        [RelayCommand]
        private async Task SubmitAnswersAsync()
        {
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

                    if (item.IsClosed)
                    {
                        var selected = item.SelectableOptions.Where(o => o.IsSelected).ToList();
                        answer.SelectedOptionText = string.Join(", ", selected.Select(s => s.Option.Text));
                        answer.PointsAwarded = selected.Sum(s => s.Option.Points);
                    }

                    response.Answers.Add(answer);
                    response.TotalScore += answer.PointsAwarded;
                }

                if (_frontPhoto != null)
                {
                    response.FrontPhotoUrl = await _firestoreService.UploadDailyPhotoAsync(_myUid, response.Id, "front", _frontPhoto);
                }
                if (_rearPhoto != null)
                {
                    response.RearPhotoUrl = await _firestoreService.UploadDailyPhotoAsync(_myUid, response.Id, "rear", _rearPhoto);
                }

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

        [RelayCommand]
        private async Task TakeFrontPhotoAsync()
        {
            _frontPhoto = await CapturePhotoHelperAsync();
            if (_frontPhoto != null)
            {
                var stream = await _frontPhoto.OpenReadAsync();
                FrontPhotoPreview = ImageSource.FromStream(() => stream);
                IsFrontPhotoCaptured = true;
            }
        }

        [RelayCommand]
        private async Task TakeRearPhotoAsync()
        {
            _rearPhoto = await CapturePhotoHelperAsync();
            if (_rearPhoto != null)
            {
                var stream = await _rearPhoto.OpenReadAsync();
                RearPhotoPreview = ImageSource.FromStream(() => stream);
                IsRearPhotoCaptured = true;
            }
        }

        // Funkcja pomocnicza, żeby nie powielać kodu błędu aparatu
        private async Task<FileResult?> CapturePhotoHelperAsync()
        {
            try
            {
                if (MediaPicker.Default.IsCaptureSupported)
                    return await MediaPicker.Default.CapturePhotoAsync();

                await Shell.Current.DisplayAlert("Błąd", "Twój telefon nie obsługuje tej funkcji.", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Błąd", $"Nie udało się otworzyć aparatu: {ex.Message}", "OK");
            }
            return null;
        }
    }
}