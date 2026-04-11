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
                // Usuwamy spacje i inne znaki niebędące cyframi/plusem
                string cleanedNumber = new string(phoneNumber.Where(c => char.IsDigit(c) || c == '+').ToArray());

                if (PhoneDialer.Default.IsSupported)
                {
                    PhoneDialer.Default.Open(cleanedNumber);
                }
                else
                {
                    await Shell.Current.DisplayAlert("Błąd", "Twoje urządzenie nie obsługuje funkcji wybierania numeru.", "OK");
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