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

    // ObservableCollection zapewnia ¿e UI siê samo aktualizuje
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
            _rawSent = freshList.Where(inv => inv.Status == "Pending" || inv.Status == "Rejected").ToList();
            foreach (var inv in _rawSent) inv.IsSentByMe = true;

            UpdateUnifiedList();
        });

        _receivedListener = _firestoreService.ListenForReceivedInvitations(myUid, (freshList) =>
        {
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

    private async void OnCancelInvitationClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Invitation inv)
        {
            await _firestoreService.DeleteInvitationPermanentlyAsync(inv.Id);
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
            await _firestoreService.RejectInvitationAsync(inv);
        }
    }

    private async void OnRemoveCareTakerClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is User targetUser)
        {
            bool confirm = await DisplayAlert("Usuñ", $"Czy usun¹æ {targetUser.Name} z listy?", "Tak", "Anuluj");
            if (confirm && _currentUser != null)
            {
                await _firestoreService.RemoveAcceptedInvitationAsync(
                      _currentUser.Id,
                      targetUser.Id,
                      _currentUser.Id,
                      _currentUser.Name);
                await LoadCareTakersAsync();
            }
        }
    }
}