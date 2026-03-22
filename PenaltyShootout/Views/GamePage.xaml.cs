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
