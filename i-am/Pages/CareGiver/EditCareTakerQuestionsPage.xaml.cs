using i_am.ViewModels;

namespace i_am.Pages.CareGiver;

public partial class EditCareTakerQuestionsPage : ContentPage
{
    private readonly EditCareTakerQuestionsViewModel _viewModel;

    public EditCareTakerQuestionsPage(EditCareTakerQuestionsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(100);
            await _viewModel.InitializeAsync();
        });
    }
}