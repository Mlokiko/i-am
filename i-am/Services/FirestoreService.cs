using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Plugin.Firebase.Firestore;
using i_am.Models;

namespace i_am.Services
{
    public class FirestoreService
    {
        // Write Data
        public async Task AddUserAsync(User user)
        {
            var firestore = CrossFirebaseFirestore.Current;
            await firestore.GetCollection("users").AddDocumentAsync(user);
        }

        // Read Data & Return it
        public async Task<List<User>> FetchUsersAsync()
        {
            var firestore = CrossFirebaseFirestore.Current;
            var snapshot = await firestore.GetCollection("users").GetDocumentsAsync<User>();

            var usersList = new List<User>();

            foreach (var document in snapshot.Documents)
            {
                if (document.Data != null)
                {
                    usersList.Add(document.Data);
                }
            }

            return usersList;
        }
    }
}
