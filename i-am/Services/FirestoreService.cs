using i_am.Models;
using Plugin.Firebase.Auth;
using Plugin.Firebase.CloudMessaging;
using Plugin.Firebase.Firestore;
using Plugin.Firebase.Storage;

namespace i_am.Services
{
    public class FirestoreService
    {
        #region User Management

        // Tworzenie użytkownika (Authentication) w Firebase
        public async Task<string> RegisterAsync(string email, string password)
        {
            var user = await CrossFirebaseAuth.Current.CreateUserAsync(email, password);
            return user.Uid;
        }

        // Tworzenie użytkownika (dokumentu) w Firestore
        public async Task CreateUserProfileAsync(string uid, User profile)
        {
            var firestore = CrossFirebaseFirestore.Current;

            await firestore.GetCollection("users").GetDocument(uid).SetDataAsync(profile);
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
            return result.User?.Uid ?? throw new Exception("Nie udało się pobrać danych autoryzacji z Firebase.");
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

        // Odczyt danych użytkownika (cały jego dokument w firestore)
        public async Task<User?> GetUserProfileAsync(string uid)
        {
            var firestore = CrossFirebaseFirestore.Current;

            var snapshot = await firestore.GetCollection("users")
                                          .GetDocument(uid)
                                          .GetDocumentSnapshotAsync<User>();

            // Plugin zwraca null jeśli dokument nie istnieje
            return snapshot.Data;
        }

        // Usuwanie konta użytkownika - najpierw Firestore, potem Authentication 
        public async Task DeleteUserAsync()
        {
            string? uid = CrossFirebaseAuth.Current.CurrentUser?.Uid;
            if (string.IsNullOrEmpty(uid)) return;

            var firestore = CrossFirebaseFirestore.Current;

            // 1. Pobierz profil użytkownika, aby uzyskać jego imię i powiązane konta przed usunięciem
            var userProfile = await GetUserProfileAsync(uid);

            if (userProfile != null)
            {
                var batch = firestore.CreateBatch();

                // Zbieramy wszystkich powiązanych użytkowników (opiekunów i podopiecznych) bez duplikatów
                var connectedUsers = new HashSet<string>();
                if (userProfile.CaregiversID != null)
                {
                    foreach (var id in userProfile.CaregiversID) connectedUsers.Add(id);
                }
                if (userProfile.CaretakersID != null)
                {
                    foreach (var id in userProfile.CaretakersID) connectedUsers.Add(id);
                }

                // 2. Usuwamy ID użytkownika z list kontaktów innych osób i wysyłamy powiadomienia
                foreach (var connectedUserId in connectedUsers)
                {
                    var connectedUserRef = firestore.GetCollection("users").GetDocument(connectedUserId);

                    // Wykorzystujemy ArrayRemove na obu listach (jeśli ID tam nie ma, nic się nie zepsuje)
                    batch.UpdateData(connectedUserRef, ("careTakersID", FieldValue.ArrayRemove(uid)));
                    batch.UpdateData(connectedUserRef, ("careGiversID", FieldValue.ArrayRemove(uid)));

                    // Tworzymy powiadomienie
                    var notification = new AppNotification
                    {
                        ReceiverId = connectedUserId,
                        Title = "Konto usunięte",
                        Message = $"Użytkownik {userProfile.Name} usunął konto.",
                        Type = "AccountDeleted", // Możesz dodać ten typ w XAML, aby ustawić mu np. szarą ikonę
                        SenderId = uid
                    };

                    // Ponieważ Twoja metoda SendNotificationAsync wykonuje niezależny zapis do innej kolekcji, 
                    // możemy ją po prostu wywołać w pętli.
                    await SendNotificationAsync(notification);
                }

                // Wykonujemy masową operację usunięcia powiązań
                await batch.CommitAsync();
            }

            // 3. Ręczne usunięcie podkolekcji użytkownika
            await DeleteUserQuestionTemplatesAsync(uid);
            await DeleteUserDailyResponsesAsync(uid);

            // Usuwanie wszystkich zdjęć z Firebase Storage
            await DeleteUserPhotosAsync(uid);

            // Opcjonalnie: usunięcie wiszących zaproszeń powiązanych z tym użytkownikiem
            await DeleteUserInvitationsForDeletedAccountAsync(uid);

            // 4. Usuń główny dokument użytkownika w Firestore
            await firestore.GetCollection("users").GetDocument(uid).DeleteDocumentAsync();

            // 5. Natywne usunięcie konta w Firebase Authentication
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

        private async Task DeleteUserQuestionTemplatesAsync(string uid)
        {
            var firestore = CrossFirebaseFirestore.Current;
            var subcollectionRef = firestore.GetCollection("users").GetDocument(uid).GetCollection("question_templates");

            // Podajemy konkretny model
            var snapshot = await subcollectionRef.GetDocumentsAsync<QuestionTemplate>();

            if (snapshot?.Documents != null)
            {
                foreach (var doc in snapshot.Documents)
                {
                    // Bezpiecznie pobieramy ID z modelu (doc.Data)
                    if (doc.Data != null && !string.IsNullOrEmpty(doc.Data.Id))
                    {
                        await subcollectionRef.GetDocument(doc.Data.Id).DeleteDocumentAsync();
                    }
                }
            }
        }

        private async Task DeleteUserDailyResponsesAsync(string uid)
        {
            var firestore = CrossFirebaseFirestore.Current;
            var subcollectionRef = firestore.GetCollection("users").GetDocument(uid).GetCollection("daily_responses");

            // Podajemy konkretny model
            var snapshot = await subcollectionRef.GetDocumentsAsync<DailyResponse>();

            if (snapshot?.Documents != null)
            {
                foreach (var doc in snapshot.Documents)
                {
                    // Bezpiecznie pobieramy ID z modelu (doc.Data)
                    if (doc.Data != null && !string.IsNullOrEmpty(doc.Data.Id))
                    {
                        await subcollectionRef.GetDocument(doc.Data.Id).DeleteDocumentAsync();
                    }
                }
            }
        }
        private async Task DeleteUserPhotosAsync(string uid)
        {
            try
            {
                var storage = CrossFirebaseStorage.Current;
                var folderRef = storage.GetRootReference().GetChild($"daily_photos/{uid}");

                // Pobieramy listę wszystkich plików znajdujących się w "folderze" użytkownika
                var result = await folderRef.ListAllAsync();

                if (result?.Items != null && result.Items.Any())
                {
                    foreach (var itemRef in result.Items)
                    {
                        // Usuwamy każdy plik po kolei
                        await itemRef.DeleteAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignorujemy błędy, np. jeśli folder nie istnieje (użytkownik nie miał jeszcze żadnych zdjęć)
                System.Diagnostics.Debug.WriteLine($"[Storage] Brak zdjęć do usunięcia lub błąd: {ex.Message}");
            }
        }

        private async Task DeleteUserInvitationsForDeletedAccountAsync(string uid)
        {
            var firestore = CrossFirebaseFirestore.Current;

            // Usuwanie zaproszeń, które użytkownik WYSŁAŁ
            var sentInvitations = await firestore.GetCollection("invitations")
                                                 .WhereEqualsTo("senderId", uid)
                                                 .GetDocumentsAsync<Invitation>();

            if (sentInvitations?.Documents != null)
            {
                foreach (var doc in sentInvitations.Documents)
                {
                    if (doc.Data != null && !string.IsNullOrEmpty(doc.Data.Id))
                    {
                        await firestore.GetCollection("invitations").GetDocument(doc.Data.Id).DeleteDocumentAsync();
                    }
                }
            }

            // Usuwanie zaproszeń, które użytkownik OTRZYMAŁ
            var receivedInvitations = await firestore.GetCollection("invitations")
                                                     .WhereEqualsTo("receiverId", uid)
                                                     .GetDocumentsAsync<Invitation>();

            if (receivedInvitations?.Documents != null)
            {
                foreach (var doc in receivedInvitations.Documents)
                {
                    if (doc.Data != null && !string.IsNullOrEmpty(doc.Data.Id))
                    {
                        await firestore.GetCollection("invitations").GetDocument(doc.Data.Id).DeleteDocumentAsync();
                    }
                }
            }
        }

        // Plugin od firebase nie posiada metody natywnej do usuwania konta, więc musimy użyć natywnych SDK Firebase dla Androida i iOS


        // Token odpowiedzialny za powiadomienia push
        // Każde logowanie powinno aktualizować token FCM, żeby mieć pewność, że powiadomienia push będą docierać do właściwego urządzenia (np. jeśli ktoś się zaloguje na nowym telefonie, stary token przestaje być ważny)
        public async Task UpdateFcmTokenAsync()
        {
            string? uid = Preferences.Get("UserId", string.Empty);
            if (string.IsNullOrEmpty(uid)) return;

            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();

                if (status != PermissionStatus.Granted)
                    return;

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

        public async Task RemoveFcmTokenAsync()
        {
            string? uid = Preferences.Get("UserId", string.Empty);
            if (string.IsNullOrEmpty(uid)) return;

            try
            {
                var firestore = CrossFirebaseFirestore.Current;
                var userDoc = firestore.GetCollection("users").GetDocument(uid);

                // Zastępujemy token pustym stringiem, aby powiadomienia przestały trafiać na to urządzenie
                await userDoc.UpdateDataAsync(new Dictionary<object, object>
                {
                    { "fcmToken", string.Empty }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd podczas usuwania tokenu FCM: {ex.Message}");
            }
        }

        public async Task UpdateUserProfileAsync(string uid, string phoneNumber, string sex)
        {
            try
            {
                var firestore = CrossFirebaseFirestore.Current;
                var userDoc = firestore.GetCollection("users").GetDocument(uid);

                await userDoc.UpdateDataAsync(new Dictionary<object, object>
                {
                    { "phoneNumber", phoneNumber },
                    { "sex", sex }
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Nie udało się zaktualizować profilu: {ex.Message}");
            }
        }

        public async Task<bool> UpdateUserSettingsAsync(string userId, int dayStartHour, bool isRestricted, int startHour, int endHour)
        {
            var firestore = CrossFirebaseFirestore.Current;
            try
            {
                await firestore
                    .GetCollection("users")
                    .GetDocument(userId)
                    .UpdateDataAsync(new Dictionary<object, object>
                    {
                        { "dayStartHour", dayStartHour },
                        { "isActivityTimeRestricted", isRestricted },
                        { "activityRestrictionStartHour", startHour },
                        { "activityRestrictionEndHour", endHour }
                    });
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating settings: {ex.Message}");
                return false;
            }
        }


        #endregion
        #region Powiadomienia (Notifications)

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
            if (isSenderCaregiver && receiver.IsCaregiver)
            {
                throw new Exception("Opiekun nie może zaprosić innego opiekuna. Zaproszenia można wysyłać tylko do podopiecznych.");
            }
            if (!isSenderCaregiver && !receiver.IsCaregiver)
            {
                throw new Exception("Podopieczny nie może zaprosić innego podopiecznego. Zaproszenia można wysyłać tylko do opiekunów.");
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
                throw new Exception("Ten użytkownik wysłał już zaproszenie do Ciebie.");
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
                Message = $"Masz nowe zaproszenie od {senderName}.",
                Type = "NewInvitation"
            };

            await SendNotificationAsync(notification);

            return true;
        }

        public async Task AcceptInvitationAsync(Invitation request)
        {
            var firestore = CrossFirebaseFirestore.Current;
            string? myUid = Preferences.Get("UserId", string.Empty);
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
                Message = $"{myName} zaakceptował(a) Twoje zaproszenie.",
                Type = "InvitationAccepted"
            };

            await SendNotificationAsync(notification);
        }

        // Odrzucenie zaproszenia (zmienia status, nadawca będzie potem w stanie usunąć zaproszenie i wysłać je ponownie)
        public async Task RejectInvitationAsync(Invitation invitation)
        {
            var firestore = CrossFirebaseFirestore.Current;
            string? myUid = Preferences.Get("UserId", string.Empty);
            if (string.IsNullOrEmpty(myUid)) return;

            await firestore.GetCollection("invitations")
                           .GetDocument(invitation.Id) // Używamy .Id z przekazanego obiektu
                           .UpdateDataAsync(("status", "Rejected"));

            var myProfile = await GetUserProfileAsync(myUid);
            string myName = myProfile?.Name ?? "Użytkownik";

            var notification = new AppNotification
            {
                ReceiverId = invitation.SenderId,
                Title = "Zaproszenie odrzucone",
                Message = $"{myName} odrzucił(a) Twoje zaproszenie.",
                Type = "InvitationRejected",
                SenderId = invitation.Id
            };

            await SendNotificationAsync(notification);
        }

        // Usunięcie zaproszenia z FireStore
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

            // Usuwamy ID z list obu użytkowników
            batch.UpdateData(firestore.GetCollection("users").GetDocument(caregiverId),
                ("careTakersID", FieldValue.ArrayRemove(caretakerId)));

            batch.UpdateData(firestore.GetCollection("users").GetDocument(caretakerId),
                ("careGiversID", FieldValue.ArrayRemove(caregiverId)));

            await batch.CommitAsync();

            // Usuwamy zaproszenie
            var queryA = await firestore.GetCollection("invitations")
                .WhereEqualsTo("senderId", caregiverId)
                .WhereEqualsTo("receiverId", caretakerId)
                .GetDocumentsAsync<Invitation>();

            foreach (var doc in queryA.Documents)
                if (!string.IsNullOrEmpty(doc.Data?.Id)) await DeleteInvitationAsync(doc.Data.Id);

            var queryB = await firestore.GetCollection("invitations")
                .WhereEqualsTo("senderId", caretakerId)
                .WhereEqualsTo("receiverId", caregiverId)
                .GetDocumentsAsync<Invitation>();

            foreach (var doc in queryB.Documents)
                if (!string.IsNullOrEmpty(doc.Data?.Id)) await DeleteInvitationAsync(doc.Data.Id);

            // Wysyłanie powiadomienia do osoby którą usuwamy
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

        // Potrzebne do wyświetlania listy opiekunów/podopiecznych w ManageConnectionsPage
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

        // Nasłuchiwanie własnego profilu (ja lub inny user wpisał mi się do listy CareGiversID/CareTakersID)
        public IDisposable ListenForUserProfileUpdates(string uid, Action<User> onUpdate)
        {
            var firestore = CrossFirebaseFirestore.Current;

            return firestore.GetCollection("users")
                .GetDocument(uid)
                .AddSnapshotListener<User>((snapshot) =>
                {
                    if (snapshot.Data != null)
                    {
                        onUpdate?.Invoke(snapshot.Data);
                    }
                });
        }

        // Nasłuchiwanie zaproszeń skierowanych DO MNIE
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

        // Nasłuchiwanie zaproszeń wysłanych PRZEZE MNIE
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
        #endregion
        #region Pytania i odpowiedzi (Questions & Answers)

        // --- ZARZĄDZANIE PYTANIAMI ---

        // Pobieranie listy pytań dla konkretnego podopiecznego
        public async Task<List<QuestionTemplate>> GetQuestionTemplatesAsync(string careTakerId)
        {
            var firestore = CrossFirebaseFirestore.Current;
            var snapshot = await firestore.GetCollection("users")
                                          .GetDocument(careTakerId)
                                          .GetCollection("question_templates")
                                          .GetDocumentsAsync<QuestionTemplate>();

            // Skoro snapshot jest już zmapowany, wystarczy wyciągnąć właściwość Data
            var templates = snapshot.Documents.Select(d => d.Data).ToList();

            return templates.OrderBy(q => q.OrderIndex).ToList();
        }

        // Zapisywanie lub aktualizacja konkretnego pytania
        public async Task SaveQuestionTemplateAsync(string careTakerId, QuestionTemplate template)
        {
            var firestore = CrossFirebaseFirestore.Current;
            var collectionRef = firestore.GetCollection("users").GetDocument(careTakerId).GetCollection("question_templates");

            if (string.IsNullOrEmpty(template.Id))
            {
                // Nowe pytanie - Firebase sam wygeneruje ID dokumentu
                await collectionRef.AddDocumentAsync(template);
            }
            else
            {
                // Aktualizacja - używamy SetDataAsync do nadpisania istniejącego dokumentu naszym modelem
                await collectionRef.GetDocument(template.Id).SetDataAsync(template);
            }
        }

        // Usuwanie pytania
        public async Task DeleteQuestionTemplateAsync(string careTakerId, string templateId)
        {
            var firestore = CrossFirebaseFirestore.Current;
            await firestore.GetCollection("users")
                           .GetDocument(careTakerId)
                           .GetCollection("question_templates")
                           .GetDocument(templateId)
                           .DeleteDocumentAsync();
        }

        // --- WYPEŁNIANIE ANKIET ---

        // Sprawdza czy podopieczny wypełnił już dziś ankietę
        public async Task<bool> HasSubmittedDailyResponseAsync(string careTakerId, string dateString)
        {
            try
            {
                var firestore = CrossFirebaseFirestore.Current;
                var snapshot = await firestore.GetCollection("users")
                                         .GetDocument(careTakerId)
                                         .GetCollection("daily_responses")
                                         .GetDocument(dateString)
                                         .GetDocumentSnapshotAsync<DailyResponse>();

                // 'snapshot' to opakowanie z Firebase. 
                // Nasz rzeczywisty obiekt (DailyResponse) znajduje się we właściwości '.Data'.
                if (snapshot != null && snapshot.Data != null && !string.IsNullOrEmpty(snapshot.Data.EvaluationStatus))
                {
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                // W razie jakichkolwiek problemów (np. braku podkolekcji), pozwalamy wypełnić ankietę
                return false;
            }
        }

        // Zapisuje odpowiedź podopiecznego i wysyła powiadomienia do opiekunów
        public async Task SaveDailyResponseAsync(string careTakerId, DailyResponse response)
        {
            var firestore = CrossFirebaseFirestore.Current;

            // 1. Zapis do podkolekcji 'daily_responses' podopiecznego
            await firestore.GetCollection("users")
                           .GetDocument(careTakerId)
                           .GetCollection("daily_responses")
                           .GetDocument(response.Id) // Id to będzie data np. "2024-05-14"
                           .SetDataAsync(response);

            // 2. Pobranie danych podopiecznego, żeby wiedzieć komu wysłać powiadomienie
            var careTakerProfile = await GetUserProfileAsync(careTakerId);
            if (careTakerProfile != null && careTakerProfile.CaregiversID.Any())
            {
                // 3. Wysłanie powiadomień do wszystkich przypisanych opiekunów
                foreach (var giverId in careTakerProfile.CaregiversID)
                {
                    var notification = new AppNotification
                    {
                        ReceiverId = giverId,
                        Title = $"Nowy raport dzienny: {careTakerProfile.Name}",
                        Message = $"Wynik: {response.TotalScore} pkt. Status: {response.EvaluationStatus}.",
                        Type = "DailyReportAlert",
                        SenderId = careTakerId,   // Przekazujemy ID Podopiecznego
                        Date = response.Id     // Format "yyyy-MM-dd"
                    };
                    await SendNotificationAsync(notification);
                }
            }
        }

        // --- METODY POMOCNICZE ---

        // Generowanie domyślnego zestawu pytań
        public async Task<List<QuestionTemplate>> InitializeDefaultQuestionsAsync(string careTakerId)
        {
            var questions = new List<QuestionTemplate>();
            int order = 0;

            // WSPÓLNA LISTA EMOCJI DO WYKORZYSTANIA W PYTANIACH
            var emotionOptions = new List<QuestionOption>
            {
                new QuestionOption { Text = "Radość", Points = 0 },
                new QuestionOption { Text = "Spokój", Points = 0 },
                new QuestionOption { Text = "Zmotywowanie", Points = 0 },
                new QuestionOption { Text = "Obojętność", Points = -1 },
                new QuestionOption { Text = "Zmęczenie", Points = -1 },
                new QuestionOption { Text = "Smutek", Points = -2 },
                new QuestionOption { Text = "Lęk / Niepokój", Points = -2 },
                new QuestionOption { Text = "Stres", Points = -2 },
                new QuestionOption { Text = "Złość", Points = -2 }
            };

            // --- 1. PYTANIA CODZIENNE (ZAMKNIĘTE) ---
            questions.Add(new QuestionTemplate { OrderIndex = order++, IsRandomPool = false, Type = "Closed", MaxSelections = 2, Text = "Jakie emocje czułeś na ROZPOCZĘCIE dnia?", Options = new List<QuestionOption>(emotionOptions) });
            questions.Add(new QuestionTemplate { OrderIndex = order++, IsRandomPool = false, Type = "Closed", MaxSelections = 5, Text = "Jakie emocje czułeś w ŚRODKU dnia?", Options = new List<QuestionOption>(emotionOptions) });
            questions.Add(new QuestionTemplate { OrderIndex = order++, IsRandomPool = false, Type = "Closed", MaxSelections = 2, Text = "Jakie emocje czułeś na ZAKOŃCZENIE dnia?", Options = new List<QuestionOption>(emotionOptions) });

            questions.Add(new QuestionTemplate
            {
                OrderIndex = order++,
                IsRandomPool = false,
                Type = "Closed",
                MaxSelections = 1,
                Text = "Ile posiłków dzisiaj zjadłeś?",
                Options = new List<QuestionOption> {
                    new QuestionOption { Text = "0", Points = -2 },
                    new QuestionOption { Text = "1", Points = -1 },
                    new QuestionOption { Text = "2", Points = 0 },
                    new QuestionOption { Text = "3", Points = 0 },
                    new QuestionOption { Text = "4", Points = 0 },
                    new QuestionOption { Text = "5", Points = 0 },
                    new QuestionOption { Text = "Więcej niż 5", Points = 0 }
                }
            });

            questions.Add(new QuestionTemplate
            {
                OrderIndex = order++,
                IsRandomPool = false,
                Type = "Closed",
                MaxSelections = 1,
                Text = "Czy zjadłeś dzisiaj chociaż jeden pełnowartościowy posiłek?",
                Options = new List<QuestionOption> {
                    new QuestionOption { Text = "TAK", Points = 0 },
                    new QuestionOption { Text = "NIE", Points = -1 }
                }
            });

            questions.Add(new QuestionTemplate
            {
                OrderIndex = order++,
                IsRandomPool = false,
                Type = "Closed",
                MaxSelections = 1,
                Text = "Ile godzin spałeś?",
                Options = new List<QuestionOption> {
                    new QuestionOption { Text = "Poniżej 3 godzin", Points = -2 },
                    new QuestionOption { Text = "3-5 godzin", Points = -1 },
                    new QuestionOption { Text = "6-8 godzin", Points = 0 },
                    new QuestionOption { Text = "9-11 godzin", Points = 0 },
                    new QuestionOption { Text = "12 lub więcej", Points = -2 }
                }
            });

            questions.Add(new QuestionTemplate
            {
                OrderIndex = order++,
                IsRandomPool = false,
                Type = "Closed",
                MaxSelections = 1,
                Text = "Zaznacz na skali jak się dzisiaj czujesz:",
                Options = new List<QuestionOption> {
                    new QuestionOption { Text = "Przemęczony", Points = -2 },
                    new QuestionOption { Text = "Bardzo zmęczony", Points = -1 },
                    new QuestionOption { Text = "Zmęczony", Points = -1 },
                    new QuestionOption { Text = "W sam raz", Points = 0 },
                    new QuestionOption { Text = "Pełen energii", Points = 1 }
                }
            });

            // --- 2. PYTANIA CODZIENNE (OTWARTE) ---
            questions.Add(new QuestionTemplate { OrderIndex = order++, IsRandomPool = false, Type = "Open", Text = "Czy zdarzyło się dziś coś, co wywarło w tobie silne emocje? Co to było, jak się czułeś i jak się zachowałeś?" });
            questions.Add(new QuestionTemplate { OrderIndex = order++, IsRandomPool = false, Type = "Open", Text = "Co dzisiaj udało ci się zrobić?" });

            // --- 3. PULOWA LOSOWA (ZAMKNIĘTE) ---
            questions.Add(new QuestionTemplate
            {
                OrderIndex = order++,
                IsRandomPool = true,
                Type = "Closed",
                MaxSelections = 1,
                Text = "Czy czerpałeś dziś przyjemność chociaż z jednej wykonywanej czynności bądź odczuwałeś przynajmniej niewielkie zainteresowanie nią?",
                Options = new List<QuestionOption> { new QuestionOption { Text = "TAK", Points = 0 }, new QuestionOption { Text = "NIE", Points = -3 } }
            });

            questions.Add(new QuestionTemplate
            {
                OrderIndex = order++,
                IsRandomPool = true,
                Type = "Closed",
                MaxSelections = 1,
                Text = "Czy miałeś problem ze skupieniem się podczas wykonywania podstawowych czynności (np. oglądanie TV, czytanie)?",
                Options = new List<QuestionOption> { new QuestionOption { Text = "TAK", Points = -1 }, new QuestionOption { Text = "NIE", Points = 0 } }
            });

            questions.Add(new QuestionTemplate
            {
                OrderIndex = order++,
                IsRandomPool = true,
                Type = "Closed",
                MaxSelections = 1,
                Text = "Czy zdarzyło ci się dzisiaj ruszać lub mówić tak wolno, że zauważyli to inni (lub przeciwnie, nie mogłeś usiedzieć w miejscu)?",
                Options = new List<QuestionOption> { new QuestionOption { Text = "TAK", Points = -3 }, new QuestionOption { Text = "NIE", Points = 0 } }
            });

            // --- 4. PULOWA LOSOWA (OTWARTE) ---
            questions.Add(new QuestionTemplate { OrderIndex = order++, IsRandomPool = true, Type = "Open", Text = "O czym pomyślałeś jak się obudziłeś?" });
            questions.Add(new QuestionTemplate { OrderIndex = order++, IsRandomPool = true, Type = "Open", Text = "Co byś chciał dzisiaj zrobić?" });
            questions.Add(new QuestionTemplate { OrderIndex = order++, IsRandomPool = true, Type = "Open", Text = "Kiedy ostatni raz czułeś radość?" });
            questions.Add(new QuestionTemplate { OrderIndex = order++, IsRandomPool = true, Type = "Open", Text = "Co sprawiłoby Ci radość?" });
            questions.Add(new QuestionTemplate { OrderIndex = order++, IsRandomPool = true, Type = "Open", Text = "Podaj choć jedną rzecz z której byłeś dzisiaj dumny." });

            var firestore = CrossFirebaseFirestore.Current;
            var collectionRef = firestore.GetCollection("users").GetDocument(careTakerId).GetCollection("question_templates");

            foreach (var q in questions)
            {
                await collectionRef.AddDocumentAsync(q);
            }

            return questions;
        }

        // Pobiera wszystkie historyczne raporty podopiecznego
        public async Task<List<DailyResponse>> GetAllDailyResponsesAsync(string careTakerId)
        {
            try
            {
                var firestore = CrossFirebaseFirestore.Current;
                var snapshot = await firestore.GetCollection("users")
                                              .GetDocument(careTakerId)
                                              .GetCollection("daily_responses")
                                              .GetDocumentsAsync<DailyResponse>();

                return snapshot.Documents
                               .Where(d => d.Data != null)
                               .Select(d => d.Data)
                               .ToList();
            }
            catch (Exception)
            {
                return new List<DailyResponse>();
            }
        }
        public async Task<string> UploadDailyPhotoAsync(string uid, string dateId, string suffix, FileResult photo)
        {
            try
            {
                var storage = CrossFirebaseStorage.Current;

                var reference = storage.GetRootReference().GetChild($"daily_photos/{uid}/{dateId}_{suffix}.jpg");

                await reference.PutFile(photo.FullPath).AwaitAsync();
                return await reference.GetDownloadUrlAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd wgrywania zdjęcia: {ex.Message}");
                return string.Empty;
            }
        }

        #endregion
    }
}
