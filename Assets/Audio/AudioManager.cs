using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("Audio Components")]
    public AudioSource source;
    public AudioLibrary library;

    [Header("Private Helpers")]
    private Queue<AudioClip> audioQueue = new Queue<AudioClip>();
    private bool isPlaying = false;

    // Speichert pro PenaltyType den zuletzt abgespielten Clip,
    // um direkte Wiederholungen zu vermeiden
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

        var clip = library.GetScoreClip(score);

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

        var clip = library.GetPenaltyClip(type);

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

        var clip = library.GetSpecialClip(type);

        if (clip == null)
        {
            LogMissingClip($"Kein Clip für Special {type}");
            return;
        }

        Enqueue(clip);
    }

    /// <summary>
    /// Custom-Penalty-Sound (stabile String-ID). Die Kette steckt komplett
    /// in der AudioLibrary: aufgenommener Clip → eingebauter Fallback aus dem
    /// aktiven Set (Default bei WillBePublished, sonst Custom) → Warnung.
    /// </summary>
    public void PlayCustomPenaltyClip(string penaltyId)
    {
        if (!IsSoundAllowed() || string.IsNullOrEmpty(penaltyId))
            return;

        var clip = library.GetCustomClip(penaltyId);

        if (clip == null)
        {
            LogMissingClip($"Kein Clip für Custom-Penalty {penaltyId}");
            return;
        }

        Enqueue(clip);
    }

    /// <summary>
    /// Custom-Audio-Event-Sound (stabile String-ID). Die Kette steckt komplett
    /// in der AudioLibrary: aufgenommener Clip → eingebauter Fallback aus dem
    /// aktiven Set (Default bei WillBePublished, sonst Custom) → Warnung.
    /// </summary>
    public void PlayCustomEvent(string eventId)
    {
        if (!IsSoundAllowed() || string.IsNullOrEmpty(eventId))
            return;

        var clip = library.GetCustomClip(eventId);

        if (clip == null)
        {
            LogMissingClip($"Kein Clip für Custom-Event {eventId}");
            return;
        }

        Enqueue(clip);
    }

    /// <summary>
    /// Sektor-Sound (Wall/Ceiling/Custom), Key = Sektor-ID. Ohne eigenen
    /// aufgenommenen Clip bleibt der Sektor stumm; eine verknüpfte Strafe
    /// klingt weiterhin über PlayCustomPenaltyClip.
    /// </summary>
    public void PlaySectorClip(string sectorId)
    {
        if (!IsSoundAllowed() || string.IsNullOrEmpty(sectorId))
            return;

        if (library == null)
            library = FindAnyObjectByType<AudioLibrary>();

        var clip = library != null ? library.GetCustomClip(sectorId) : null;
        if (clip == null)
            return;

        Enqueue(clip);
    }

// ================= PRIVATE HELPERS =================

    private bool IsSoundAllowed()
    {
        var settings = AppSettingsManager.Instance.Settings;

        // Defensive Checks → falls Settings unvollständig sind
        if (settings == null || settings.Sound == null)
            return true;

        return settings.Sound.Enabled;
    }

    /// <summary>
    /// Turn-Score-Ansagen (Score-Sammel-Row, Tab 3). Default true
    /// (Altsaves ohne Feld ebenfalls).
    /// </summary>
    private bool AreScoreAnnouncementsEnabled()
    {
        var settings = AppSettingsManager.Instance.Settings;

        if (settings == null || settings.Sound == null)
            return true;

        return settings.Sound.ScoreAnnouncementsEnabled;
    }

    /// <summary>
    /// Fehlender Clip: Ist der eingebaute Fallback aktiv, ist das ein
    /// Fehlerfall → Warnung. Ist der Fallback abgeschaltet, ist "kein Clip"
    /// der Normalfall → nur Log, sonst spammt die Konsole bei jedem Zug.
    /// </summary>
    private void LogMissingClip(string message)
    {
        if (library != null && !library.UseBuiltInFallbackEnabled)
            Debug.Log($"[AUDIO] {message} (eingebauter Fallback deaktiviert)");
        else
            Debug.LogWarning($"[AUDIO] {message}");
    }

    private void Enqueue(AudioClip clip)
    {
        if (clip == null)
            return;

        audioQueue.Enqueue(clip);

        // Startet Coroutine nur, wenn aktuell keine läuft
        if (!isPlaying)
            StartCoroutine(PlayQueue());
    }

private IEnumerator PlayQueue()
    {
        isPlaying = true;

        // Arbeitet die Queue strikt FIFO ab
        while (audioQueue.Count > 0)
        {
            var clip = audioQueue.Dequeue();

            if (clip == null)
                continue;

            source.PlayOneShot(clip, GetVolume());

            // Wartet exakt die Länge des Clips → sequentielle Wiedergabe garantiert
            yield return new WaitForSeconds(clip.length);
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

        // Wenn nur ein Clip vorhanden ist → keine Randomisierung nötig
        if (clips.Length == 1)
            return clips[0];

        AudioClip lastClip = null;
        lastPlayedClips.TryGetValue(type, out lastClip);

        AudioClip newClip;
        int attempts = 0;

        // Versucht bis zu 10x einen anderen Clip als den zuletzt gespielten zu finden
        // → verhindert monotone Wiederholungen
        do
        {
            newClip = clips[Random.Range(0, clips.Length)];
            attempts++;
        }
        while (newClip == lastClip && attempts < 10);

        // Merkt sich den zuletzt gespielten Clip für diesen PenaltyType
        lastPlayedClips[type] = newClip;

        return newClip;
    }

private float GetVolume()
    {
        var settings = AppSettingsManager.Instance.Settings;

        if (settings?.Sound == null)
            return 1f;

        return settings.Sound.Volume;
    }























    

    

    


    

    


    

    


    

    
}

