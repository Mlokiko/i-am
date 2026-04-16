using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Pages.Authentication;
using i_am.Pages.Main;
using i_am.Pages.CareGiver;
using i_am.Resources.Constants;
using i_am.Resources.Strings;
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
        private async Task GoToNotificationsAsync() => await Shell.Current.GoToAsync(NavigationRoutes.NotificationsPage);

        [RelayCommand]
        private async Task GoToCalendarAsync() => await Shell.Current.GoToAsync(NavigationRoutes.CalendarPage);

        [RelayCommand]
        private async Task GoToStatisticsAsync() => await Shell.Current.GoToAsync(NavigationRoutes.StatisticsPage);


        [RelayCommand]
        private async Task GoToEditCareTakerQuestionsAsync() => await Shell.Current.GoToAsync(NavigationRoutes.EditCareTakerQuestionsPage);

        [RelayCommand]
        private async Task GoToManageCareTakersAsync() => await Shell.Current.GoToAsync(NavigationRoutes.ManageConnectionsPage);

        [RelayCommand]
        private async Task GoToSettingsAsync() => await Shell.Current.GoToAsync(NavigationRoutes.SettingsPage);

        [RelayCommand]
        private async Task GoToManageAccountAsync() => await Shell.Current.GoToAsync(NavigationRoutes.ManageAccountPage);

        [RelayCommand]
        private async Task GoToInformationAsync() => await Shell.Current.GoToAsync(NavigationRoutes.InformationPage);

        // --- KOMENDA WYLOGOWANIA ---
        [RelayCommand]
        private async Task LogoutAsync()
        {
            try
            {
                bool confirm = await Shell.Current.DisplayAlert(
                    LocalizationManager.Auth_LogoutTitle, 
                    LocalizationManager.Auth_LogoutConfirm, 
                    LocalizationManager.Auth_Yes, 
                    LocalizationManager.Auth_No);

                if (confirm)
                {
                    await _firestoreService.RemoveFcmTokenAsync();

                    // Usuwa lokalny cache
                    Preferences.Default.Remove(PreferencesKeys.IsCaregiver);
                    Preferences.Default.Remove(PreferencesKeys.UserId);

                    // Firebase czyści sesje
                    await _firestoreService.SignOutAsync();

                    // Podwójny ukośnik, aby zresetować stos nawigacji
                    await Shell.Current.GoToAsync($"//{NavigationRoutes.LandingPage}");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert(LocalizationManager.Error, $"{LocalizationManager.Auth_LogoutError} {ex.Message}", LocalizationManager.OK);
            }
        }
    }
}