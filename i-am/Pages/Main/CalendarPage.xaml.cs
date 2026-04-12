using i_am.ViewModels;

namespace i_am.Pages.Main;

public partial class CalendarPage : ContentPage
{
    private readonly CalendarViewModel _viewModel;

    // Mamy tylko 3 zmienne pocz¹tkowe. 
    // ¯adnego dodawania, ¿adnego zapisywania w trakcie trwania ruchu!
    private double _startScale = 1;
    private double _startX = 0;
    private double _startY = 0;

    public CalendarPage(CalendarViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Zastosowanie Twojego sprawdzonego Dispatchera
        Dispatcher.Dispatch(async () =>
        {
            await _viewModel.InitializeAsync();
        });
    }

    // --- OBS£UGA KLIKNIÊCIA W DZIEÑ KALENDARZA ---
    private void OnDayTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is CalendarDayItem tappedDay)
        {
            if (BindingContext is CalendarViewModel vm)
            {
                // Wywo³ujemy komendê SelectDayCommand, któr¹ masz w ViewModelu
                if (vm.SelectDayCommand.CanExecute(tappedDay))
                {
                    vm.SelectDayCommand.Execute(tappedDay);
                }
            }
        }
    }

    // --- METODA POMOCNICZA ---
    private void ResetImage()
    {
        EnlargedImage.Scale = 1;
        EnlargedImage.TranslationX = 0;
        EnlargedImage.TranslationY = 0;

        _startScale = 1;
        _startX = 0;
        _startY = 0;
    }

    // --- OBS£UGA POWIÊKSZANIA (Pinch) ---
    private void OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started)
        {
            // Zapisujemy skalê TYLKO raz, przy dotkniêciu ekranu
            _startScale = EnlargedImage.Scale;
        }
        else if (e.Status == GestureStatus.Running)
        {
            // Prawid³owa matematyka MAUI: Mno¿ymy skalê z momentu dotkniêcia
            // przez to, jak bardzo rozszerzy³y siê Twoje palce (e.Scale).
            double targetScale = _startScale * e.Scale;

            // Nak³adamy sztywny limit (1x - 4x)
            EnlargedImage.Scale = Math.Clamp(targetScale, 1, 4);
        }
        else if (e.Status == GestureStatus.Completed || e.Status == GestureStatus.Canceled)
        {
            // Zabezpieczenie przed krzywym powrotem
            if (EnlargedImage.Scale <= 1.05)
            {
                ResetImage();
            }
        }
    }

    // --- OBS£UGA PRZESUWANIA (Pan) ---
    private void OnPanUpdated(object sender, PanUpdatedEventArgs e)
    {
        // Przesuwanie dzia³a tylko po powiêkszeniu
        if (EnlargedImage.Scale <= 1.05) return;

        if (e.StatusType == GestureStatus.Started)
        {
            // Zapisujemy pozycjê TYLKO raz, przy dotkniêciu ekranu
            _startX = EnlargedImage.TranslationX;
            _startY = EnlargedImage.TranslationY;
        }
        else if (e.StatusType == GestureStatus.Running)
        {
            // Prawid³owa matematyka MAUI: Dodajemy przejechany dystans (e.TotalX/Y)
            // do pozycji, w której obrazek by³ przy dotkniêciu.
            EnlargedImage.TranslationX = _startX + e.TotalX;
            EnlargedImage.TranslationY = _startY + e.TotalY;
        }
    }

    // --- OBS£UGA ZAMYKANIA ---
    private void OnClosePhotoTapped(object sender, EventArgs e)
    {
        ResetImage();

        if (BindingContext is CalendarViewModel vm)
        {
            if (vm.CloseEnlargedPhotoCommand.CanExecute(null))
                vm.CloseEnlargedPhotoCommand.Execute(null);
        }
    }
}