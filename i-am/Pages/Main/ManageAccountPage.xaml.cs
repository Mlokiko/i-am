using i_am.ViewModels;

namespace i_am.Pages.Main;

public partial class ManageAccountPage : ContentPage
{
    private readonly ManageAccountViewModel _viewModel;

    public ManageAccountPage(ManageAccountViewModel viewModel)
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
}