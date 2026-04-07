using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using i_am.Models;
using i_am.Services;

namespace i_am.ViewModels
{
    public partial class ManageCareGiversViewModel : ObservableObject
    {
        private readonly IFirestoreService _firestoreService;
        private User? _currentUser;

        private IDisposable? _sentListener;
        private IDisposable? _receivedListener;

        private List<Invitation> _rawSent = new();
        private List<Invitation> _rawReceived = new();

        public ObservableCollection<User> CareGivers { get; } = new();
        public ObservableCollection<Invitation> AllInvitations { get; } = new();

        [ObservableProperty]
        private string inviteEmail = string.Empty;

        public ManageCareGiversViewModel(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
        }

        public async Task InitializeAsync()
        {
            string? myUid = _firestoreService.GetCurrentUserId();
            if (string.IsNullOrEmpty(myUid)) return;

            _currentUser = await _firestoreService.GetUserProfileAsync(myUid);
            await LoadCareGiversAsync();

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

        private async Task LoadCareGiversAsync()
        {
            string? myUid = _firestoreService.GetCurrentUserId();
            if (string.IsNullOrEmpty(myUid)) return;

            var profile = await _firestoreService.GetUserProfileAsync(myUid);
            if (profile != null)
            {
                // Tutaj pobieramy CaregiversID, a nie CaretakersID
                var careGivers = await _firestoreService.GetUsersByIdsAsync(profile.CaregiversID);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CareGivers.Clear();
                    foreach (var cg in careGivers) CareGivers.Add(cg);
                });
            }
        }

        [RelayCommand]
        private async Task InviteAsync()
        {
            if (string.IsNullOrWhiteSpace(InviteEmail))
            {
                await Shell.Current.DisplayAlert("Błąd", "Podaj adres email opiekuna.", "OK");
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
            await LoadCareGiversAsync();
        }

        [RelayCommand]
        private async Task RejectAsync(Invitation inv)
        {
            if (inv != null) await _firestoreService.RejectInvitationAsync(inv);
        }

        [RelayCommand]
        private async Task RemoveCareGiverAsync(User targetUser)
        {
            if (targetUser == null || _currentUser == null) return;

            bool confirm = await Shell.Current.DisplayAlert("Usuń", $"Czy usunąć {targetUser.Name} z listy opiekunów?", "Tak", "Anuluj");
            if (confirm)
            {
                // Zachowujemy Twoją logikę z odwrotną kolejnością dla opiekuna
                await _firestoreService.RemoveAcceptedInvitationAsync(
                      targetUser.Id,     // Opiekun idzie jako pierwszy parametr
                      _currentUser.Id,   // Podopieczny (ja)
                      _currentUser.Id,   // Kto usuwa
                      _currentUser.Name);
                await LoadCareGiversAsync();
            }
        }
    }
}