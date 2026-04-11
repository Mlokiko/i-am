using i_am.Pages.Authentication;
using i_am.Pages.Main;
using i_am.Pages.CareGiver;
using i_am.Pages.CareTaker;

namespace i_am
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
            Routing.RegisterRoute(nameof(ManageAccountPage), typeof(ManageAccountPage));
            Routing.RegisterRoute(nameof(InformationPage), typeof(InformationPage));
            Routing.RegisterRoute(nameof(NotificationsPage), typeof(NotificationsPage));
            Routing.RegisterRoute(nameof(ManageConnectionsPage), typeof(ManageConnectionsPage));
            Routing.RegisterRoute(nameof(EditCareTakerQuestionsPage), typeof(EditCareTakerQuestionsPage));
            Routing.RegisterRoute(nameof(DailyActivityPage), typeof(DailyActivityPage));
            Routing.RegisterRoute(nameof(CalendarPage), typeof(CalendarPage));
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));

        }
    }
}
