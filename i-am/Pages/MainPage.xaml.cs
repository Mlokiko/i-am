using i_am.Models;
using Plugin.Firebase.Firestore;
using System.Collections.ObjectModel;
using i_am.Services;

namespace i_am
{
    public partial class MainPage : ContentPage
{
    public MainPage(FirestoreService firestoreService)
    {
        InitializeComponent();
    }
}
}
