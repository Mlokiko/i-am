using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Pages.Authentication;

namespace i_am.ViewModels
{
    public partial class PermissionsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isNotificationGranted;

        // Domyślny kolor to niebieski (standardowy stan przed zapytaniem)
        [ObservableProperty]
        private Color notificationStatusColor = Colors.DodgerBlue;

        public async Task CheckCurrentPermissionsAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            UpdateUI(status);
        }

        [RelayCommand]
        private async Task RequestNotificationsAsync()
        {
            // Zapisujemy, że użytkownik właśnie podjął próbę zapytania o uprawnienia
            Preferences.Default.Set("HasAskedForNotifications", true);

            var status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            UpdateUI(status);
        }

        private void UpdateUI(PermissionStatus status)
        {
            IsNotificationGranted = status == PermissionStatus.Granted;

            // Sprawdzamy, czy aplikacja kiedykolwiek zapytała o uprawnienia
            bool hasAsked = Preferences.Default.Get("HasAskedForNotifications", false);

            if (status == PermissionStatus.Granted)
            {
                // Przyznano -> Zielony
                NotificationStatusColor = Colors.Green;
            }
            else if (!hasAsked)
            {
                // Jeszcze nie kliknięto "Zezwól" -> Wymuszamy Niebieski
                NotificationStatusColor = Colors.DodgerBlue;
            }
            else
            {
                // Zapytano, ale odrzucono lub zablokowano -> Czerwony
                NotificationStatusColor = Colors.Red;
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