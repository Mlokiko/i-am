using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Pages.Authentication;

namespace i_am.ViewModels
{
    public partial class PermissionsViewModel : ObservableObject
    {
        // --- POWIADOMIENIA ---
        [ObservableProperty] private bool isNotificationGranted;
        [ObservableProperty] private Color notificationStatusColor = Colors.DodgerBlue;

        // --- KAMERA ---
        [ObservableProperty] private bool isCameraGranted;
        [ObservableProperty] private Color cameraStatusColor = Colors.DodgerBlue;

        // --- PAMIĘĆ / MULTIMEDIA ---
        [ObservableProperty] private bool isStorageGranted;
        [ObservableProperty] private Color storageStatusColor = Colors.DodgerBlue;

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
                statusColor = Colors.Green;
            }
            else if (!hasAsked)
            {
                statusColor = Colors.DodgerBlue;
            }
            else
            {
                statusColor = Colors.Red;
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

        [RelayCommand]
        private async Task FinishOnboardingAsync()
        {
            Preferences.Default.Set("IsFirstLaunch", false);
            await Shell.Current.GoToAsync($"//{nameof(LandingPage)}");
        }
    }
}