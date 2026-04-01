using System.Collections.ObjectModel;
using i_am.Models;
using i_am.Services;

namespace i_am.Pages.CareGiver;

public partial class ManageCareTakersPage : ContentPage
{
    private readonly FirestoreService _firestoreService;
    private User? _currentUser;
    private IDisposable? _sentListener;
    private IDisposable? _receivedListener;

    // ObservableCollection ensures the UI updates automatically
    public ObservableCollection<User> CareTakers { get; set; } = new ObservableCollection<User>();
    public ObservableCollection<Invitation> AllInvitations { get; set; } = new ObservableCollection<Invitation>();
    private List<Invitation> _rawSent = new();
    private List<Invitation> _rawReceived = new();
    public ManageCareTakersPage(FirestoreService firestoreService)
    {
        InitializeComponent();
        _firestoreService = firestoreService;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        string? myUid = _firestoreService.GetCurrentUserId();
        if (string.IsNullOrEmpty(myUid)) return;

        await LoadCareTakersAsync();

        _sentListener = _firestoreService.ListenForSentInvitations(myUid, (freshList) =>
        {
            // FIX: Dodano "Deleted", aby nadawca widzia³, ¿e po³¹czenie zosta³o zakoñczone
            _rawSent = freshList.Where(inv => inv.Status == "Pending" || inv.Status == "Rejected" || inv.Status == "Deleted").ToList();
            foreach (var inv in _rawSent) inv.IsSentByMe = true;

            UpdateUnifiedList();
        });

        _receivedListener = _firestoreService.ListenForReceivedInvitations(myUid, (freshList) =>
        {
            // Usunêliœmy filtr 'IsSenderCaregiver' ca³kowicie! 
            // Teraz aplikacja po prostu pobierze KA¯DE zaproszenie, które do Ciebie przysz³o i ma status "Pending".
            _rawReceived = freshList.Where(inv => inv.Status == "Pending").ToList();

            foreach (var inv in _rawReceived) inv.IsSentByMe = false;

            UpdateUnifiedList();
        });
    }
    private void UpdateUnifiedList()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            AllInvitations.Clear();

            // £¹czymy obie listy w jedn¹
            var combined = _rawSent.Concat(_rawReceived).ToList();

            foreach (var inv in combined)
            {
                AllInvitations.Add(inv);
            }
        });
    }
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _sentListener?.Dispose(); _sentListener = null;
        _receivedListener?.Dispose(); _receivedListener = null;
    }

    private async Task LoadCareTakersAsync()
    {
        string? myUid = _firestoreService.GetCurrentUserId();
        if (string.IsNullOrEmpty(myUid)) return;
        _currentUser = await _firestoreService.GetUserProfileAsync(myUid);
        if (_currentUser != null)
        {
            CareTakers.Clear();
            var myCareTakers = await _firestoreService.GetUsersByIdsAsync(_currentUser.CaretakersID);
            foreach (var person in myCareTakers) CareTakers.Add(person);
        }
    }

    private async void OnSendInviteClicked(object sender, EventArgs e)
    {
        string email = EmailEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || _currentUser == null) return;
        try
        {
            if (sender is Button btn) btn.IsEnabled = false;
            await _firestoreService.SendInvitationAsync(_currentUser.Id, _currentUser.Name, _currentUser.IsCaregiver, email);
            EmailEntry.Text = string.Empty;
        }
        catch (Exception ex) { await DisplayAlert("B³¹d", ex.Message, "OK"); }
        finally { if (sender is Button btn) btn.IsEnabled = true; }
    }
    // NOWE: Anulowanie wys³anego zaproszenia
    private async void OnCancelInvitationClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Invitation inv)
        {
            if (inv.Status == "Pending") await _firestoreService.MarkInvitationAsDeletedAsync(inv.Id);
            else await _firestoreService.DeleteInvitationPermanentlyAsync(inv.Id);
        }
    }
    private async void OnAcceptClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Invitation inv)
        {
            await _firestoreService.AcceptInvitationAsync(inv);
            await LoadCareTakersAsync();
        }
    }
    private async void OnRejectClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Invitation inv)
        {
            await _firestoreService.RejectInvitationAsync(inv.Id);
        }
    }
    //private async void OnAcknowledgeDeletedClicked(object sender, EventArgs e)
    //{
    //    if (sender is Button btn && btn.CommandParameter is Invitation invitation)
    //    {
    //        try
    //        {
    //            await _firestoreService.DeleteInvitationPermanentlyAsync(invitation.Id);
    //        }
    //        catch (Exception ex)
    //        {
    //            await DisplayAlert("B³¹d", $"Nie uda³o siê usun¹æ powiadomienia: {ex.Message}", "OK");
    //        }
    //    }
    //}
    // NOWE: Usuwanie aktywnego podopiecznego
    private async void OnRemoveCareTakerClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is User targetUser)
        {
            bool confirm = await DisplayAlert("Usuñ", $"Czy usun¹æ {targetUser.Name} z listy?", "Tak", "Anuluj");
            if (confirm && _currentUser != null)
            {
                await _firestoreService.RemoveAcceptedInvitationAsync(_currentUser.Id, targetUser.Id);
                await LoadCareTakersAsync();
            }
        }
    }
}