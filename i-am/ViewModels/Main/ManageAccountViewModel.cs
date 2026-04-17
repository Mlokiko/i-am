using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Pages.Authentication;
using i_am.Services;
using System.Collections.ObjectModel;

namespace i_am.ViewModels
{
    public partial class ManageAccountViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;

        // Właściwości bindowane do UI (Podgląd)
        [ObservableProperty] private string name = "Wczytywanie...";
        [ObservableProperty] private string email = "...";
        [ObservableProperty] private string phoneNumber = "...";
        [ObservableProperty] private string birthDate = "...";
        [ObservableProperty] private string sex = "...";
        [ObservableProperty] private string role = "...";
        [ObservableProperty] private string createdAt = "...";

        // --- ZMIENNA DLA EKRANU ŁADOWANIA ---
        [ObservableProperty]
        private bool isLoading;

        // --- ZMIENNE DLA TRYBU EDYCJI ---
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotEditing))]
        private bool isEditing;

        public bool IsNotEditing => !IsEditing;

        [ObservableProperty] private string editPhoneNumber = string.Empty;
        [ObservableProperty] private string editSex = string.Empty;

        public ObservableCollection<string> SexOptions { get; } = new()
        {
            "Mężczyzna",
            "Kobieta",
            "Inne"
        };

        public ManageAccountViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public async Task InitializeAsync()
        {
            string? uid = Preferences.Get("UserId", string.Empty);
            if (!string.IsNullOrEmpty(uid))
            {
                var profile = await _firestoreService.GetUserProfileAsync(uid);

                if (profile != null)
                {
                    Name = profile.Name;
                    Email = profile.Email;
                    PhoneNumber = string.IsNullOrWhiteSpace(profile.PhoneNumber) ? "Nie podano" : profile.PhoneNumber;
                    BirthDate = profile.BirthDate.ToLocalTime().ToString("yyyy.MM.dd");
                    Sex = profile.Sex;
                    Role = profile.IsCaregiver ? "Opiekun" : "Podopieczny";
                    CreatedAt = profile.CreatedAt.ToLocalTime().ToString("yyyy.MM.dd");
                }
            }
        }

        // --- KOMENDY EDYCJI ---
        [RelayCommand]
        private void EnableEditMode()
        {
            EditPhoneNumber = PhoneNumber == "Nie podano" ? string.Empty : PhoneNumber;
            EditSex = Sex;
            IsEditing = true;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditing = false;
        }

        [RelayCommand]
        private async Task SaveChangesAsync()
        {
            string? uid = Preferences.Get("UserId", string.Empty);
            if (string.IsNullOrEmpty(uid)) return;

            try
            {
                // Zapisz w bazie danych
                await _firestoreService.UpdateUserProfileAsync(uid, EditPhoneNumber, EditSex);

                // Zaktualizuj widok podglądu
                PhoneNumber = string.IsNullOrWhiteSpace(EditPhoneNumber) ? "Nie podano" : EditPhoneNumber;
                Sex = EditSex;

                IsEditing = false;
                await Shell.Current.DisplayAlert("Sukces", "Twoje dane zostały zaktualizowane.", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Błąd", ex.Message, "OK");
            }
        }

        // --- KOMENDA USUWANIA KONTA ---
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
                // Włączamy ekran ładowania
                IsLoading = true;

                try
                {
                    await _firestoreService.RemoveFcmTokenAsync();
                    await _firestoreService.DeleteUserAsync();
                    Preferences.Default.Remove("IsCaregiver");
                    Preferences.Default.Remove("UserId");
                    await Shell.Current.GoToAsync($"//{nameof(LandingPage)}");
                }
                catch (Exception ex)
                {
                    await Shell.Current.DisplayAlert("Błąd", $"Problem z usuwaniem konta: {ex.Message}", "OK");
                }
                finally
                {
                    // Zawsze wyłączamy ekran ładowania (nawet jeśli wystąpił błąd lub powrót z alertu o reautentykacji)
                    IsLoading = false;
                }
            }
        }
    }
}