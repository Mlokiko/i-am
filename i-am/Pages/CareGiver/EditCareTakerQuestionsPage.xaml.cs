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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}