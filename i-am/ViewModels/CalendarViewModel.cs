using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Models;
using i_am.Services;

namespace i_am.ViewModels
{
    // Model reprezentujący pojedynczą kratkę w kalendarzu
    public partial class CalendarDayItem : ObservableObject
    {
        public DateTime Date { get; set; }
        public bool IsEmpty { get; set; } // Puste dni na wyrównanie siatki miesiąca
        public string DayText => IsEmpty ? "" : Date.Day.ToString();

        public DailyResponse? Response { get; set; }
        public bool HasData => Response != null;

        [ObservableProperty]
        private bool isSelected;

        // Zwraca kolor kropki/tła w zależności od wyniku
        public string StatusColor
        {
            get
            {
                if (!HasData) return "Transparent";
                if (Response!.TotalScore <= -3) return "Red"; // Krytyczne
                if (Response!.TotalScore <= -2) return "Orange"; // Ostrzeżenie
                return "Green"; // W normie
            }
        }
    }

    public partial class CalendarViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private string _myUid = string.Empty;
        private List<DailyResponse> _allResponses = new();

        [ObservableProperty] private bool isLoading = true;
        [ObservableProperty] private bool isCareGiver;
        [ObservableProperty] private bool hasCareTakerSelected;

        // Zmienne do nawigacji po miesiącach
        private DateTime _currentMonthDate;
        [ObservableProperty] private string currentMonthName = string.Empty;

        // Listy
        public ObservableCollection<User> CareTakers { get; } = new();
        [ObservableProperty] private User? selectedCareTaker;
        public ObservableCollection<CalendarDayItem> Days { get; } = new();

        // Wybrany dzień i jego szczegóły
        [ObservableProperty] private CalendarDayItem? selectedDay;
        [ObservableProperty] private bool isDayDetailsVisible;
        public ObservableCollection<GivenAnswer> SelectedDayAnswers { get; } = new();

        public CalendarViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
            _currentMonthDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        }

        public async Task InitializeAsync()
        {
            IsLoading = true;
            _myUid = _firestoreService.GetCurrentUserId() ?? string.Empty;
            IsCareGiver = Preferences.Default.Get("IsCaregiver", false);

            if (IsCareGiver)
            {
                // Opiekun musi najpierw wybrać podopiecznego
                var profile = await _firestoreService.GetUserProfileAsync(_myUid);
                if (profile != null)
                {
                    var careTakers = await _firestoreService.GetUsersByIdsAsync(profile.CaretakersID);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        CareTakers.Clear();
                        foreach (var ct in careTakers) CareTakers.Add(ct);
                    });
                }
                HasCareTakerSelected = false;
            }
            else
            {
                // Podopieczny od razu ładuje swoje dane
                HasCareTakerSelected = true;
                await LoadResponsesAsync(_myUid);
            }

            IsLoading = false;
        }

        partial void OnSelectedCareTakerChanged(User? value)
        {
            if (value != null)
            {
                HasCareTakerSelected = true;
                IsDayDetailsVisible = false;
                SelectedDay = null;
                _ = LoadResponsesAsync(value.Id);
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
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Days.Clear();
                CurrentMonthName = _currentMonthDate.ToString("MMMM yyyy").ToUpper();

                int daysInMonth = DateTime.DaysInMonth(_currentMonthDate.Year, _currentMonthDate.Month);
                DateTime firstDayOfMonth = new DateTime(_currentMonthDate.Year, _currentMonthDate.Month, 1);

                // Obliczanie pustych kratek na początku (poniedziałek = 1, niedziela = 7)
                int startDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
                if (startDayOfWeek == 0) startDayOfWeek = 7; // Niedziela jako ostatnia

                for (int i = 1; i < startDayOfWeek; i++)
                {
                    Days.Add(new CalendarDayItem { IsEmpty = true });
                }

                // Generowanie właściwych dni
                for (int i = 1; i <= daysInMonth; i++)
                {
                    var date = new DateTime(_currentMonthDate.Year, _currentMonthDate.Month, i);
                    var response = _allResponses.FirstOrDefault(r => r.Id == date.ToString("yyyy-MM-dd"));

                    Days.Add(new CalendarDayItem
                    {
                        Date = date,
                        IsEmpty = false,
                        Response = response
                    });
                }
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
            if (day == null || day.IsEmpty || !day.HasData) return;

            // Odznacz poprzedni
            if (SelectedDay != null) SelectedDay.IsSelected = false;

            day.IsSelected = true;
            SelectedDay = day;
            IsDayDetailsVisible = true;

            SelectedDayAnswers.Clear();
            if (day.Response != null && day.Response.Answers != null)
            {
                foreach (var ans in day.Response.Answers)
                {
                    SelectedDayAnswers.Add(ans);
                }
            }
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