namespace i_am
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            LoadTheme();
        }
        private void LoadTheme()
        {
            string savedTheme = Preferences.Default.Get("AppTheme", "Systemowy");

            Application.Current!.UserAppTheme = savedTheme switch
            {
                "Jasny" => AppTheme.Light,
                "Ciemny" => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}