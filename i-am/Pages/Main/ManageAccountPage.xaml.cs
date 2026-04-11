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

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // U¿ywamy Dispatchera, aby unikn¹æ crashy ze StaticResource w trybie Release
        Dispatcher.Dispatch(async () =>
        {
            await _viewModel.InitializeAsync();
        });
    }
}