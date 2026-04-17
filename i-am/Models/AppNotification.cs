using Plugin.Firebase.Firestore;

namespace i_am.Models
{
    public class AppNotification : IFirestoreObject
    {
        [FirestoreDocumentId]
        public string Id { get; private set; } = string.Empty;

        [FirestoreProperty("receiverId")]
        public string ReceiverId { get; set; } = string.Empty;

        [FirestoreProperty("title")]
        public string Title { get; set; } = string.Empty;

        [FirestoreProperty("message")]
        public string Message { get; set; } = string.Empty;

        [FirestoreProperty("type")]     // Określa typ powiadomienia, np. "ConnectionDeleted", "Reminder", "Alert", "InvitationRejected" itp.
        public string Type { get; set; } = string.Empty;

        [FirestoreProperty("isRead")]
        public bool IsRead { get; set; } = false;

        [FirestoreProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [FirestoreProperty("senderId")]
        public string SenderId { get; set; } = string.Empty;

        [FirestoreProperty("date")]
        public string Date { get; set; } = string.Empty;
    }
}
