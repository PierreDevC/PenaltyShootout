namespace PenaltyShootout.Models;

/// <summary>
/// Represents the AI goalkeeper, with normalized coordinates (0–1).
/// The keeper paces left and right continuously — no intentional dive AI.
/// </summary>
public class Goalkeeper
{
    /// <summary>Normalized horizontal center position (updated each frame by PhysicsEngine).</summary>
    public float X { get; set; } = 0.5f;

    /// <summary>Normalized vertical position (at the goal line).</summary>
    public float Y { get; set; } = 0.15f;

    /// <summary>Current pacing direction: +1 = moving right, -1 = moving left.</summary>
    public float PaceDirection { get; set; } = 1f;

    /// <summary>Resets the keeper to a random starting position along the goal line.</summary>
    public void Reset()
    {
        // Randomize start position so each round feels different
        X = 0.5f + (Random.Shared.NextSingle() - 0.5f) * 0.3f;
        Y = 0.15f;
        // Randomize initial pace direction
        PaceDirection = Random.Shared.NextSingle() > 0.5f ? 1f : -1f;
    }
}
