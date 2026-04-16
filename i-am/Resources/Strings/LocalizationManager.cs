using System.Collections.ObjectModel;
using System.Globalization;
using System.Resources;

namespace i_am.Resources.Strings
{
    public static class LocalizationManager
    {
        private static ResourceManager? _resourceManager;
        private static string _currentLanguage = "pl";
        private static CultureInfo _currentCulture = new("pl-PL");

        public static event EventHandler? LanguageChanged;

        public static void Initialize(string language = "pl")
        {
            _currentLanguage = language;
            _currentCulture = new CultureInfo(language == "en" ? "en-US" : "pl-PL");
            _resourceManager = new ResourceManager("i_am.Resources.Strings.AppStrings", typeof(LocalizationManager).Assembly);
        }

        public static void SetLanguage(string language)
        {
            if (_currentLanguage == language)
                return;

            _currentLanguage = language;
            _currentCulture = new CultureInfo(language == "en" ? "en-US" : "pl-PL");

            Preferences.Default.Set("CurrentLanguage", language);
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        public static string GetString(string key)
        {
            if (_resourceManager == null)
                Initialize();

            try
            {
                var value = _resourceManager?.GetString(key, _currentCulture);
                return value ?? $"[{key}]";
            }
            catch
            {
                return $"[{key}]";
            }
        }

        public static string CurrentLanguage => _currentLanguage;

        public static CultureInfo CurrentCulture => _currentCulture;

        // Property accessors for common strings
        public static string AppName => GetString("AppName");
        public static string Loading => GetString("Loading");
        public static string Cancel => GetString("Cancel");
        public static string OK => GetString("OK");
        public static string Save => GetString("Save");
        public static string Delete => GetString("Delete");
        public static string Edit => GetString("Edit");
        public static string Error => GetString("Error");
        public static string Success => GetString("Success");
        public static string Understand => GetString("Understand");

        // Authentication
        public static string Auth_FillAllFields => GetString("Auth_FillAllFields");
        public static string Auth_NoConnection => GetString("Auth_NoConnection");
        public static string Auth_NoConnectionMessage => GetString("Auth_NoConnectionMessage");
        public static string Auth_LoginFailed => GetString("Auth_LoginFailed");
        public static string Auth_LogoutConfirm => GetString("Auth_LogoutConfirm");
        public static string Auth_LogoutTitle => GetString("Auth_LogoutTitle");
        public static string Auth_Yes => GetString("Auth_Yes");
        public static string Auth_No => GetString("Auth_No");
        public static string Auth_LogoutError => GetString("Auth_LogoutError");
        public static string Auth_SelectGender => GetString("Auth_SelectGender");
        public static string Auth_WeakPassword => GetString("Auth_WeakPassword");
        public static string Auth_PasswordRequirements => GetString("Auth_PasswordRequirements");
        public static string Auth_InvalidBirthdate => GetString("Auth_InvalidBirthdate");
        public static string Auth_InvalidAge => GetString("Auth_InvalidAge");
        public static string Auth_SuccessAccount => GetString("Auth_SuccessAccount");
        public static string Auth_RegistrationFailed => GetString("Auth_RegistrationFailed");

        // Info Dialogs
        public static string Info_Password => GetString("Info_Password");
        public static string Info_PasswordMessage => GetString("Info_PasswordMessage");
        public static string Info_Name => GetString("Info_Name");
        public static string Info_NameMessage => GetString("Info_NameMessage");
        public static string Info_Phone => GetString("Info_Phone");
        public static string Info_PhoneMessage => GetString("Info_PhoneMessage");
        public static string Info_Birthdate => GetString("Info_Birthdate");
        public static string Info_BirthdateMessage => GetString("Info_BirthdateMessage");
        public static string Info_Gender => GetString("Info_Gender");
        public static string Info_GenderMessage => GetString("Info_GenderMessage");

        // Settings
        public static string Settings_Title => GetString("Settings_Title");
        public static string Settings_AppearanceAndLanguage => GetString("Settings_AppearanceAndLanguage");
        public static string Settings_Theme => GetString("Settings_Theme");
        public static string Settings_ThemeDesc => GetString("Settings_ThemeDesc");
        public static string Settings_Language => GetString("Settings_Language");
        public static string Settings_LanguageDesc => GetString("Settings_LanguageDesc");
        public static string Settings_TimeAndReports => GetString("Settings_TimeAndReports");
        public static string Settings_DayStart => GetString("Settings_DayStart");
        public static string Settings_DayStartDesc => GetString("Settings_DayStartDesc");
        public static string Settings_RestrictActivityTime => GetString("Settings_RestrictActivityTime");
        public static string Settings_RestrictActivityTimeDesc => GetString("Settings_RestrictActivityTimeDesc");
        public static string Settings_TimeWindow => GetString("Settings_TimeWindow");
        public static string Settings_From => GetString("Settings_From");
        public static string Settings_To => GetString("Settings_To");
        public static string Settings_Notifications => GetString("Settings_Notifications");
        public static string Settings_PushNotifications => GetString("Settings_PushNotifications");
        public static string Settings_PushNotificationsDesc => GetString("Settings_PushNotificationsDesc");
        public static string Settings_SystemPermissions => GetString("Settings_SystemPermissions");
        public static string Settings_ManagePermissions => GetString("Settings_ManagePermissions");
        public static string Settings_ManagePermissionsDesc => GetString("Settings_ManagePermissionsDesc");
        public static string Settings_Open => GetString("Settings_Open");
        public static string Settings_Save => GetString("Settings_Save");
        public static string Settings_SavedSuccessfully => GetString("Settings_SavedSuccessfully");
        public static string Settings_SaveFailedCloud => GetString("Settings_SaveFailedCloud");
        public static string Settings_NotificationsError => GetString("Settings_NotificationsError");

        // Landing Page
        public static string Landing_Login => GetString("Landing_Login");
        public static string Landing_Register => GetString("Landing_Register");

        // Loading Page
        public static string Loading_Title => GetString("Loading_Title");

        // Login Page
        public static string Login_Title => GetString("Login_Title");
        public static string Login_Email => GetString("Login_Email");
        public static string Login_Password => GetString("Login_Password");
        public static string Login_Button => GetString("Login_Button");
        public static string Login_NoAccount => GetString("Login_NoAccount");
        public static string Login_SignUp => GetString("Login_SignUp");

        // Register Page
        public static string Register_Title => GetString("Register_Title");
        public static string Register_Email => GetString("Register_Email");
        public static string Register_EmailPlaceholder => GetString("Register_EmailPlaceholder");
        public static string Register_InvalidEmail => GetString("Register_InvalidEmail");
        public static string Register_Password => GetString("Register_Password");
        public static string Register_PasswordPlaceholder => GetString("Register_PasswordPlaceholder");
        public static string Register_WeakPassword => GetString("Register_WeakPassword");
        public static string Register_Show => GetString("Register_Show");
        public static string Register_Hide => GetString("Register_Hide");
        public static string Register_ConfirmPassword => GetString("Register_ConfirmPassword");
        public static string Register_Name => GetString("Register_Name");
        public static string Register_Phone => GetString("Register_Phone");
        public static string Register_PhonePlaceholder => GetString("Register_PhonePlaceholder");
        public static string Register_Birthdate => GetString("Register_Birthdate");
        public static string Register_Gender => GetString("Register_Gender");
        public static string Register_IsCareGiver => GetString("Register_IsCareGiver");
        public static string Register_Button => GetString("Register_Button");
        public static string Register_AlreadyHaveAccount => GetString("Register_AlreadyHaveAccount");
        public static string Register_BackToLogin => GetString("Register_BackToLogin");

        // Permissions Page
        public static string Permissions_Title => GetString("Permissions_Title");
        public static string Permissions_Description => GetString("Permissions_Description");
        public static string Permissions_Notifications => GetString("Permissions_Notifications");
        public static string Permissions_NotificationsDesc => GetString("Permissions_NotificationsDesc");
        public static string Permissions_Allow => GetString("Permissions_Allow");
        public static string Permissions_Granted => GetString("Permissions_Granted");
        public static string Permissions_Denied => GetString("Permissions_Denied");
        public static string Permissions_Continue => GetString("Permissions_Continue");

        // CareGiver Main Page
        public static string CareGiver_Notifications => GetString("CareGiver_Notifications");
        public static string CareGiver_Calendar => GetString("CareGiver_Calendar");
        public static string CareGiver_Statistics => GetString("CareGiver_Statistics");
        public static string CareGiver_EditQuestions => GetString("CareGiver_EditQuestions");
        public static string CareGiver_ManageDependents => GetString("CareGiver_ManageDependents");
        public static string CareGiver_Settings => GetString("CareGiver_Settings");
        public static string CareGiver_ManageAccount => GetString("CareGiver_ManageAccount");
        public static string CareGiver_Logout => GetString("CareGiver_Logout");
        public static string CareGiver_EmergencyInfo => GetString("CareGiver_EmergencyInfo");

        // CareTaker Main Page
        public static string CareTaker_Notifications => GetString("CareTaker_Notifications");
        public static string CareTaker_DailyActivity => GetString("CareTaker_DailyActivity");
        public static string CareTaker_Calendar => GetString("CareTaker_Calendar");
        public static string CareTaker_ManageCareGivers => GetString("CareTaker_ManageCareGivers");
        public static string CareTaker_Settings => GetString("CareTaker_Settings");
        public static string CareTaker_ManageAccount => GetString("CareTaker_ManageAccount");
        public static string CareTaker_Logout => GetString("CareTaker_Logout");
        public static string CareTaker_EmergencyInfo => GetString("CareTaker_EmergencyInfo");
    }
}
