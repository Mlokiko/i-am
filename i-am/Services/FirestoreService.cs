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
                // Nie wiem czy to tutaj nie jest zdublowane, pytamy o uprawnienia już w PermissionsPage, ale dla pewności sprawdzamy to jeszcze raz tutaj, bo bez tego aplikacja będzie crashować przy próbie pobrania tokenu na urządzeniach z Androidem 13+ które nie mają nadanych uprawnień do powiadomień
                // Sprawdzenie i prośba o uprawnienia (Systemowe okienko)
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

        public async Task RemoveFcmTokenAsync()
        {
            string? uid = GetCurrentUserId();
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

                // Opcjonalnie, jeśli chcesz całkowicie usunąć token z urządzenia:
                // await Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.DeleteTokenAsync();
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
        #region Pytania i odpowiedzi (Questions & Answers)

        // --- ZARZĄDZANIE SZABLONAMI PYTAŃ ---

        // Zwraca dzisiejszą datę raportowania (doba trwa od 4:00 do 4:00 następnego dnia)
        public string GetReportingDateString()
        {
            var now = DateTime.Now;
            if (now.Hour < 4)
            {
                // Jeśli jest np. 2:00 w nocy w środę, to raport zaliczamy jeszcze do wtorku
                return now.AddDays(-1).ToString("yyyy-MM-dd");
            }
            return now.ToString("yyyy-MM-dd");
        }

        // Pobieranie listy pytań dla konkretnego podopiecznego
        public async Task<List<QuestionTemplate>> GetQuestionTemplatesAsync(string careTakerId)
        {
            var firestore = CrossFirebaseFirestore.Current;
            var snapshot = await firestore.GetCollection("users")
                                          .GetDocument(careTakerId)
                                          .GetCollection("question_templates")
                                          .GetDocumentsAsync<QuestionTemplate>(); // <-- DODANY TYP TUTAJ

            // Skoro snapshot jest już zmapowany, wystarczy wyciągnąć właściwość Data
            var templates = snapshot.Documents.Select(d => d.Data).ToList();

            return templates.OrderBy(q => q.OrderIndex).ToList();
        }

        // Zapisywanie lub aktualizacja konkretnego pytania (Dla Opiekuna)
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

        // Usuwanie pytania (Dla Opiekuna)
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
                        Type = "DailyReportAlert"
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
                // Zmieniono nazwę pliku, np. 2026-04-12_front.jpg
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
