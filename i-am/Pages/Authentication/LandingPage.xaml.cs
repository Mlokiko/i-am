namespace i_am.Pages.Authentication;

public partial class LandingPage : ContentPage
{
    public LandingPage()
	{
		InitializeComponent();
    }
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(LoginPage));
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RegisterPage));
    }
}