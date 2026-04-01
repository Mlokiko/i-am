using System.Collections.ObjectModel;
using i_am.Models;
using i_am.Services;

namespace i_am.Pages.CareTaker;

public partial class ManageCareGiversPage : ContentPage
{
    private readonly FirestoreService _firestoreService;
    private User? _currentUser;
    private IDisposable? _sentListener;
    private IDisposable? _receivedListener;

    public ObservableCollection<User> CareGivers { get; set; } = new ObservableCollection<User>();
    public ObservableCollection<Invitation> AllInvitations { get; set; } = new ObservableCollection<Invitation>();
    private List<Invitation> _rawSent = new();
    private List<Invitation> _rawReceived = new();
    public ManageCareGiversPage(FirestoreService firestoreService)
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

        await LoadCareGiversAsync();

        _sentListener = _firestoreService.ListenForSentInvitations(myUid, (freshList) =>
        {
            // FIX: Dodano "Deleted", aby nadawca widzia³, ¿e po³¹czenie zosta³o zakoñczone
            _rawSent = freshList.Where(inv => inv.Status == "Pending" || inv.Status == "Rejected" || inv.Status == "Deleted").ToList();
            foreach (var inv in _rawSent) inv.IsSentByMe = true;

            UpdateUnifiedList();
        });

        _receivedListener = _firestoreService.ListenForReceivedInvitations(myUid, (freshList) =>
        {
            // Usunêliœmy filtr 'IsSenderCaregiver' równie¿ tutaj.
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

    private async Task LoadCareGiversAsync()
    {
        string? myUid = _firestoreService.GetCurrentUserId();
        if (string.IsNullOrEmpty(myUid)) return;
        _currentUser = await _firestoreService.GetUserProfileAsync(myUid);
        if (_currentUser != null)
        {
            CareGivers.Clear();
            var myCareGivers = await _firestoreService.GetUsersByIdsAsync(_currentUser.CaregiversID);
            foreach (var person in myCareGivers) CareGivers.Add(person);
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
            await LoadCareGiversAsync();
        }
    }

    private async void OnRejectClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Invitation inv)
        {
            await _firestoreService.RejectInvitationAsync(inv.Id);
        }
    }

    private async void OnRemoveCareGiverClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is User targetUser)
        {
            bool confirm = await DisplayAlert("Usuñ", $"Czy usun¹æ {targetUser.Name} z listy?", "Tak", "Anuluj");
            if (confirm && _currentUser != null)
            {
                // Odwrotna kolejnoœæ przy usuwaniu opiekuna! (Opiekun idzie jako pierwszy parametr)
                await _firestoreService.RemoveAcceptedInvitationAsync(
                      targetUser.Id,
                      _currentUser.Id,
                      _currentUser.Id,
                      _currentUser.Name);
                await LoadCareGiversAsync();
            }
        }
    }
}