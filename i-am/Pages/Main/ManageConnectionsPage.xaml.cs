using i_am.ViewModels;

namespace i_am.Pages.Main;

public partial class ManageConnectionsPage : ContentPage
{
    private readonly ManageConnectionsViewModel _viewModel;

    public ManageConnectionsPage(ManageConnectionsViewModel viewModel)
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