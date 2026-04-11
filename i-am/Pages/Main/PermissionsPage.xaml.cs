using i_am.ViewModels;

namespace i_am.Pages.Main;

public partial class PermissionsPage : ContentPage
{
    private readonly PermissionsViewModel _viewModel;

    public PermissionsPage(PermissionsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Zastosowanie Twojego rozwi¹zania zapobiegaj¹cego crashom StaticResource
        Dispatcher.Dispatch(async () =>
        {
            await _viewModel.CheckCurrentPermissionsAsync();
        });
    }
}