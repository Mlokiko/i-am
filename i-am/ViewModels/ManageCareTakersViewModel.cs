using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Models;
using i_am.Services;

namespace i_am.ViewModels
{
    public partial class ManageCareTakersViewModel : ObservableObject
    {
        private readonly IFirestoreService _firestoreService;
        private User? _currentUser;

        private IDisposable? _sentListener;
        private IDisposable? _receivedListener;

        private List<Invitation> _rawSent = new();
        private List<Invitation> _rawReceived = new();

        public ObservableCollection<User> CareTakers { get; } = new();
        public ObservableCollection<Invitation> AllInvitations { get; } = new();

        [ObservableProperty]
        private string inviteEmail = string.Empty;

        public ManageCareTakersViewModel(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public async Task InitializeAsync()
        {
            string? myUid = _firestoreService.GetCurrentUserId();
            if (string.IsNullOrEmpty(myUid)) return;

            _currentUser = await _firestoreService.GetUserProfileAsync(myUid);
            await LoadCareTakersAsync();

            _sentListener = _firestoreService.ListenForSentInvitations(myUid, (freshList) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _rawSent = freshList.Where(inv => inv.Status == "Pending" || inv.Status == "Rejected").ToList();
                    foreach (var inv in _rawSent) inv.IsSentByMe = true;
                    UpdateCombinedInvitationsList();
                });
            });

            _receivedListener = _firestoreService.ListenForReceivedInvitations(myUid, (freshList) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _rawReceived = freshList.Where(inv => inv.Status == "Pending").ToList();
                    foreach (var inv in _rawReceived) inv.IsSentByMe = false;
                    UpdateCombinedInvitationsList();
                });
            });
        }

        public void Cleanup()
        {
            _sentListener?.Dispose();
            _receivedListener?.Dispose();
        }

        private void UpdateCombinedInvitationsList()
        {
            AllInvitations.Clear();
            var combined = _rawSent.Concat(_rawReceived).OrderByDescending(i => i.CreatedAt).ToList();
            foreach (var inv in combined) AllInvitations.Add(inv);
        }

        private async Task LoadCareTakersAsync()
        {
            string? myUid = _firestoreService.GetCurrentUserId();
            if (string.IsNullOrEmpty(myUid)) return;

            var profile = await _firestoreService.GetUserProfileAsync(myUid);
            if (profile != null)
            {
                var careTakers = await _firestoreService.GetUsersByIdsAsync(profile.CaretakersID);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CareTakers.Clear();
                    foreach (var ct in careTakers) CareTakers.Add(ct);
                });
            }
        }

        [RelayCommand]
        private async Task InviteAsync()
        {
            if (string.IsNullOrWhiteSpace(InviteEmail))
            {
                await Shell.Current.DisplayAlert("Błąd", "Podaj adres email podopiecznego.", "OK");
                return;
            }

            if (_currentUser == null) return;

            string targetEmail = InviteEmail.Trim().ToLower();

            if (_currentUser.Email.ToLower() == targetEmail)
            {
                await Shell.Current.DisplayAlert("Błąd", "Nie możesz zaprosić samego siebie.", "OK");
                return;
            }

            try
            {
                // Zgodnie z Twoją nową sygnaturą: (senderId, senderName, isSenderCaregiver, receiverEmail)
                bool success = await _firestoreService.SendInvitationAsync(
                    _currentUser.Id,
                    _currentUser.Name,
                    _currentUser.IsCaregiver,
                    targetEmail);

                if (success)
                {
                    await Shell.Current.DisplayAlert("Sukces", "Zaproszenie zostało wysłane.", "OK");
                    InviteEmail = string.Empty;
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Błąd", ex.Message, "OK");
            }
        }

        [RelayCommand]
        private async Task CancelInvitationAsync(Invitation inv)
        {
            if (inv != null) await _firestoreService.DeleteInvitationPermanentlyAsync(inv.Id);
        }

        [RelayCommand]
        private async Task AcceptAsync(Invitation inv)
        {
            if (inv == null) return;
            await _firestoreService.AcceptInvitationAsync(inv);
            await LoadCareTakersAsync();
        }

        [RelayCommand]
        private async Task RejectAsync(Invitation inv)
        {
            if (inv != null) await _firestoreService.RejectInvitationAsync(inv);
        }

        [RelayCommand]
        private async Task RemoveCareTakerAsync(User targetUser)
        {
            if (targetUser == null || _currentUser == null) return;

            bool confirm = await Shell.Current.DisplayAlert("Usuń", $"Czy usunąć {targetUser.Name} z listy?", "Tak", "Anuluj");
            if (confirm)
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