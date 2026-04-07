using i_am.ViewModels;
using i_am.Models;

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

    private void OnEditQuestionClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is QuestionTemplate template)
            _viewModel.OpenEditQuestionEditorCommand.Execute(template);
    }

    private void OnDeleteQuestionClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is QuestionTemplate template)
            _viewModel.DeleteQuestionCommand.Execute(template);
    }

    private void OnRemoveOptionClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is EditorOptionItem option)
            _viewModel.RemoveOptionCommand.Execute(option);
    }
}