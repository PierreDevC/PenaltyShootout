using Plugin.Maui.Audio;

namespace PenaltyShootout.Services;

/// <summary>
/// Cross-platform audio playback wrapper for game sound effects.
/// Audio files must exist in Resources/Raw/ with MauiAsset build action.
/// </summary>
public class AudioService(IAudioManager audioManager)
{
    private IAudioPlayer? _crowdPlayer;

    /// <summary>Plays the ball kick sound effect.</summary>
    public async Task PlayKickAsync() => await PlayAsync("kick.wav");

    /// <summary>Plays the goal celebration sound.</summary>
    public async Task PlayGoalAsync() => await PlayAsync("goal.wav");

    /// <summary>Plays the save sound effect.</summary>
    public async Task PlaySaveAsync() => await PlayAsync("save.wav");

    /// <summary>Plays the miss sound effect.</summary>
    public async Task PlayMissAsync() => await PlayAsync("miss.wav");

    /// <summary>Plays the referee whistle.</summary>
    public async Task PlayWhistleAsync() => await PlayAsync("whistle.wav");

    /// <summary>Starts the looping crowd ambient sound.</summary>
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
        catch { /* Audio file not yet available */ }
    }

    /// <summary>Stops the crowd ambient sound.</summary>
    public void StopCrowdAmbience()
    {
        _crowdPlayer?.Stop();
        _crowdPlayer?.Dispose();
        _crowdPlayer = null;
    }

    /// <summary>
    /// Plays a one-shot sound effect, disposing the player when playback ends
    /// to avoid exhausting native audio handles (CB-04).
    /// </summary>
    private async Task PlayAsync(string fileName)
    {
        try
        {
            var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
            var player = audioManager.CreatePlayer(stream);
            // Dispose the native handle as soon as playback finishes
            player.PlaybackEnded += (_, _) => player.Dispose();
            player.Play();
        }
        catch { /* Audio file not yet available */ }
    }
}
