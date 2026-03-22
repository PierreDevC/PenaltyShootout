using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using PenaltyShootout.Services;
using PenaltyShootout.ViewModels;
using PenaltyShootout.Views;

namespace PenaltyShootout;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Audio
        builder.Services.AddSingleton<IAudioManager>(AudioManager.Current);
        builder.Services.AddSingleton<AudioService>();

        // Game services
        builder.Services.AddSingleton<PhysicsEngine>();

        // MVVM
        builder.Services.AddTransient<GameViewModel>();
        builder.Services.AddTransient<GamePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
