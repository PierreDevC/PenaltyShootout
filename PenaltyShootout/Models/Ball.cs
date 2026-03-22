namespace PenaltyShootout.Models;

/// <summary>Represents the football, with normalized coordinates (0–1).</summary>
public class Ball
{
    /// <summary>Normalized horizontal position (0 = left, 1 = right).</summary>
    public float X { get; set; } = 0.5f;

    /// <summary>Normalized vertical position (0 = top, 1 = bottom).</summary>
    public float Y { get; set; } = 1.0f;

    /// <summary>Horizontal velocity in normalized units per second.</summary>
    public float VelocityX { get; set; }

    /// <summary>Vertical velocity in normalized units per second.</summary>
    public float VelocityY { get; set; }

    /// <summary>Visual scale factor (1.0 = full size, 0.5 = minimum/far away).</summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>Whether the ball is visible on screen.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Total distance the ball has traveled (used for scale decay).</summary>
    public float DistanceTraveled { get; set; }

    /// <summary>Resets the ball to its starting position at bottom-center.</summary>
    public void Reset()
    {
        X = 0.5f;
        Y = 1.0f;
        VelocityX = 0f;
        VelocityY = 0f;
        Scale = 1.0f;
        IsVisible = true;
        DistanceTraveled = 0f;
    }
}
