using Microsoft.Maui.Graphics.Platform;
using PenaltyShootout.ViewModels;

namespace PenaltyShootout.Views;

/// <summary>
/// Écran de jeu : héberge le canvas GraphicsView, le gestionnaire de gestes et le timer de la boucle de jeu.
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

        // CB-05 : Arrêter tout timer existant avant d'en créer un nouveau (protège contre un double OnAppearing)
        _timer?.Stop();
        _timer = null;

        _viewModel.Resume();

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += OnTick;
        _timer.Start();

        // Charge les sprites une seule fois — sans effet lors des appels suivants si déjà chargés
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
    /// Charge un sprite MauiImage par son nom de base (sans extension) sur toutes les plateformes.
    /// Windows  : name.scale-100.png dans le dossier de l'exe
    /// Android  : ressource drawable via le système de ressources Android
    /// iOS/macOS : name.png via l'API de package FileSystem
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
        // iOS / macOS : resizetizer place les assets en tant que name.png dans le bundle de l'app
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
            "Quitter le match",
            "Retourner au menu principal ? La partie en cours sera perdue.",
            "Oui, quitter",
            "Annuler");

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
                // CB-01 : Tirer uniquement sur Completed — Canceled signifie que l'OS a interrompu le geste
                // (appel téléphonique, défilement concurrent, etc.) et que le joueur n'avait pas l'intention de tirer.
                _viewModel.Shoot(e.TotalX, e.TotalY);
                break;

            case GestureStatus.Canceled:
                // Réinitialiser l'indicateur de visée sans tirer
                _viewModel.CancelAim();
                break;
        }
    }
}
