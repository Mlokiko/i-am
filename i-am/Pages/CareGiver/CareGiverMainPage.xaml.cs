using i_am.ViewModels;

namespace i_am.Pages.CareGiver;

public partial class CareGiverMainPage : ContentPage
{
    public CareGiverMainPage(CareGiverMainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}