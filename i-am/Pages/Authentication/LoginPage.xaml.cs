using i_am.Pages.CareGiver;
using i_am.Pages.CareTaker;
using i_am.Services;

namespace i_am.Pages.Authentication;

public partial class LoginPage : ContentPage
{
    private readonly FirestoreService _firestoreService;

    public LoginPage(FirestoreService firestoreService)
    {
        InitializeComponent();
        _firestoreService = firestoreService;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        string email = EmailEntry.Text?.Trim() ?? string.Empty;
        string password = PasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("B³¹d", "Wype³nij wszystkie pola", "OK");
            return;
        }

        try
        {
            string uid = await _firestoreService.LoginAsync(email, password);

            var profile = await _firestoreService.GetUserProfileAsync(uid);

            // Czyszczenie pól, ¿eby po wylogowaniu ich nie by³o
            EmailEntry.Text = string.Empty;
            PasswordEntry.Text = string.Empty;

            if (profile != null)
            {
                // MAUI ma wbudowany mechanizm do przechowywania danych, skorzystamy z niego.
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
            await DisplayAlert("Logowanie nie powiod³o siê", ex.Message, "OK");
        }
    }

    private async void OnGoToRegisterTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RegisterPage));
    }
}