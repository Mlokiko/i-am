using System.Collections.ObjectModel;
using i_am.Models;
using i_am.Services;

namespace i_am.Pages.Main;

public partial class NotificationsPage : ContentPage
{
    private readonly FirestoreService _firestoreService;
    private IDisposable? _notificationListener;

    // Zmieniono typ z Invitation na AppNotification
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

        // U¿ywamy nowej metody nas³uchuj¹cej powiadomieñ
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

    // Nowa metoda do usuwania/odczytywania powiadomieñ
    private async void OnDeleteNotificationClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is AppNotification notification)
        {
            try
            {
                // Usuwa powiadomienie z bazy (Listener zaktualizuje listê automatycznie)
                await _firestoreService.DeleteNotificationAsync(notification.Id);
            }
            catch (Exception ex)
            {
                await DisplayAlert("B³¹d", $"Nie uda³o siê usun¹æ powiadomienia: {ex.Message}", "OK");
            }
        }
    }
}