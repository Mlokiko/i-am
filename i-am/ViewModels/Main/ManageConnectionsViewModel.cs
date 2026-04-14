using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Models;
using i_am.Services;

namespace i_am.ViewModels
{
    public partial class ManageConnectionsViewModel : ObservableObject
    {
        private readonly FirestoreService _firestoreService;
        private User? _currentUser;

        private IDisposable? _sentListener;
        private IDisposable? _receivedListener;
        private IDisposable? _userProfileListener;

        private List<Invitation> _rawSent = new();
        private List<Invitation> _rawReceived = new();

        public ObservableCollection<User> Connections { get; } = new();
        public ObservableCollection<Invitation> AllInvitations { get; } = new();

        [ObservableProperty]
        private string inviteEmail = string.Empty;

        // --- DYNAMICZNE TEKSTY DLA UI ---
        [ObservableProperty] private string pageTitle = "Zarządzanie połączeniami";
        [ObservableProperty] private string inviteLabelText = "Zaproś";
        [ObservableProperty] private string invitePlaceholder = "Adres email";
        [ObservableProperty] private string listTitleText = "Moi przypisani użytkownicy";
        [ObservableProperty] private string emptyListText = "Brak połączeń.";

        public ManageConnectionsViewModel(FirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public async Task InitializeAsync()
        {
            string? myUid = _firestoreService.GetCurrentUserId();
            if (string.IsNullOrEmpty(myUid)) return;

            _currentUser = await _firestoreService.GetUserProfileAsync(myUid);

            // Ustawiamy teksty na podstawie roli
            if (_currentUser != null)
            {
                if (_currentUser.IsCaregiver)
                {
                    PageTitle = "Zarządzaj Podopiecznymi";
                    InviteLabelText = "Zaproś Podopiecznego";
                    InvitePlaceholder = "Adres email podopiecznego";
                    ListTitleText = "Moi Podopieczni";
                    EmptyListText = "Nie masz jeszcze żadnych podopiecznych.";
                }
                else
                {
                    PageTitle = "Zarządzaj Opiekunami";
                    InviteLabelText = "Zaproś Opiekuna";
                    InvitePlaceholder = "Adres email opiekuna";
                    ListTitleText = "Moi Opiekunowie";
                    EmptyListText = "Nie masz jeszcze żadnych opiekunów.";
                }
            }

            _userProfileListener = _firestoreService.ListenForUserProfileUpdates(myUid, async (updatedUser) =>
            {
                _currentUser = updatedUser;
                await LoadConnectionsAsync();
            });

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
            _userProfileListener?.Dispose();
        }

        private void UpdateCombinedInvitationsList()
        {
            AllInvitations.Clear();
            var combined = _rawSent.Concat(_rawReceived).OrderByDescending(i => i.CreatedAt).ToList();
            foreach (var inv in combined) AllInvitations.Add(inv);
        }

        private async Task LoadConnectionsAsync()
        {
            if (_currentUser == null) return;

            // Wybieramy odpowiednią listę ID w zależności od roli
            var targetIds = _currentUser.IsCaregiver ? _currentUser.CaretakersID : _currentUser.CaregiversID;

            var connectionsList = await _firestoreService.GetUsersByIdsAsync(targetIds);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Connections.Clear();
                foreach (var user in connectionsList) Connections.Add(user);
            });
        }

        [RelayCommand]
        private async Task InviteAsync()
        {
            if (string.IsNullOrWhiteSpace(InviteEmail))
            {
                await Shell.Current.DisplayAlert("Błąd", "Podaj adres email.", "OK");
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
        }

        [RelayCommand]
        private async Task RejectAsync(Invitation inv)
        {
            if (inv != null) await _firestoreService.RejectInvitationAsync(inv);
        }

        [RelayCommand]
        private async Task RemoveConnectionAsync(User targetUser)
        {
            if (targetUser == null || _currentUser == null) return;

            bool confirm = await Shell.Current.DisplayAlert("Usuń", $"Czy na pewno usunąć użytkownika {targetUser.Name} z listy?", "Tak", "Anuluj");
            if (confirm)
            {
                // FirestoreService.RemoveAcceptedInvitationAsync wymaga kolejności: caregiverId, caretakerId
                string caregiverId = _currentUser.IsCaregiver ? _currentUser.Id : targetUser.Id;
                string caretakerId = _currentUser.IsCaregiver ? targetUser.Id : _currentUser.Id;

                await _firestoreService.RemoveAcceptedInvitationAsync(
                      caregiverId,
                      caretakerId,
                      _currentUser.Id,
                      _currentUser.Name);
            }
        }
    }
}