using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Pages.Authentication;
using i_am.Pages.Main;
using i_am.Pages.CareTaker;
using i_am.Services;

namespace i_am.ViewModels
{
    public partial class CareTakerMainViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;

        public CareTakerMainViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        // --- KOMENDY NAWIGACYJNE ---
        [RelayCommand]
        private async Task GoToNotificationsAsync() => await Shell.Current.GoToAsync(nameof(NotificationsPage));

        [RelayCommand]
        private async Task GoToDailyActivityAsync() => await Shell.Current.GoToAsync(nameof(DailyActivityPage));

        [RelayCommand]
        private async Task GoToCalendarAsync() => await Shell.Current.GoToAsync(nameof(InformationPage)); // Zastąp w przyszłości nameof(CalendarPage)

        [RelayCommand]
        private async Task GoToManageCareGiversAsync() => await Shell.Current.GoToAsync(nameof(ManageCareGiversPage));

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
                bool confirm = await Shell.Current.DisplayAlert("Wyloguj", "Jesteś pewien, że chcesz się wylogować?", "Tak", "Nie");

                if (confirm)
                {
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