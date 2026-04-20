using Plugin.Firebase.Firestore;

namespace i_am.Models
{
    public class User : IFirestoreObject
    {
        // Id jest tworzone automatycznie po stronie firebase
        [FirestoreDocumentId]
        public string Id { get; private set; } = string.Empty;

        // Nazwa użytkownika. Wyświetla się wszędzie gdzie zarządzamy podopiecznym (nie po emailu), oraz w ManageAccounts (lista użytkowników, oraz zaproszenia). Widoczne również w ManageAccount
        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;

        // Potrzebne do logownia oraz do zapraszania użytkowników
        [FirestoreProperty("email")]
        public string Email { get; set; } = string.Empty;

        [FirestoreProperty("phoneNumber")]
        public string PhoneNumber { get; set; } = string.Empty;

        [FirestoreProperty("birthdate")]
        public DateTimeOffset BirthDate { get; set; }

        [FirestoreProperty("sex")]
        public string Sex { get; set; } = string.Empty;

        // Czas stworzenia konta. U Opiekuna nie jest wykorzystywane nigdzie (oprócz wyświetlania tej daty w ManageAccount) ale dla podopiecznego pomaga obliczyć średnią wyników od początku założenia konta (zamierzam również dodać lepsze wyświetlanie dni w kalendarzu, do tego również się przyda)
        //DateTime nie zapisuje się dobrze do Firestore, więc używamy DateTimeOffset, który jest kompatybilny z Firestore i przechowuje zarówno datę, jak i strefę czasową (mimo że niepotrzebnie)
        [FirestoreProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        // Tłumaczy się samo za siebie - czy dany użytkownik jest Opiekunem
        [FirestoreProperty("isCareGiver")]
        public bool IsCaregiver { get; set; }

        // Token potrzebny do wysyłania powiadomień Push (FCM)
        [FirestoreProperty("fcmToken")] 
        public string FcmToken { get; set; } = string.Empty;

        // Filtr wykorzystywany przy wysyłaniu powiadomień o zaproszeniach (nowe,odrzucone), usunięciu konta, usunięciu ze znajomych. TLDR; wszystkie powiadomienia oprócz DailyActivity reports
        [FirestoreProperty("systemNotificationFilter")]
        public string SystemNotificationFilter { get; set; } = "All"; // Dostępne: "CriticalOnly" (tylko usunięcia), "All"


        //
        // --- Podopieczny ---
        //

        //  Lista Id wszystkich opiekunów danego podopiecznego
        [FirestoreProperty("careGiversID")]
        public List<string> CaregiversID { get; set; } = new();

        // Czas od którego liczony jest nowy dzień. przy standardowej wartości 4. gdy podopieczny wypełnia ankiete o np. 3:00 16.04.2026, ankieta jest zaliczana jeszcze jako 15.04.2026
        [FirestoreProperty("dayStartHour")]
        public int DayStartHour { get; set; } = 4; // Domyślnie start o 4:00 rano

        // Po uruchomieniu w ustawieniach, podopieczny może ograniczyć sobie możliwość odpowiedzi do zadanych ramek czasowych
        [FirestoreProperty("isActivityTimeRestricted")] 
        public bool IsActivityTimeRestricted { get; set; } = false; // Domyślnie wyłączone ograniczenie

        [FirestoreProperty("activityRestrictionStartHour")]
        public int ActivityRestrictionStartHour { get; set; } = 4; // od 4:00 aktualnego dnia

        [FirestoreProperty("activityRestrictionEndHour")]
        public int ActivityRestrictionEndHour { get; set; } = 4; // 4:00 następnego dnia

        // Czas odświeżany kiedy podopieczny jest w aplikacji (aktualnie aktualizuje się tylko podczas LoadingPage. Używane przez funkcję Firebase do wysyłania powiadomień do opiekunów
        [FirestoreProperty("lastActiveAt")]
        public DateTimeOffset LastActiveAt { get; set; } = DateTimeOffset.UtcNow;

        // Czy podopieczny chcę otrzymywać codzienne przypomnienie o wypełnieniu ankiety
        [FirestoreProperty("isDailyReminderEnabled")]
        public bool IsDailyReminderEnabled { get; set; } = true;

        [FirestoreProperty("dailyReminderHour")]
        public int DailyReminderHour { get; set; } = 20; // Domyślnie 20:00

        [FirestoreProperty("dailyReminderMinute")]
        public int DailyReminderMinute { get; set; } = 0; // Domyślnie 20:00


        //
        // --- Opiekun ---
        //

        //  Lista Id wszystkich podopiecznych danego opiekuna
        [FirestoreProperty("careTakersID")]
        public List<string> CaretakersID { get; set; } = new();

        // Czy chce dostawać powiadomienia o braku aktywności podopiecznego
        [FirestoreProperty("inactivityAlertsEnabled")]
        public bool InactivityAlertsEnabled { get; set; } = true;

        // Po ilu godzinach ma przyjść powiadomienie
        [FirestoreProperty("inactivityThresholdHours")]
        public int InactivityThresholdHours { get; set; } = 24; 

        // Filtr dzięki któremu opiekun może ustawić sobie jakie powiadomienia wypełnienia ankiety chce otrzymywać
        [FirestoreProperty("surveyNotificationFilter")]
        public string SurveyNotificationFilter { get; set; } = "All"; // Dostępne: "CriticalOnly", "WarningAndWorse", "NegativeAndWorse", "NormalOnly", "All"
    }
}
