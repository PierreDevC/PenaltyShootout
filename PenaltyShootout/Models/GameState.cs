using PenaltyShootout.Enums;

namespace PenaltyShootout.Models;

/// <summary>Conserve tout l'état du match : score, manche, phase et difficulté.</summary>
public class GameState
{
    /// <summary>Nombre de buts marqués par le joueur.</summary>
    public int PlayerScore { get; set; }

    /// <summary>Nombre d'arrêts / de tirs ratés par le gardien.</summary>
    public int KeeperScore { get; set; }

    /// <summary>Numéro de la manche courante (base 1).</summary>
    public int CurrentRound { get; set; } = 1;

    /// <summary>Nombre total de manches dans un match normal.</summary>
    public int TotalRounds { get; set; } = 5;

    /// <summary>Phase courante de la machine à états.</summary>
    public GamePhase CurrentPhase { get; set; } = GamePhase.Aiming;

    /// <summary>Niveau de difficulté sélectionné.</summary>
    public Difficulty CurrentDifficulty { get; set; } = Difficulty.Medium;

    /// <summary>Indique si la partie est en mort subite.</summary>
    public bool IsSuddenDeath { get; set; }

    /// <summary>Résultat du dernier tir effectué.</summary>
    public ShotResult LastResult { get; set; } = ShotResult.None;

    /// <summary>Remet le jeu à son état initial pour un nouveau match.</summary>
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
