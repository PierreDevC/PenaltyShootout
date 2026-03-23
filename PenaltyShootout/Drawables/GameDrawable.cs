using PenaltyShootout.Enums;
using PenaltyShootout.Models;

namespace PenaltyShootout.Drawables;

/// <summary>
/// Dessine toute la scène de jeu à chaque frame. Lit uniquement les modèles — ne contient aucune logique métier.
/// Toutes les coordonnées normalisées (0–1) sont multipliées par les dimensions du canvas dans Draw().
/// </summary>
public class GameDrawable : IDrawable
{
    // Modèles — définis par GameViewModel avant le premier rendu
    public Ball? Ball { get; set; }
    public Goalkeeper? Goalkeeper { get; set; }
    public GoalPost? GoalPost { get; set; }
    public GameState? GameState { get; set; }

    // Sprites — chargés de manière asynchrone par GamePage ; retour au dessin programmatique si null
    public Microsoft.Maui.Graphics.IImage? BallImage { get; set; }
    public Microsoft.Maui.Graphics.IImage? GoalkeeperIdleImage { get; set; }
    public Microsoft.Maui.Graphics.IImage? GoalkeeperCrouchImage { get; set; }

    // État de l'indicateur de visée — défini par GameViewModel pendant la phase Aiming
    public float AimX { get; set; } = 0.5f;
    public float AimY { get; set; } = 0.5f;
    public bool ShowAimIndicator { get; set; }

    // Traînée du ballon pour l'effet visuel (N dernières positions pendant le vol)
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

    // ─── Terrain ─────────────────────────────────────────────────────────────

    private static void DrawField(ICanvas canvas, RectF r)
    {
        // Fond dégradé vert foncé
        var paint = new LinearGradientPaint
        {
            StartColor = Color.FromArgb("#1a6b1a"),
            EndColor   = Color.FromArgb("#2d9e2d"),
            StartPoint = new Point(0, 0),
            EndPoint   = new Point(0, 1)
        };

        canvas.SetFillPaint(paint, r);
        canvas.FillRectangle(r);

        // Arc de réparation — ancré depuis le bas pour rester visible sur les écrans allongés
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 2f;
        canvas.Alpha = 0.4f;
        float cx = r.Width * 0.5f;
        float arcRadius = r.Width * 0.28f;
        float spotY = r.Height - r.Width * 0.20f; // distance fixe depuis le bas, proportionnelle à la largeur
        canvas.DrawArc(cx - arcRadius, spotY - arcRadius, arcRadius * 2, arcRadius * 2, 0, 180, false, false);

        // Point de penalty
        canvas.FillColor = Colors.White;
        canvas.FillCircle(cx, spotY, 4f);

        // Lignes de la surface de réparation
        float boxLeft  = r.Width * 0.18f;
        float boxRight = r.Width * 0.82f;
        float boxTop   = r.Height * 0.60f;
        canvas.DrawRectangle(boxLeft, boxTop, boxRight - boxLeft, r.Height - boxTop);

        // Surface de but (petit rectangle)
        float gaLeft  = r.Width * 0.33f;
        float gaRight = r.Width * 0.67f;
        float gaTop   = r.Height * 0.70f;
        canvas.DrawRectangle(gaLeft, gaTop, gaRight - gaLeft, r.Height - gaTop);

        canvas.Alpha = 1f;
    }

    // ─── But ─────────────────────────────────────────────────────────────────

    private void DrawGoal(ICanvas canvas, RectF r)
    {
        if (GoalPost is null) return;

        float left   = GoalPost.Left   * r.Width;
        float top    = GoalPost.Top    * r.Height;
        float width  = GoalPost.Width  * r.Width;
        float height = GoalPost.Height * r.Height;

#if ANDROID || IOS
        // Agrandir le but sur mobile — expansion symétrique depuis le centre
        const float goalMobileScale = 1.35f;
        float extraW = width  * (goalMobileScale - 1f) / 2f;
        float extraH = height * (goalMobileScale - 1f) / 2f;
        left   -= extraW;
        top    -= extraH;
        width  *= goalMobileScale;
        height *= goalMobileScale;
#endif

        float right  = left + width;
        float bottom = top + height;

        // ── Ombre de profondeur — donne un effet 3D en creux ──
        canvas.FillColor = Color.FromArgb("#50000000");
        canvas.FillRectangle(left + 5, top + 4, width, height);

        // ── Fond du filet — dégradé du foncé (fond) au clair (avant) ──
        var netPaint = new LinearGradientPaint
        {
            StartColor = Color.FromArgb("#28ffffff"),
            EndColor   = Color.FromArgb("#55ffffff"),
            StartPoint = new Point(0, 0),
            EndPoint   = new Point(0, 1)
        };
        canvas.SetFillPaint(netPaint, new RectF(left, top, width, height));
        canvas.FillRectangle(left, top, width, height);

        // ── Maillage du filet — découpé au cadre du but ──
        canvas.SaveState();
        canvas.ClipRectangle(left, top, width, height);

        // Cordes verticales
        canvas.StrokeColor = Color.FromArgb("#75ffffff");
        canvas.StrokeSize = 0.8f;
        float colSpacing = width / 16f;
        for (float x = left; x <= right + 0.5f; x += colSpacing)
            canvas.DrawLine(x, top, x, bottom);

        // Cordes horizontales avec légère convergence en perspective vers le haut
        const int rowCount = 6;
        for (int row = 0; row <= rowCount; row++)
        {
            float t = (float)row / rowCount;
            float y = top + t * height;
            float inset = (1f - t) * width * 0.03f; // légère convergence au sommet
            canvas.DrawLine(left + inset, y, right - inset, y);
        }

        // Cordes diagonales — croisées dans les deux sens pour créer un maillage en losange
        canvas.StrokeColor = Color.FromArgb("#40ffffff");
        canvas.StrokeSize = 0.6f;
        float diagStep = colSpacing * 1.6f;
        for (float d = -height * 2f; d <= width + height; d += diagStep)
        {
            canvas.DrawLine(left + d, bottom, left + d + height, top);
            canvas.DrawLine(left + d, top,    left + d + height, bottom);
        }

        canvas.RestoreState();

        // ── Ombres des poteaux ──
        canvas.StrokeColor = Color.FromArgb("#55000000");
        canvas.StrokeSize = 9f;
        canvas.DrawLine(left  + 4, top + 3, left  + 4, bottom + 3);
        canvas.DrawLine(right + 4, top + 3, right + 4, bottom + 3);
        canvas.DrawLine(left  + 3, top + 3, right + 3, top    + 3);

        // ── Poteaux et barre transversale (blancs, épais) ──
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 6f;
        canvas.DrawLine(left,  top,    left,  bottom);
        canvas.DrawLine(right, top,    right, bottom);
        canvas.DrawLine(left,  top,    right, top);
        canvas.DrawLine(left,  bottom, right, bottom);

        // ── Reflet intérieur — simule une section de tube ronde ──
        canvas.StrokeColor = Color.FromArgb("#AAFFFFFF");
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(left  + 2, top + 1, left  + 2, bottom);
        canvas.DrawLine(right - 2, top + 1, right - 2, bottom);
        canvas.DrawLine(left  + 1, top + 2, right - 1, top   + 2);
    }

    // ─── Gardien de but ──────────────────────────────────────────────────────

    private void DrawGoalkeeper(ICanvas canvas, RectF r)
    {
        if (Goalkeeper is null || GameState is null) return;

        float cx = Goalkeeper.X * r.Width;
        float cy = Goalkeeper.Y * r.Height;

        // Taille du corps proportionnelle à la difficulté — gardien plus grand = plus difficile
        float baseBodyW = GameState.CurrentDifficulty switch
        {
            Difficulty.Easy   => 0.05f,
            Difficulty.Medium => 0.07f,
            Difficulty.Hard   => 0.10f,
            _                 => 0.07f
        };
        float baseBodyH = GameState.CurrentDifficulty switch
        {
            Difficulty.Easy   => 0.10f,
            Difficulty.Medium => 0.13f,
            Difficulty.Hard   => 0.17f,
            _                 => 0.13f
        };

        bool isSettling = GameState.CurrentPhase == GamePhase.KeeperDive;
        float bodyW = r.Width  * (isSettling ? baseBodyW * 1.15f : baseBodyW);
        float bodyH = r.Height * (isSettling ? baseBodyH * 0.85f : baseBodyH);

#if ANDROID || IOS
        // Agrandir le gardien sur mobile
        const float keeperMobileScale = 1.35f;
        bodyW *= keeperMobileScale;
        bodyH *= keeperMobileScale;
#endif

        var sprite = isSettling ? GoalkeeperCrouchImage : GoalkeeperIdleImage;

        if (sprite is not null)
        {
            // Conserver le ratio du sprite — hauteur dérivée de la largeur pour éviter l'écrasement sur écrans allongés
            float spriteBodyH = sprite.Width > 0
                ? bodyW * ((float)sprite.Height / sprite.Width)
                : bodyH;
            canvas.DrawImage(sprite, cx - bodyW / 2, cy - spriteBodyH, bodyW, spriteBodyH);
        }
        else
        {
            // Dessin de secours : rectangle jaune
            canvas.FillColor = Color.FromArgb("#FFD700");
            canvas.FillRectangle(cx - bodyW / 2, cy - bodyH / 2, bodyW, bodyH);
            canvas.StrokeColor = Colors.Black;
            canvas.StrokeSize = 2f;
            canvas.DrawRectangle(cx - bodyW / 2, cy - bodyH / 2, bodyW, bodyH);

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
        }
    }

    // ─── Ballon ──────────────────────────────────────────────────────────────

    private void DrawBall(ICanvas canvas, RectF r)
    {
        if (Ball is null || !Ball.IsVisible) return;

        // Enregistrer la traînée pendant le vol
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

        if (BallImage is not null)
        {
            // Dessiner le sprite centré sur la position du ballon
            canvas.DrawImage(BallImage, bx - radius, by - radius, radius * 2, radius * 2);
        }
        else
        {
            // Dessin de secours : cercle blanc avec patch en pentagone
            canvas.FillColor = Colors.White;
            canvas.FillCircle(bx, by, radius);
            canvas.StrokeColor = Colors.Black;
            canvas.StrokeSize = Math.Max(1f, radius * 0.12f);
            canvas.DrawCircle(bx, by, radius);

            if (radius > 4f)
            {
                canvas.StrokeColor = Color.FromArgb("#333333");
                canvas.StrokeSize = Math.Max(1f, radius * 0.15f);
                float patchR = radius * 0.45f;
                canvas.DrawArc(bx - patchR, by - patchR, patchR * 2, patchR * 2, 40, 100, false, false);
                canvas.DrawArc(bx - patchR * 0.4f, by + patchR * 0.1f, patchR * 2, patchR * 2, 180, 80, false, false);
            }
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

    // ─── Indicateur de visée ─────────────────────────────────────────────────

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

        // Cercle du point de visée
        canvas.StrokeColor = Color.FromArgb("#FFD700");
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(endX, endY, 8f);
    }

    // ─── Texte de résultat ───────────────────────────────────────────────────

    private void DrawResultText(ICanvas canvas, RectF r)
    {
        if (GameState?.CurrentPhase != GamePhase.Result) return;

        string text = GameState.LastResult switch
        {
            ShotResult.Goal => "BUT !",
            ShotResult.Save => "ARRÊTÉ !",
            ShotResult.Miss => "RATÉ !",
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

        // Ombre du texte
        canvas.FontColor = Colors.Black;
        canvas.FontSize = fontSize;
        canvas.Font = Microsoft.Maui.Graphics.Font.DefaultBold;
        canvas.DrawString(text, cx + 3, cy + 3, HorizontalAlignment.Center);

        // Texte principal
        canvas.FontColor = color;
        canvas.DrawString(text, cx, cy, HorizontalAlignment.Center);
    }

    // ─── Écran de fin de partie ───────────────────────────────────────────────

    private void DrawGameOver(ICanvas canvas, RectF r)
    {
        if (GameState?.CurrentPhase != GamePhase.GameOver) return;

        // Fond semi-transparent
        canvas.FillColor = Color.FromArgb("#CC000000");
        canvas.FillRectangle(r);

        float cx = r.Width / 2f;
        float cy = r.Height / 2f;

        string outcome = GameState.PlayerScore > GameState.KeeperScore ? "VICTOIRE !"
                       : GameState.PlayerScore < GameState.KeeperScore ? "DÉFAITE !"
                       : "ÉGALITÉ !";
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
        canvas.DrawString("Appuyez sur 'Rejouer' pour recommencer", cx, cy + r.Height * 0.1f, HorizontalAlignment.Center);
    }

    // ─── Overlay de sélection de difficulté (dessiné derrière les boutons XAML) ──

    private static void DrawDifficultyOverlay(ICanvas canvas, RectF r)
    {
        // Rien à dessiner — la sélection de difficulté est gérée par l'overlay XAML
        _ = canvas; _ = r;
    }
}
