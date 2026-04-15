using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Models;
using i_am.Services;

namespace i_am.ViewModels
{
    public partial class StatisticsViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private string _myUid = string.Empty;

        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private bool isDataVisible;

        public ObservableCollection<User> CareTakers { get; } = new();

        [ObservableProperty] private User? selectedCareTaker;
        [ObservableProperty] private string selectedCareTakerName = "Wybierz podopiecznego...";

        // --- Właściwości bindowane do UI ---
        [ObservableProperty] private string monthDaysAnswered = "-";
        [ObservableProperty] private string monthPhotosTaken = "-";

        [ObservableProperty] private string currentWeekStat = "-";
        [ObservableProperty] private string lastWeekStat = "-";
        [ObservableProperty] private string currentMonthStat = "-";
        [ObservableProperty] private string lastMonthStat = "-";
        [ObservableProperty] private string allTimeStat = "-";

        [ObservableProperty] private string dayHoursConfig = "-";
        [ObservableProperty] private string activityWindowConfig = "-";

        public StatisticsViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public async Task InitializeAsync()
        {
            IsLoading = true;
            _myUid = Preferences.Default.Get("UserId", string.Empty);

            if (string.IsNullOrEmpty(_myUid)) return;

            var user = await _firestoreService.GetUserProfileAsync(_myUid);
            if (user != null && user.CaretakersID.Any())
            {
                var fetchedTakers = await _firestoreService.GetUsersByIdsAsync(user.CaretakersID);
                CareTakers.Clear();
                foreach (var t in fetchedTakers) CareTakers.Add(t);
            }
            IsLoading = false;
        }

        [RelayCommand]
        private async Task SelectCareTakerAsync()
        {
            if (!CareTakers.Any())
            {
                await Shell.Current.DisplayAlert("Brak", "Nie masz przypisanych żadnych podopiecznych.", "OK");
                return;
            }

            var names = CareTakers.Select(c => c.Name).ToArray();
            var action = await Shell.Current.DisplayActionSheet("Wybierz podopiecznego", "Anuluj", null, names);

            if (action != "Anuluj" && !string.IsNullOrEmpty(action))
            {
                var selected = CareTakers.FirstOrDefault(c => c.Name == action);
                if (selected != null)
                {
                    SelectedCareTaker = selected;
                    SelectedCareTakerName = selected.Name;
                    await LoadStatisticsAsync(selected);
                }
            }
        }

        private async Task LoadStatisticsAsync(User taker)
        {
            IsLoading = true;
            IsDataVisible = false;

            try
            {
                // Pobieramy całą historię odpowiedzi
                var responses = await _firestoreService.GetAllDailyResponsesAsync(taker.Id);

                DateTime today = DateTime.Today;

                // --- USTAWIENIA CZASU ---
                int startHour = taker.DayStartHour;
                DayHoursConfig = $"Od {startHour:D2}:00 do {startHour:D2}:00 następnego dnia";

                if (taker.IsActivityTimeRestricted)
                    ActivityWindowConfig = $"Od {taker.ActivityRestrictionStartHour:D2}:00 do {taker.ActivityRestrictionEndHour:D2}:00";
                else
                    ActivityWindowConfig = "Cały dzień (Brak ograniczeń)";

                // --- ZMIENNE CZASOWE DO FILTROWANIA ---
                int diffToMonday = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                DateTime currentWeekStart = today.AddDays(-1 * diffToMonday);
                DateTime lastWeekStart = currentWeekStart.AddDays(-7);
                DateTime lastWeekEnd = currentWeekStart.AddDays(-1);

                DateTime currentMonthStart = new DateTime(today.Year, today.Month, 1);
                DateTime lastMonthStart = currentMonthStart.AddMonths(-1);
                DateTime lastMonthEnd = currentMonthStart.AddDays(-1);

                // --- FILTROWANIE ---
                var currentMonthResponses = new List<DailyResponse>();
                var lastMonthResponses = new List<DailyResponse>();
                var currentWeekResponses = new List<DailyResponse>();
                var lastWeekResponses = new List<DailyResponse>();

                foreach (var r in responses)
                {
                    // Dokumenty mają ID w formacie yyyy-MM-dd
                    if (DateTime.TryParseExact(r.Id, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime rDate))
                    {
                        if (rDate >= currentMonthStart && rDate <= today) currentMonthResponses.Add(r);
                        if (rDate >= lastMonthStart && rDate <= lastMonthEnd) lastMonthResponses.Add(r);
                        if (rDate >= currentWeekStart && rDate <= today) currentWeekResponses.Add(r);
                        if (rDate >= lastWeekStart && rDate <= lastWeekEnd) lastWeekResponses.Add(r);
                    }
                }

                // Funkcja pomocnicza generująca napis np. "4.5 pkt (5 / 7 dni)"
                string FormatStat(List<DailyResponse> list, int maxDays)
                {
                    if (!list.Any()) return $"Brak danych (0 / {maxDays} dni)";
                    double avg = list.Sum(x => x.TotalScore) / (double)list.Count;
                    return $"{avg:F1} pkt ({list.Count} / {maxDays} dni)";
                }

                int daysInCurrentMonth = DateTime.DaysInMonth(today.Year, today.Month);

                // 1. Liczba dni w bieżącym miesiącu (Zgodnie z wymaganiem format: np. 12/30)
                MonthDaysAnswered = $"{currentMonthResponses.Count} / {daysInCurrentMonth} dni";

                // 2. Liczba dni ze zdjęciami w tym miesiącu
                int frontPhotos = currentMonthResponses.Count(r => !string.IsNullOrEmpty(r.FrontPhotoUrl));
                int rearPhotos = currentMonthResponses.Count(r => !string.IsNullOrEmpty(r.RearPhotoUrl));
                MonthPhotosTaken = $"Twarz: {frontPhotos}/{daysInCurrentMonth}, Otoczenie: {rearPhotos}/{daysInCurrentMonth}";

                // 3. Średnia: Bieżący tydzień
                CurrentWeekStat = FormatStat(currentWeekResponses, 7);

                // 4. Średnia: Zeszły tydzień
                LastWeekStat = FormatStat(lastWeekResponses, 7);

                // 5. Średnia: Bieżący miesiąc
                CurrentMonthStat = FormatStat(currentMonthResponses, daysInCurrentMonth);

                // 6. Średnia: Zeszły miesiąc
                int daysInLastMonth = DateTime.DaysInMonth(lastMonthStart.Year, lastMonthStart.Month);
                LastMonthStat = FormatStat(lastMonthResponses, daysInLastMonth);

                // 7. Średnia: Cały czas (od daty utworzenia konta)
                int maxAllTimeDays = (today - taker.CreatedAt.Date).Days + 1;
                if (maxAllTimeDays < 1) maxAllTimeDays = 1;
                AllTimeStat = FormatStat(responses, maxAllTimeDays);

                IsDataVisible = true;
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Błąd", $"Błąd podczas ładowania statystyk: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}