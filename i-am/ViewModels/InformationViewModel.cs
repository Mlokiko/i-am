using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace i_am.ViewModels
{
    public partial class InformationViewModel : ObservableObject
    {
        [RelayCommand]
        private async Task OpenPhoneDialerAsync(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber)) return;

            try
            {
                if (PhoneDialer.Default.IsSupported)
                {
                    PhoneDialer.Default.Open(phoneNumber);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Błąd", $"Nie udało się otworzyć aplikacji telefonu: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        private async Task OpenWebsiteAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            try
            {
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }

                await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Błąd", $"Nie udało się otworzyć przeglądarki: {ex.Message}", "OK");
            }
        }
    }
}