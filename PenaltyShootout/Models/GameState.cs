using PenaltyShootout.Enums;

namespace PenaltyShootout.Models;

/// <summary>Tracks all match-level state: score, round, phase, and difficulty.</summary>
public class GameState
{
    /// <summary>Player's goal count.</summary>
    public int PlayerScore { get; set; }

    /// <summary>Goalkeeper's save/miss count.</summary>
    public int KeeperScore { get; set; }

    /// <summary>Current round number (1-based).</summary>
    public int CurrentRound { get; set; } = 1;

    /// <summary>Total rounds in a normal match.</summary>
    public int TotalRounds { get; set; } = 5;

    /// <summary>Current phase of the game state machine.</summary>
    public GamePhase CurrentPhase { get; set; } = GamePhase.Aiming;

    /// <summary>Selected difficulty level.</summary>
    public Difficulty CurrentDifficulty { get; set; } = Difficulty.Medium;

    /// <summary>Whether the game is in sudden-death overtime.</summary>
    public bool IsSuddenDeath { get; set; }

    /// <summary>Result of the last shot taken.</summary>
    public ShotResult LastResult { get; set; } = ShotResult.None;

    /// <summary>Resets the game to its initial state for a new match.</summary>
    public void Reset()
    {
        PlayerScore = 0;
        KeeperScore = 0;
        CurrentRound = 1;
        CurrentPhase = GamePhase.Aiming;
        IsSuddenDeath = false;
        LastResult = ShotResult.None;
    }
}
