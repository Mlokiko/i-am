using i_am.Models;
using Plugin.Firebase.Auth;
using Plugin.Firebase.CloudMessaging;
using Plugin.Firebase.Core.Exceptions;
using Plugin.Firebase.Firestore;
using Plugin.Firebase.Storage;
using Plugin.LocalNotification;

namespace i_am.Services
{
    public class FirestoreService
    {
        #region User Management

        // Tworzenie użytkownika (Authentication) w Firebase
        public async Task<string> RegisterAsync(string email, string password)
        {

            try
            {
                var user = await CrossFirebaseAuth.Current.CreateUserAsync(email, password);
                return user.Uid;
            }
            catch (CrossPlatformFirebaseAuthException ex) // Obsługa dla Plugin.Firebase >= 5.0.0
            {
                // Klasyfikator rozpoznaje natywny błąd i zamienia go na enum
                var errorType = FirebaseAuthErrorClassifier.TryClassify(ex);

                if (errorType == FirebaseAuthFailure.UserCollision)
                {
                    throw new InvalidOperationException("Ten adres e-mail jest już używany przez inne konto.");
                }
                if (errorType == FirebaseAuthFailure.WeakPassword)
                {
                    throw new InvalidOperationException("Podane hasło jest zbyt słabe.");
                }
                throw new InvalidOperationException($"Błąd rejestracji: {ex.NativeErrorMessage}");
            }
        }

        // Tworzenie użytkownika (dokumentu) w Firestore
        public async Task CreateUserProfileAsync(string uid, User profile)
        {
            var firestore = CrossFirebaseFirestore.Current;
            await firestore.GetCollection("users").GetDocument(uid).SetDataAsync(profile);
        }

        public async Task<string> LoginAsync(string email, string password)
        {
#if ANDROID
            var result = await Firebase.Auth.FirebaseAuth.Instance.SignInWithEmailAndPasswordAsync(email, password);
            return result.User?.Uid ?? throw new Exception("Nie udało się pobrać danych autoryzacji z Firebase.");
#elif IOS
            var result = await Firebase.Auth.Auth.DefaultInstance.SignInWithEmailAndPasswordAsync(email, password);
            return result.User.Uid;
#else
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

            return snapshot.Data;
        }

        // Usuwanie konta użytkownika - najpierw Firestore, potem Authentication 
        public async Task DeleteUserAsync()
        {
            var currentUser = CrossFirebaseAuth.Current.CurrentUser;
            string? uid = currentUser?.Uid;

            if (string.IsNullOrEmpty(uid)) return;

            // 1. Pobierz profil użytkownika
            var userProfile = await GetUserProfileAsync(uid);
            if (userProfile == null) return;

            // --- 2. SPRAWDZENIE CZASU OD OSTATNIEGO LOGOWANIA ---
            var recentLogin = Preferences.Get("RecentLogIn", DateTime.MinValue);
            TimeSpan timeSinceLastLogin = DateTime.UtcNow - recentLogin;

            if (timeSinceLastLogin.TotalMinutes >= 4)
            {
                await Shell.Current.DisplayAlert(
                    "Wymagane ponowne logowanie",
                    "Ze względów bezpieczeństwa (ochrona przed nieautoryzowanym usunięciem) ta operacja wymaga bardzo świeżej sesji.\n\nWyloguj się, zaloguj ponownie i od razu spróbuj usunąć konto.",
                    "Rozumiem");
                return;
            }
            // --- KONIEC SPRAWDZANIA ---

            var firestore = CrossFirebaseFirestore.Current;
            var batch = firestore.CreateBatch();

            // Zbieramy wszystkich powiązanych użytkowników bez duplikatów przy użyciu nowoczesnego C#
            var connectedUsers = new HashSet<string>(userProfile.CaregiversID ?? []);
            connectedUsers.UnionWith(userProfile.CaretakersID ?? []);

            // 3. Usuwamy ID użytkownika z list kontaktów innych osób i wysyłamy powiadomienia (RÓWNOLEGLE)
            var notificationTasks = connectedUsers.Select(connectedUserId =>
            {
                var connectedUserRef = firestore.GetCollection("users").GetDocument(connectedUserId);

                batch.UpdateData(connectedUserRef, ("careTakersID", FieldValue.ArrayRemove(uid)));
                batch.UpdateData(connectedUserRef, ("careGiversID", FieldValue.ArrayRemove(uid)));

                var notification = new AppNotification
                {
                    ReceiverId = connectedUserId,
                    Title = "Konto usunięte",
                    Message = $"Użytkownik {userProfile.Name} usunął konto.",
                    Type = "AccountDeleted",
                    SenderId = uid
                };

                return SendNotificationAsync(notification);
            });

            await Task.WhenAll(notificationTasks);
            await batch.CommitAsync();

            // 4. Usuwanie podkolekcji i plików użytkownika
            await DeleteUserQuestionTemplatesAsync(uid);
            await DeleteUserDailyResponsesAsync(uid);
            await DeleteUserPhotosAsync(uid);
            await DeleteUserInvitationsForDeletedAccountAsync(uid);

            // 5. Usuń główny dokument użytkownika w Firestore
            await firestore.GetCollection("users").GetDocument(uid).DeleteDocumentAsync();

            // 6. Natywne usunięcie konta w Firebase Authentication
#if ANDROID
            var androidUser = Firebase.Auth.FirebaseAuth.Instance.CurrentUser;
            if (androidUser != null) await androidUser.DeleteAsync();
#elif IOS
            var iosUser = Firebase.Auth.Auth.DefaultInstance.CurrentUser;
            if (iosUser != null) await iosUser.DeleteAsync();
#endif
        }

        // --- GENERYCZNA METODA DO USUWANIA DOKUMENTÓW W KOLEKCJACH ---
        private async Task DeleteAllDocumentsAsync<T>(ICollectionReference collectionRef, Func<T, string> idSelector)
        {
            var snapshot = await collectionRef.GetDocumentsAsync<T>();
            if (snapshot?.Documents == null) return;

            var deleteTasks = snapshot.Documents
                .Where(doc => doc.Data != null && !string.IsNullOrEmpty(idSelector(doc.Data)))
                .Select(doc => collectionRef.GetDocument(idSelector(doc.Data)).DeleteDocumentAsync());

            await Task.WhenAll(deleteTasks);
        }

        private async Task DeleteUserQuestionTemplatesAsync(string uid)
        {
            var refCol = CrossFirebaseFirestore.Current.GetCollection("users").GetDocument(uid).GetCollection("question_templates");
            await DeleteAllDocumentsAsync<QuestionTemplate>(refCol, q => q.Id);
        }

        private async Task DeleteUserDailyResponsesAsync(string uid)
        {
            var refCol = CrossFirebaseFirestore.Current.GetCollection("users").GetDocument(uid).GetCollection("daily_responses");
            await DeleteAllDocumentsAsync<DailyResponse>(refCol, r => r.Id);
        }

        private async Task DeleteUserPhotosAsync(string uid)
        {
            try
            {
                var storage = CrossFirebaseStorage.Current;
                var folderRef = storage.GetRootReference().GetChild($"daily_photos/{uid}");
                var result = await folderRef.ListAllAsync();

                if (result?.Items != null && result.Items.Any())
                {
                    var deleteTasks = result.Items.Select(itemRef => itemRef.DeleteAsync());
                    await Task.WhenAll(deleteTasks); // Równoległe usuwanie zdjęć
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Storage] Brak zdjęć do usunięcia lub błąd: {ex.Message}");
            }
        }

        private async Task DeleteUserInvitationsForDeletedAccountAsync(string uid)
        {
            var firestore = CrossFirebaseFirestore.Current;
            var colRef = firestore.GetCollection("invitations");

            var sentSnapshot = await colRef.WhereEqualsTo("senderId", uid).GetDocumentsAsync<Invitation>();
            var receivedSnapshot = await colRef.WhereEqualsTo("receiverId", uid).GetDocumentsAsync<Invitation>();

            var deleteTasks = new List<Task>();

            if (sentSnapshot?.Documents != null)
                deleteTasks.AddRange(sentSnapshot.Documents.Where(d => d.Data != null).Select(d => colRef.GetDocument(d.Data.Id).DeleteDocumentAsync()));

            if (receivedSnapshot?.Documents != null)
                deleteTasks.AddRange(receivedSnapshot.Documents.Where(d => d.Data != null).Select(d => colRef.GetDocument(d.Data.Id).DeleteDocumentAsync()));

            await Task.WhenAll(deleteTasks);
        }

        public async Task UpdateFcmTokenAsync()
        {
            string? uid = Preferences.Get("UserId", string.Empty);
            if (string.IsNullOrEmpty(uid)) return;

            try
            {
                var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                if (status != PermissionStatus.Granted) return;

                await CrossFirebaseCloudMessaging.Current.CheckIfValidAsync();
                var token = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();

                if (!string.IsNullOrEmpty(token))
                {
                    await CrossFirebaseFirestore.Current
                        .GetCollection("users")
                        .GetDocument(uid)
                        .UpdateDataAsync(("fcmToken", token)); // Użycie Tuple
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
                await CrossFirebaseFirestore.Current
                    .GetCollection("users")
                    .GetDocument(uid)
                    .UpdateDataAsync(("fcmToken", string.Empty)); // Użycie Tuple
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd podczas usuwania tokenu FCM: {ex.Message}");
            }
        }

        public async Task UpdateFieldAsync(string collectionName, string documentId, string fieldName, object value)
        {
            var firestore = CrossFirebaseFirestore.Current;
            await firestore.GetCollection(collectionName)
                           .GetDocument(documentId)
                           .UpdateDataAsync((fieldName, value)); // Użycie Tuple
        }

        public async Task UpdateUserProfileAsync(string uid, string phoneNumber, string sex)
        {
            try
            {
                await CrossFirebaseFirestore.Current
                    .GetCollection("users")
                    .GetDocument(uid)
                    .UpdateDataAsync(
                        ("phoneNumber", phoneNumber),
                        ("sex", sex)); // Użycie Tuple
            }
            catch (Exception ex)
            {
                throw new Exception($"Nie udało się zaktualizować profilu: {ex.Message}");
            }
        }

        public async Task<bool> UpdateUserSettingsAsync(string userId, int dayStartHour, bool isRestricted, int startHour, int endHour)
        {
            try
            {
                await CrossFirebaseFirestore.Current
                    .GetCollection("users")
                    .GetDocument(userId)
                    .UpdateDataAsync(
                        ("dayStartHour", dayStartHour),
                        ("isActivityTimeRestricted", isRestricted),
                        ("activityRestrictionStartHour", startHour),
                        ("activityRestrictionEndHour", endHour)); // Użycie Tuple
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

        public IDisposable ListenForNotifications(string myUid, Action<List<AppNotification>> onUpdate)
        {
            var firestore = CrossFirebaseFirestore.Current;

            return firestore.GetCollection("notifications")
                .WhereEqualsTo("receiverId", myUid)
                .AddSnapshotListener<AppNotification>((snapshot) =>
                {
                    var notifications = snapshot.Documents
                                                .Select(doc => doc.Data)
                                                .OrderByDescending(n => n.CreatedAt)
                                                .ToList();
                    onUpdate?.Invoke(notifications);
                });
        }

        public async Task DeleteNotificationAsync(string notificationId)
        {
            var firestore = CrossFirebaseFirestore.Current;
            await firestore.GetCollection("notifications")
                           .GetDocument(notificationId)
                           .DeleteDocumentAsync();
        }

        public async Task UpdateLastActiveAsync()
        {
            string? uid = Preferences.Get("UserId", string.Empty);
            if (string.IsNullOrEmpty(uid)) return;

            try
            {
                await CrossFirebaseFirestore.Current
                    .GetCollection("users")
                    .GetDocument(uid)
                    .UpdateDataAsync(("lastActiveAt", DateTimeOffset.UtcNow)); // Użycie Tuple
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Nie udało się zaktualizować aktywności: {ex.Message}");
            }
        }
        #endregion

        #region Zaproszenia (Invitations)

        public async Task<bool> SendInvitationAsync(string senderId, string senderName, bool isSenderCaregiver, string receiverEmail)
        {
            var firestore = CrossFirebaseFirestore.Current;

            var usersQuery = await firestore.GetCollection("users")
                                            .WhereEqualsTo("email", receiverEmail.ToLower())
                                            .GetDocumentsAsync<User>();

            var receiver = usersQuery.Documents.FirstOrDefault()?.Data;

            if (receiver == null) throw new Exception("Użytkownik z podanym adresem email nie został znaleziony.");
            if (receiver.Id == senderId) throw new Exception("Nie możesz wysłać zaproszenia do samego siebie.");
            if (isSenderCaregiver && receiver.IsCaregiver) throw new Exception("Opiekun nie może zaprosić innego opiekuna. Zaproszenia można wysyłać tylko do podopiecznych.");
            if (!isSenderCaregiver && !receiver.IsCaregiver) throw new Exception("Podopieczny nie może zaprosić innego podopiecznego. Zaproszenia można wysyłać tylko do opiekunów.");

            var existingSent = await firestore.GetCollection("invitations")
                                              .WhereEqualsTo("senderId", senderId)
                                              .WhereEqualsTo("receiverId", receiver.Id)
                                              .GetDocumentsAsync<Invitation>();

            if (existingSent.Documents.Any()) throw new Exception("Zaproszenie do tego użytkownika zostało już wysłane lub odrzucone. Usuń stare zaproszenie, aby wysłać nowe.");

            var existingReceived = await firestore.GetCollection("invitations")
                                                  .WhereEqualsTo("senderId", receiver.Id)
                                                  .WhereEqualsTo("receiverId", senderId)
                                                  .GetDocumentsAsync<Invitation>();

            if (existingReceived.Documents.Any()) throw new Exception("Ten użytkownik wysłał już zaproszenie do Ciebie.");

            var request = new Invitation
            {
                SenderId = senderId,
                SenderName = senderName,
                ReceiverId = receiver.Id,
                ReceiverEmail = receiverEmail,
                IsSenderCaregiver = isSenderCaregiver,
                Status = "Pending"
            };

            await firestore.GetCollection("invitations").AddDocumentAsync(request);

            if (receiver.SystemNotificationFilter is null or "All")
            {
                await SendNotificationAsync(new AppNotification
                {
                    ReceiverId = receiver.Id,
                    Title = "Nowe zaproszenie",
                    Message = $"Masz nowe zaproszenie od {senderName}.",
                    Type = "NewInvitation"
                });
            }

            return true;
        }

        public async Task AcceptInvitationAsync(Invitation request)
        {
            var firestore = CrossFirebaseFirestore.Current;
            string? myUid = Preferences.Get("UserId", string.Empty);
            if (string.IsNullOrEmpty(myUid)) return;

            var batch = firestore.CreateBatch();
            var requestRef = firestore.GetCollection("invitations").GetDocument(request.Id);

            string caregiverId = request.IsSenderCaregiver ? request.SenderId : request.ReceiverId;
            string caretakerId = !request.IsSenderCaregiver ? request.SenderId : request.ReceiverId;

            batch.UpdateData(firestore.GetCollection("users").GetDocument(caregiverId), ("careTakersID", FieldValue.ArrayUnion(caretakerId)));
            batch.UpdateData(firestore.GetCollection("users").GetDocument(caretakerId), ("careGiversID", FieldValue.ArrayUnion(caregiverId)));
            batch.UpdateData(requestRef, ("status", "Accepted"));

            await batch.CommitAsync();

            var myProfile = await GetUserProfileAsync(myUid);
            string myName = myProfile?.Name ?? "Użytkownik";

            var giverProfile = await GetUserProfileAsync(request.SenderId);
            if (giverProfile?.SystemNotificationFilter == "All")
            {
                await SendNotificationAsync(new AppNotification
                {
                    ReceiverId = request.SenderId,
                    Title = "Zaproszenie zaakceptowane",
                    Message = $"{myName} zaakceptował(a) Twoje zaproszenie.",
                    Type = "InvitationAccepted"
                });
            }
        }

        public async Task RejectInvitationAsync(Invitation invitation)
        {
            var firestore = CrossFirebaseFirestore.Current;
            string? myUid = Preferences.Get("UserId", string.Empty);
            if (string.IsNullOrEmpty(myUid)) return;

            await firestore.GetCollection("invitations")
                           .GetDocument(invitation.Id)
                           .UpdateDataAsync(("status", "Rejected")); // Użycie Tuple

            var myProfile = await GetUserProfileAsync(myUid);
            string myName = myProfile?.Name ?? "Użytkownik";

            var giverProfile = await GetUserProfileAsync(invitation.SenderId);
            if (giverProfile?.SystemNotificationFilter == "All")
            {
                await SendNotificationAsync(new AppNotification
                {
                    ReceiverId = invitation.SenderId,
                    Title = "Zaproszenie odrzucone",
                    Message = $"{myName} odrzucił(a) Twoje zaproszenie.",
                    Type = "InvitationRejected"
                });
            }
        }

        public async Task DeleteInvitationAsync(string invitationId)
        {
            await CrossFirebaseFirestore.Current.GetCollection("invitations")
                                                .GetDocument(invitationId)
                                                .DeleteDocumentAsync();
        }

        public async Task RemoveAcceptedInvitationAsync(string caregiverId, string caretakerId, string removerUid, string removerName)
        {
            var firestore = CrossFirebaseFirestore.Current;
            var batch = firestore.CreateBatch();

            batch.UpdateData(firestore.GetCollection("users").GetDocument(caregiverId), ("careTakersID", FieldValue.ArrayRemove(caretakerId)));
            batch.UpdateData(firestore.GetCollection("users").GetDocument(caretakerId), ("careGiversID", FieldValue.ArrayRemove(caregiverId)));
            await batch.CommitAsync();

            var queryA = await firestore.GetCollection("invitations")
                .WhereEqualsTo("senderId", caregiverId)
                .WhereEqualsTo("receiverId", caretakerId)
                .GetDocumentsAsync<Invitation>();

            var queryB = await firestore.GetCollection("invitations")
                .WhereEqualsTo("senderId", caretakerId)
                .WhereEqualsTo("receiverId", caregiverId)
                .GetDocumentsAsync<Invitation>();

            var deleteTasks = new List<Task>();
            deleteTasks.AddRange(queryA.Documents.Where(d => !string.IsNullOrEmpty(d.Data?.Id)).Select(d => DeleteInvitationAsync(d.Data.Id)));
            deleteTasks.AddRange(queryB.Documents.Where(d => !string.IsNullOrEmpty(d.Data?.Id)).Select(d => DeleteInvitationAsync(d.Data.Id)));

            await Task.WhenAll(deleteTasks); // Równoległe usuwanie powiązanych zaproszeń

            string targetUserId = (removerUid == caregiverId) ? caretakerId : caregiverId;
            await SendNotificationAsync(new AppNotification
            {
                ReceiverId = targetUserId,
                Title = "Zmiana w kontaktach",
                Message = $"Użytkownik {removerName} usunął Cię ze swojej listy kontaktów.",//$"Użytkownik {removerName} przestał być twoim [opiekunem/podopiecznym]"
                Type = "ConnectionDeleted"
            });
        }

        // Zoptymalizowane pobieranie użytkowników przy pomocy Task.WhenAll
        public async Task<List<User>> GetUsersByIdsAsync(List<string>? userIds)
        {
            if (userIds is not { Count: > 0 }) return []; // Zwracamy pustą kolekcję w nowym formacie

            var firestore = CrossFirebaseFirestore.Current.GetCollection("users");

            var tasks = userIds.Select(id => firestore.GetDocument(id).GetDocumentSnapshotAsync<User>());
            var snapshots = await Task.WhenAll(tasks);

            return snapshots.Where(s => s.Data != null).Select(s => s.Data!).ToList();
        }

        public IDisposable ListenForUserProfileUpdates(string uid, Action<User> onUpdate)
        {
            return CrossFirebaseFirestore.Current.GetCollection("users")
                .GetDocument(uid)
                .AddSnapshotListener<User>((snapshot) =>
                {
                    if (snapshot.Data != null) onUpdate?.Invoke(snapshot.Data);
                });
        }

        public IDisposable ListenForReceivedInvitations(string myUid, Action<List<Invitation>> onUpdate)
        {
            return CrossFirebaseFirestore.Current.GetCollection("invitations")
                .WhereEqualsTo("receiverId", myUid)
                .AddSnapshotListener<Invitation>((snapshot) =>
                {
                    onUpdate?.Invoke(snapshot.Documents.Select(doc => doc.Data).ToList());
                });
        }

        public IDisposable ListenForSentInvitations(string myUid, Action<List<Invitation>> onUpdate)
        {
            return CrossFirebaseFirestore.Current.GetCollection("invitations")
                .WhereEqualsTo("senderId", myUid)
                .AddSnapshotListener<Invitation>((snapshot) =>
                {
                    onUpdate?.Invoke(snapshot.Documents.Select(doc => doc.Data).ToList());
                });
        }
        #endregion

        #region Pytania i odpowiedzi (Questions & Answers)

        public async Task<List<QuestionTemplate>> GetQuestionTemplatesAsync(string careTakerId)
        {
            var snapshot = await CrossFirebaseFirestore.Current.GetCollection("users")
                                                               .GetDocument(careTakerId)
                                                               .GetCollection("question_templates")
                                                               .GetDocumentsAsync<QuestionTemplate>();

            return snapshot.Documents.Select(d => d.Data).OrderBy(q => q.OrderIndex).ToList();
        }

        public async Task SaveQuestionTemplateAsync(string careTakerId, QuestionTemplate template)
        {
            var collectionRef = CrossFirebaseFirestore.Current.GetCollection("users").GetDocument(careTakerId).GetCollection("question_templates");

            if (string.IsNullOrEmpty(template.Id))
            {
                await collectionRef.AddDocumentAsync(template);
            }
            else
            {
                await collectionRef.GetDocument(template.Id).SetDataAsync(template);
            }
        }

        public async Task DeleteQuestionTemplateAsync(string careTakerId, string templateId)
        {
            await CrossFirebaseFirestore.Current.GetCollection("users")
                                                .GetDocument(careTakerId)
                                                .GetCollection("question_templates")
                                                .GetDocument(templateId)
                                                .DeleteDocumentAsync();
        }

        public async Task<bool> HasSubmittedDailyResponseAsync(string careTakerId, string dateString)
        {
            try
            {
                var snapshot = await CrossFirebaseFirestore.Current.GetCollection("users")
                                                                   .GetDocument(careTakerId)
                                                                   .GetCollection("daily_responses")
                                                                   .GetDocument(dateString)
                                                                   .GetDocumentSnapshotAsync<DailyResponse>();

                return snapshot?.Data != null && !string.IsNullOrEmpty(snapshot.Data.EvaluationStatus);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task SaveDailyResponseAsync(string careTakerId, DailyResponse response)
        {
            var firestore = CrossFirebaseFirestore.Current;

            await firestore.GetCollection("users")
                           .GetDocument(careTakerId)
                           .GetCollection("daily_responses")
                           .GetDocument(response.Id)
                           .SetDataAsync(response);

            string? userId = Preferences.Default.Get("UserId", string.Empty);
            var userProfile = await GetUserProfileAsync(userId);

            if (userProfile != null && userProfile.IsDailyReminderEnabled)
            {
                LocalNotificationCenter.Current.Cancel(1001);

                var tomorrowNotifyTime = DateTime.Today.AddDays(1)
                                                       .AddHours(userProfile.DailyReminderHour)
                                                       .AddMinutes(userProfile.DailyReminderMinute);

                await LocalNotificationCenter.Current.Show(new NotificationRequest
                {
                    NotificationId = 1001,
                    Title = "Czas na Twój wpis!",
                    Description = "Hej! Poświęć chwilę na uzupełnienie dzisiejszej ankiety.",
                    Schedule = new NotificationRequestSchedule
                    {
                        NotifyTime = tomorrowNotifyTime,
                        RepeatType = NotificationRepeat.Daily
                    }
                });
            }

            var careTakerProfile = await GetUserProfileAsync(careTakerId);

            // Zoptymalizowane wysyłanie powiadomień przez Task.WhenAll oraz użycie Switch Expression
            if (careTakerProfile?.CaregiversID != null && careTakerProfile.CaregiversID.Any())
            {
                var notificationTasks = careTakerProfile.CaregiversID.Select(async giverId =>
                {
                    var giverProfile = await GetUserProfileAsync(giverId);
                    if (giverProfile == null) return;

                    string filter = giverProfile.SurveyNotificationFilter;

                    bool shouldNotify = filter switch
                    {
                        "All" => true,
                        "CriticalOnly" when response.TotalScore <= -3 => true,
                        "WarningAndWorse" when response.TotalScore <= -2 => true,
                        "NegativeAndWorse" when response.TotalScore <= -1 => true,
                        "NormalOnly" when response.TotalScore == 0 => true,
                        _ => false
                    };

                    if (shouldNotify)
                    {
                        await SendNotificationAsync(new AppNotification
                        {
                            ReceiverId = giverId,
                            Title = $"Nowy wpis od {careTakerProfile.Name}",
                            Message = $"Wynik: {response.TotalScore} pkt. Status: {response.EvaluationStatus}.",
                            Type = "DailyReportAlert",
                            SenderId = careTakerId,
                            Date = response.Id
                        });
                    }
                });

                await Task.WhenAll(notificationTasks);
            }
        }

        // Zoptymalizowane inicjalizowanie domyślnych pytań (wyrażenia kolekcji)
        public async Task<List<QuestionTemplate>> InitializeDefaultQuestionsAsync(string careTakerId)
        {
            int order = 0;

            List<QuestionOption> emotionOptions = [
                new() { Text = "Radość", Points = 0 },
                new() { Text = "Spokój", Points = 0 },
                new() { Text = "Zmotywowanie", Points = 0 },
                new() { Text = "Obojętność", Points = -1 },
                new() { Text = "Zmęczenie", Points = -1 },
                new() { Text = "Smutek", Points = -2 },
                new() { Text = "Lęk / Niepokój", Points = -2 },
                new() { Text = "Stres", Points = -2 },
                new() { Text = "Złość", Points = -2 }
            ];

            List<QuestionTemplate> questions = [
                new() { OrderIndex = order++, IsRandomPool = false, Type = "Closed", MaxSelections = 2, Text = "Jakie emocje czułeś na ROZPOCZĘCIE dnia?", Options = [.. emotionOptions] },
                new() { OrderIndex = order++, IsRandomPool = false, Type = "Closed", MaxSelections = 5, Text = "Jakie emocje czułeś w ŚRODKU dnia?", Options = [.. emotionOptions] },
                new() { OrderIndex = order++, IsRandomPool = false, Type = "Closed", MaxSelections = 2, Text = "Jakie emocje czułeś na ZAKOŃCZENIE dnia?", Options = [.. emotionOptions] },
                new() { OrderIndex = order++, IsRandomPool = false, Type = "Closed", MaxSelections = 1, Text = "Ile posiłków dzisiaj zjadłeś?", Options = [
                    new() { Text = "0", Points = -2 },
                    new() { Text = "1", Points = -1 },
                    new() { Text = "2", Points = 0 },
                    new() { Text = "3", Points = 0 },
                    new() { Text = "4", Points = 0 },
                    new() { Text = "5", Points = 0 },
                    new() { Text = "Więcej niż 5", Points = 0 }
                ]},
                new() { OrderIndex = order++, IsRandomPool = false, Type = "Closed", MaxSelections = 1, Text = "Czy zjadłeś dzisiaj chociaż jeden pełnowartościowy posiłek?", Options = [
                    new() { Text = "TAK", Points = 0 },
                    new() { Text = "NIE", Points = -1 }
                ]},
                new() { OrderIndex = order++, IsRandomPool = false, Type = "Closed", MaxSelections = 1, Text = "Ile godzin spałeś?", Options = [
                    new() { Text = "Poniżej 3 godzin", Points = -2 },
                    new() { Text = "3-5 godzin", Points = -1 },
                    new() { Text = "6-8 godzin", Points = 0 },
                    new() { Text = "9-11 godzin", Points = 0 },
                    new() { Text = "12 lub więcej", Points = -2 }
                ]},
                new() { OrderIndex = order++, IsRandomPool = false, Type = "Closed", MaxSelections = 1, Text = "Zaznacz na skali jak się dzisiaj czujesz:", Options = [
                    new() { Text = "Przemęczony", Points = -2 },
                    new() { Text = "Bardzo zmęczony", Points = -1 },
                    new() { Text = "Zmęczony", Points = -1 },
                    new() { Text = "W sam raz", Points = 0 },
                    new() { Text = "Pełen energii", Points = 1 }
                ]},
                new() { OrderIndex = order++, IsRandomPool = false, Type = "Open", Text = "Czy zdarzyło się dziś coś, co wywarło w tobie silne emocje? Co to było, jak się czułeś i jak się zachowałeś?" },
                new() { OrderIndex = order++, IsRandomPool = false, Type = "Open", Text = "Co dzisiaj udało ci się zrobić?" },
                new() { OrderIndex = order++, IsRandomPool = true, Type = "Closed", MaxSelections = 1, Text = "Czy czerpałeś dziś przyjemność chociaż z jednej wykonywanej czynności bądź odczuwałeś przynajmniej niewielkie zainteresowanie nią?", Options = [new() { Text = "TAK", Points = 0 }, new() { Text = "NIE", Points = -3 }] },
                new() { OrderIndex = order++, IsRandomPool = true, Type = "Closed", MaxSelections = 1, Text = "Czy miałeś problem ze skupieniem się podczas wykonywania podstawowych czynności (np. oglądanie TV, czytanie)?", Options = [new() { Text = "TAK", Points = -1 }, new() { Text = "NIE", Points = 0 }] },
                new() { OrderIndex = order++, IsRandomPool = true, Type = "Closed", MaxSelections = 1, Text = "Czy zdarzyło ci się dzisiaj ruszać lub mówić tak wolno, że zauważyli to inni (lub przeciwnie, nie mogłeś usiedzieć w miejscu)?", Options = [new() { Text = "TAK", Points = -3 }, new() { Text = "NIE", Points = 0 }] },
                new() { OrderIndex = order++, IsRandomPool = true, Type = "Open", Text = "O czym pomyślałeś jak się obudziłeś?" },
                new() { OrderIndex = order++, IsRandomPool = true, Type = "Open", Text = "Co byś chciał dzisiaj zrobić?" },
                new() { OrderIndex = order++, IsRandomPool = true, Type = "Open", Text = "Kiedy ostatni raz czułeś radość?" },
                new() { OrderIndex = order++, IsRandomPool = true, Type = "Open", Text = "Co sprawiłoby Ci radość?" },
                new() { OrderIndex = order++, IsRandomPool = true, Type = "Open", Text = "Podaj choć jedną rzecz z której byłeś dzisiaj dumny." }
            ];

            var collectionRef = CrossFirebaseFirestore.Current.GetCollection("users").GetDocument(careTakerId).GetCollection("question_templates");

            // Równoległe zapisywanie pytań
            var saveTasks = questions.Select(q => collectionRef.AddDocumentAsync(q));
            await Task.WhenAll(saveTasks);

            return questions;
        }

        public async Task<List<DailyResponse>> GetAllDailyResponsesAsync(string careTakerId)
        {
            try
            {
                var snapshot = await CrossFirebaseFirestore.Current.GetCollection("users")
                                                                   .GetDocument(careTakerId)
                                                                   .GetCollection("daily_responses")
                                                                   .GetDocumentsAsync<DailyResponse>();

                return snapshot.Documents.Where(d => d.Data != null).Select(d => d.Data).ToList();
            }
            catch (Exception)
            {
                return []; // Zwrot pustej tablicy w nowym C#
            }
        }

        public async Task<string> UploadDailyPhotoAsync(string uid, string dateId, string suffix, FileResult photo)
        {
            try
            {
                var reference = CrossFirebaseStorage.Current.GetRootReference().GetChild($"daily_photos/{uid}/{dateId}_{suffix}.jpg");
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