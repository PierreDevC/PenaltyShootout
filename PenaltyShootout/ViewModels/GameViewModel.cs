using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PenaltyShootout.Drawables;
using PenaltyShootout.Enums;
using PenaltyShootout.Models;
using PenaltyShootout.Services;

namespace PenaltyShootout.ViewModels;

/// <summary>
/// Orchestrateur central de toute la logique de jeu. Possède les modèles, pilote les mises à jour physiques,
/// gère les transitions de phase et expose l'état observable à la Vue.
/// </summary>
public partial class GameViewModel : ObservableObject
{
    private readonly PhysicsEngine _physics;
    private readonly AudioService _audio;
    private readonly Stopwatch _phaseClock = new();
    private readonly Stopwatch _deltaClock = new();

    // ─── Modèles ─────────────────────────────────────────────────────────────

    public Ball Ball { get; } = new();
    public Goalkeeper Goalkeeper { get; } = new();
    public GoalPost GoalPost { get; } = new();
    public GameState GameState { get; } = new();

    // ─── Propriétés observables ───────────────────────────────────────────────

#pragma warning disable MVVMTK0045
    [ObservableProperty] private int _playerScore;
    [ObservableProperty] private int _keeperScore;
    [ObservableProperty] private string _roundText = "Manche 1 / 5";
    [ObservableProperty] private GamePhase _currentPhase = GamePhase.Aiming;
    [ObservableProperty] private bool _showDifficultySelector = true;
    [ObservableProperty] private bool _showPlayAgain;
#pragma warning restore MVVMTK0045

    /// <summary>Instance du drawable connectée au GraphicsView.</summary>
    public GameDrawable Drawable { get; }

    // État de la visée
    private float _rawAimX = 0.5f;
    private float _rawAimY = 0.15f;
    private float _canvasWidth  = 400f;
    private float _canvasHeight = 800f;
    private const float AimSensitivity = 300f;

    // Position X finale du ballon capturée avant le blocage en Y (pour la détection d'arrêt)
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

    // ─── Taille du canvas ────────────────────────────────────────────────────

    /// <summary>Appelé par la Vue pour communiquer les dimensions du canvas pour la normalisation de la visée.</summary>
    public void SetCanvasSize(double width, double height)
    {
        _canvasWidth  = (float)Math.Max(1, width);
        _canvasHeight = (float)Math.Max(1, height);
    }

    // ─── Boucle de jeu ───────────────────────────────────────────────────────

    /// <summary>Appelé à chaque tick du timer (~16 ms) pour faire avancer la simulation.</summary>
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
        // Le gardien continue de se déplacer pendant que le ballon est en vol
        PhysicsEngine.UpdateKeeperPacing(Goalkeeper, GameState.CurrentDifficulty, deltaTime);

        bool reachedGoal = PhysicsEngine.UpdateBall(Ball, deltaTime);
        if (reachedGoal)
        {
            // Capturer la position X finale avant de bloquer Y sur la ligne de but
            _ballFinalX = Ball.X;
            Ball.Y = PhysicsEngine.GoalLineY;
            TransitionTo(GamePhase.KeeperDive);
        }
    }

    private void UpdateKeeperSettle(float deltaTime)
    {
        // Le gardien continue brièvement de se déplacer pour plus de naturel, puis se fige
        PhysicsEngine.UpdateKeeperPacing(Goalkeeper, GameState.CurrentDifficulty, deltaTime);

        if (_phaseClock.Elapsed.TotalMilliseconds >= PhysicsEngine.KeeperSettleMs)
            TransitionTo(GamePhase.Result);
    }

    private void UpdateResult()
    {
        if (_phaseClock.Elapsed.TotalMilliseconds >= PhysicsEngine.ResultDisplayMs)
            AdvanceRound();
    }

    // ─── Transitions de phase ─────────────────────────────────────────────────

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
        ShotResult result = PhysicsEngine.EvaluateShot(_ballFinalX, Goalkeeper.X, GameState.CurrentDifficulty);
        GameState.LastResult = result;

        switch (result)
        {
            case ShotResult.Goal:
                GameState.PlayerScore++;
                break;
            case ShotResult.Save:
                GameState.KeeperScore++;
                // Placer le ballon dans les mains du gardien — légèrement devant lui vers le joueur
                Ball.X = Goalkeeper.X;
                Ball.Y = Goalkeeper.Y + 0.03f;
                break;
            // Raté : aucun score ne change
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

    // ─── Gestion des entrées ──────────────────────────────────────────────────

    /// <summary>Met à jour la direction de visée à partir des deltas du geste de glissement.</summary>
    public void UpdateAim(double totalX, double totalY, double canvasWidth, double canvasHeight)
    {
        if (GameState.CurrentPhase != GamePhase.Aiming) return;

        SetCanvasSize(canvasWidth, canvasHeight);

        float normalizedX = Math.Clamp((float)(totalX / AimSensitivity), -1f, 1f);
        float normalizedY = Math.Clamp((float)(-totalY / AimSensitivity), -1f, 1f);
        float verticalT   = Math.Clamp((normalizedY + 1f) / 2f, 0f, 1f);

        _rawAimX = 0.5f + normalizedX * 0.40f;

        // Élargir la plage verticale pour que les tirs haut/bas soient clairement différents.
        // Sommet de visée = 0.04 au-dessus de la barre, bas de visée = 0.06 sous la ligne de but.
        float aimTop    = GoalPost.Top    - 0.04f;
        float aimBottom = GoalPost.Bottom + 0.06f;
        _rawAimY = aimBottom - verticalT * (aimBottom - aimTop);

        Drawable.AimX = _rawAimX;
        Drawable.AimY = _rawAimY;
        Drawable.ShowAimIndicator = true;
    }

    /// <summary>
    /// Tire le ballon. Un glissement de longueur nulle (simple tap) est ignoré pour éviter un blocage en phase Shooting.
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

        // Puissance déterminée par la hauteur de visée : tir haut = rapide, tir bas = lent.
        // aimT = 0 en haut de la plage, 1 en bas.
        float aimTop    = GoalPost.Top    - 0.04f;
        float aimBottom = GoalPost.Bottom + 0.06f;
        float aimT = Math.Clamp((_rawAimY - aimTop) / (aimBottom - aimTop), 0f, 1f);
        float power = PhysicsEngine.MaxPower - aimT * (PhysicsEngine.MaxPower - PhysicsEngine.MinPower);

        Ball.VelocityX = (dx / dist) * power;
        Ball.VelocityY = (dy / dist) * power;

        Drawable.ShowAimIndicator = false;
        _deltaClock.Restart();
        TransitionTo(GamePhase.Shooting);
        _ = _audio.PlayKickAsync();
    }

    /// <summary>Réinitialise l'indicateur de visée quand l'OS annule un geste. Ne tire pas le ballon.</summary>
    public void CancelAim()
    {
        Drawable.ShowAimIndicator = false;
    }

    // ─── Réinitialisation des manches et du jeu ───────────────────────────────

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
            ? "Mort Subite"
            : $"Manche {Math.Min(GameState.CurrentRound, GameState.TotalRounds)} / {GameState.TotalRounds}";
    }

    // ─── Commandes ───────────────────────────────────────────────────────────

    /// <summary>Relance le jeu depuis le début et retourne au sélecteur de difficulté.</summary>
    [RelayCommand]
    private void PlayAgain() => ReturnToMenu();

    /// <summary>Réinitialise tout l'état et affiche le sélecteur de difficulté. Appelé par PlayAgain et le bouton menu.</summary>
    public void ReturnToMenu()
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

    /// <summary>Définit la difficulté et démarre la partie.</summary>
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

    /// <summary>Appelé lors de la reprise de l'application pour redémarrer le chronomètre de delta.</summary>
    public void Resume() => _deltaClock.Restart();
}
