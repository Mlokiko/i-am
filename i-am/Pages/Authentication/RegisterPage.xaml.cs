using i_am.Services;
using i_am.Models;
using System.Text.RegularExpressions;
using i_am.Pages.CareGiver;
using i_am.Pages.CareTaker;

namespace i_am.Pages.Authentication;

public partial class RegisterPage : ContentPage
{
    private readonly FirestoreService _firestoreService;

    public RegisterPage(FirestoreService firestoreService)
    {
        InitializeComponent();
        _firestoreService = firestoreService;
        BirthdatePicker.MaximumDate = DateTime.Today.AddYears(-5);
        BirthdatePicker.MinimumDate = DateTime.Today.AddYears(-100);
    }
    
    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EmailEntry.Text) ||
            string.IsNullOrWhiteSpace(PasswordEntry.Text) ||
            string.IsNullOrWhiteSpace(ConfirmPasswordEntry.Text) ||
            string.IsNullOrWhiteSpace(NameEntry.Text) ||
            SexPicker.SelectedItem == null ||
            string.IsNullOrWhiteSpace(PhonePrefixEntry.Text) ||
            string.IsNullOrWhiteSpace(PhoneEntry.Text))
        {
            await DisplayAlert("B³¹d", "Wype³nij wszystkie wymagane pola.", "OK");
            return;
        }

        if (PasswordEntry.Text != ConfirmPasswordEntry.Text)
        {
            await DisplayAlert("B³¹d", "Podane has³a nie s¹ identyczne.", "OK");
            return;
        }

        if (!IsValidEmail(EmailEntry.Text.Trim()))
        {
            await DisplayAlert("B³¹d", "WprowadŸ poprawny adres email.", "OK");
            return;
        }

        if (!IsStrongPassword(PasswordEntry.Text))
        {
            await DisplayAlert("S³abe has³o", "Has³o musi mieæ co najmniej 8 znaków, zawieraæ co najmniej jedn¹ wielk¹ literê, jedn¹ ma³¹ literê i jedn¹ cyfrê.", "OK");
            return;
        }

        // Teoretycznie niepotrzebne, bo w konstruktorze ustawiamy min/max wiek
        int age = CalculateAge(BirthdatePicker.Date);
        if (age <= 5)
        {
            await DisplayAlert("B³¹d daty urodzenia", "U¿ytkownik musi byæ powy¿ej 5 roku ¿ycia.", "OK");
            return;
        }
        if (age >= 100)
        {
            await DisplayAlert("B³¹d daty urodzenia", "Wprowadzony wiek to 100 lat lub wiêcej. Upewnij siê, ¿e wprowadzono poprawn¹ datê.", "OK");
            return;
        }

        // --- START LOADING ---
        // Disable the button to prevent double-clicks and show the spinner
        RegisterButton.IsEnabled = false;
        LoadingSpinner.IsVisible = true;
        LoadingSpinner.IsRunning = true;

        try
        {
            // tworzenie konta w Firebase Authentication, co zwraca unikalny UID u¿ytkownika, który bêdzie u¿ywany jako klucz do przechowywania profilu w Firestore
            string uid = await _firestoreService.RegisterAsync(EmailEntry.Text, PasswordEntry.Text);

            string fullPhoneNumber = $"{PhonePrefixEntry.Text.Trim()} {PhoneEntry.Text.Trim()}";

            // tworzenie obiektu  user
            var userProfile = new User
            {
                // Id jest automatycznie obs³ugiwany przez atrybut [FirestoreDocumentId]
                Name = NameEntry.Text.Trim(),
                Email = EmailEntry.Text.Trim(),
                PhoneNumber = fullPhoneNumber,
                BirthDate = BirthdatePicker.Date.ToUniversalTime(),
                Sex = SexPicker.SelectedItem.ToString(),
                IsCaregiver = CaregiverSwitch.IsToggled
                // CreatedAT, CareTtakersID i CaregiversID s¹ automatycznie ustawiane przez model User
            };

            Preferences.Default.Set("IsCaregiver", userProfile.IsCaregiver);
            await _firestoreService.CreateUserProfileAsync(uid, userProfile);
            await _firestoreService.UpdateFcmTokenAsync();

            await DisplayAlert("Sukces", "Konto zosta³o pomyœlnie stworzone!", "OK");

            if (userProfile.IsCaregiver)
                await Shell.Current.GoToAsync($"//{nameof(CareGiverMainPage)}");
            else
                await Shell.Current.GoToAsync($"//{nameof(CareTakerMainPage)}");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Rejestracja nie powiod³a siê", ex.Message, "OK");
        }
        finally
        {
            LoadingSpinner.IsRunning = false;
            LoadingSpinner.IsVisible = false;
            RegisterButton.IsEnabled = true;
        }
    }

    #region Przyciski, znaki zapytania
    
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"{nameof(LoginPage)}");
    }
    private void OnTogglePasswordTapped(object sender, TappedEventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        ConfirmPasswordEntry.IsPassword = !ConfirmPasswordEntry.IsPassword;

        TogglePasswordLabel.Text = PasswordEntry.IsPassword ? "Poka¿" : "Ukryj";
        ToggleConfirmPasswordLabel.Text = ConfirmPasswordEntry.IsPassword ? "Poka¿" : "Ukryj";
    }
    private async void OnPasswordInfoTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert(
            "Wymagania has³a",
            "Has³o musi mieæ co najmniej 8 znaków, zawieraæ co najmniej jedn¹ wielk¹ literê, jedn¹ ma³¹ literê i jedn¹ cyfrê.",
            "Rozumiem");
    }
    private async void OnNameInfoTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert(
            "Dlaczego potrzebujemy Twojej nazwy?",
            "Twoja nazwa u¿ytkownika (pseudonim lub imiê) bêdzie widoczna w aplikacji dla Twoich opiekunów lub podopiecznych, aby ³atwiej by³o im Ciê rozpoznaæ.",
            "Rozumiem");
    }
    private async void OnPhoneInfoTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert(
            "Dlaczego potrzebujemy twój numer telefonu?",
            "Numer telefonu jest u¿ywany do szybkiego kontaktu w nag³ych wypadkach, oraz umo¿liwia nam wysy³anie powiadomieñ SMS do opiekunów.",
            "Rozumiem");
    }
    private async void OnSexInfoTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert(
            "Dlaczego potrzebujemy Twojej P³ci?",
            "Pomo¿e to lepiej dobraæ pytania, a w przypadku zdjêæ, pozwoli na lepsze rozpoznanie otoczenia/mimiki twarzy",
            "Rozumiem");
    }
    private async void OnBirthdayInfoTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert(
            "Dlaczego potrzebujemy Twojej daty urodzenia?",
            "Pomo¿e to lepiej dobraæ pytania, a w przypadku zdjêæ, pozwoli na lepsze rozpoznanie otoczenia/mimiki twarzy",
            "Rozumiem");
    }
    private async void OnCareGiverInfoTapped(object sender, TappedEventArgs e)
    {
        await DisplayAlert(
            "Do czego dok³adnie s³u¿y ta opcja?",
            "Je¿eli jesteœ osob¹ która chce zostaæ opiekunem, zaznacz t¹ opcjê. Twoje konto zostanie stworzone z statusem 'opiekun', bêdziesz posiadaæ opcje sprawdzania swojego podopiecznego, otrzymywania komunikatów na temat jego samopoczucia. Je¿eli tworzysz konto w celu zostania podpiecznym, nie zaznaczaj tej opcji",
            "Rozumiem");
    }
    #endregion
    #region Funkcje pomocnicze
    private bool IsValidEmail(string email)
    {
        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        return emailRegex.IsMatch(email);
    }

    private bool IsStrongPassword(string password)
    {
        // Regex rules:
        // (?=.*[a-z]) - at least one lowercase letter
        // (?=.*[A-Z]) - at least one uppercase letter
        // (?=.*\d)    - at least one digit
        // .{8,}       - minimum 8 characters long
        var passwordRegex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$");
        return passwordRegex.IsMatch(password);
    }

    private int CalculateAge(DateTime birthdate)
    {
        DateTime today = DateTime.Today;
        int age = today.Year - birthdate.Year;

        if (birthdate.Date > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    // Pobieramy kolory z motywu systemowego
    private Color DangerColor => AppInfo.RequestedTheme == AppTheme.Light ? Colors.IndianRed : Colors.DarkRed;
    private Color NormalColor => AppInfo.RequestedTheme == AppTheme.Light ? Colors.LightGray : Colors.DimGray;

    // 1. Zdarzenia odpalane, gdy u¿ytkownik "WYCHODZI" z pola
    private void OnEmailUnfocused(object sender, FocusEventArgs e) => ValidateEmail(showVisualError: true);
    private void OnPasswordUnfocused(object sender, FocusEventArgs e) => ValidatePassword(showVisualError: true);
    private void OnConfirmPasswordUnfocused(object sender, FocusEventArgs e) => ValidateConfirmPassword(showVisualError: true);
    private void OnNameUnfocused(object sender, FocusEventArgs e) => ValidateName(showVisualError: true);
    private void OnPhoneUnfocused(object sender, FocusEventArgs e) => ValidatePhone(showVisualError: true); // DODANO

    // 2. Zdarzenie odpalane w trakcie pisania (Ukrywa b³êdy i sprawdza czy w³¹czyæ przycisk)
    private void OnFieldTextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateEmail(showVisualError: false);
        ValidatePassword(showVisualError: false);
        ValidateConfirmPassword(showVisualError: false);
        ValidateName(showVisualError: false);
        ValidatePhone(showVisualError: false); // DODANO

        CheckFormValidity();
    }

    // 3. Funkcje sprawdzaj¹ce poszczególne pola
    private void ValidateEmail(bool showVisualError)
    {
        bool isValid = !string.IsNullOrEmpty(EmailEntry.Text) && EmailEntry.Text.Contains("@") && EmailEntry.Text.Contains(".");
        SetFieldVisualState(isValid, showVisualError, EmailEntry.Text, EmailBorder, EmailErrorLabel);
    }

    private void ValidatePassword(bool showVisualError)
    {
        bool isValid = !string.IsNullOrEmpty(PasswordEntry.Text) && PasswordEntry.Text.Length >= 6;
        SetFieldVisualState(isValid, showVisualError, PasswordEntry.Text, PasswordBorder, PasswordErrorLabel);
    }

    private void ValidateConfirmPassword(bool showVisualError)
    {
        bool isValid = !string.IsNullOrEmpty(ConfirmPasswordEntry.Text) && ConfirmPasswordEntry.Text == PasswordEntry.Text;
        SetFieldVisualState(isValid, showVisualError, ConfirmPasswordEntry.Text, ConfirmPasswordBorder, ConfirmPasswordErrorLabel);
    }

    private void ValidateName(bool showVisualError)
    {
        bool isValid = !string.IsNullOrEmpty(NameEntry.Text) && NameEntry.Text.Length >= 3;
        SetFieldVisualState(isValid, showVisualError, NameEntry.Text, NameBorder, NameErrorLabel);
    }

    // NOWA FUNKCJA WALIDACJI TELEFONU
    private void ValidatePhone(bool showVisualError)
    {
        // Usuwamy spacje (jeœli u¿ytkownik wpisa³ "111 222 333"), aby prawid³owo policzyæ cyfry
        string cleanPhone = PhoneEntry.Text?.Replace(" ", "") ?? "";

        // Sprawdzamy czy nie jest pusty, czy ma min. 9 znaków i czy sk³ada siê tylko z cyfr
        bool isValid = !string.IsNullOrEmpty(cleanPhone) && cleanPhone.Length >= 9 && cleanPhone.All(char.IsDigit);

        SetFieldVisualState(isValid, showVisualError, PhoneEntry.Text, PhoneBorder, PhoneErrorLabel);
    }

    // 4. Mened¿er wygl¹du
    private void SetFieldVisualState(bool isValid, bool showVisualError, string text, Border border, Label errorLabel)
    {
        if (isValid)
        {
            border.Stroke = NormalColor;
            border.StrokeThickness = 1;
            if (errorLabel != null) errorLabel.IsVisible = false;
        }
        else if (showVisualError && !string.IsNullOrEmpty(text))
        {
            border.Stroke = DangerColor;
            border.StrokeThickness = 2;
            if (errorLabel != null) errorLabel.IsVisible = true;
        }
        else if (!showVisualError)
        {
            border.Stroke = NormalColor;
            border.StrokeThickness = 1;
            if (errorLabel != null) errorLabel.IsVisible = false;
        }
    }

    // 5. Ostateczna aktywacja przycisku
    private void CheckFormValidity()
    {
        bool isEmailValid = !string.IsNullOrEmpty(EmailEntry.Text) && EmailEntry.Text.Contains("@") && EmailEntry.Text.Contains(".");
        bool isPasswordValid = !string.IsNullOrEmpty(PasswordEntry.Text) && PasswordEntry.Text.Length >= 6;
        bool isConfirmPasswordValid = !string.IsNullOrEmpty(ConfirmPasswordEntry.Text) && ConfirmPasswordEntry.Text == PasswordEntry.Text;
        bool isNameValid = !string.IsNullOrEmpty(NameEntry.Text) && NameEntry.Text.Length >= 3;

        string cleanPhone = PhoneEntry.Text?.Replace(" ", "") ?? "";
        bool isPhoneValid = !string.IsNullOrEmpty(cleanPhone) && cleanPhone.Length >= 9 && cleanPhone.All(char.IsDigit);

        RegisterButton.IsEnabled = isEmailValid && isPasswordValid && isConfirmPasswordValid && isNameValid && isPhoneValid;
    }
    #endregion
}