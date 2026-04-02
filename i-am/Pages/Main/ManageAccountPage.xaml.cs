using i_am.Pages.Authentication;
using i_am.Services;

namespace i_am.Pages.Main;

public partial class ManageAccountPage : ContentPage
{
    private readonly FirestoreService _firestoreService;
    public ManageAccountPage(FirestoreService firestoreService)
	{
		InitializeComponent();
        _firestoreService = firestoreService;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        string? uid = _firestoreService.GetCurrentUserId();
        if (!string.IsNullOrEmpty(uid))
        {
            var profile = await _firestoreService.GetUserProfileAsync(uid);

            if (profile != null)
            {
                HeaderNameLabel.Text = profile.Name;
                EmailLabel.Text = profile.Email;
                PhoneLabel.Text = string.IsNullOrEmpty(profile.PhoneNumber) ? "Nie podano" : profile.PhoneNumber;
                BirthDateLabel.Text = profile.BirthDate.ToLocalTime().ToString("yyyy.MM.dd");
                SexLabel.Text = profile.Sex;
                RoleLabel.Text = profile.IsCaregiver ? "Opiekun" : "Podopieczny";
                CreatedLabel.Text = profile.CreatedAt.ToLocalTime().ToString("yyyy.MM.dd");
            }
        }
    }

    private async void OnDeleteAccountClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Usuwanie konta",
            "Jesteœ tego pewien? Tej akcji nie da siê cofn¹æ. Usuniête zostan¹ wszystkie dane zwi¹zane z twoim kontem.",
            "Tak, usuñ je",
            "Anuluj");

        if (confirm)
        {
            try
            {
                await _firestoreService.DeleteAccountAndProfileAsync();
                Preferences.Default.Remove("IsCaregiver");
                await Shell.Current.GoToAsync($"//{nameof(LandingPage)}");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Problem z usuwaniem konta: {ex.Message}", "OK");
            }
        }
    }
}