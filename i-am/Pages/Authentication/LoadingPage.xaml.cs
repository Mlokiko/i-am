using i_am.Pages.CareGiver;
using i_am.Pages.CareTaker;
using i_am.Resources.Constants;
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
        bool isFirstLaunch = Preferences.Default.Get(PreferencesKeys.IsFirstLaunch, true);

        if (isFirstLaunch)
        {
            // Kierujemy na ekran uprawnieñ i przerywamy dalsze ³adowanie
            await Shell.Current.GoToAsync($"//{NavigationRoutes.PermissionsPage}");
            return;
        }

        // 2. Jeœli to nie jest pierwsze uruchomienie, sprawdzamy logowanie
        string userId = Preferences.Default.Get(PreferencesKeys.UserId, string.Empty);
        if (!string.IsNullOrEmpty(userId))
        {
            await _firestoreService.UpdateFcmTokenAsync();
            bool isCaregiver = Preferences.Default.Get(PreferencesKeys.IsCaregiver, false);

            if (isCaregiver)
                await Shell.Current.GoToAsync($"//{NavigationRoutes.CareGiverMainPage}");
            else
                await Shell.Current.GoToAsync($"//{NavigationRoutes.CareTakerMainPage}");
        }
        else
        {
            await Shell.Current.GoToAsync($"//{NavigationRoutes.LandingPage}");
        }
    }
}