using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Models;
using i_am.Services;
using Microsoft.Maui.ApplicationModel;
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
    public enum DailyActivityStatus
    {
        EmptyOrOutOfRange, // Dni przed założeniem konta, w przyszłości lub puste pola
        NoData,            // Brak wypełnionej ankiety w wymaganym terminie
        Good,              // 0
        NotGood,           // -1
        DefinetlyNotGood,  // -2
        Bad,               // -3
        VeryBad,           // -5
        HorriblyBad        // -7 i mniej
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
        private bool isSelected;

        // Nowa właściwość określająca stan dnia
        [ObservableProperty]
        private DailyActivityStatus activityStatus = DailyActivityStatus.EmptyOrOutOfRange;

        // Właściwość pomocnicza do określania koloru tekstu w XAML
        public bool HasScore => ActivityStatus != DailyActivityStatus.EmptyOrOutOfRange && ActivityStatus != DailyActivityStatus.NoData;
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
                    var target = CareTakers?.FirstOrDefault(c => c.Id == PassedCareTakerId);
                    if (target != null)
                    {
                        SelectedCareTaker = target;
                        SelectedCareTakerName = target.Name;
                        HasCareTakerSelected = true;
                        IsCareTakerSelectionVisible = CareTakers?.Count > 1;

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
                    if (CareTakers?.Count == 1)
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
                        IsCareTakerSelectionVisible = CareTakers?.Count > 1;
                    }
                }
            }
            else
            {
                HasCareTakerSelected = true;
                IsLoading = true;
                var myProfile = await _firestoreService.GetUserProfileAsync(_myUid);
                if (myProfile != null)
                {
                    SelectedCareTaker = myProfile;
                }

                await LoadResponsesAsync(_myUid);
            }

            IsLoading = false;
        }

        private async Task LoadResponsesAsync(string targetUid)
        {
            _allResponses = await _firestoreService.GetAllDailyResponsesAsync(targetUid);
            GenerateCalendarGrid();
        }

        private DailyActivityStatus GetActivityStatus(int score)
        {
            if (score <= -7) return DailyActivityStatus.HorriblyBad;
            if (score <= -5) return DailyActivityStatus.VeryBad;
            if (score <= -3) return DailyActivityStatus.Bad;
            if (score <= -2) return DailyActivityStatus.DefinetlyNotGood;
            if (score <= -1) return DailyActivityStatus.NotGood;
            return DailyActivityStatus.Good; // >= 0
        }

        private void GenerateCalendarGrid()
        {
            var backgroundDays = new List<CalendarDayItem>();
            string newMonthName = _currentMonthDate.ToString("MMMM yyyy", new CultureInfo("pl-PL")).ToUpper();

            int daysInMonth = DateTime.DaysInMonth(_currentMonthDate.Year, _currentMonthDate.Month);
            DateTime firstDay = new DateTime(_currentMonthDate.Year, _currentMonthDate.Month, 1);

            int offset = (int)firstDay.DayOfWeek - (int)DayOfWeek.Monday;
            if (offset < 0) offset += 7;

            // Pobierz datę założenia konta aktualnie wybranego użytkownika (lub domyślnie min value)
            // UWAGA: Jeśli w SelectedCareTaker masz właściwość CreatedAt, użyj jej.
            DateTime createdAtDate = SelectedCareTaker?.CreatedAt.Date ?? DateTime.MinValue.Date;

            for (int i = 0; i < offset; i++)
                backgroundDays.Add(new CalendarDayItem { IsEmpty = true });

            for (int i = 1; i <= daysInMonth; i++)
            {
                var date = new DateTime(_currentMonthDate.Year, _currentMonthDate.Month, i);
                var response = _allResponses.FirstOrDefault(r => r.Id == date.ToString("yyyy-MM-dd"));

                var dayItem = new CalendarDayItem
                {
                    Date = date,
                    IsEmpty = false,
                    Response = response
                };

                // Sprawdzenie: Czy dzień jest po założeniu konta ORAZ nie jest w przyszłości
                if (date >= createdAtDate && date <= DateTime.Today)
                {
                    if (response != null)
                    {
                        dayItem.ActivityStatus = GetActivityStatus(response.TotalScore);
                    }
                    else
                    {
                        dayItem.ActivityStatus = DailyActivityStatus.NoData; // Pominięta ankieta
                    }
                }
                else
                {
                    dayItem.ActivityStatus = DailyActivityStatus.EmptyOrOutOfRange; // Zachowuje domyślny (przezroczysty) kolor
                }

                backgroundDays.Add(dayItem);
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

        [RelayCommand]
        private async Task NavigateToStatisticsAsync()
        {
            if (SelectedCareTaker != null)
            {
                // Przekazujemy CareTakerId do strony statystyk, aby automatycznie załadowała dane
                await Shell.Current.GoToAsync($"{nameof(i_am.Pages.CareGiver.StatisticsPage)}?CareTakerId={SelectedCareTaker.Id}");
            }
        }
    }
}