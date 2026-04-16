using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Resources.Constants;
using i_am.Resources.Strings;
using i_am.Services;
using i_am.Pages.Authentication;
using i_am.Pages.CareGiver;
using i_am.Pages.CareTaker;

namespace i_am.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;

        // Te właściwości automatycznie powiadomią UI o zmianach (zastąpią wpisywanie EmailEntry.Text)
        [ObservableProperty]
        private string email = string.Empty;

        [ObservableProperty]
        private string password = string.Empty;

        public LoginViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        [RelayCommand]
        private async Task LoginAsync()
        {
            // 1. Walidacja pustości pól
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Shell.Current.DisplayAlert(AppStrings.Error, AppStrings.Auth_FillAllFields, AppStrings.OK);
                return;
            }

            // 2. Sprawdzenie połączenia z internetem przed akcją
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await Shell.Current.DisplayAlert(AppStrings.Auth_NoConnection, AppStrings.Auth_NoConnectionMessage, AppStrings.OK);
                return;
            }

            try
            {
                string uid = await _firestoreService.LoginAsync(Email, Password);
                var profile = await _firestoreService.GetUserProfileAsync(uid);

                // Czyszczenie pól po udanym zalogowaniu
                Email = string.Empty;
                Password = string.Empty;

                if (profile != null)
                {
                    Preferences.Default.Set(PreferencesKeys.UserId, profile.Id);
                    Preferences.Default.Set(PreferencesKeys.IsCaregiver, profile.IsCaregiver);
                    await _firestoreService.UpdateFcmTokenAsync();

                    if (profile.IsCaregiver)
                        await Shell.Current.GoToAsync($"//{NavigationRoutes.CareGiverMainPage}");
                    else
                        await Shell.Current.GoToAsync($"//{NavigationRoutes.CareTakerMainPage}");
                }
            }
            catch (Exception ex)
            {
                // 3. Tłumaczenie błędu na polski
                string errorMessage = TranslateFirebaseError(ex.Message);
                await Shell.Current.DisplayAlert(AppStrings.Auth_LoginFailed, errorMessage, AppStrings.OK);
            }
        }

        [RelayCommand]
        private async Task GoToRegisterAsync()
        {
            await Shell.Current.GoToAsync(NavigationRoutes.RegisterPage);
        }

        /// <summary>
        /// Metoda tłumacząca surowe błędy Firebase (zawarte w ex.Message) na przyjazne komunikaty.
        /// </summary>
        private string TranslateFirebaseError(string errorMessage)
        {
            string lowerError = errorMessage.ToLower();

            // Błędne dane (email lub hasło)
            if (lowerError.Contains("the supplied auth credential is incorrect, malformed or has expired"))
            {
                return "Nieprawidłowy adres email lub hasło.";
            }

            // Format emaila
            if (lowerError.Contains("the email address is badly formatted."))
            {
                return "Podany adres email ma nieprawidłowy format.";
            }

            // Zbyt wiele prób logowania
            if (lowerError.Contains("too-many-requests"))
            {
                return "Zbyt wiele nieudanych prób logowania. Ze względów bezpieczeństwa konto zostało tymczasowo zablokowane. Spróbuj ponownie później.";
            }

            // Konto wyłączone przez administratora (w Firebase Console)
            if (lowerError.Contains("user-disabled"))
            {
                return "To konto zostało zablokowane.";
            }

            // Brak połączenia z internetem
            if (lowerError.Contains("network error"))
            {
                return "Urządzenie nie ma połączenia z internetem.";
            }

            // Inne problemy z siecią, które przepuściło Connectivity (np. timeout serwera)
            if (lowerError.Contains("network-request-failed") || lowerError.Contains("timeout") || lowerError.Contains("offline"))
            {
                return "Wystąpił problem z serwerem lub połączeniem sieciowym. Spróbuj ponownie za chwilę.";
            }

            return "Wystąpił nieoczekiwany błąd. Spróbuj ponownie później.";
        }
    }
}