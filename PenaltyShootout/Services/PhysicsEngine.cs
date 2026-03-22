using PenaltyShootout.Enums;
using PenaltyShootout.Models;

namespace PenaltyShootout.Services;

/// <summary>
/// Handles ball trajectory updates, goal-line detection, keeper pacing, and result detection.
/// All timing and physics constants live here for easy tuning.
/// </summary>
public class PhysicsEngine
{
    /// <summary>Base velocity multiplier applied to shot power.</summary>
    public const float SpeedMultiplier = 1.0f;

    /// <summary>Ball shrink rate per unit of distance traveled.</summary>
    public const float ScaleDecay = 0.003f;

    /// <summary>Minimum ball visual scale (simulates maximum depth/distance).</summary>
    public const float MinScale = 0.5f;

    /// <summary>Normalized Y coordinate of the goal line.</summary>
    public const float GoalLineY = 0.15f;

    /// <summary>Minimum shot power clamp.</summary>
    public const float MinPower = 0.3f;

    /// <summary>Maximum shot power clamp.</summary>
    public const float MaxPower = 1.0f;

    /// <summary>Maximum deltaTime in seconds to prevent ball teleporting after a pause.</summary>
    public const float MaxDeltaTime = 0.05f;

    /// <summary>How long the result text ("GOAL!", "SAVED!", "MISS!") is displayed, in milliseconds.</summary>
    public const float ResultDisplayMs = 1500f;

    /// <summary>How long the keeper holds position after the ball arrives before Result is shown, in milliseconds.</summary>
    public const float KeeperSettleMs = 300f;

    // ─── Keeper Pacing ───────────────────────────────────────────────────────

    /// <summary>Keeper pacing speed on Easy — predictable but covers meaningful ground.</summary>
    public const float KeeperPaceSpeedEasy = 0.35f;

    /// <summary>Keeper pacing speed on Medium.</summary>
    public const float KeeperPaceSpeedMedium = 0.62f;

    /// <summary>Keeper pacing speed on Hard — covers the full goal in ~0.6 s.</summary>
    public const float KeeperPaceSpeedHard = 1.00f;

    /// <summary>Keeper hitbox half-width on Easy — small, tight to the body.</summary>
    public const float KeeperHalfWidthEasy   = 0.04f;

    /// <summary>Keeper hitbox half-width on Medium.</summary>
    public const float KeeperHalfWidthMedium = 0.06f;

    /// <summary>Keeper hitbox half-width on Hard — large, covers more of the goal.</summary>
    public const float KeeperHalfWidthHard   = 0.09f;

    /// <summary>Returns the save hitbox half-width for the given difficulty.</summary>
    public static float GetKeeperHalfWidth(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy   => KeeperHalfWidthEasy,
        Difficulty.Medium => KeeperHalfWidthMedium,
        Difficulty.Hard   => KeeperHalfWidthHard,
        _                 => KeeperHalfWidthMedium
    };

    /// <summary>Left pacing boundary (stays inside goal frame with a small margin).</summary>
    public const float KeeperMinX = 0.27f;

    /// <summary>Right pacing boundary (stays inside goal frame with a small margin).</summary>
    public const float KeeperMaxX = 0.73f;

    // ─── Methods ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Advances the ball's position by one frame.
    /// </summary>
    /// <param name="ball">The ball model to update.</param>
    /// <param name="deltaTime">Time elapsed since last frame, in seconds.</param>
    /// <returns>True if the ball has reached or crossed the goal line.</returns>
    public bool UpdateBall(Ball ball, float deltaTime)
    {
        float dt = Math.Min(deltaTime, MaxDeltaTime);

        ball.X += ball.VelocityX * SpeedMultiplier * dt;
        ball.Y += ball.VelocityY * SpeedMultiplier * dt;

        float speed = MathF.Sqrt(ball.VelocityX * ball.VelocityX + ball.VelocityY * ball.VelocityY);
        ball.DistanceTraveled += speed * SpeedMultiplier * dt;
        ball.Scale = MathF.Max(MinScale, 1.0f - ball.DistanceTraveled * ScaleDecay);

        return ball.Y <= GoalLineY;
    }

    /// <summary>
    /// Moves the keeper left or right at difficulty-scaled speed, bouncing at the goal edges.
    /// Called every frame during Shooting and KeeperDive phases.
    /// </summary>
    /// <param name="keeper">The goalkeeper model to update.</param>
    /// <param name="difficulty">Current difficulty level.</param>
    /// <param name="deltaTime">Time elapsed since last frame, in seconds.</param>
    public void UpdateKeeperPacing(Goalkeeper keeper, Difficulty difficulty, float deltaTime)
    {
        float dt = Math.Min(deltaTime, MaxDeltaTime);
        float speed = difficulty switch
        {
            Difficulty.Easy   => KeeperPaceSpeedEasy,
            Difficulty.Medium => KeeperPaceSpeedMedium,
            Difficulty.Hard   => KeeperPaceSpeedHard,
            _                 => KeeperPaceSpeedMedium
        };

        keeper.X += keeper.PaceDirection * speed * dt;

        // Bounce off walls
        if (keeper.X >= KeeperMaxX)
        {
            keeper.X = KeeperMaxX;
            keeper.PaceDirection = -1f;
        }
        else if (keeper.X <= KeeperMinX)
        {
            keeper.X = KeeperMinX;
            keeper.PaceDirection = 1f;
        }
    }

    /// <summary>
    /// Determines whether the ball's final X position is within the keeper's body (a save),
    /// on target but outside the keeper (a goal), or off target (a miss).
    /// Hitbox size scales with difficulty — harder keepers have a larger reach.
    /// </summary>
    /// <param name="ballX">Ball's final normalized X position.</param>
    /// <param name="keeperX">Keeper's normalized X position at time of arrival.</param>
    /// <param name="difficulty">Current difficulty, used to select hitbox size.</param>
    /// <returns>Goal, Save, or Miss.</returns>
    public ShotResult EvaluateShot(float ballX, float keeperX, Difficulty difficulty)
    {
        // Off-target: outside goal frame horizontal bounds
        if (ballX < 0.25f || ballX > 0.75f)
            return ShotResult.Miss;

        // Save: ball lands within difficulty-scaled keeper reach
        if (MathF.Abs(ballX - keeperX) <= GetKeeperHalfWidth(difficulty))
            return ShotResult.Save;

        return ShotResult.Goal;
    }
}
