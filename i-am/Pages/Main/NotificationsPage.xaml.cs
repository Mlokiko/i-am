using System.Collections.ObjectModel;
using i_am.Models;
using i_am.Services;

namespace i_am.Pages.Main;

public partial class NotificationsPage : ContentPage
{
    private readonly FirestoreService _firestoreService;
    private IDisposable? _notificationListener;

    public ObservableCollection<AppNotification> NotificationsList { get; set; } = new ObservableCollection<AppNotification>();

    public NotificationsPage(FirestoreService firestoreService)
    {
        InitializeComponent();
        _firestoreService = firestoreService;
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        string? myUid = _firestoreService.GetCurrentUserId();
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _notificationListener?.Dispose();
    }

    private async void OnNotificationActionClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is AppNotification notification)
        {
            try
            {
                // Jeœli to jest powiadomienie o nowym zaproszeniu -> Przenosimy u¿ytkownika
                if (notification.Type == "NewInvitation")
                {
                    bool isCaregiver = Preferences.Default.Get("IsCaregiver", false);

                    // Nawigacja na podstawie roli
                    if (isCaregiver)
                        await Shell.Current.GoToAsync(nameof(Pages.CareGiver.ManageCareTakersPage));
                    else
                        await Shell.Current.GoToAsync(nameof(Pages.CareTaker.ManageCareGiversPage));

                    // Opcjonalnie: automatyczne usuniêcie powiadomienia po klikniêciu
                    await _firestoreService.DeleteNotificationAsync(notification.Id);
                }
                else
                {
                    // Jeœli to zwyk³e powiadomienie (np. ConnectionDeleted) -> Tylko usuwamy
                    await _firestoreService.DeleteNotificationAsync(notification.Id);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("B³¹d", $"Wyst¹pi³ problem: {ex.Message}", "OK");
            }
        }
    }

    private async void OnDismissNotificationClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is AppNotification notification)
        {
            try
            {
                await _firestoreService.DeleteNotificationAsync(notification.Id);
            }
            catch (Exception ex)
            {
                await DisplayAlert("B³¹d", $"Nie uda³o siê usun¹æ: {ex.Message}", "OK");
            }
        }
    }
}