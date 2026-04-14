using i_am.Pages.CareGiver;
using i_am.Pages.CareTaker;
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
        if (_firestoreService.IsUserLoggedIn())
        {
            await _firestoreService.UpdateFcmTokenAsync();
            bool isCaregiver = Preferences.Default.Get("IsCaregiver", false);

            if (isCaregiver)
                await Shell.Current.GoToAsync($"//{nameof(CareGiverMainPage)}");
            else
                await Shell.Current.GoToAsync($"//{nameof(CareTakerMainPage)}");
        }
        else
        {
            await Shell.Current.GoToAsync($"//{nameof(LandingPage)}");
        }
    }
}