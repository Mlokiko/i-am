using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Shell.Current.DisplayAlert("Błąd", "Wypełnij wszystkie pola", "OK");
                return;
            }

            try
            {
                string uid = await _firestoreService.LoginAsync(Email, Password);
                var profile = await _firestoreService.GetUserProfileAsync(uid);

                // Czyszczenie pól
                Email = string.Empty;
                Password = string.Empty;

                if (profile != null)
                {
                    Preferences.Default.Set("UserId", profile.Id);
                    Preferences.Default.Set("IsCaregiver", profile.IsCaregiver);
                    await _firestoreService.UpdateFcmTokenAsync();

                    if (profile.IsCaregiver)
                        await Shell.Current.GoToAsync($"//{nameof(CareGiverMainPage)}");
                    else
                        await Shell.Current.GoToAsync($"//{nameof(CareTakerMainPage)}");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Logowanie nie powiodło się", ex.Message, "OK");
            }
        }

        [RelayCommand]
        private async Task GoToRegisterAsync()
        {
            await Shell.Current.GoToAsync(nameof(RegisterPage));
        }
    }
}