using System.Collections.ObjectModel;
using i_am.Models;
using i_am.Services;

namespace i_am.Pages.Main;

public partial class NotificationsPage : ContentPage
{
    private readonly FirestoreService _firestoreService; // Zmieñ na nazwê Twojego po³¹czonego serwisu
    private IDisposable? _notificationListener;

    // ObservableCollection automatycznie odœwie¿a interfejs, gdy lista siê zmienia!
    public ObservableCollection<Invitation> Invitations { get; set; } = new ObservableCollection<Invitation>();

    public NotificationsPage(FirestoreService firestoreService)
    {
        InitializeComponent();
        _firestoreService = firestoreService;

        // Wa¿ne: to mówi stronie, ¿eby szuka³a zmiennej "Invitations" w tym pliku
        BindingContext = this;
    }

    // Odpala siê zawsze, gdy u¿ytkownik WCHODZI na tê stronê
    protected override void OnAppearing()
    {
        base.OnAppearing();

        string? myUid = _firestoreService.GetCurrentUserId();
        if (string.IsNullOrEmpty(myUid)) return;

        // Uruchamiamy nas³uchiwanie w czasie rzeczywistym
        _notificationListener = _firestoreService.ListenForReceivedInvitations(myUid, (freshList) =>
        {
            // Poniewa¿ nas³uchiwacz dzia³a w tle, musimy zaktualizowaæ interfejs na G³ównym W¹tku (MainThread)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Invitations.Clear();

                // Filtrujemy, ¿eby pokazywaæ tylko te oczekuj¹ce (Pending) na akcjê
                var pendingRequests = freshList.Where(inv => inv.Status == "Pending" || inv.Status == "Rejected" || inv.Status == "Deleted").ToList();

                foreach (var inv in pendingRequests)
                {
                    Invitations.Add(inv);
                }
            });
        });
    }
    private async void OnAcknowledgeDeletedClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Invitation invitation)
        {
            try
            {
                // To trwale usunie dokument z bazy i zniknie on z ekranu
                await _firestoreService.DeleteInvitationPermanentlyAsync(invitation.Id);
            }
            catch (Exception ex)
            {
                await DisplayAlert("B³¹d", $"Nie uda³o siê usun¹æ powiadomienia: {ex.Message}", "OK");
            }
        }
    }

    // Odpala siê zawsze, gdy u¿ytkownik WYCHODZI z tej strony (lub j¹ cofa)
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // WY£¥CZAMY nas³uchiwacz, aby oszczêdzaæ bateriê i pamiêæ telefonu
        _notificationListener?.Dispose();
        _notificationListener = null;
    }

    // Obs³uga klikniêcia "Akceptuj"
    private async void OnAcceptClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Invitation invitation)
        {
            try
            {
                // Wyœwietlamy "krêcio³ek" na przycisku lub blokujemy interfejs, jeœli chcesz
                await _firestoreService.AcceptInvitationAsync(invitation);
                await DisplayAlert("Sukces", $"Zaakceptowano zaproszenie od {invitation.SenderName}!", "OK");

                // Zauwa¿: Nie musimy usuwaæ go z listy rêcznie! 
                // Skrypt w bazie zmieni status, nas³uchiwacz to wykryje i SAM zaktualizuje listê natychmiast!
            }
            catch (Exception ex)
            {
                await DisplayAlert("B³¹d", $"Nie uda³o siê zaakceptowaæ: {ex.Message}", "OK");
            }
        }
    }

    // Obs³uga klikniêcia "Odrzuæ"
    private async void OnRejectClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Invitation invitation)
        {
            bool confirm = await DisplayAlert("Odrzuæ", $"Czy na pewno chcesz odrzuciæ zaproszenie od {invitation.SenderName}?", "Tak", "Anuluj");

            if (confirm)
            {
                try
                {
                    await _firestoreService.RejectInvitationAsync(invitation.Id);
                    // Podobnie jak wy¿ej, usuniêcie z ekranu nast¹pi automatycznie przez Listener
                }
                catch (Exception ex)
                {
                    await DisplayAlert("B³¹d", $"Nie uda³o siê odrzuciæ: {ex.Message}", "OK");
                }
            }
        }
    }
}