using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Models;
using i_am.Pages.Main;
using i_am.Services;
using System.Collections.ObjectModel;

namespace i_am.ViewModels
{
    public partial class NotificationsViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private IDisposable? _notificationListener;

        public ObservableCollection<AppNotification> NotificationsList { get; } = new();

        public NotificationsViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public void Initialize()
        {
            string? myUid = Preferences.Get("UserId", string.Empty);
            if (string.IsNullOrEmpty(myUid)) return;

            _notificationListener = _firestoreService.ListenForNotifications(myUid, (freshList) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    NotificationsList.Clear();
                    foreach (var notification in freshList)
                    {
                        NotificationsList.Add(notification);
                    }
                });
            });
        }

        public void Cleanup()
        {
            _notificationListener?.Dispose();
        }

        [RelayCommand]
        private async Task NotificationActionAsync(AppNotification notification)
        {
            if (notification == null) return;

            try
            {
                if (notification.Type == "NewInvitation")
                {
                    await Shell.Current.GoToAsync(nameof(ManageConnectionsPage));

                    // Automatyczne usunięcie powiadomienia po przejściu
                    await _firestoreService.DeleteNotificationAsync(notification.Id);
                }
                else if (notification.Type == "DailyReportAlert")
                {
                    // Budujemy ścieżkę z parametrami (QueryProperties)
                    string route = $"{nameof(CalendarPage)}?CareTakerId={notification.SenderId}&Date={notification.Date}";

                    await Shell.Current.GoToAsync(route);
                    await _firestoreService.DeleteNotificationAsync(notification.Id);
                }
                else if (notification.Type == "InvitationRejected")
                {
                    // Jeśli powiadomienie ma zapisane ID zaproszenia, usuń to zaproszenie z bazy
                    if (!string.IsNullOrEmpty(notification.SenderId))
                    {
                        await _firestoreService.DeleteInvitationAsync(notification.SenderId);
                    }

                    // Następnie standardowo usuń samo powiadomienie
                    await _firestoreService.DeleteNotificationAsync(notification.Id);
                }
                else
                {
                    // Jeśli to zwykłe powiadomienie -> Tylko usuwamy
                    await _firestoreService.DeleteNotificationAsync(notification.Id);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Błąd", $"Wystąpił problem: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        private async Task DismissNotificationAsync(AppNotification notification)
        {
            if (notification == null) return;

            try
            {
                await _firestoreService.DeleteNotificationAsync(notification.Id);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Błąd", $"Nie udało się usunąć: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        private async Task ClearAllNotificationsAsync()
        {
            // Zabezpieczenie przed kliknięciem, gdy lista jest pusta
            if (NotificationsList.Count == 0) return;

            // Opcjonalne okienko z potwierdzeniem (dobra praktyka UX)
            bool isConfirmed = await Shell.Current.DisplayAlert("Wyczyść powiadomienia", "Czy na pewno chcesz usunąć wszystkie powiadomienia?", "Tak", "Nie");
            if (!isConfirmed) return;

            try
            {
                // Wykonanie usunięcia wszystkich powiadomień równolegle dla lepszej wydajności
                var deleteTasks = NotificationsList.Select(n => _firestoreService.DeleteNotificationAsync(n.Id)).ToList();
                await Task.WhenAll(deleteTasks);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Błąd", $"Nie udało się wyczyścić powiadomień: {ex.Message}", "OK");
            }
        }
    }
}