namespace PenaltyShootout.Models;

/// <summary>Représente le ballon de football, en coordonnées normalisées (0–1).</summary>
public class Ball
{
    /// <summary>Position horizontale normalisée (0 = gauche, 1 = droite).</summary>
    public float X { get; set; } = 0.5f;

    /// <summary>Position verticale normalisée (0 = haut, 1 = bas).</summary>
    public float Y { get; set; } = 1.0f;

    /// <summary>Vitesse horizontale en unités normalisées par seconde.</summary>
    public float VelocityX { get; set; }

    /// <summary>Vitesse verticale en unités normalisées par seconde.</summary>
    public float VelocityY { get; set; }

    /// <summary>Facteur d'échelle visuel (1.0 = taille réelle, 0.5 = minimum / loin).</summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>Indique si le ballon est visible à l'écran.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Distance totale parcourue par le ballon (utilisée pour la décroissance d'échelle).</summary>
    public float DistanceTraveled { get; set; }

    /// <summary>Remet le ballon à sa position initiale en bas au centre.</summary>
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
