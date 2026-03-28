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
        public string Id { get; private set; }

        [FirestoreProperty("name")]
        public string Name { get; set; }

        [FirestoreProperty("age")]
        public int Age { get; set; }
    }
}
