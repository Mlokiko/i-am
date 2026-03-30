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

            // 3. Clear the fields so they are empty if the user logs out later
            EmailEntry.Text = string.Empty;
            PasswordEntry.Text = string.Empty;

            // 4. Navigate to the main app screen
            // Using "//" tells the AppShell to clear the navigation stack so the user can't hit "Back" to go to the login screen
            await Shell.Current.GoToAsync($"//{nameof(MainPage)}");
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