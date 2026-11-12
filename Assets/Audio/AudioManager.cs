using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("Audio Components")]
    public AudioSource source;
    public AudioLibrary library;

    [Header("Private Helpers")]
    private const int MaxQueuedClips = 32;
    private Queue<AudioClip> audioQueue = new Queue<AudioClip>();
    private bool isPlaying = false;

    // Save the last clip played per PenaltyType,
    // to avoid direct repetitions
    private Dictionary<PenaltyType, AudioClip> lastPlayedClips = new Dictionary<PenaltyType, AudioClip>();


// ================= UNITY LIFECYCLE =================

private void Start()
    {
        if (library == null)
        {
            library = FindAnyObjectByType<AudioLibrary>();
        }
    }

// ================= PUBLIC METHODS =================

    public void PlayScore(int score)
    {
        if (!IsSoundAllowed() || !AreScoreAnnouncementsEnabled())
            return;

        var clip = Lib?.GetScoreClip(score);

        if (clip == null)
        {
            LogMissingClip($"Kein Clip für Score {score}");
            return;
        }
        Enqueue(clip);
    }

public void PlayPenalty(PenaltyType type)
    {
        if (!IsSoundAllowed())
            return;

        var clip = Lib?.GetPenaltyClip(type);

        if (clip == null)
        {
            LogMissingClip($"Kein Clip für Penalty {type}");
            return;
        }

        Enqueue(clip);
    }

    public void PlaySpecialClip(SpecialAudioType type)
    {
        if (!IsSoundAllowed())
            return;

        var clip = Lib?.GetSpecialClip(type);

        if (clip == null)
        {
            LogMissingClip($"Kein Clip für Special {type}");
            return;
        }

        Enqueue(clip);
    }

    /// <summary>
    /// Custom penalty sound (stable string ID).
    /// The chain lives in the AudioLibrary: recorded clip → built-in fallback from the
    /// active set (default if WillBePublished, otherwise custom) → warning.
    /// </summary>
    public void PlayCustomPenaltyClip(string penaltyId)
    {
        if (!IsSoundAllowed() || string.IsNullOrEmpty(penaltyId))
            return;

        var clip = Lib?.GetCustomClip(penaltyId);

        if (clip == null)
        {
            LogMissingClip($"Kein Clip für Custom-Penalty {penaltyId}");
            return;
        }

        Enqueue(clip);
    }

    /// <summary>
    /// Custom audio event sound (stable string ID).
    /// The chain lives in the AudioLibrary: recorded clip → built-in fallback from the
    /// active set (default if WillBePublished, otherwise custom) → warning.
    /// </summary>
    public void PlayCustomEvent(string eventId)
    {
        if (!IsSoundAllowed() || string.IsNullOrEmpty(eventId))
            return;

        var clip = Lib?.GetCustomClip(eventId);

        if (clip == null)
        {
            LogMissingClip($"Kein Clip für Custom-Event {eventId}");
            return;
        }

        Enqueue(clip);
    }

    /// <summary>
    /// Sector sound (Wall/Ceiling/Custom), Key = Sector ID. Without own
    /// recorded clip, the sector remains muted; a linked penalty
    /// Sounds still on PlayCustomPenaltyClip.
    /// </summary>
    public void PlaySectorClip(string sectorId)
    {
        if (!IsSoundAllowed() || string.IsNullOrEmpty(sectorId))
            return;

        var clip = Lib != null ? Lib.GetCustomClip(sectorId) : null;
        if (clip == null)
            return;

        Enqueue(clip);
    }

// ================= PRIVATE HELPERS =================

    private AudioLibrary Lib
    {
        get
        {
            if (library == null)
                library = FindAnyObjectByType<AudioLibrary>();

            return library;
        }
    }

    private bool IsSoundAllowed()
    {
        var settings = AppSettingsManager.Instance?.Settings;

        // Defensive checks → if settings are incomplete
        if (settings == null || settings.Sound == null)
            return true;

        return settings.Sound.Enabled;
    }

    /// <summary>
    /// Turn score announcements (score summary row, Tab 3). Default true
    /// (old saves without field as well).
    /// </summary>
    private bool AreScoreAnnouncementsEnabled()
    {
        var settings = AppSettingsManager.Instance?.Settings;

        if (settings == null || settings.Sound == null)
            return true;

        return settings.Sound.ScoreAnnouncementsEnabled;
    }

    /// <summary>
    /// Missing Clip: If the built-in fallback is active, this is a
    /// If the fallback is switched off, "no clip" is
    /// the normal case → only log, otherwise the console spams every move.
    /// </summary>
    private void LogMissingClip(string message)
    {
        if (Lib != null && !Lib.UseBuiltInFallbackEnabled)
            Debug.Log($"[AUDIO] {message} (eingebauter Fallback deaktiviert)");
        else
            Debug.LogWarning($"[AUDIO] {message}");
    }

    private void Enqueue(AudioClip clip)
    {
        if (clip == null || source == null)
            return;

        if (audioQueue.Count >= MaxQueuedClips)
        {
            Debug.LogWarning("[AUDIO] Warteschlange voll, ältester Clip wird verworfen.");
            audioQueue.Dequeue();
        }

        audioQueue.Enqueue(clip);

        // Starts coroutine only if currently none is running
        if (!isPlaying)
            StartCoroutine(PlayQueue());
    }

private IEnumerator PlayQueue()
    {
        isPlaying = true;

        // Processes the queue strictly FIFO
        while (audioQueue.Count > 0)
        {
            if (source == null)
            {
                audioQueue.Clear();
                break;
            }

            var clip = audioQueue.Dequeue();

            if (clip == null)
                continue;

            source.PlayOneShot(clip, GetVolume());

            // Waits exactly the length of the clip → sequential playback guaranteed
            yield return new WaitForSeconds(Mathf.Max(0f, clip.length));
        }

        isPlaying = false;
    }

    private AudioClip GetRandomClip(AudioClip[] clips, PenaltyType type)
    {
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning($"[AUDIO] Keine Clips für {type}");
            return null;
        }

        // If there is only one clip → no randomization is necessary
        if (clips.Length == 1)
            return clips[0];

        AudioClip lastClip = null;
        lastPlayedClips.TryGetValue(type, out lastClip);

        AudioClip newClip;
        int attempts = 0;

        // Try to find up to 10x a different clip than the last one played
        // → prevents monotonous repetitions
        do
        {
            newClip = clips[Random.Range(0, clips.Length)];
            attempts++;
        }
        while (newClip == lastClip && attempts < 10);

        // Remember the last clip played for this PenaltyType
        lastPlayedClips[type] = newClip;

        return newClip;
    }

private float GetVolume()
    {
        var settings = AppSettingsManager.Instance?.Settings;

        if (settings?.Sound == null)
            return 1f;

        return Mathf.Clamp01(settings.Sound.Volume);
    }























    

    

    


    

    


    

    


    

    
}

