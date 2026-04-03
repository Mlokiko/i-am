using i_am.ViewModels;

namespace i_am.Pages.Authentication;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Limity daty nadal warto ustawiæ z poziomu UI
        BirthdatePicker.MaximumDate = DateTime.Today.AddYears(-5);
        BirthdatePicker.MinimumDate = DateTime.Today.AddYears(-100);
    }
}