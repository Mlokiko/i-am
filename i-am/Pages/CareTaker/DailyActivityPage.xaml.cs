using i_am.ViewModels;

namespace i_am.Pages.CareTaker;

public partial class DailyActivityPage : ContentPage
{
    private readonly DailyActivityViewModel _viewModel;

    public DailyActivityPage(DailyActivityViewModel viewModel)
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