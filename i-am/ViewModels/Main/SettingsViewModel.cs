using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Pages.Authentication;
using i_am.Pages.Main;
using i_am.Services;
using Plugin.LocalNotification;
using System.Collections.ObjectModel;

namespace i_am.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;

        // --- LISTY OPCJI ---
        public ObservableCollection<string> ThemeOptions { get; } = new()
        {
            "Systemowy",
            "Jasny",
            "Ciemny"
        };

        public ObservableCollection<string> SurveyFilterOptions { get; } = new()
        {
            "Wszystkie (niezależnie od wyniku)",
            "W normie (0 pkt)",
            "Niewspierające doznania i gorsze (<= -1 pkt)",
            "Stan zaniżony i gorsze (<= -2 pkt)",
            "Tylko Sugeruje zaburzenie (Krytyczne) (<= -3 pkt)"
        };

        public ObservableCollection<string> SystemFilterOptions { get; } = new()
        {
            "Wszystkie",
            "Tylko krytyczne (np. usunięcia kont)"
        };

        [ObservableProperty]
        private ObservableCollection<string> hoursList = new();

        [ObservableProperty]
        private ObservableCollection<string> minutesList = new();


        // --- WSPÓLNE WŁAŚCIWOŚCI ---
        [ObservableProperty]
        private string selectedTheme;

        [ObservableProperty]
        private bool areNotificationsEnabled;

        [ObservableProperty]
        private bool isCareTaker;

        public bool IsCareGiver => !IsCareTaker;


        // --- USTAWIENIA PODOPIECZNEGO (Wpisy i ograniczenia) ---
        [ObservableProperty]
        private string selectedDayStartHour = "04:00";

        [ObservableProperty]
        private bool isActivityTimeRestricted;

        [ObservableProperty]
        private string selectedRestrictionStartHour = "18:00";

        [ObservableProperty]
        private string selectedRestrictionEndHour = "20:00";


        // --- USTAWIENIA PODOPIECZNEGO (Lokalne Przypomnienia) ---
        [ObservableProperty]
        private bool isDailyReminderEnabled = true;

        [ObservableProperty]
        private string selectedReminderHour = "20:00";

        [ObservableProperty]
        private string selectedReminderMinute = "00";


        // --- USTAWIENIA OPIEKUNA (Filtry i brak aktywności) ---
        [ObservableProperty]
        private bool inactivityAlertsEnabled = true;

        [ObservableProperty]
        private int inactivityThresholdHours = 24;

        [ObservableProperty]
        private string selectedSurveyFilter = "Wszystkie (niezależnie od wyniku)";

        [ObservableProperty]
        private string selectedSystemFilter = "Wszystkie";


        public SettingsViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;

            AreNotificationsEnabled = Preferences.Default.Get("PushEnabled", true);
            selectedTheme = Preferences.Default.Get("AppTheme", "Systemowy");

            IsCareTaker = !Preferences.Default.Get("IsCaregiver", false);

            HoursList = new ObservableCollection<string>(
                Enumerable.Range(0, 24).Select(h => $"{h:D2}:00")
            );

            MinutesList = new ObservableCollection<string>(
                Enumerable.Range(0, 60).Select(m => $"{m:D2}")
            );
        }

        public async Task InitializeAsync()
        {
            string userId = Preferences.Default.Get("UserId", string.Empty);
            if (string.IsNullOrEmpty(userId)) return;

            var user = await _firestoreService.GetUserProfileAsync(userId);
            if (user != null)
            {
                // To ustawienie jest WSPÓLNE dla opiekuna i podopiecznego
                SelectedSystemFilter = MapDbToSystemFilter(user.SystemNotificationFilter);

                if (IsCareTaker)
                {
                    SelectedDayStartHour = $"{user.DayStartHour:D2}:00";
                    IsActivityTimeRestricted = user.IsActivityTimeRestricted;
                    SelectedRestrictionStartHour = $"{user.ActivityRestrictionStartHour:D2}:00";
                    SelectedRestrictionEndHour = $"{user.ActivityRestrictionEndHour:D2}:00";

                    IsDailyReminderEnabled = user.IsDailyReminderEnabled;
                    SelectedReminderHour = $"{user.DailyReminderHour:D2}:00";
                    SelectedReminderMinute = $"{user.DailyReminderMinute:D2}";
                }
                else
                {
                    InactivityAlertsEnabled = user.InactivityAlertsEnabled;
                    InactivityThresholdHours = user.InactivityThresholdHours <= 0 ? 24 : user.InactivityThresholdHours;
                    SelectedSurveyFilter = MapDbToSurveyFilter(user.SurveyNotificationFilter);
                }
            }
        }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            // 1. Zapis lokalny motywu
            Preferences.Default.Set("AppTheme", SelectedTheme);
            ApplyTheme(SelectedTheme);

            string userId = Preferences.Default.Get("UserId", string.Empty);
            if (string.IsNullOrEmpty(userId)) return;

            try
            {
                var firestore = Plugin.Firebase.Firestore.CrossFirebaseFirestore.Current;

                if (IsCareTaker)
                {
                    int dayStart = int.Parse(SelectedDayStartHour.Split(':')[0]);
                    int restrictStart = int.Parse(SelectedRestrictionStartHour.Split(':')[0]);
                    int restrictEnd = int.Parse(SelectedRestrictionEndHour.Split(':')[0]);

                    int reminderHour = int.Parse(SelectedReminderHour.Split(':')[0]);
                    int reminderMinute = int.Parse(SelectedReminderMinute);

                    // Zapis w Firebase (zarówno istniejące opcje, jak i nowe pola przypomnień)
                    await firestore.GetCollection("users").GetDocument(userId).UpdateDataAsync(new Dictionary<object, object>
                    {
                        { "dayStartHour", dayStart },
                        { "isActivityTimeRestricted", IsActivityTimeRestricted },
                        { "activityRestrictionStartHour", restrictStart },
                        { "activityRestrictionEndHour", restrictEnd },
                        { "isDailyReminderEnabled", IsDailyReminderEnabled },
                        { "dailyReminderHour", reminderHour },
                        { "dailyReminderMinute", reminderMinute },
                        { "systemNotificationFilter", MapSystemFilterToDb(SelectedSystemFilter) }
                    });

                    // Ustawianie LOKALNEGO powiadomienia na urządzeniu
                    await SetupLocalDailyReminder(IsDailyReminderEnabled, reminderHour, reminderMinute);

                    await Shell.Current.DisplayAlert("Sukces", "Ustawienia zostały zapisane.", "OK");
                }
                else
                {
                    // Zapis w Firebase dla Opiekuna
                    await firestore.GetCollection("users").GetDocument(userId).UpdateDataAsync(new Dictionary<object, object>
                    {
                        { "inactivityAlertsEnabled", InactivityAlertsEnabled },
                        { "inactivityThresholdHours", InactivityThresholdHours },
                        { "surveyNotificationFilter", MapSurveyFilterToDb(SelectedSurveyFilter) },
                        { "systemNotificationFilter", MapSystemFilterToDb(SelectedSystemFilter) }
                    });

                    await Shell.Current.DisplayAlert("Sukces", "Ustawienia opiekuna zostały zapisane.", "OK");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Błąd", $"Wystąpił problem podczas zapisu w chmurze: {ex.Message}", "OK");
            }
        }

        // --- OBSŁUGA LOKALNEGO PRZYPOMNIENIA ---
        private async Task SetupLocalDailyReminder(bool isEnabled, int hour, int minute)
        {
            int notificationId = 1001; // Unikalne ID naszego powiadomienia ankiety

            // Najpierw zawsze anulujemy poprzednie, by nie tworzyć duplikatów
            LocalNotificationCenter.Current.Cancel(notificationId);

            if (!isEnabled)
            {
                return; // Jeśli wyłączone, kończymy (powiadomienie już jest anulowane)
            }

            // Obliczamy czas powiadomienia na dzisiaj
            var notifyTime = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, hour, minute, 0);

            // Jeśli dzisiejsza godzina przypomnienia już minęła, planujemy pierwsze na jutro
            if (notifyTime <= DateTime.Now)
            {
                notifyTime = notifyTime.AddDays(1);
            }

            var request = new NotificationRequest
            {
                NotificationId = notificationId,
                Title = "Czas na Twój wpis!",
                Description = "Hej! Poświęć chwilę na uzupełnienie dzisiejszej ankiety.",
                Schedule = new NotificationRequestSchedule
                {
                    NotifyTime = notifyTime,
                    RepeatType = NotificationRepeat.Daily // Zapewnia codzienne powtarzanie o tej samej porze
                }
            };

            await LocalNotificationCenter.Current.Show(request);
        }

        // --- ZARZĄDZANIE PUSH FCM ---
        async partial void OnAreNotificationsEnabledChanged(bool value)
        {
            Preferences.Default.Set("PushEnabled", value);

            try
            {
                if (value) await _firestoreService.UpdateFcmTokenAsync();
                else await _firestoreService.RemoveFcmTokenAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Błąd", $"Nie udało się zaktualizować tokena powiadomień: {ex.Message}", "OK");
                areNotificationsEnabled = !value;
                OnPropertyChanged(nameof(AreNotificationsEnabled));
            }
        }

        // --- MOTYW ---
        private void ApplyTheme(string themeText)
        {
            if (Application.Current == null) return;
            Application.Current.UserAppTheme = themeText switch
            {
                "Jasny" => AppTheme.Light,
                "Ciemny" => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };
        }

        [RelayCommand]
        private void OpenSystemSettings()
        {
            AppInfo.Current.ShowSettingsUI();
        }

        // --- MAPOWANIA WIDOKU NA DANE BAZOWE (OPIEKUN) ---
        private string MapSurveyFilterToDb(string displayValue) => displayValue switch
        {
            "Tylko Sugeruje zaburzenie (Krytyczne) (<= -3 pkt)" => "CriticalOnly",
            "Stan zaniżony i gorsze (<= -2 pkt)" => "WarningAndWorse",
            "Niewspierające doznania i gorsze (<= -1 pkt)" => "NegativeAndWorse",
            "W normie (0 pkt)" => "NormalOnly",
            _ => "All"
        };

        private string MapDbToSurveyFilter(string dbValue) => dbValue switch
        {
            "CriticalOnly" => "Tylko Sugeruje zaburzenie (Krytyczne) (<= -3 pkt)",
            "WarningAndWorse" => "Stan zaniżony i gorsze (<= -2 pkt)",
            "NegativeAndWorse" => "Niewspierające doznania i gorsze (<= -1 pkt)",
            "NormalOnly" => "W normie (0 pkt)",
            _ => "Wszystkie (niezależnie od wyniku)"
        };

        private string MapSystemFilterToDb(string displayValue) => displayValue switch
        {
            "Tylko krytyczne (np. usunięcia kont)" => "CriticalOnly",
            _ => "All"
        };

        private string MapDbToSystemFilter(string dbValue) => dbValue switch
        {
            "CriticalOnly" => "Tylko krytyczne (np. usunięcia kont)",
            _ => "Wszystkie"
        };

        [RelayCommand]
        private async Task GoToManageAccountAsync() => await Shell.Current.GoToAsync(nameof(ManageAccountPage));

        // --- Wylogowanie ---
        [RelayCommand]
        private async Task LogoutAsync()
        {
            try
            {
                bool confirm = await Shell.Current.DisplayAlert("Wyloguj", "Jesteś pewien, że chcesz się wylogować?", "Tak", "Nie");

                if (confirm)
                {
                    await _firestoreService.RemoveFcmTokenAsync();

                    // Usuwa lokalny cache
                    Preferences.Default.Remove("IsCaregiver");
                    Preferences.Default.Remove("UserId");

                    // Firebase czyści sesje
                    await _firestoreService.SignOutAsync();

                    // Podwójny ukośnik, aby zresetować stos nawigacji
                    await Shell.Current.GoToAsync($"//{nameof(LandingPage)}");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Błąd", $"Problem z wylogowaniem: {ex.Message}", "OK");
            }
        }
    }
}