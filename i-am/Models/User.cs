using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plugin.Firebase.Firestore;

namespace i_am.Models
{
    public class User : IFirestoreObject
    {
        [FirestoreDocumentId]
        public string Id { get; private set; } = string.Empty;

        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty("email")]
        public string Email { get; set; } = string.Empty;

        [FirestoreProperty("phoneNumber")]
        public string PhoneNumber { get; set; } = string.Empty;

        [FirestoreProperty("birthdate")]
        public DateTimeOffset BirthDate { get; set; }

        [FirestoreProperty("sex")]
        public string Sex { get; set; } = string.Empty;

        //DateTime nie zapisuje się dobrze do Firestore, więc używamy DateTimeOffset, który jest kompatybilny z Firestore i przechowuje zarówno datę, jak i strefę czasową (mimo że niepotrzebnie)
        [FirestoreProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [FirestoreProperty("isCareGiver")]
        public bool IsCaregiver { get; set; }

        [FirestoreProperty("careTakersID")]
        public List<string> CaretakersID { get; set; } = new();

        [FirestoreProperty("careGiversID")]
        public List<string> CaregiversID { get; set; } = new();
        
        [FirestoreProperty("fcmToken")] // Token potrzebny do wysyłania powiadomień Push (FCM)
        public string FcmToken { get; set; } = string.Empty;
    }
}
