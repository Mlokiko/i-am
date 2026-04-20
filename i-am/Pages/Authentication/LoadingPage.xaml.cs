using i_am.Pages.CareGiver;
using i_am.Pages.CareTaker;
using i_am.Pages.Main;
using i_am.Services;

namespace i_am.Pages.Authentication;

public partial class LoadingPage : ContentPage
{
    private readonly FirestoreService _firestoreService;
    public LoadingPage(FirestoreService firestoreService)
    {
        InitializeComponent();
        _firestoreService = firestoreService;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // 1. Sprawdzamy czy to pierwsze uruchomienie
        bool isFirstLaunch = Preferences.Default.Get("IsFirstLaunch", true);

        if (isFirstLaunch)
        {
            // Kierujemy na ekran uprawnieñ i przerywamy dalsze ³adowanie
            await Shell.Current.GoToAsync($"//{nameof(Main.PermissionsPage)}");
            return;
        }

        // 2. Jeœli to nie jest pierwsze uruchomienie, sprawdzamy logowanie
        string userId = Preferences.Default.Get("UserId", string.Empty);
        if (!string.IsNullOrEmpty(userId))
        {
            await _firestoreService.UpdateLastActiveAsync();
            await _firestoreService.UpdateFcmTokenAsync();
            await Shell.Current.GoToAsync($"//{nameof(MainPage)}");
        }
        else
        {
            await Shell.Current.GoToAsync($"//{nameof(LandingPage)}");
        }
    }
}