namespace PenaltyShootout.Models;

/// <summary>
/// Defines the goal frame in normalized coordinates (0–1).
/// Goal spans X: 0.25–0.75, Y: 0.10–0.20.
/// Properties are init-only — the goal frame is fixed for the lifetime of the game.
/// </summary>
public class GoalPost
{
    /// <summary>Normalized left edge of the goal.</summary>
    public float Left { get; init; } = 0.25f;

    /// <summary>Normalized top edge of the goal.</summary>
    public float Top { get; init; } = 0.10f;

    /// <summary>Normalized width of the goal.</summary>
    public float Width { get; init; } = 0.50f;

    /// <summary>Normalized height of the goal.</summary>
    public float Height { get; init; } = 0.10f;

    /// <summary>Normalized right edge of the goal.</summary>
    public float Right => Left + Width;

    /// <summary>Normalized bottom edge of the goal.</summary>
    public float Bottom => Top + Height;
}
