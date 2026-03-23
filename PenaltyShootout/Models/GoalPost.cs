namespace PenaltyShootout.Models;

/// <summary>
/// Définit le cadre du but en coordonnées normalisées (0–1).
/// Le but s'étend de X : 0.25–0.75, Y : 0.10–0.20.
/// Les propriétés sont en init-only — le cadre est fixe pour toute la durée de la partie.
/// </summary>
public class GoalPost
{
    /// <summary>Bord gauche normalisé du but.</summary>
    public float Left { get; init; } = 0.25f;

    /// <summary>Bord supérieur normalisé du but.</summary>
    public float Top { get; init; } = 0.10f;

    /// <summary>Largeur normalisée du but.</summary>
    public float Width { get; init; } = 0.50f;

    /// <summary>Hauteur normalisée du but.</summary>
    public float Height { get; init; } = 0.10f;

    /// <summary>Bord droit normalisé du but.</summary>
    public float Right => Left + Width;

    /// <summary>Bord inférieur normalisé du but.</summary>
    public float Bottom => Top + Height;
}
