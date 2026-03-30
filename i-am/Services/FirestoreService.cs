using i_am.Models;
using Plugin.Firebase.Auth;
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
        public async Task<string> LoginAsync(string email, string password)
        {
            var user = await CrossFirebaseAuth.Current.SignInWithEmailAndPasswordAsync(email, password);
            return user.Uid;
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
        public async Task CreateUserProfileAsync(string uid, User profile)
        {
            var firestore = CrossFirebaseFirestore.Current;

            // Instead of AddDocumentAsync (which generates a random ID), 
            // we GET the specific document path using the Auth UID, and then SET the data.
            await firestore.GetCollection("users").GetDocument(uid).SetDataAsync(profile);
        }

        #endregion
    }
}
