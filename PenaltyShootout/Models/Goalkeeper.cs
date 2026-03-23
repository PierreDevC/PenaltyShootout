namespace PenaltyShootout.Models;

/// <summary>
/// Représente le gardien de but IA, en coordonnées normalisées (0–1).
/// Le gardien se déplace continuellement de gauche à droite — sans IA de plongeon intentionnelle.
/// </summary>
public class Goalkeeper
{
    /// <summary>Position horizontale centrale normalisée (mise à jour chaque frame par PhysicsEngine).</summary>
    public float X { get; set; } = 0.5f;

    /// <summary>Position verticale normalisée — bas du cadre de but, le gardien se tient devant le filet.</summary>
    public float Y { get; set; } = 0.20f;

    /// <summary>Direction de déplacement courante : +1 = vers la droite, -1 = vers la gauche.</summary>
    public float PaceDirection { get; set; } = 1f;

    /// <summary>Remet le gardien à une position de départ aléatoire sur la ligne de but.</summary>
    public void Reset()
    {
        // Position de départ aléatoire pour que chaque manche soit différente
        X = 0.5f + (Random.Shared.NextSingle() - 0.5f) * 0.3f;
        Y = 0.20f;
        // Direction initiale aléatoire
        PaceDirection = Random.Shared.NextSingle() > 0.5f ? 1f : -1f;
    }
}
