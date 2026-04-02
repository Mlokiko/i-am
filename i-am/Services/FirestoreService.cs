using Android.App.Admin;
using Android.Telecom;
using i_am.Models;
using Plugin.Firebase.Auth;
using Plugin.Firebase.CloudMessaging;
using Plugin.Firebase.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace i_am.Services
{
    public class FirestoreService
    {
        #region User Management

        public async Task<string> RegisterAsync(string email, string password)
        {
            var user = await CrossFirebaseAuth.Current.CreateUserAsync(email, password);
            return user.Uid;
        }

        // wbudowana metoda w plugin nie działa prawidłowo... logowanie nie powinno tworzyć user (sic!), dlatego używam tutaj "własnego" kodu - gada z SDK Firebase bezpośrednio
        //public async Task<string> LoginAsync(string email, string password)
        //{
        //    var user = await CrossFirebaseAuth.Current.SignInWithEmailAndPasswordAsync(email, password);
        //    return user.Uid;
        //}
        public async Task<string> LoginAsync(string email, string password)
        {
#if ANDROID
            var result = await Firebase.Auth.FirebaseAuth.Instance.SignInWithEmailAndPasswordAsync(email, password);
            return result.User.Uid;
#elif IOS
    var result = await Firebase.Auth.Auth.DefaultInstance.SignInWithEmailAndPasswordAsync(email, password);
    return result.User.Uid;
#else
    // Fallback just in case (e.g. Windows)
    var user = await CrossFirebaseAuth.Current.SignInWithEmailAndPasswordAsync(email, password);
    return user.Uid;
#endif
        }

        public async Task SignOutAsync()
        {
            await CrossFirebaseAuth.Current.SignOutAsync();
        }

        public bool IsUserLoggedIn()
        {
            return CrossFirebaseAuth.Current.CurrentUser != null;
        }

        public string? GetCurrentUserId()
        {
            return CrossFirebaseAuth.Current.CurrentUser?.Uid;
        }

        // Po rejestracji, tworzymy profil użytkownika w Firestore
        public async Task CreateUserProfileAsync(string uid, User profile)
        {
            var firestore = CrossFirebaseFirestore.Current;

            await firestore.GetCollection("users").GetDocument(uid).SetDataAsync(profile);
        }

        // Potrzebne do odczytania czy użytkownik jest opiekunem czy podopiecznym, żeby odpowiednio przekierować go do właściwego widoku
        public async Task<User?> GetUserProfileAsync(string uid)
        {
            var firestore = CrossFirebaseFirestore.Current;

            var snapshot = await firestore.GetCollection("users")
                                          .GetDocument(uid)
                                          .GetDocumentSnapshotAsync<User>();

            // Plugin zwraca null jeśli dokument nie istnieje
            return snapshot.Data;
        }

        public async Task DeleteAccountAndProfileAsync()
        {
            string? uid = CrossFirebaseAuth.Current.CurrentUser?.Uid;
            if (string.IsNullOrEmpty(uid)) return;

            // 1. Delete the Firestore Document
            await CrossFirebaseFirestore.Current
                .GetCollection("users")
                .GetDocument(uid)
                .DeleteDocumentAsync();

            // Plugin od firebase nie posiada metody natywnej do usuwania konta, więc musimy użyć natywnych SDK Firebase dla Androida i iOS
#if ANDROID
            var androidUser = Firebase.Auth.FirebaseAuth.Instance.CurrentUser;
            if (androidUser != null)
            {
                await androidUser.DeleteAsync();
            }
#elif IOS
        var iosUser = Firebase.Auth.Auth.DefaultInstance.CurrentUser;
        if (iosUser != null)
        {
            await iosUser.DeleteAsync();
        }
#endif
        }

        // Token odpowiedzialny za powiadomienia push
        // Każde logowanie powinno aktualizować token FCM, żeby mieć pewność, że powiadomienia push będą docierać do właściwego urządzenia (np. jeśli ktoś się zaloguje na nowym telefonie, stary token przestaje być ważny)
        public async Task UpdateFcmTokenAsync()
        {
            string? uid = GetCurrentUserId();
            if (string.IsNullOrEmpty(uid)) return;

            try
            {
                // 1. Sprawdzenie i prośba o uprawnienia (Systemowe okienko)
                var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();

                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.PostNotifications>();
                }

                if (status != PermissionStatus.Granted)
                {
                    Console.WriteLine("[FCM] Użytkownik odmówił uprawnień do powiadomień.");
                    return;
                }

                await CrossFirebaseCloudMessaging.Current.CheckIfValidAsync();

                var token = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();

                if (!string.IsNullOrEmpty(token))
                {
                    await CrossFirebaseFirestore.Current
                        .GetCollection("users")
                        .GetDocument(uid)
                        .UpdateDataAsync(("fcmToken", token));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FCM Error] Błąd podczas pobierania tokenu: {ex.Message}");
            }
        }



        #endregion
        #region Notifications

        public async Task SendNotificationAsync(AppNotification notification)
        {
            var firestore = CrossFirebaseFirestore.Current;
            await firestore.GetCollection("notifications").AddDocumentAsync(notification);
        }

        // Nasłuchiwanie powiadomień w czasie rzeczywistym
        public IDisposable ListenForNotifications(string myUid, Action<List<AppNotification>> onUpdate)
        {
            var firestore = CrossFirebaseFirestore.Current;

            return firestore.GetCollection("notifications")
                .WhereEqualsTo("receiverId", myUid)
                .AddSnapshotListener<AppNotification>((snapshot) =>
                {
                    // Opcjonalnie: posortuj najnowsze na górze
                    var notifications = snapshot.Documents
                                                .Select(doc => doc.Data)
                                                .OrderByDescending(n => n.CreatedAt)
                                                .ToList();
                    onUpdate?.Invoke(notifications);
                });
        }

        // Usuwanie odczytanego powiadomienia
        public async Task DeleteNotificationAsync(string notificationId)
        {
            var firestore = CrossFirebaseFirestore.Current;
            await firestore.GetCollection("notifications")
                           .GetDocument(notificationId)
                           .DeleteDocumentAsync();
        }
        #endregion
        #region Zaproszenia (Invitations)
        public async Task<bool> SendInvitationAsync(string senderId, string senderName, bool isSenderCaregiver, string receiverEmail)
        {
            var firestore = CrossFirebaseFirestore.Current;

            // Szukanie poprzez email
            var usersQuery = await firestore.GetCollection("users")
                                            .WhereEqualsTo("email", receiverEmail.ToLower())
                                            .GetDocumentsAsync<User>();

            var receiver = usersQuery.Documents.FirstOrDefault()?.Data;

            if (receiver == null)
            {
                throw new Exception("Użytkownik z podanym adresem email nie został znaleziony.");
            }

            if (receiver.Id == senderId)
            {
                throw new Exception("Nie możesz wysłać zaproszenia do samego siebie.");
            }

            // Sprawdzanie dubli - czy zostało już wysłane takie zaproszenie?
            var existingSent = await firestore.GetCollection("invitations")
                                              .WhereEqualsTo("senderId", senderId)
                                              .WhereEqualsTo("receiverId", receiver.Id)
                                              .GetDocumentsAsync<Invitation>();

            if (existingSent.Documents.Any())
            {
                throw new Exception("Zaproszenie do tego użytkownika zostało już wysłane lub odrzucone. Usuń stare zaproszenie, aby wysłać nowe.");
            }

            // Czy druga strona już mi wysłała zaproszenie?
            var existingReceived = await firestore.GetCollection("invitations")
                                                  .WhereEqualsTo("senderId", receiver.Id)
                                                  .WhereEqualsTo("receiverId", senderId)
                                                  .GetDocumentsAsync<Invitation>();

            if (existingReceived.Documents.Any())
            {
                throw new Exception("Ten użytkownik wysłał już zaproszenie do Ciebie. Sprawdź swoje powiadomienia.");
            }

            var request = new Invitation
            {
                SenderId = senderId,
                SenderName = senderName,
                ReceiverId = receiver.Id,
                ReceiverEmail = receiverEmail,
                IsSenderCaregiver = isSenderCaregiver,
                Status = "Pending" // Możliwe statusy: "Pending", "Accepted", "Rejected"
            };

            await firestore.GetCollection("invitations").AddDocumentAsync(request);

            var notification = new AppNotification
            {
                ReceiverId = receiver.Id,
                Title = "Nowe zaproszenie",
                Message = $"Masz nowe zaproszenie do współpracy od {senderName}.",
                Type = "NewInvitation"
            };

            await SendNotificationAsync(notification);

            return true;
        }

        // Listener 1: Invitations sent TO ME
        public IDisposable ListenForReceivedInvitations(string myUid, Action<List<Invitation>> onUpdate)
        {
            var firestore = CrossFirebaseFirestore.Current;

            return firestore.GetCollection("invitations")
                .WhereEqualsTo("receiverId", myUid)
                .AddSnapshotListener<Invitation>((snapshot) =>
                {
                    var receivedInvitations = snapshot.Documents.Select(doc => doc.Data).ToList();
                    onUpdate?.Invoke(receivedInvitations);
                });
        }

        // Listener 2: Invitations sent BY ME
        public IDisposable ListenForSentInvitations(string myUid, Action<List<Invitation>> onUpdate)
        {
            var firestore = CrossFirebaseFirestore.Current;

            return firestore.GetCollection("invitations")
                .WhereEqualsTo("senderId", myUid)
                .AddSnapshotListener<Invitation>((snapshot) =>
                {
                    var sentInvitations = snapshot.Documents.Select(doc => doc.Data).ToList();
                    onUpdate?.Invoke(sentInvitations);
                });
        }

        public async Task AcceptInvitationAsync(Invitation request)
        {
            var firestore = CrossFirebaseFirestore.Current;
            string? myUid = GetCurrentUserId();
            var batch = firestore.CreateBatch();
            if (string.IsNullOrEmpty(myUid)) return;

            var requestRef = firestore.GetCollection("invitations").GetDocument(request.Id);

            string caregiverId = request.IsSenderCaregiver ? request.SenderId : request.ReceiverId;
            string caretakerId = !request.IsSenderCaregiver ? request.SenderId : request.ReceiverId;

            batch.UpdateData(firestore.GetCollection("users").GetDocument(caregiverId),
                ("careTakersID", FieldValue.ArrayUnion(caretakerId)));

            batch.UpdateData(firestore.GetCollection("users").GetDocument(caretakerId),
                ("careGiversID", FieldValue.ArrayUnion(caregiverId)));

            batch.UpdateData(requestRef, ("status", "Accepted"));

            await batch.CommitAsync();

            var myProfile = await GetUserProfileAsync(myUid);
            string myName = myProfile?.Name ?? "Użytkownik";

            var notification = new AppNotification
            {
                ReceiverId = request.SenderId,
                Title = "Zaproszenie zaakceptowane",
                Message = $"{myName} zaakceptował(a) Twoje zaproszenie do współpracy.",
                Type = "InvitationAccepted"
            };

            await SendNotificationAsync(notification);
        }

        // NEW: Reject the invitation (changes status, but keeps the document so the sender gets blocked from spamming)
        public async Task RejectInvitationAsync(Invitation invitation)
        {
            var firestore = CrossFirebaseFirestore.Current;
            string? myUid = GetCurrentUserId();
            if (string.IsNullOrEmpty(myUid)) return;

            // Aktualizacja statusu zaproszenia na "Rejected"
            await firestore.GetCollection("invitations")
                           .GetDocument(invitation.Id) // Używamy .Id z przekazanego obiektu
                           .UpdateDataAsync(("status", "Rejected"));

            // -- NOWY KOD DO POWIADOMIEŃ PUSH --
            var myProfile = await GetUserProfileAsync(myUid);
            string myName = myProfile?.Name ?? "Użytkownik";

            var notification = new AppNotification
            {
                ReceiverId = invitation.SenderId, // Wysyłamy do nadawcy
                Title = "Zaproszenie odrzucone",
                Message = $"{myName} odrzucił(a) Twoje zaproszenie.",
                Type = "InvitationRejected"
            };

            await SendNotificationAsync(notification);
        }

        // Delete the invitation entirely (Allows the sender to clear rejected invites and try again)
        public async Task DeleteInvitationAsync(string invitationId)
        {
            var firestore = CrossFirebaseFirestore.Current;
            await firestore.GetCollection("invitations")
                           .GetDocument(invitationId)
                           .DeleteDocumentAsync();
        }

        // Usuwa połączenie między użytkownikami
        public async Task RemoveAcceptedInvitationAsync(string caregiverId, string caretakerId, string removerUid, string removerName)
        {
            var firestore = CrossFirebaseFirestore.Current;
            var batch = firestore.CreateBatch();

            // 1. Usuń ID z list obu użytkowników
            batch.UpdateData(firestore.GetCollection("users").GetDocument(caregiverId),
                ("careTakersID", FieldValue.ArrayRemove(caretakerId)));

            batch.UpdateData(firestore.GetCollection("users").GetDocument(caretakerId),
                ("careGiversID", FieldValue.ArrayRemove(caregiverId)));

            await batch.CommitAsync();

            // 2. Ostatecznie i trwale usuwamy zaproszenie z bazy (żadnego "Soft Delete")
            var queryA = await firestore.GetCollection("invitations")
                .WhereEqualsTo("senderId", caregiverId)
                .WhereEqualsTo("receiverId", caretakerId)
                .GetDocumentsAsync<Invitation>();

            foreach (var doc in queryA.Documents)
                if (!string.IsNullOrEmpty(doc.Data?.Id)) await DeleteInvitationPermanentlyAsync(doc.Data.Id);

            var queryB = await firestore.GetCollection("invitations")
                .WhereEqualsTo("senderId", caretakerId)
                .WhereEqualsTo("receiverId", caregiverId)
                .GetDocumentsAsync<Invitation>();

            foreach (var doc in queryB.Documents)
                if (!string.IsNullOrEmpty(doc.Data?.Id)) await DeleteInvitationPermanentlyAsync(doc.Data.Id);

            // 3. WYSYŁANIE NOWEGO POWIADOMIENIA DO DRUGIEJ OSOBY
            // Kto ma dostać powiadomienie? Ten, którego ID NIE jest ID osoby usuwającej.
            string targetUserId = (removerUid == caregiverId) ? caretakerId : caregiverId;

            var notification = new AppNotification
            {
                ReceiverId = targetUserId,
                Title = "Zakończono współpracę",
                Message = $"Użytkownik {removerName} usunął Cię ze swojej listy kontaktów.",
                Type = "ConnectionDeleted"
            };

            await SendNotificationAsync(notification);
        }

        // Ostateczne usunięcie z bazy
        public async Task DeleteInvitationPermanentlyAsync(string invitationId)
        {
            var firestore = CrossFirebaseFirestore.Current;
            await firestore.GetCollection("invitations")
                           .GetDocument(invitationId)
                           .DeleteDocumentAsync();
        }

        // Potrzebne do wyświetlania listy opiekunów/podopiecznych w ManageCareTakers/ManageCareGivers
        public async Task<List<User>> GetUsersByIdsAsync(List<string>? userIds)
        {
            var users = new List<User>();
            if (userIds == null || !userIds.Any()) return users;

            var firestore = CrossFirebaseFirestore.Current;

            foreach (var id in userIds)
            {
                var snapshot = await firestore.GetCollection("users").GetDocument(id).GetDocumentSnapshotAsync<User>();
                if (snapshot.Data != null)
                {
                    users.Add(snapshot.Data);
                }
            }
            return users;
        }
        #endregion
    }
}
