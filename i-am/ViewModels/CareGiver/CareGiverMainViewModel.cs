using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Pages.Authentication;
using i_am.Pages.Main;
using i_am.Pages.CareGiver;
using i_am.Services;

namespace i_am.ViewModels
{
    public partial class CareGiverMainViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;

        public CareGiverMainViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        // --- KOMENDY NAWIGACYJNE ---
        [RelayCommand]
        private async Task GoToNotificationsAsync() => await Shell.Current.GoToAsync(nameof(NotificationsPage));

        [RelayCommand]
        private async Task GoToCalendarAsync() => await Shell.Current.GoToAsync(nameof(CalendarPage));

        [RelayCommand]
        private async Task GoToEditCareTakerQuestionsAsync() => await Shell.Current.GoToAsync(nameof(EditCareTakerQuestionsPage));

        [RelayCommand]
        private async Task GoToManageCareTakersAsync() => await Shell.Current.GoToAsync(nameof(ManageConnectionsPage));

        [RelayCommand]
        private async Task GoToSettingsAsync() => await Shell.Current.GoToAsync(nameof(SettingsPage));

        [RelayCommand]
        private async Task GoToManageAccountAsync() => await Shell.Current.GoToAsync(nameof(ManageAccountPage));

        [RelayCommand]
        private async Task GoToInformationAsync() => await Shell.Current.GoToAsync(nameof(InformationPage));

        // --- KOMENDA WYLOGOWANIA ---
        [RelayCommand]
        private async Task LogoutAsync()
        {
            try
            {
                bool confirm = await Shell.Current.DisplayAlert("Wyloguj", "Jesteś pewien że chcesz się wylogować?", "Tak", "Nie");

                if (confirm)
                {
                    await _firestoreService.RemoveFcmTokenAsync();

                    // Usuwa lokalny cache
                    Preferences.Default.Remove("IsCaregiver");

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