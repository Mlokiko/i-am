using i_am.ViewModels;

namespace i_am.Pages.Main;

public partial class InformationPage : ContentPage
{
    public InformationPage(InformationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}