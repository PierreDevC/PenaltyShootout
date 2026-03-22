using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PenaltyShootout.Drawables;
using PenaltyShootout.Enums;
using PenaltyShootout.Models;
using PenaltyShootout.Services;

namespace PenaltyShootout.ViewModels;

/// <summary>
/// Central orchestrator for all game logic. Owns models, drives physics updates,
/// manages phase transitions, and exposes observable state for the View.
/// </summary>
public partial class GameViewModel : ObservableObject
{
    private readonly PhysicsEngine _physics;
    private readonly AudioService _audio;
    private readonly Stopwatch _phaseClock = new();
    private readonly Stopwatch _deltaClock = new();

    // ─── Models ──────────────────────────────────────────────────────────────

    public Ball Ball { get; } = new();
    public Goalkeeper Goalkeeper { get; } = new();
    public GoalPost GoalPost { get; } = new();
    public GameState GameState { get; } = new();

    // ─── Observable Properties ───────────────────────────────────────────────

    [ObservableProperty] private int _playerScore;
    [ObservableProperty] private int _keeperScore;
    [ObservableProperty] private string _roundText = "Round 1 / 5";
    [ObservableProperty] private GamePhase _currentPhase = GamePhase.Aiming;
    [ObservableProperty] private bool _showDifficultySelector = true;
    [ObservableProperty] private bool _showPlayAgain;

    /// <summary>The drawable instance wired to the GraphicsView.</summary>
    public GameDrawable Drawable { get; }

    // Aim state
    private float _rawAimX = 0.5f;
    private float _rawAimY = 0.15f;
    private float _canvasWidth  = 400f;
    private float _canvasHeight = 800f;
    private const float AimSensitivity = 300f;

    // Ball final position captured before Y-clamp (for save detection)
    private float _ballFinalX;

    public GameViewModel(PhysicsEngine physics, AudioService audio)
    {
        _physics = physics;
        _audio = audio;

        Drawable = new GameDrawable
        {
            Ball       = Ball,
            Goalkeeper = Goalkeeper,
            GoalPost   = GoalPost,
            GameState  = GameState
        };
    }

    // ─── Canvas Size ─────────────────────────────────────────────────────────

    /// <summary>Called by the View to report canvas dimensions for aim normalization.</summary>
    public void SetCanvasSize(double width, double height)
    {
        _canvasWidth  = (float)Math.Max(1, width);
        _canvasHeight = (float)Math.Max(1, height);
    }

    // ─── Game Loop ───────────────────────────────────────────────────────────

    /// <summary>Called every timer tick (≈16ms) to advance game simulation.</summary>
    public void Update()
    {
        float deltaTime = _deltaClock.IsRunning
            ? (float)_deltaClock.Elapsed.TotalSeconds
            : 0f;
        _deltaClock.Restart();

        switch (GameState.CurrentPhase)
        {
            case GamePhase.Shooting:
                UpdateShooting(deltaTime);
                break;

            case GamePhase.KeeperDive:
                UpdateKeeperSettle(deltaTime);
                break;

            case GamePhase.Result:
                UpdateResult();
                break;
        }
    }

    private void UpdateShooting(float deltaTime)
    {
        // Keeper paces continuously while ball is in flight
        _physics.UpdateKeeperPacing(Goalkeeper, GameState.CurrentDifficulty, deltaTime);

        bool reachedGoal = _physics.UpdateBall(Ball, deltaTime);
        if (reachedGoal)
        {
            // Capture final X before clamping Y
            _ballFinalX = Ball.X;
            Ball.Y = PhysicsEngine.GoalLineY;
            TransitionTo(GamePhase.KeeperDive);
        }
    }

    private void UpdateKeeperSettle(float deltaTime)
    {
        // Keeper briefly continues pacing for visual naturalness, then freezes
        _physics.UpdateKeeperPacing(Goalkeeper, GameState.CurrentDifficulty, deltaTime);

        if (_phaseClock.Elapsed.TotalMilliseconds >= PhysicsEngine.KeeperSettleMs)
            TransitionTo(GamePhase.Result);
    }

    private void UpdateResult()
    {
        if (_phaseClock.Elapsed.TotalMilliseconds >= PhysicsEngine.ResultDisplayMs)
            AdvanceRound();
    }

    // ─── Phase Transitions ───────────────────────────────────────────────────

    private void TransitionTo(GamePhase phase)
    {
        GameState.CurrentPhase = phase;
        CurrentPhase = phase;
        _phaseClock.Restart();

        if (phase == GamePhase.Result)
        {
            EvaluateResult();
            UpdateScoreDisplay();
            PlayResultAudio();
        }
    }

    private void EvaluateResult()
    {
        ShotResult result = _physics.EvaluateShot(_ballFinalX, Goalkeeper.X, GameState.CurrentDifficulty);
        GameState.LastResult = result;

        switch (result)
        {
            case ShotResult.Goal:
                GameState.PlayerScore++;
                break;
            case ShotResult.Save:
                GameState.KeeperScore++;
                break;
            // Miss: neither score changes
        }
    }

    private void PlayResultAudio()
    {
        _ = GameState.LastResult switch
        {
            ShotResult.Goal => _audio.PlayGoalAsync(),
            ShotResult.Save => _audio.PlaySaveAsync(),
            ShotResult.Miss => _audio.PlayMissAsync(),
            _               => Task.CompletedTask
        };
    }

    private void AdvanceRound()
    {
        GameState.CurrentRound++;
        bool normalOver = GameState.CurrentRound > GameState.TotalRounds && !GameState.IsSuddenDeath;

        if (normalOver)
        {
            if (GameState.PlayerScore == GameState.KeeperScore)
            {
                GameState.IsSuddenDeath = true;
                ResetRound();
            }
            else
            {
                TransitionToGameOver();
            }
        }
        else if (GameState.IsSuddenDeath)
        {
            if (GameState.PlayerScore != GameState.KeeperScore)
                TransitionToGameOver();
            else
                ResetRound();
        }
        else
        {
            ResetRound();
        }
    }

    private void TransitionToGameOver()
    {
        GameState.CurrentPhase = GamePhase.GameOver;
        CurrentPhase = GamePhase.GameOver;
        ShowPlayAgain = true;
        _ = _audio.PlayWhistleAsync();
        _phaseClock.Stop();
    }

    // ─── Input Handling ──────────────────────────────────────────────────────

    /// <summary>Updates aim direction from pan gesture deltas.</summary>
    public void UpdateAim(double totalX, double totalY, double canvasWidth, double canvasHeight)
    {
        if (GameState.CurrentPhase != GamePhase.Aiming) return;

        SetCanvasSize(canvasWidth, canvasHeight);

        float normalizedX = Math.Clamp((float)(totalX / AimSensitivity), -1f, 1f);
        float normalizedY = Math.Clamp((float)(-totalY / AimSensitivity), -1f, 1f);
        float verticalT   = Math.Clamp((normalizedY + 1f) / 2f, 0f, 1f);

        _rawAimX = 0.5f + normalizedX * 0.25f;
        _rawAimY = GoalPost.Bottom - verticalT * GoalPost.Height;

        Drawable.AimX = _rawAimX;
        Drawable.AimY = _rawAimY;
        Drawable.ShowAimIndicator = true;
    }

    /// <summary>
    /// Fires the shot. A zero-length swipe (tap) is rejected to prevent a Shooting-phase freeze.
    /// </summary>
    public void Shoot(double totalX, double totalY)
    {
        if (GameState.CurrentPhase != GamePhase.Aiming) return;
        if (ShowDifficultySelector) return;

        float dx = _rawAimX - Ball.X;
        float dy = _rawAimY - Ball.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist < 1e-6f)
        {
            Drawable.ShowAimIndicator = false;
            return;
        }

        double magnitude = Math.Sqrt(totalX * totalX + totalY * totalY);
        float power = Math.Clamp((float)(magnitude / AimSensitivity), PhysicsEngine.MinPower, PhysicsEngine.MaxPower);

        Ball.VelocityX = (dx / dist) * power;
        Ball.VelocityY = (dy / dist) * power;

        Drawable.ShowAimIndicator = false;
        _deltaClock.Restart();
        TransitionTo(GamePhase.Shooting);
        _ = _audio.PlayKickAsync();
    }

    /// <summary>Resets the aim indicator when the OS cancels a pan gesture. Does not fire a shot.</summary>
    public void CancelAim()
    {
        Drawable.ShowAimIndicator = false;
    }

    // ─── Round / Game Reset ──────────────────────────────────────────────────

    private void ResetRound()
    {
        Ball.Reset();
        Goalkeeper.Reset();
        _rawAimX = 0.5f;
        _rawAimY = 0.15f;
        _ballFinalX = 0.5f;
        GameState.LastResult = ShotResult.None;
        GameState.CurrentPhase = GamePhase.Aiming;
        CurrentPhase = GamePhase.Aiming;
        Drawable.ShowAimIndicator = false;
        UpdateRoundText();
        _phaseClock.Restart();
        _deltaClock.Restart();
    }

    private void UpdateScoreDisplay()
    {
        PlayerScore = GameState.PlayerScore;
        KeeperScore = GameState.KeeperScore;
    }

    private void UpdateRoundText()
    {
        RoundText = GameState.IsSuddenDeath
            ? "Sudden Death"
            : $"Round {Math.Min(GameState.CurrentRound, GameState.TotalRounds)} / {GameState.TotalRounds}";
    }

    // ─── Commands ────────────────────────────────────────────────────────────

    /// <summary>Restarts the game from the beginning.</summary>
    [RelayCommand]
    private void PlayAgain()
    {
        GameState.Reset();
        Ball.Reset();
        Goalkeeper.Reset();
        PlayerScore = 0;
        KeeperScore = 0;
        _rawAimX = 0.5f;
        _rawAimY = 0.15f;
        _ballFinalX = 0.5f;
        UpdateRoundText();
        ShowPlayAgain = false;
        ShowDifficultySelector = true;
        GameState.CurrentPhase = GamePhase.Aiming;
        CurrentPhase = GamePhase.Aiming;
        Drawable.ShowAimIndicator = false;
        _phaseClock.Restart();
        _deltaClock.Restart();
    }

    /// <summary>Sets the difficulty and starts the game.</summary>
    [RelayCommand]
    private void SetDifficulty(string level)
    {
        GameState.CurrentDifficulty = level switch
        {
            "Easy" => Difficulty.Easy,
            "Hard" => Difficulty.Hard,
            _      => Difficulty.Medium
        };
        ShowDifficultySelector = false;
        UpdateRoundText();
        _ = _audio.StartCrowdAmbienceAsync();
        _ = _audio.PlayWhistleAsync();
        _phaseClock.Restart();
        _deltaClock.Restart();
    }

    /// <summary>Called when the app resumes to restart the delta clock.</summary>
    public void Resume() => _deltaClock.Restart();
}
