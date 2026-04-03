using i_am.ViewModels;

namespace i_am.Pages.CareTaker;

public partial class CareTakerMainPage : ContentPage
{
    public CareTakerMainPage(CareTakerMainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}