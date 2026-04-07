using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Pages.Authentication;
using i_am.Services;

namespace i_am.ViewModels
{
    public partial class ManageAccountViewModel : ObservableObject
    {
        private readonly IFirestoreService _firestoreService;

        // Właściwości bindowane do UI
        [ObservableProperty] private string name = "Wczytywanie...";
        [ObservableProperty] private string email = "...";
        [ObservableProperty] private string phoneNumber = "...";
        [ObservableProperty] private string birthDate = "...";
        [ObservableProperty] private string sex = "...";
        [ObservableProperty] private string role = "...";
        [ObservableProperty] private string createdAt = "...";

        public ManageAccountViewModel(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        // Metoda ładująca dane przy wejściu na ekran
        public async Task InitializeAsync()
        {
            string? uid = _firestoreService.GetCurrentUserId();
            if (!string.IsNullOrEmpty(uid))
            {
                var profile = await _firestoreService.GetUserProfileAsync(uid);

                if (profile != null)
                {
                    Name = profile.Name;
                    Email = profile.Email;
                    PhoneNumber = string.IsNullOrEmpty(profile.PhoneNumber) ? "Nie podano" : profile.PhoneNumber;
                    BirthDate = profile.BirthDate.ToLocalTime().ToString("yyyy.MM.dd");
                    Sex = profile.Sex;
                    Role = profile.IsCaregiver ? "Opiekun" : "Podopieczny";
                    CreatedAt = profile.CreatedAt.ToLocalTime().ToString("yyyy.MM.dd");
                }
            }
        }

        // Komenda do usuwania konta
        [RelayCommand]
        private async Task DeleteAccountAsync()
        {
            bool confirm = await Shell.Current.DisplayAlert(
                "Usuwanie konta",
                "Jesteś tego pewien? Tej akcji nie da się cofnąć. Usunięte zostaną wszystkie dane związane z twoim kontem.",
                "Usuń",
                "Anuluj");

            if (confirm)
            {
                try
                {
                    await _firestoreService.DeleteAccountAndProfileAsync();

                    // Wyczyść zapisany typ konta
                    Preferences.Default.Remove("IsCaregiver");

                    // Przekieruj do LandingPage (wymaga podwójnego ukośnika dla resetu stosu nawigacji w Shell)
                    await Shell.Current.GoToAsync($"//{nameof(LandingPage)}");
                }
                catch (Exception ex)
                {
                    await Shell.Current.DisplayAlert("Błąd", $"Problem z usuwaniem konta: {ex.Message}", "OK");
                }
            }
        }
    }
}