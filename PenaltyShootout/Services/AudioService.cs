using Plugin.Maui.Audio;

namespace PenaltyShootout.Services;

/// <summary>
/// Wrapper de lecture audio multiplateforme pour les effets sonores du jeu.
/// Les fichiers audio doivent être présents dans Resources/Raw/ avec l'action de build MauiAsset.
/// </summary>
public class AudioService(IAudioManager audioManager)
{
    private IAudioPlayer? _crowdPlayer;

    /// <summary>Joue l'effet sonore du coup de pied.</summary>
    public async Task PlayKickAsync() => await PlayAsync("kick.wav");

    /// <summary>Joue le son de célébration d'un but.</summary>
    public async Task PlayGoalAsync() => await PlayAsync("goal.wav");

    /// <summary>Joue l'effet sonore d'un arrêt.</summary>
    public async Task PlaySaveAsync() => await PlayAsync("save.wav");

    /// <summary>Joue l'effet sonore d'un tir raté.</summary>
    public async Task PlayMissAsync() => await PlayAsync("miss.wav");

    /// <summary>Joue le coup de sifflet de l'arbitre.</summary>
    public async Task PlayWhistleAsync() => await PlayAsync("whistle.wav");

    /// <summary>Démarre l'ambiance sonore en boucle de la foule.</summary>
    public async Task StartCrowdAmbienceAsync()
    {
        if (_crowdPlayer is not null) return;
        try
        {
            var stream = await FileSystem.OpenAppPackageFileAsync("crowd.wav");
            _crowdPlayer = audioManager.CreatePlayer(stream);
            _crowdPlayer.Loop = true;
            _crowdPlayer.Volume = 0.4;
            _crowdPlayer.Play();
        }
        catch { /* Fichier audio non encore disponible */ }
    }

    /// <summary>Arrête l'ambiance sonore de la foule.</summary>
    public void StopCrowdAmbience()
    {
        _crowdPlayer?.Stop();
        _crowdPlayer?.Dispose();
        _crowdPlayer = null;
    }

    /// <summary>
    /// Joue un effet sonore ponctuel et libère le lecteur dès la fin de la lecture
    /// pour éviter d'épuiser les handles audio natifs (CB-04).
    /// </summary>
    private async Task PlayAsync(string fileName)
    {
        try
        {
            var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
            var player = audioManager.CreatePlayer(stream);
            // Libère la ressource native dès la fin de la lecture
            player.PlaybackEnded += (_, _) => player.Dispose();
            player.Play();
        }
        catch { /* Fichier audio non encore disponible */ }
    }
}
