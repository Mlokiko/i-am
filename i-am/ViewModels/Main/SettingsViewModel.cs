using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Resources.Constants;
using i_am.Resources.Strings;
using i_am.Services;
using System.Collections.ObjectModel;

namespace i_am.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;

        public ObservableCollection<string> ThemeOptions { get; } = new()
        {
            "Systemowy",
            "Jasny",
            "Ciemny"
        };

        public ObservableCollection<string> LanguageOptions { get; } = new()
        {
            "Polski (PL)",
            "English (EN)"
        };

        [ObservableProperty]
        private string selectedTheme;

        [ObservableProperty]
        private string selectedLanguage;

        [ObservableProperty]
        private bool areNotificationsEnabled;

        [ObservableProperty]
        private bool isCareTaker;

        [ObservableProperty]
        private ObservableCollection<string> hoursList = new();

        [ObservableProperty]
        private string selectedDayStartHour = "04:00";

        [ObservableProperty]
        private bool isActivityTimeRestricted;

        [ObservableProperty]
        private string selectedRestrictionStartHour = "18:00";

        [ObservableProperty]
        private string selectedRestrictionEndHour = "20:00";

        // Wstrzykujemy FirestoreService
        public SettingsViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;

            AreNotificationsEnabled = Preferences.Default.Get("PushEnabled", true);

            string savedTheme = Preferences.Default.Get("AppTheme", "Systemowy");
            selectedTheme = savedTheme;

            // Pobierz zapisany język
            string savedLanguage = Preferences.Default.Get(PreferencesKeys.CurrentLanguage, "pl");
            SelectedLanguage = savedLanguage == "en" ? "English (EN)" : "Polski (PL)";

            IsCareTaker = !Preferences.Default.Get(PreferencesKeys.IsCaregiver, false);

            HoursList = new ObservableCollection<string>(
                Enumerable.Range(0, 24).Select(h => $"{h:D2}:00")
            );
        }
        public async Task InitializeAsync()
        {
            if (IsCareTaker)
            {
                string userId = Preferences.Default.Get(PreferencesKeys.UserId, string.Empty);
                if (string.IsNullOrEmpty(userId)) return;

                var user = await _firestoreService.GetUserProfileAsync(userId);
                if (user != null)
                {
                    SelectedDayStartHour = $"{user.DayStartHour:D2}:00";
                    IsActivityTimeRestricted = user.IsActivityTimeRestricted;
                    SelectedRestrictionStartHour = $"{user.ActivityRestrictionStartHour:D2}:00";
                    SelectedRestrictionEndHour = $"{user.ActivityRestrictionEndHour:D2}:00";
                }
            }
        }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            // Zapis lokalny motywu
            Preferences.Default.Set("AppTheme", SelectedTheme);
            ApplyTheme(SelectedTheme);

            // Zapis języka
            string languageCode = SelectedLanguage.Contains("English") ? "en" : "pl";
            LocalizationManager.SetLanguage(languageCode);
            Preferences.Default.Set(PreferencesKeys.CurrentLanguage, languageCode);

            // Zapis w chmurze (tylko dla podopiecznego)
            if (IsCareTaker)
            {
                string userId = Preferences.Default.Get(PreferencesKeys.UserId, string.Empty);
                if (!string.IsNullOrEmpty(userId))
                {
                    int dayStart = int.Parse(SelectedDayStartHour.Split(':')[0]);
                    int restrictStart = int.Parse(SelectedRestrictionStartHour.Split(':')[0]);
                    int restrictEnd = int.Parse(SelectedRestrictionEndHour.Split(':')[0]);

                    bool success = await _firestoreService.UpdateUserSettingsAsync(
                        userId, dayStart, IsActivityTimeRestricted, restrictStart, restrictEnd);

                    if (success)
                        await Shell.Current.DisplayAlert(LocalizationManager.Success, LocalizationManager.Settings_SavedSuccessfully, LocalizationManager.OK);
                    else
                        await Shell.Current.DisplayAlert(LocalizationManager.Error, LocalizationManager.Settings_SaveFailedCloud, LocalizationManager.OK);
                }
                else
                {
                    await Shell.Current.DisplayAlert(LocalizationManager.Success, LocalizationManager.Settings_SavedSuccessfully, LocalizationManager.OK);
                }
            }
            else
            {
                await Shell.Current.DisplayAlert(LocalizationManager.Success, LocalizationManager.Settings_SavedSuccessfully, LocalizationManager.OK);
            }
        }

        // Dodajemy async, aby móc wywoływać metody z FirestoreService
        async partial void OnAreNotificationsEnabledChanged(bool value)
        {
            Preferences.Default.Set("PushEnabled", value);

            try
            {
                if (value)
                {
                    await _firestoreService.UpdateFcmTokenAsync();
                }
                else
                {
                    await _firestoreService.RemoveFcmTokenAsync();
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert(LocalizationManager.Error, $"{LocalizationManager.Settings_NotificationsError} {ex.Message}", LocalizationManager.OK);

                // Jeśli zapis do bazy się nie powiódł, warto cofnąć switch w UI
                areNotificationsEnabled = !value;
                OnPropertyChanged(nameof(AreNotificationsEnabled));
            }
        }

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
            // Otwiera natywne, systemowe ustawienia dla tej konkretnej aplikacji (Android/iOS)
            AppInfo.Current.ShowSettingsUI();
        }
    }
}