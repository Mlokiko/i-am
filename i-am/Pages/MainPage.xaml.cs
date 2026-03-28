using i_am.Models;
using Plugin.Firebase.Firestore;
using System.Collections.ObjectModel;
using i_am.Services;

namespace i_am
{
    public partial class MainPage : ContentPage
{
    private readonly FirestoreService _firestoreService;
    
    public ObservableCollection<User> UsersList { get; set; } = new ObservableCollection<User>();

    public MainPage(FirestoreService firestoreService)
    {
        InitializeComponent();
        
        _firestoreService = firestoreService;
        
        UsersCollectionView.ItemsSource = UsersList;
    }

    private async void OnAddUserClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameEntry.Text) || !int.TryParse(AgeEntry.Text, out int age))
        {
            await DisplayAlert("Error", "Please enter a valid name and age.", "OK");
            return;
        }

        var newUser = new User { Name = NameEntry.Text, Age = age };

        try
        {
            await _firestoreService.AddUserAsync(newUser);

            NameEntry.Text = string.Empty;
            AgeEntry.Text = string.Empty;
            await DisplayAlert("Success", "User added via Service!", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Database Error", ex.Message, "OK");
        }
    }

    private async void OnLoadUsersClicked(object sender, EventArgs e)
    {
        try
        {
            var fetchedUsers = await _firestoreService.FetchUsersAsync();

            UsersList.Clear();

            foreach (var user in fetchedUsers)
            {
                UsersList.Add(user);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Database Error", ex.Message, "OK");
        }
    }
}
}
