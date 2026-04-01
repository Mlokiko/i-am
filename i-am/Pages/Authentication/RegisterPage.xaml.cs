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
            // 2. Create the Authentication Account. This returns the unique Firebase UID for this user
            string uid = await _firestoreService.RegisterAsync(EmailEntry.Text, PasswordEntry.Text);

            string fullPhoneNumber = $"{PhonePrefixEntry.Text.Trim()} {PhoneEntry.Text.Trim()}";

            // 3. Build the UserProfile object using the data from the UI
            var userProfile = new User
            {
                // Id is automatically handled by the [FirestoreDocumentId] attribute
                Name = NameEntry.Text.Trim(),
                Email = EmailEntry.Text.Trim(),
                PhoneNumber = fullPhoneNumber,
                BirthDate = BirthdatePicker.Date.ToUniversalTime(),
                Sex = SexPicker.SelectedItem.ToString(),
                IsCaregiver = CaregiverSwitch.IsToggled
                // CreatedAt, CaretakersID, and CaregiversID are automatically set by User model
            };

            Preferences.Default.Set("IsCaregiver", userProfile.IsCaregiver);
            // 4. Save the profile to Firestore under the exact same UID
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
    #endregion
}