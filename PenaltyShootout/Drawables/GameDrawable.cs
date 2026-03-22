using PenaltyShootout.Enums;
using PenaltyShootout.Models;
using PenaltyShootout.Services;

namespace PenaltyShootout.Drawables;

/// <summary>
/// Renders the entire game scene each frame. Reads from models — contains no business logic.
/// All normalized coordinates (0–1) are multiplied by canvas dimensions inside Draw().
/// </summary>
public class GameDrawable : IDrawable
{
    // Models — set by GameViewModel before first draw
    public Ball? Ball { get; set; }
    public Goalkeeper? Goalkeeper { get; set; }
    public GoalPost? GoalPost { get; set; }
    public GameState? GameState { get; set; }

    // Aim indicator state — set by GameViewModel during Aiming phase
    public float AimX { get; set; } = 0.5f;
    public float AimY { get; set; } = 0.5f;
    public bool ShowAimIndicator { get; set; }

    // Ball trail for polish (last N positions during flight)
    private readonly record struct TrailPoint(float X, float Y, float Scale);
    private readonly Queue<TrailPoint> _trail = new();
    private const int TrailLength = 6;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Ball is null || Goalkeeper is null || GoalPost is null || GameState is null)
            return;

        DrawField(canvas, dirtyRect);
        DrawGoal(canvas, dirtyRect);
        DrawGoalkeeper(canvas, dirtyRect);
        DrawBallTrail(canvas, dirtyRect);
        DrawBall(canvas, dirtyRect);
        DrawAimIndicator(canvas, dirtyRect);
        DrawResultText(canvas, dirtyRect);
        DrawGameOver(canvas, dirtyRect);
        DrawDifficultyOverlay(canvas, dirtyRect);
    }

    // ─── Field ───────────────────────────────────────────────────────────────

    private static void DrawField(ICanvas canvas, RectF r)
    {
        // Dark green gradient background
        var paint = new LinearGradientPaint
        {
            StartColor = Color.FromArgb("#1a6b1a"),
            EndColor   = Color.FromArgb("#2d9e2d"),
            StartPoint = new Point(0, 0),
            EndPoint   = new Point(0, 1)
        };

        canvas.SetFillPaint(paint, r);
        canvas.FillRectangle(r);

        // Penalty arc — white arc at bottom-center
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 2f;
        canvas.Alpha = 0.4f;
        float cx = r.Width * 0.5f;
        float arcRadius = r.Width * 0.22f;
        canvas.DrawArc(cx - arcRadius, r.Height * 0.72f, arcRadius * 2, arcRadius * 2, 0, 180, false, false);

        // Penalty spot
        canvas.FillColor = Colors.White;
        canvas.FillCircle(cx, r.Height * 0.82f, 4f);

        // Penalty box lines
        float boxLeft  = r.Width * 0.18f;
        float boxRight = r.Width * 0.82f;
        float boxTop   = r.Height * 0.60f;
        canvas.DrawRectangle(boxLeft, boxTop, boxRight - boxLeft, r.Height - boxTop);

        // Goal area (smaller box)
        float gaLeft  = r.Width * 0.33f;
        float gaRight = r.Width * 0.67f;
        float gaTop   = r.Height * 0.70f;
        canvas.DrawRectangle(gaLeft, gaTop, gaRight - gaLeft, r.Height - gaTop);

        canvas.Alpha = 1f;
    }

    // ─── Goal ────────────────────────────────────────────────────────────────

    private void DrawGoal(ICanvas canvas, RectF r)
    {
        if (GoalPost is null) return;

        float left   = GoalPost.Left   * r.Width;
        float top    = GoalPost.Top    * r.Height;
        float width  = GoalPost.Width  * r.Width;
        float height = GoalPost.Height * r.Height;

        // Net background (light gray with transparency)
        canvas.FillColor = Color.FromArgb("#40ffffff");
        canvas.FillRectangle(left, top, width, height);

        // Net grid pattern
        canvas.StrokeColor = Color.FromArgb("#60ffffff");
        canvas.StrokeSize = 1f;
        int cols = 12;
        int rows = 4;
        for (int i = 0; i <= cols; i++)
        {
            float x = left + (width / cols) * i;
            canvas.DrawLine(x, top, x, top + height);
        }
        for (int i = 0; i <= rows; i++)
        {
            float y = top + (height / rows) * i;
            canvas.DrawLine(left, y, left + width, y);
        }

        // Goal frame posts and crossbar
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 5f;
        // Left post
        canvas.DrawLine(left, top, left, top + height);
        // Right post
        canvas.DrawLine(left + width, top, left + width, top + height);
        // Crossbar
        canvas.DrawLine(left, top, left + width, top);
        // Goal line (bottom of frame)
        canvas.DrawLine(left, top + height, left + width, top + height);
    }

    // ─── Goalkeeper ──────────────────────────────────────────────────────────

    private void DrawGoalkeeper(ICanvas canvas, RectF r)
    {
        if (Goalkeeper is null || GameState is null) return;

        float cx = Goalkeeper.X * r.Width;
        float cy = Goalkeeper.Y * r.Height;

        // Body size scales with difficulty — harder keeper is physically larger
        float baseBodyW = GameState.CurrentDifficulty switch
        {
            Difficulty.Easy   => 0.05f,
            Difficulty.Medium => 0.07f,
            Difficulty.Hard   => 0.10f,
            _                 => 0.07f
        };
        float baseBodyH = GameState.CurrentDifficulty switch
        {
            Difficulty.Easy   => 0.06f,
            Difficulty.Medium => 0.075f,
            Difficulty.Hard   => 0.10f,
            _                 => 0.075f
        };

        // Crouch slightly during KeeperDive settle phase to show alertness
        bool isSettling = GameState.CurrentPhase == GamePhase.KeeperDive;
        float bodyW = r.Width  * (isSettling ? baseBodyW * 1.15f : baseBodyW);
        float bodyH = r.Height * (isSettling ? baseBodyH * 0.85f : baseBodyH);

        // Jersey (yellow with black outline)
        canvas.FillColor = Color.FromArgb("#FFD700");
        canvas.FillRectangle(cx - bodyW / 2, cy - bodyH / 2, bodyW, bodyH);
        canvas.StrokeColor = Colors.Black;
        canvas.StrokeSize = 2f;
        canvas.DrawRectangle(cx - bodyW / 2, cy - bodyH / 2, bodyW, bodyH);

        // Head — scales slightly with body
        float headR = r.Width * (0.014f + GameState.CurrentDifficulty switch
        {
            Difficulty.Easy   => 0.000f,
            Difficulty.Medium => 0.004f,
            Difficulty.Hard   => 0.008f,
            _                 => 0.004f
        });
        canvas.FillColor = Color.FromArgb("#FFDAB9");
        canvas.FillCircle(cx, cy - bodyH / 2 - headR, headR);
        canvas.StrokeColor = Colors.Black;
        canvas.StrokeSize = 1.5f;
        canvas.DrawCircle(cx, cy - bodyH / 2 - headR, headR);

        // Arms — tips drawn at exactly KeeperHalfWidth from center so visual matches hitbox
        float hitboxPx = PhysicsEngine.GetKeeperHalfWidth(GameState.CurrentDifficulty) * r.Width;
        canvas.StrokeColor = Color.FromArgb("#FFD700");
        canvas.StrokeSize = isSettling ? 5f : 4f;
        canvas.DrawLine(cx - bodyW / 2, cy, cx - hitboxPx, cy - bodyH * 0.25f);
        canvas.DrawLine(cx + bodyW / 2, cy, cx + hitboxPx, cy - bodyH * 0.25f);
    }

    // ─── Ball ────────────────────────────────────────────────────────────────

    private void DrawBall(ICanvas canvas, RectF r)
    {
        if (Ball is null || !Ball.IsVisible) return;

        // Record trail during flight
        if (GameState?.CurrentPhase == GamePhase.Shooting)
        {
            _trail.Enqueue(new TrailPoint(Ball.X, Ball.Y, Ball.Scale));
            if (_trail.Count > TrailLength) _trail.Dequeue();
        }
        else
        {
            _trail.Clear();
        }

        float baseRadius = r.Width * 0.03f;
        float bx = Ball.X * r.Width;
        float by = Ball.Y * r.Height;
        float radius = baseRadius * Ball.Scale;

        // White ball with black outline and a pentagon hint
        canvas.FillColor = Colors.White;
        canvas.FillCircle(bx, by, radius);
        canvas.StrokeColor = Colors.Black;
        canvas.StrokeSize = Math.Max(1f, radius * 0.12f);
        canvas.DrawCircle(bx, by, radius);

        // Simple pentagon patch — two black arcs for visual interest
        if (radius > 4f)
        {
            canvas.StrokeColor = Color.FromArgb("#333333");
            canvas.StrokeSize = Math.Max(1f, radius * 0.15f);
            float patchR = radius * 0.45f;
            canvas.DrawArc(bx - patchR, by - patchR, patchR * 2, patchR * 2, 40, 100, false, false);
            canvas.DrawArc(bx - patchR * 0.4f, by + patchR * 0.1f, patchR * 2, patchR * 2, 180, 80, false, false);
        }
    }

    private void DrawBallTrail(ICanvas canvas, RectF r)
    {
        if (_trail.Count == 0) return;

        float baseRadius = r.Width * 0.03f;
        int i = 0;
        foreach (var pt in _trail)
        {
            float alpha = (float)(i + 1) / (_trail.Count + 1) * 0.4f;
            float radius = baseRadius * pt.Scale * ((float)(i + 1) / _trail.Count);
            canvas.FillColor = Color.FromArgb("#ffffff").WithAlpha(alpha);
            canvas.FillCircle(pt.X * r.Width, pt.Y * r.Height, radius);
            i++;
        }
    }

    // ─── Aim Indicator ───────────────────────────────────────────────────────

    private void DrawAimIndicator(ICanvas canvas, RectF r)
    {
        if (!ShowAimIndicator || Ball is null || GameState?.CurrentPhase != GamePhase.Aiming) return;

        float startX = Ball.X * r.Width;
        float startY = Ball.Y * r.Height;
        float endX   = AimX  * r.Width;
        float endY   = AimY  * r.Height;

        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 2.5f;
        canvas.StrokeDashPattern = [8f, 6f];
        canvas.Alpha = 0.75f;
        canvas.DrawLine(startX, startY, endX, endY);
        canvas.StrokeDashPattern = null;
        canvas.Alpha = 1f;

        // Aim point circle
        canvas.StrokeColor = Color.FromArgb("#FFD700");
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(endX, endY, 8f);
    }

    // ─── Result Text ─────────────────────────────────────────────────────────

    private void DrawResultText(ICanvas canvas, RectF r)
    {
        if (GameState?.CurrentPhase != GamePhase.Result) return;

        string text = GameState.LastResult switch
        {
            ShotResult.Goal => "GOAL!",
            ShotResult.Save => "SAVED!",
            ShotResult.Miss => "MISS!",
            _               => string.Empty
        };
        if (string.IsNullOrEmpty(text)) return;

        Color color = GameState.LastResult switch
        {
            ShotResult.Goal => Color.FromArgb("#FFD700"),
            ShotResult.Save => Color.FromArgb("#FF4444"),
            _               => Colors.White
        };

        float cx = r.Width / 2f;
        float cy = r.Height / 2f;
        float fontSize = r.Width * 0.13f;

        // Shadow
        canvas.FontColor = Colors.Black;
        canvas.FontSize = fontSize;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.DrawString(text, cx + 3, cy + 3, HorizontalAlignment.Center);

        // Main text
        canvas.FontColor = color;
        canvas.DrawString(text, cx, cy, HorizontalAlignment.Center);
    }

    // ─── Game Over ───────────────────────────────────────────────────────────

    private void DrawGameOver(ICanvas canvas, RectF r)
    {
        if (GameState?.CurrentPhase != GamePhase.GameOver) return;

        // Semi-transparent overlay
        canvas.FillColor = Color.FromArgb("#CC000000");
        canvas.FillRectangle(r);

        float cx = r.Width / 2f;
        float cy = r.Height / 2f;

        string outcome = GameState.PlayerScore > GameState.KeeperScore ? "YOU WIN!"
                       : GameState.PlayerScore < GameState.KeeperScore ? "YOU LOSE!"
                       : "DRAW!";
        Color outcomeColor = GameState.PlayerScore > GameState.KeeperScore
            ? Color.FromArgb("#FFD700") : Colors.White;

        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.FontSize = r.Width * 0.12f;
        canvas.FontColor = outcomeColor;
        canvas.DrawString(outcome, cx, cy - r.Height * 0.1f, HorizontalAlignment.Center);

        canvas.FontSize = r.Width * 0.07f;
        canvas.FontColor = Colors.White;
        canvas.DrawString(
            $"{GameState.PlayerScore} – {GameState.KeeperScore}",
            cx, cy + r.Height * 0.02f, HorizontalAlignment.Center);

        canvas.FontSize = r.Width * 0.05f;
        canvas.FontColor = Color.FromArgb("#AAAAAA");
        canvas.DrawString("Tap 'Play Again' to restart", cx, cy + r.Height * 0.1f, HorizontalAlignment.Center);
    }

    // ─── Difficulty Overlay (drawn behind XAML buttons) ──────────────────────

    private void DrawDifficultyOverlay(ICanvas canvas, RectF r)
    {
        // No drawing needed — difficulty selection is handled by XAML overlay
    }
}
