using i_am.Pages.Authentication;
using i_am.Pages.CareGiver;
using i_am.Pages.CareTaker;
using i_am.Pages.Main;
using i_am.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
#if IOS
using Plugin.Firebase.Core.Platforms.iOS;
#elif ANDROID
using Plugin.Firebase.Core.Platforms.Android;
using Plugin.LocalNotification;
#endif

namespace i_am
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseLocalNotification()
                .RegisterFirebaseServices()

                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<FirestoreService>(); // Creates one instance for the whole app

            builder.Services.AddTransient<LoadingPage>();
            builder.Services.AddTransient<LandingPage>();
            builder.Services.AddTransient<ViewModels.LoginViewModel>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<ViewModels.RegisterViewModel>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<ViewModels.MainPageViewModel>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<ViewModels.ManageAccountViewModel>();
            builder.Services.AddTransient<ManageAccountPage>();
            builder.Services.AddTransient<ViewModels.InformationViewModel>();
            builder.Services.AddTransient<InformationPage>();
            builder.Services.AddTransient<ViewModels.NotificationsViewModel>();
            builder.Services.AddTransient<NotificationsPage>();
            builder.Services.AddTransient<ViewModels.CalendarViewModel>();
            builder.Services.AddTransient<CalendarPage>();
            builder.Services.AddTransient<ViewModels.ManageConnectionsViewModel>();
            builder.Services.AddTransient<ManageConnectionsPage>();
            builder.Services.AddTransient<ViewModels.SettingsViewModel>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<ViewModels.PermissionsViewModel>();
            builder.Services.AddTransient<PermissionsPage>();


            builder.Services.AddTransient<ViewModels.EditCareTakerQuestionsViewModel>();
            builder.Services.AddTransient<EditCareTakerQuestionsPage>();
            builder.Services.AddTransient<ViewModels.StatisticsViewModel>();
            builder.Services.AddTransient<StatisticsPage>();


            builder.Services.AddTransient<ViewModels.DailyActivityViewModel>();
            builder.Services.AddTransient<DailyActivityPage>();


            return builder.Build();
        }
    }
    public static class FirebaseExtensions
    {
        public static MauiAppBuilder RegisterFirebaseServices(this MauiAppBuilder builder)
        {
            builder.ConfigureLifecycleEvents(events =>
            {
#if IOS
            events.AddiOS(iOS => iOS.FinishedLaunching((app, dict) => {
                // 2. Call it directly without the "Plugin.Firebase.Core" prefix
                CrossFirebase.Initialize();
                return true;
            }));
#elif ANDROID
                events.AddAndroid(android => android.OnCreate((activity, state) =>
                {
                    CrossFirebase.Initialize(activity, () => Platform.CurrentActivity!);
                }));
#endif
            });
            return builder;
        }
    }
}
