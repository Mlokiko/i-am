using i_am.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
#if IOS
using Plugin.Firebase.Core.Platforms.iOS;
#elif ANDROID
using Plugin.Firebase.Core.Platforms.Android;
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
            builder.Services.AddTransient<MainPage>();         // Registers the page to receive the service
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
                    // 3. In the newest versions, Android requires a locator function for the Activity
                    CrossFirebase.Initialize(activity, () => Platform.CurrentActivity);
                }));
#endif
            });

            return builder;
        }
    }
}
