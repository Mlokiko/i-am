using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Models;
using i_am.Pages.Authentication;
using i_am.Pages.CareGiver;
using i_am.Pages.CareTaker;
using i_am.Resources.Constants;
using i_am.Resources.Strings;
using i_am.Services;
using System.Text.RegularExpressions;

namespace i_am.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;

        public RegisterViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
            Birthdate = DateTime.Today.AddYears(-20); // Domyślna data
        }

        // --- POLA FORMULARZA ---
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private string email = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private string password = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private string confirmPassword = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private string name = string.Empty;

        [ObservableProperty]
        private string phonePrefix = "+48";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        private string phone = string.Empty;

        [ObservableProperty]
        private DateTime birthdate;

        [ObservableProperty]
        private string? selectedSex;

        [ObservableProperty]
        private bool isCaregiver;

        [ObservableProperty]
        private bool isPasswordHidden = true;

        [ObservableProperty]
        private bool isLoading = false;

        // --- WIDOCZNOŚĆ BŁĘDÓW W UI ---
        [ObservableProperty] private bool showEmailError;
        [ObservableProperty] private bool showPasswordError;
        [ObservableProperty] private bool showConfirmPasswordError;
        [ObservableProperty] private bool showNameError;
        [ObservableProperty] private bool showPhoneError;

        // --- WALIDACJA W CZASIE RZECZYWISTYM (Podczas pisania) ---
        partial void OnEmailChanged(string value)
        {
            // Pokaż błąd tylko wtedy, gdy pole nie jest puste i jest niepoprawne
            ShowEmailError = !string.IsNullOrEmpty(value) && !IsValidEmail(value);
        }

        partial void OnPasswordChanged(string value)
        {
            ShowPasswordError = !string.IsNullOrEmpty(value) && value.Length < 6;

            // Jeśli użytkownik zmieni hasło, odśwież też walidację pola "Potwierdź hasło"
            if (!string.IsNullOrEmpty(ConfirmPassword))
            {
                ShowConfirmPasswordError = ConfirmPassword != value;
            }
        }

        partial void OnConfirmPasswordChanged(string value)
        {
            ShowConfirmPasswordError = !string.IsNullOrEmpty(value) && value != Password;
        }

        partial void OnNameChanged(string value)
        {
            ShowNameError = !string.IsNullOrEmpty(value) && value.Length < 3;
        }

        partial void OnPhoneChanged(string value)
        {
            string cleanPhone = value?.Replace(" ", "") ?? "";
            ShowPhoneError = !string.IsNullOrEmpty(cleanPhone) && (cleanPhone.Length < 9 || !cleanPhone.All(char.IsDigit));
        }

        // --- LOGIKA REJESTRACJI ---
        private bool CanRegister()
        {
            string cleanPhone = Phone?.Replace(" ", "") ?? "";
            return !string.IsNullOrEmpty(Email) && Email.Contains("@") && Email.Contains(".") &&
                   !string.IsNullOrEmpty(Password) && Password.Length >= 6 &&
                   !string.IsNullOrEmpty(ConfirmPassword) && ConfirmPassword == Password &&
                   !string.IsNullOrEmpty(Name) && Name.Length >= 3 &&
                   !string.IsNullOrEmpty(cleanPhone) && cleanPhone.Length >= 9 && cleanPhone.All(char.IsDigit);
        }

        [RelayCommand(CanExecute = nameof(CanRegister))]
        private async Task RegisterAsync()
        {
            if (SelectedSex == null)
            {
                await Shell.Current.DisplayAlert(AppStrings.Error, AppStrings.Auth_SelectGender, AppStrings.OK);
                return;
            }

            if (!IsStrongPassword(Password))
            {
                await Shell.Current.DisplayAlert(AppStrings.Auth_WeakPassword, AppStrings.Auth_PasswordRequirements, AppStrings.OK);
                return;
            }

            int age = CalculateAge(Birthdate);
            if (age <= 5 || age >= 100)
            {
                await Shell.Current.DisplayAlert(AppStrings.Auth_InvalidBirthdate, AppStrings.Auth_InvalidAge, AppStrings.OK);
                return;
            }

            IsLoading = true;

            try
            {
                string uid = await _firestoreService.RegisterAsync(Email, Password);
                string fullPhoneNumber = $"{PhonePrefix.Trim()} {Phone.Trim()}";

                var userProfile = new User
                {
                    Name = Name.Trim(),
                    Email = Email.Trim(),
                    PhoneNumber = fullPhoneNumber,
                    BirthDate = Birthdate.ToUniversalTime(),
                    Sex = SelectedSex,
                    IsCaregiver = IsCaregiver
                };

                Preferences.Default.Set(PreferencesKeys.IsCaregiver, userProfile.IsCaregiver);
                Preferences.Default.Set(PreferencesKeys.UserId, uid);

                await _firestoreService.CreateUserProfileAsync(uid, userProfile);
                await _firestoreService.UpdateFcmTokenAsync();
                if (!IsCaregiver)
                {
                    // Generuje startowe pytania dla nowo utworzonego podopiecznego
                    await _firestoreService.InitializeDefaultQuestionsAsync(uid);
                }

                await Shell.Current.DisplayAlert(AppStrings.Success, AppStrings.Auth_SuccessAccount, AppStrings.OK);

                if (userProfile.IsCaregiver)
                    await Shell.Current.GoToAsync($"//{NavigationRoutes.CareGiverMainPage}");
                else
                    await Shell.Current.GoToAsync($"//{NavigationRoutes.CareTakerMainPage}");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert(AppStrings.Auth_RegistrationFailed, ex.Message, AppStrings.OK);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // --- AKCJE POMOCNICZE ---
        [RelayCommand]
        private void TogglePassword() => IsPasswordHidden = !IsPasswordHidden;

        [RelayCommand]
        private async Task GoToLoginAsync() => await Shell.Current.GoToAsync(NavigationRoutes.LoginPage);

        [RelayCommand]
        private async Task ShowInfo(string type)
        {
            string title = type switch
            {
                "Password" => LocalizationManager.Info_Password,
                "Name" => LocalizationManager.Info_Name,
                "Phone" => LocalizationManager.Info_Phone,
                "birthdate" => LocalizationManager.Info_Birthdate,
                "sex" => LocalizationManager.Info_Gender,
                _ => "Informacja"
            };

            string message = type switch
            {
                "Password" => LocalizationManager.Info_PasswordMessage,
                "Name" => LocalizationManager.Info_NameMessage,
                "Phone" => LocalizationManager.Info_PhoneMessage,
                "birthdate" => LocalizationManager.Info_BirthdateMessage,
                "sex" => LocalizationManager.Info_GenderMessage,
                _ => ""
            };

            await Shell.Current.DisplayAlert(title, message, LocalizationManager.Understand);
        }

        // --- METODY PRYWATNE (z poprzedniego kodu) ---
        private bool IsValidEmail(string email) => new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").IsMatch(email);
        private bool IsStrongPassword(string password) => new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$").IsMatch(password);
        private int CalculateAge(DateTime birthdate)
        {
            DateTime today = DateTime.Today;
            int age = today.Year - birthdate.Year;
            if (birthdate.Date > today.AddYears(-age)) age--;
            return age;
        }
    }
}