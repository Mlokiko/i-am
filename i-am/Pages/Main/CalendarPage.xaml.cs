using i_am.ViewModels;

namespace i_am.Pages.Main;

public partial class CalendarPage : ContentPage
{
    private readonly CalendarViewModel _viewModel;

    public CalendarPage(CalendarViewModel viewModel)
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
            await _viewModel.InitializeAsync();
        });
    }

    private void OnDayTapped(object sender, TappedEventArgs e)
    {
        var dayItem = e.Parameter as CalendarDayItem ?? (sender as BindableObject)?.BindingContext as CalendarDayItem;
        if (dayItem != null) _viewModel.SelectDayCommand.Execute(dayItem);
    }

    private void OnDayDetailsClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is CalendarDayItem dayItem)
        {
            _viewModel.SelectDayCommand.Execute(dayItem);
        }
    }
}