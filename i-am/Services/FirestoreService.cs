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
        // wbudowana metoda w plugin nie działa prawidłowo... logowanie nie powinno tworzyć user (sic!), dlatego używam tutaj "własnego" kodu
        //public async Task<string> LoginAsync(string email, string password)
        //{
        //    var user = await CrossFirebaseAuth.Current.SignInWithEmailAndPasswordAsync(email, password);
        //    return user.Uid;
        //}
        public async Task<string> LoginAsync(string email, string password)
        {
            // Bypassing the Plugin wrapper to talk directly to Google's native SDKs
#if ANDROID
            var result = await Firebase.Auth.FirebaseAuth.Instance.SignInWithEmailAndPasswordAsync(email, password);
            return result.User.Uid;
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
        public async Task CreateUserProfileAsync(string uid, User profile)
        {
            var firestore = CrossFirebaseFirestore.Current;

            // Instead of AddDocumentAsync (which generates a random ID), 
            // we GET the specific document path using the Auth UID, and then SET the data.
            await firestore.GetCollection("users").GetDocument(uid).SetDataAsync(profile);
        }
        // Potrzebne do odczytania czy użytkownik jest opiekunem czy podopiecznym, żeby odpowiednio przekierować go do właściwego widoku
        public async Task<User?> GetUserProfileAsync(string uid)
        {
            var firestore = CrossFirebaseFirestore.Current;

            var snapshot = await firestore.GetCollection("users")
                                          .GetDocument(uid)
                                          .GetDocumentSnapshotAsync<User>();

            // 2. The plugin automatically returns null if the document doesn't exist, 
            // so we can just return the Data property directly!
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

        #endregion
    }
}
