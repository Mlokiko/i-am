using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Models;
using i_am.Services;
using System.Collections.ObjectModel;
using System.Globalization;

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

        public bool HasFrontPhoto => Response != null && !string.IsNullOrEmpty(Response.FrontPhotoUrl);
        public string FrontPhotoUrl => Response?.FrontPhotoUrl ?? string.Empty;
        public bool HasRearPhoto => Response != null && !string.IsNullOrEmpty(Response.RearPhotoUrl);
        public string RearPhotoUrl => Response?.RearPhotoUrl ?? string.Empty;
        public bool HasAnyPhoto => HasFrontPhoto || HasRearPhoto;

        public DailyResponse? Response { get; set; }
        public bool HasData => Response != null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BorderThick))]
        [NotifyPropertyChangedFor(nameof(HighlightBorderColor))]
        private bool isSelected;

        private bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

        // UŻYWAMY POLA isSelected ZAMIAST WŁAŚCIWOŚCI IsSelected, ABY NIE BLOKOWAĆ GENERATORA
        public Color BgColor
        {
            get
            {
                if (IsEmpty || !HasData) return Colors.Transparent;
                if (Response!.TotalScore <= -3) return IsDark ? Color.FromArgb("#CF6679") : Color.FromArgb("#E57373");
                if (Response!.TotalScore <= -2) return IsDark ? Color.FromArgb("#FFB74D") : Color.FromArgb("#FF9800");
                return IsDark ? Color.FromArgb("#81C784") : Color.FromArgb("#4CAF50");
            }
        }

        public Color TxtColor
        {
            get
            {
                if (IsEmpty) return Colors.Transparent;
                if (HasData) return Colors.White;
                return IsDark ? Colors.White : Colors.Black;
            }
        }

        public Color BorderColor
        {
            get
            {
                if (IsEmpty || HasData) return Colors.Transparent;
                return IsDark ? Color.FromArgb("#404040") : Color.FromArgb("#E0E0E0");
            }
        }

        public double BorderThick => isSelected ? 2 : 1;
        public Color HighlightBorderColor => isSelected ? (IsDark ? Colors.White : Colors.Black) : BorderColor;
    }

    [QueryProperty(nameof(PassedCareTakerId), "CareTakerId")]
    [QueryProperty(nameof(PassedDate), "Date")]
    public partial class CalendarViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private string _myUid = string.Empty;
        private List<DailyResponse> _allResponses = new();

        [ObservableProperty] private ObservableCollection<User> careTakers = new();
        [ObservableProperty] private ObservableCollection<CalendarDayItem> days = new();
        [ObservableProperty] private ObservableCollection<GivenAnswerDisplay> selectedDayAnswers = new();

        [ObservableProperty] private bool isLoading = true;
        [ObservableProperty] private bool isCareGiver;
        [ObservableProperty] private bool hasCareTakerSelected;
        [ObservableProperty] private bool isCareTakerSelectionVisible;

        private DateTime _currentMonthDate;
        [ObservableProperty] private string currentMonthName = string.Empty;

        [ObservableProperty] private User? selectedCareTaker;
        [ObservableProperty] private string selectedCareTakerName = "Kliknij, aby wybrać...";
        [ObservableProperty] private CalendarDayItem? selectedDay;
        [ObservableProperty] private bool isDayDetailsVisible;

        [ObservableProperty] private bool isPhotoEnlarged;
        [ObservableProperty] private string enlargedPhotoUrl = string.Empty;

        [ObservableProperty] private string passedCareTakerId = string.Empty;
        [ObservableProperty] private string passedDate = string.Empty;

        public CalendarViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
            _currentMonthDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        }

        public async Task InitializeAsync()
        {
            _myUid = Preferences.Default.Get("UserId", string.Empty);
            IsCareGiver = Preferences.Default.Get("IsCaregiver", false);

            if (IsCareGiver)
            {
                if (CareTakers == null || !CareTakers.Any())
                {
                    IsLoading = true;
                    var profile = await _firestoreService.GetUserProfileAsync(_myUid);
                    if (profile != null)
                    {
                        var list = await _firestoreService.GetUsersByIdsAsync(profile.CaretakersID);
                        CareTakers = new ObservableCollection<User>(list);
                    }
                }

                if (!string.IsNullOrEmpty(PassedCareTakerId))
                {
                    var target = CareTakers.FirstOrDefault(c => c.Id == PassedCareTakerId);
                    if (target != null)
                    {
                        SelectedCareTaker = target;
                        SelectedCareTakerName = target.Name;
                        HasCareTakerSelected = true;
                        IsCareTakerSelectionVisible = CareTakers.Count > 1;

                        if (DateTime.TryParseExact(PassedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime targetDate))
                        {
                            _currentMonthDate = new DateTime(targetDate.Year, targetDate.Month, 1);
                        }

                        await LoadResponsesAsync(target.Id);

                        var dayToSelect = Days.FirstOrDefault(d => !d.IsEmpty && d.Date.Date == targetDate.Date);
                        if (dayToSelect != null)
                        {
                            SelectDay(dayToSelect);
                        }
                    }
                    PassedCareTakerId = string.Empty;
                    PassedDate = string.Empty;
                }
                else if (!HasCareTakerSelected)
                {
                    if (CareTakers.Count == 1)
                    {
                        var single = CareTakers.First();
                        SelectedCareTaker = single;
                        SelectedCareTakerName = single.Name;
                        HasCareTakerSelected = true;
                        IsCareTakerSelectionVisible = false;
                        await LoadResponsesAsync(single.Id);
                    }
                    else
                    {
                        IsCareTakerSelectionVisible = CareTakers.Count > 1;
                    }
                }
            }
            else
            {
                HasCareTakerSelected = true;
                await LoadResponsesAsync(_myUid);
            }

            IsLoading = false;
        }

        private async Task LoadResponsesAsync(string targetUid)
        {
            _allResponses = await _firestoreService.GetAllDailyResponsesAsync(targetUid);
            GenerateCalendarGrid();
        }

        private void GenerateCalendarGrid()
        {
            var backgroundDays = new List<CalendarDayItem>();
            string newMonthName = _currentMonthDate.ToString("MMMM yyyy", new CultureInfo("pl-PL")).ToUpper();

            int daysInMonth = DateTime.DaysInMonth(_currentMonthDate.Year, _currentMonthDate.Month);
            DateTime firstDay = new DateTime(_currentMonthDate.Year, _currentMonthDate.Month, 1);

            int offset = (int)firstDay.DayOfWeek - (int)DayOfWeek.Monday;
            if (offset < 0) offset += 7;

            for (int i = 0; i < offset; i++)
                backgroundDays.Add(new CalendarDayItem { IsEmpty = true });

            for (int i = 1; i <= daysInMonth; i++)
            {
                var date = new DateTime(_currentMonthDate.Year, _currentMonthDate.Month, i);
                var response = _allResponses.FirstOrDefault(r => r.Id == date.ToString("yyyy-MM-dd"));
                backgroundDays.Add(new CalendarDayItem { Date = date, IsEmpty = false, Response = response });
            }

            CurrentMonthName = newMonthName;
            Days = new ObservableCollection<CalendarDayItem>(backgroundDays);
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
            if (SelectedDay != null) SelectedDay.IsSelected = false;
            day.IsSelected = true;
            SelectedDay = day;
            IsDayDetailsVisible = true;
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

        [RelayCommand]
        private void EnlargePhoto(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                EnlargedPhotoUrl = url;
                IsPhotoEnlarged = true;
            }
        }

        [RelayCommand]
        private void CloseEnlargedPhoto()
        {
            IsPhotoEnlarged = false;
            EnlargedPhotoUrl = string.Empty;
        }
    }
}