using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Pages.Authentication;

namespace i_am.ViewModels
{
    public partial class PermissionsViewModel : ObservableObject
    {
        // --- POWIADOMIENIA ---
        [ObservableProperty] private bool isNotificationGranted;
        [ObservableProperty] private Color notificationStatusColor;

        // --- KAMERA ---
        [ObservableProperty] private bool isCameraGranted;
        [ObservableProperty] private Color cameraStatusColor;

        // --- PAMIĘĆ / MULTIMEDIA ---
        [ObservableProperty] private bool isStorageGranted;
        [ObservableProperty] private Color storageStatusColor;

        public PermissionsViewModel()
        {
            // Ustawienie początkowych kolorów na podstawie domyślnego stanu ("DodgerBlue")
            notificationStatusColor = GetThemeColor("Primary", "PrimaryDark");
            cameraStatusColor = GetThemeColor("Primary", "PrimaryDark");
            storageStatusColor = GetThemeColor("Primary", "PrimaryDark");
        }

        public async Task CheckCurrentPermissionsAsync()
        {
            var notifStatus = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            UpdateUI(notifStatus, "Notifications");

            var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            UpdateUI(cameraStatus, "Camera");

            // W zależności od wersji Androida (13+) może tu być wymagane Permissions.Photos
            // Domyślnie używamy StorageRead do odczytu multimediów
            var storageStatus = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
            UpdateUI(storageStatus, "Storage");
        }

        [RelayCommand]
        private async Task RequestNotificationsAsync()
        {
            Preferences.Default.Set("HasAskedForNotifications", true);
            var status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            UpdateUI(status, "Notifications");
        }

        [RelayCommand]
        private async Task RequestCameraAsync()
        {
            Preferences.Default.Set("HasAskedForCamera", true);
            var status = await Permissions.RequestAsync<Permissions.Camera>();
            UpdateUI(status, "Camera");
        }

        [RelayCommand]
        private async Task RequestStorageAsync()
        {
            Preferences.Default.Set("HasAskedForStorage", true);
            var status = await Permissions.RequestAsync<Permissions.StorageRead>();
            UpdateUI(status, "Storage");
        }

        // Zunifikowana metoda aktualizacji UI
        private void UpdateUI(PermissionStatus status, string type)
        {
            bool isGranted = status == PermissionStatus.Granted;
            Color statusColor;

            bool hasAsked = type switch
            {
                "Notifications" => Preferences.Default.Get("HasAskedForNotifications", false),
                "Camera" => Preferences.Default.Get("HasAskedForCamera", false),
                "Storage" => Preferences.Default.Get("HasAskedForStorage", false),
                _ => false
            };

            if (isGranted)
            {
                // Zastępuje sztywne Colors.Green
                statusColor = GetThemeColor("SuccessLight", "SuccessDark");
            }
            else if (!hasAsked)
            {
                // Zastępuje sztywne Colors.DodgerBlue
                statusColor = GetThemeColor("Primary", "PrimaryDark");
            }
            else
            {
                // Zastępuje sztywne Colors.Red
                statusColor = GetThemeColor("DangerLight", "DangerDark");
            }

            switch (type)
            {
                case "Notifications":
                    IsNotificationGranted = isGranted;
                    NotificationStatusColor = statusColor;
                    break;
                case "Camera":
                    IsCameraGranted = isGranted;
                    CameraStatusColor = statusColor;
                    break;
                case "Storage":
                    IsStorageGranted = isGranted;
                    StorageStatusColor = statusColor;
                    break;
            }
        }

        // Metoda pomocnicza do pobierania kolorów zdefiniowanych w Colors.xaml
        private Color GetThemeColor(string lightResourceKey, string darkResourceKey)
        {
            // Sprawdza aktualny motyw, jeżeli nie jest dostępny używa domyślnie jasnego
            var currentTheme = Application.Current?.RequestedTheme ?? AppTheme.Light;
            string targetKey = currentTheme == AppTheme.Dark ? darkResourceKey : lightResourceKey;

            if (Application.Current?.Resources.TryGetValue(targetKey, out var resourceValue) == true && resourceValue is Color color)
            {
                return color;
            }

            return Colors.Transparent; // Bezpieczny fallback na wypadek braku klucza w słowniku
        }

        [RelayCommand]
        private async Task FinishOnboardingAsync()
        {
            Preferences.Default.Set("IsFirstLaunch", false);
            await Shell.Current.GoToAsync($"//{nameof(LandingPage)}");
        }
    }
}