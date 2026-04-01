using i_am.Pages.Authentication;
using i_am.Models;
using i_am.Services;
using System.Collections.ObjectModel;
using i_am.Pages.Main;

namespace i_am.Pages.CareGiver;

public partial class CareGiverMainPage : ContentPage
{
    private readonly FirestoreService _firestoreService;
    public ObservableCollection<User> UsersList { get; set; } = new ObservableCollection<User>();
    public CareGiverMainPage(FirestoreService firestoreService)
	{
		InitializeComponent();
        _firestoreService = firestoreService;
    }
   
    private async void OnNotificationsButtonClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(NotificationsPage));
    }
    private async void OnCalendarButtonClicked(object sender, EventArgs e)
    {
        //await Shell.Current.GoToAsync(nameof(CalendarPage));
        await Shell.Current.GoToAsync(nameof(InformationPage));
    }
    private async void OnEditCareTakerQuestionsButtonClicked(object sender, EventArgs e)
    {
        //await Shell.Current.GoToAsync(nameof(EditCareTakerQuestionsPage));
        await Shell.Current.GoToAsync(nameof(InformationPage));
    }
    private async void OnManageCareTakersButtonClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ManageCareTakersPage));
    }
    private async void OnManageAccountButtonClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ManageAccountPage));
    }
    private async void OnLogoutButtonClicked(object sender, EventArgs e)
    {
        try
        {
            bool confirm = await DisplayAlert("Wyloguj", "Jesteœ pewien ¿e chcesz siê wylogowaæ?", "Tak", "Nie");

            if (confirm)
            {
                // Delete the local cache
                Preferences.Default.Remove("IsCaregiver");

                // Tell Firebase to clear the saved session
                await _firestoreService.SignOutAsync();

                // Teleport the user back to the Landing Page, clearing the history stack
                await Shell.Current.GoToAsync($"//{nameof(LandingPage)}");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("B³¹d", $"Problem z wylogowaniem: {ex.Message}", "OK");
        }
    }
    private async void OnAlarmInformationTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(InformationPage));
    }
}