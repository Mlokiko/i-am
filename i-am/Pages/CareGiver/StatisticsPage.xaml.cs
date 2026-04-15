using i_am.ViewModels;

namespace i_am.Pages.CareGiver;

public partial class StatisticsPage : ContentPage
{
    private readonly StatisticsViewModel _viewModel;

    public StatisticsPage(StatisticsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.Dispatch(async () => await _viewModel.InitializeAsync());
    }
}