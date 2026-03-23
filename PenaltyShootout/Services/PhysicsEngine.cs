using PenaltyShootout.Enums;
using PenaltyShootout.Models;

namespace PenaltyShootout.Services;

/// <summary>
/// Gère la trajectoire du ballon, la détection de la ligne de but, le déplacement du gardien et la détection du résultat.
/// Toutes les constantes de temps et de physique sont regroupées ici pour un réglage facile.
/// </summary>
public class PhysicsEngine
{
    /// <summary>Multiplicateur de vitesse de base appliqué à la puissance du tir.</summary>
    public const float SpeedMultiplier = 1.0f;

    /// <summary>Taux de réduction d'échelle du ballon par unité de distance parcourue.</summary>
    public const float ScaleDecay = 0.003f;

    /// <summary>Échelle visuelle minimale du ballon (simule la distance maximale).</summary>
    public const float MinScale = 0.5f;

    /// <summary>Coordonnée Y normalisée de la ligne de but.</summary>
    public const float GoalLineY = 0.15f;

    /// <summary>Puissance minimale d'un tir.</summary>
    public const float MinPower = 0.3f;

    /// <summary>Puissance maximale d'un tir.</summary>
    public const float MaxPower = 1.0f;

    /// <summary>DeltaTime maximal en secondes pour éviter que le ballon ne téléporte après une pause.</summary>
    public const float MaxDeltaTime = 0.05f;

    /// <summary>Durée d'affichage du texte de résultat ("BUT !", "ARRÊTÉ !", "RATÉ !"), en millisecondes.</summary>
    public const float ResultDisplayMs = 1500f;

    /// <summary>Durée pendant laquelle le gardien maintient sa position après l'arrivée du ballon, en millisecondes.</summary>
    public const float KeeperSettleMs = 300f;

    // ─── Déplacement du gardien ───────────────────────────────────────────────

    /// <summary>Vitesse de déplacement du gardien en Facile — prévisible mais couvre une distance significative.</summary>
    public const float KeeperPaceSpeedEasy = 0.35f;

    /// <summary>Vitesse de déplacement du gardien en Moyen.</summary>
    public const float KeeperPaceSpeedMedium = 0.62f;

    /// <summary>Vitesse de déplacement du gardien en Difficile — couvre tout le but en ~0.6 s.</summary>
    public const float KeeperPaceSpeedHard = 1.00f;

    /// <summary>Demi-largeur de la hitbox du gardien en Facile — petite, proche du corps.</summary>
    public const float KeeperHalfWidthEasy   = 0.04f;

    /// <summary>Demi-largeur de la hitbox du gardien en Moyen.</summary>
    public const float KeeperHalfWidthMedium = 0.06f;

    /// <summary>Demi-largeur de la hitbox du gardien en Difficile — grande, couvre plus du but.</summary>
    public const float KeeperHalfWidthHard   = 0.09f;

    /// <summary>Retourne la demi-largeur de la hitbox d'arrêt pour la difficulté donnée.</summary>
    public static float GetKeeperHalfWidth(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy   => KeeperHalfWidthEasy,
        Difficulty.Medium => KeeperHalfWidthMedium,
        Difficulty.Hard   => KeeperHalfWidthHard,
        _                 => KeeperHalfWidthMedium
    };

    /// <summary>Limite gauche de déplacement du gardien (reste dans le cadre du but avec une petite marge).</summary>
    public const float KeeperMinX = 0.27f;

    /// <summary>Limite droite de déplacement du gardien (reste dans le cadre du but avec une petite marge).</summary>
    public const float KeeperMaxX = 0.73f;

    // ─── Méthodes ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fait avancer la position du ballon d'une frame.
    /// </summary>
    /// <param name="ball">Le modèle du ballon à mettre à jour.</param>
    /// <param name="deltaTime">Temps écoulé depuis la dernière frame, en secondes.</param>
    /// <returns>Vrai si le ballon a atteint ou dépassé la ligne de but.</returns>
    public static bool UpdateBall(Ball ball, float deltaTime)
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
    /// Déplace le gardien vers la gauche ou la droite à la vitesse correspondant à la difficulté, en rebondissant aux bords du but.
    /// Appelé chaque frame pendant les phases Shooting et KeeperDive.
    /// </summary>
    /// <param name="keeper">Le modèle du gardien à mettre à jour.</param>
    /// <param name="difficulty">Niveau de difficulté actuel.</param>
    /// <param name="deltaTime">Temps écoulé depuis la dernière frame, en secondes.</param>
    public static void UpdateKeeperPacing(Goalkeeper keeper, Difficulty difficulty, float deltaTime)
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

        // Rebond sur les bords
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
    /// Détermine si la position X finale du ballon est dans le corps du gardien (arrêt),
    /// dans le cadre mais hors du gardien (but), ou hors cadre (raté).
    /// La taille de la hitbox évolue avec la difficulté — les gardiens difficiles ont une plus grande portée.
    /// </summary>
    /// <param name="ballX">Position X normalisée finale du ballon.</param>
    /// <param name="keeperX">Position X normalisée du gardien au moment de l'arrivée.</param>
    /// <param name="difficulty">Difficulté courante, utilisée pour choisir la taille de la hitbox.</param>
    /// <returns>But, Arrêté ou Raté.</returns>
    public static ShotResult EvaluateShot(float ballX, float keeperX, Difficulty difficulty)
    {
        // Hors cadre : en dehors des limites horizontales du but
        if (ballX < 0.25f || ballX > 0.75f)
            return ShotResult.Miss;

        // Arrêté : le ballon atterrit dans la portée du gardien selon la difficulté
        if (MathF.Abs(ballX - keeperX) <= GetKeeperHalfWidth(difficulty))
            return ShotResult.Save;

        return ShotResult.Goal;
    }
}
