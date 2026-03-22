using Microsoft.Maui.Graphics.Platform;
using PenaltyShootout.ViewModels;

namespace PenaltyShootout.Views;

/// <summary>
/// Game screen: hosts the GraphicsView canvas, gesture recognizer, and game loop timer.
/// </summary>
public partial class GamePage : ContentPage
{
    private readonly GameViewModel _viewModel;
    private IDispatcherTimer? _timer;

    public GamePage(GameViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        GameCanvas.Drawable = viewModel.Drawable;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // CB-05: Stop any existing timer before creating a new one (guards against double OnAppearing)
        _timer?.Stop();
        _timer = null;

        _viewModel.Resume();

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += OnTick;
        _timer.Start();

        // Load sprites once — no-op on subsequent OnAppearing calls if already loaded
        if (_viewModel.Drawable.BallImage is null ||
            _viewModel.Drawable.GoalkeeperIdleImage is null ||
            _viewModel.Drawable.GoalkeeperCrouchImage is null)
        {
            _ = LoadAllSpritesAsync();
        }
    }

    private async Task LoadAllSpritesAsync()
    {
        var tasks = new[]
        {
            LoadSpriteAsync("ball",              img => _viewModel.Drawable.BallImage = img),
            LoadSpriteAsync("goalkeeper_idle",   img => _viewModel.Drawable.GoalkeeperIdleImage = img),
            LoadSpriteAsync("goalkeeper_crouch", img => _viewModel.Drawable.GoalkeeperCrouchImage = img),
        };
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Loads a MauiImage sprite by its base name (no extension) on all platforms.
    /// Windows:  name.scale-100.png beside the exe
    /// Android:  drawable resource via Android resource system
    /// iOS/macOS: name.png via FileSystem package API
    /// </summary>
    private static async Task LoadSpriteAsync(string baseName, Action<Microsoft.Maui.Graphics.IImage> assign)
    {
#if WINDOWS
        await Task.Run(() =>
        {
            foreach (var candidate in new[] { $"{baseName}.scale-100.png", $"{baseName}.png" })
            {
                try
                {
                    using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, candidate));
                    var img = PlatformImage.FromStream(stream);
                    if (img is not null) { assign(img); return; }
                }
                catch { }
            }
        });
#elif ANDROID
        await Task.Run(() =>
        {
            try
            {
                var context = Android.App.Application.Context;
                var resId = context.Resources?.GetIdentifier(baseName, "drawable", context.PackageName) ?? 0;
                if (resId == 0) return;

                var bitmap = Android.Graphics.BitmapFactory.DecodeResource(context.Resources, resId);
                if (bitmap is null) return;

                using var ms = new MemoryStream();
                bitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Png!, 100, ms);
                ms.Position = 0;
                var img = PlatformImage.FromStream(ms);
                if (img is not null) assign(img);
            }
            catch { }
        });
#else
        // iOS / macOS: resizetizer places assets as name.png in the app bundle
        foreach (var candidate in new[] { $"{baseName}.png", $"{baseName}@2x.png" })
        {
            try
            {
                await using var stream = await FileSystem.OpenAppPackageFileAsync(candidate);
                var img = PlatformImage.FromStream(stream);
                if (img is not null) { assign(img); return; }
            }
            catch { }
        }
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
        _timer = null;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        _viewModel.SetCanvasSize(width, height);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _viewModel.Update();
        GameCanvas.Invalidate();
    }

    private async void OnMenuButtonClicked(object? sender, EventArgs e)
    {
        bool confirmed = await DisplayAlert(
            "End Match",
            "Return to the main menu? Your current match will be lost.",
            "Yes, quit",
            "Cancel");

        if (confirmed)
            _viewModel.ReturnToMenu();
    }

    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Running:
                _viewModel.UpdateAim(e.TotalX, e.TotalY, GameCanvas.Width, GameCanvas.Height);
                break;

            case GestureStatus.Completed:
                // CB-01: Only fire shot on Completed — Canceled means OS interrupted the gesture (phone call,
                // competing scroll, etc.) and the player did not intend to shoot.
                _viewModel.Shoot(e.TotalX, e.TotalY);
                break;

            case GestureStatus.Canceled:
                // Reset the aim indicator without shooting
                _viewModel.CancelAim();
                break;
        }
    }
}
