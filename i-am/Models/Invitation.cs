using Plugin.Firebase.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace i_am.Models
{
    public class Invitation : IFirestoreObject
    {
        [FirestoreDocumentId]
        public string Id { get; private set; } = string.Empty;

        [FirestoreProperty("senderId")]
        public string SenderId { get; set; } = string.Empty;

        [FirestoreProperty("senderName")]
        public string SenderName { get; set; } = string.Empty;

        [FirestoreProperty("receiverId")]
        public string ReceiverId { get; set; } = string.Empty;

        [FirestoreProperty("status")] // "Pending", "Accepted", "Rejected"
        public string Status { get; set; } = "Pending";

        [FirestoreProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [FirestoreProperty("isSenderCaregiver")]
        public bool IsSenderCaregiver { get; set; }
        // Zmienna tylko lokalna, nie wysyłana do firestore
        public bool IsSentByMe { get; set; }
    }
}
