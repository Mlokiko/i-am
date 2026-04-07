using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Models;
using i_am.Services;
using System.Collections.ObjectModel;

namespace i_am.ViewModels
{
    public class GivenAnswerDisplay
    {
        public string QuestionText { get; set; } = string.Empty;
        public string SelectedOptionText { get; set; } = string.Empty;
        public string OpenTextResponse { get; set; } = string.Empty;
        public int PointsAwarded { get; set; }
        public bool IsVisibleToCareGiver { get; set; }
    }

    public partial class CalendarDayItem : ObservableObject
    {
        public DateTime Date { get; set; }
        public bool IsEmpty { get; set; }
        public string DayText => IsEmpty ? "" : Date.Day.ToString();

        public DailyResponse? Response { get; set; }
        public bool HasData => Response != null;

        [ObservableProperty]
        private bool isSelected;

        public string StatusColor
        {
            get
            {
                if (!HasData) return "Transparent";
                if (Response!.TotalScore <= -3) return "Red";
                if (Response!.TotalScore <= -2) return "Orange";
                return "Green";
            }
        }
    }

    public partial class CalendarViewModel : ObservableObject
    {
        private readonly IFirestoreService _firestoreService;
        private string _myUid = string.Empty;
        private List<DailyResponse> _allResponses = new();

        [ObservableProperty] private ObservableCollection<User> careTakers = new();
        [ObservableProperty] private ObservableCollection<CalendarDayItem> days = new();

        // POPRAWKA: Lista przechowuje teraz nowy typ GivenAnswerDisplay
        [ObservableProperty] private ObservableCollection<GivenAnswerDisplay> selectedDayAnswers = new();

        [ObservableProperty] private bool isLoading = true;
        [ObservableProperty] private bool isCareGiver;
        [ObservableProperty] private bool hasCareTakerSelected;

        private DateTime _currentMonthDate;
        [ObservableProperty] private string currentMonthName = string.Empty;

        [ObservableProperty] private User? selectedCareTaker;
        [ObservableProperty] private string selectedCareTakerName = "Kliknij, aby wybrać...";
        [ObservableProperty] private CalendarDayItem? selectedDay;
        [ObservableProperty] private bool isDayDetailsVisible;

        public CalendarViewModel(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
            _currentMonthDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        }

        public async Task InitializeAsync()
        {
            if (CareTakers.Any()) return;

            IsLoading = true;
            _myUid = _firestoreService.GetCurrentUserId() ?? string.Empty;
            IsCareGiver = Preferences.Default.Get("IsCaregiver", false);

            if (IsCareGiver)
            {
                var profile = await _firestoreService.GetUserProfileAsync(_myUid);
                if (profile != null)
                {
                    var careTakersList = await _firestoreService.GetUsersByIdsAsync(profile.CaretakersID);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SelectedCareTaker = null;
                        SelectedCareTakerName = "Kliknij, aby wybrać...";
                        CareTakers = new ObservableCollection<User>(careTakersList);
                    });
                }
                HasCareTakerSelected = false;
            }
            else
            {
                HasCareTakerSelected = true;
                await LoadResponsesAsync(_myUid);
            }

            IsLoading = false;
        }

        [RelayCommand]
        private async Task SelectCareTakerAsync()
        {
            if (!CareTakers.Any()) return;

            var names = CareTakers.Select(c => c.Name).ToArray();
            var action = await Shell.Current.DisplayActionSheet("Wybierz podopiecznego", "Anuluj", null, names);

            if (action != "Anuluj" && !string.IsNullOrEmpty(action))
            {
                var selected = CareTakers.FirstOrDefault(c => c.Name == action);
                if (selected != null)
                {
                    SelectedCareTaker = selected;
                    SelectedCareTakerName = selected.Name;
                    HasCareTakerSelected = true;
                    IsDayDetailsVisible = false;
                    SelectedDay = null;
                    await LoadResponsesAsync(selected.Id);
                }
            }
        }

        private async Task LoadResponsesAsync(string targetUid)
        {
            IsLoading = true;
            _allResponses = await _firestoreService.GetAllDailyResponsesAsync(targetUid);
            GenerateCalendarGrid();
            IsLoading = false;
        }

        private void GenerateCalendarGrid()
        {
            var backgroundDays = new List<CalendarDayItem>();
            string newMonthName = _currentMonthDate.ToString("MMMM yyyy").ToUpper();

            int daysInMonth = DateTime.DaysInMonth(_currentMonthDate.Year, _currentMonthDate.Month);

            for (int i = 1; i <= daysInMonth; i++)
            {
                var date = new DateTime(_currentMonthDate.Year, _currentMonthDate.Month, i);
                var response = _allResponses.FirstOrDefault(r => r.Id == date.ToString("yyyy-MM-dd"));
                backgroundDays.Add(new CalendarDayItem { Date = date, IsEmpty = false, Response = response });
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                CurrentMonthName = newMonthName;
                Days = new ObservableCollection<CalendarDayItem>(backgroundDays);
            });
        }

        [RelayCommand]
        private void ChangeMonth(string direction)
        {
            if (direction == "Prev") _currentMonthDate = _currentMonthDate.AddMonths(-1);
            else if (direction == "Next") _currentMonthDate = _currentMonthDate.AddMonths(1);

            IsDayDetailsVisible = false;
            SelectedDay = null;
            GenerateCalendarGrid();
        }

        [RelayCommand]
        private void SelectDay(CalendarDayItem? day)
        {
            if (day == null || !day.HasData) return;
            if (SelectedDay != null) SelectedDay.IsSelected = false;

            day.IsSelected = true;
            SelectedDay = day;
            IsDayDetailsVisible = true;

            // Ładowanie z Wrapperem (błąd CS0029 zniknie)
            SelectedDayAnswers = new ObservableCollection<GivenAnswerDisplay>(
                (day.Response?.Answers ?? new List<GivenAnswer>())
                .Select(a => new GivenAnswerDisplay
                {
                    QuestionText = a.QuestionText,
                    SelectedOptionText = a.SelectedOptionText,
                    OpenTextResponse = a.OpenTextResponse,
                    PointsAwarded = a.PointsAwarded,
                    IsVisibleToCareGiver = IsCareGiver
                })
            );
        }

        [RelayCommand]
        private void CloseDetails()
        {
            IsDayDetailsVisible = false;
            if (SelectedDay != null) SelectedDay.IsSelected = false;
            SelectedDay = null;
        }
    }
}