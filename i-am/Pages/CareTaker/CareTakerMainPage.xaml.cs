using i_am.Pages.Authentication;
using i_am.Services;
using i_am.Models;
using System.Collections.ObjectModel;

namespace i_am.Pages.CareTaker;

public partial class CareTakerMainPage : ContentPage
{
    private readonly FirestoreService _firestoreService;
    public ObservableCollection<User> UsersList { get; set; } = new ObservableCollection<User>();
    public CareTakerMainPage(FirestoreService firestoreService)
	{
		InitializeComponent();
        _firestoreService = firestoreService;
    }
    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        try
        {
            // Ask the user to confirm they actually want to log out
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
}