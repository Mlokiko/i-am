namespace i_am.Pages.Main;

public partial class InformationPage : ContentPage
{
	public InformationPage()
	{
		InitializeComponent();
	}
    private void OnPhoneNumberTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string phoneNumber)
        {
            try
            {
                if (PhoneDialer.Default.IsSupported)
                {
                    PhoneDialer.Default.Open(phoneNumber);
                }
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("B³¹d", $"Nie uda³o siê otworzyæ aplikacji telefonu: {ex.Message}", "OK");
                });
            }
        }
    }

    private void OnWebsiteTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string url)
        {
            try
            {
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("B³¹d", $"Nie uda³o siê otworzyæ przegl¹darki: {ex.Message}", "OK");
                });
            }
        }
    }
}