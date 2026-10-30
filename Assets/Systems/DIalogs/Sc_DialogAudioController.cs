using UnityEngine;

/// <summary>
/// Owns dialogue audio playback state independently from dialogue presentation.
/// </summary>
public sealed class Sc_DialogAudioController
{
    private readonly AudioSource _audioSource;
    private bool _pausedByController;

    public bool HasAudioSource => _audioSource != null;
    public bool IsPlaying => _audioSource != null && _audioSource.isPlaying;

    public Sc_DialogAudioController(AudioSource audioSource)
    {
        _audioSource = audioSource;
    }

    public bool Play(AudioClip clip)
    {
        Stop();

        if (_audioSource == null || clip == null)
            return false;

        _audioSource.clip = clip;
        _audioSource.Play();
        return true;
    }

    public void Stop()
    {
        if (_audioSource == null)
            return;

        _audioSource.Stop();
        _pausedByController = false;
    }

    public void Pause()
    {
        if (!IsPlaying)
            return;

        _audioSource.Pause();
        _pausedByController = true;
    }

    public void Resume()
    {
        if (_audioSource == null || !_pausedByController)
            return;

        _audioSource.UnPause();
        _pausedByController = false;
    }
}
