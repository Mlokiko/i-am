using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        [ObservableProperty]
        private string selectedTheme;

        [ObservableProperty]
        private bool areNotificationsEnabled;

        // Wstrzykujemy FirestoreService
        public SettingsViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;

            AreNotificationsEnabled = Preferences.Default.Get("PushEnabled", true);

            string savedTheme = Preferences.Default.Get("AppTheme", "Systemowy");
            selectedTheme = savedTheme;
        }

        partial void OnSelectedThemeChanged(string value)
        {
            Preferences.Default.Set("AppTheme", value);
            ApplyTheme(value);
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
                await Shell.Current.DisplayAlert("Błąd", $"Nie udało się zaktualizować ustawień powiadomień: {ex.Message}", "OK");

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