using i_am.ViewModels;

namespace i_am.Pages.CareGiver;

public partial class ManageCareTakersPage : ContentPage
{
    private readonly ManageCareTakersViewModel _viewModel;

    public ManageCareTakersPage(ManageCareTakersViewModel viewModel)
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