using i_am.ViewModels;

namespace i_am.Pages.CareTaker;

public partial class ManageCareGiversPage : ContentPage
{
    private readonly ManageCareGiversViewModel _viewModel;

    public ManageCareGiversPage(ManageCareGiversViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Cleanup();
    }
}