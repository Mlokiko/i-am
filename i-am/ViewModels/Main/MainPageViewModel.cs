using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Pages.CareTaker;
using i_am.Pages.Main;
using i_am.Services;

namespace i_am.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;

        [ObservableProperty]
        private bool isCaregiver;

        [ObservableProperty]
        private bool isCareTaker;

        public MainPageViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
            IsCaregiver = Preferences.Default.Get("IsCaregiver", false);
            IsCareTaker = !IsCaregiver;
        }

        // --- Wspólne komendy ---
        [RelayCommand]
        private async Task GoToNotificationsAsync() => await Shell.Current.GoToAsync(nameof(NotificationsPage));

        [RelayCommand]
        private async Task GoToCalendarAsync() => await Shell.Current.GoToAsync(nameof(CalendarPage));

        [RelayCommand]
        private async Task GoToSettingsAsync() => await Shell.Current.GoToAsync(nameof(SettingsPage));

        [RelayCommand]
        private async Task GoToInformationAsync() => await Shell.Current.GoToAsync(nameof(InformationPage));


        // --- Opiekun ---
        [RelayCommand]
        private async Task GoToManageCareTakersAsync() => await Shell.Current.GoToAsync(nameof(ManageConnectionsPage));


        // --- Podopieczny ---
        [RelayCommand]
        private async Task GoToDailyActivityAsync() => await Shell.Current.GoToAsync(nameof(DailyActivityPage));

        [RelayCommand]
        private async Task GoToManageCareGiversAsync() => await Shell.Current.GoToAsync(nameof(ManageConnectionsPage));

        [RelayCommand]
        private async Task SendEmergencyAlertAsync()
        {
            bool confirm = await Shell.Current.DisplayAlert(
                "Potwierdzenie",
                "Czy na pewno chcesz wysłać pilny alert do swoich opiekunów?",
                "Tak, wyślij",
                "Anuluj");

            if (!confirm) return;

            try
            {
                string userId = Preferences.Default.Get("UserId", string.Empty);
                if (string.IsNullOrEmpty(userId))
                {
                    await Shell.Current.DisplayAlert("Błąd", "Nie można zidentyfikować użytkownika.", "OK");
                    return;
                }

                await _firestoreService.SendEmergencyAlertAsync(userId);

                await Shell.Current.DisplayAlert(
                    "Alert wysłany",
                    "Twoi opiekunowie zostali powiadomieni o potrzebie pilnego kontaktu.",
                    "OK");
            }
            catch (InvalidOperationException ex)
            {
                await Shell.Current.DisplayAlert("Błąd", ex.Message, "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Błąd", $"Nie udało się wysłać alertu: {ex.Message}", "OK");
            }
        }
    }
}