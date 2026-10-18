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
            Debug.LogWarning($"[AUDIO] Kein Clip für Score {score}");
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
            Debug.LogWarning($"[AUDIO] Kein Clip für Penalty {type}");
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
            Debug.LogWarning($"[AUDIO] Kein Clip für Special {type}");
            return;
        }

        Enqueue(clip);
    }

    /// <summary>
    /// Custom-Penalty-Sound (stabile String-ID). Kette: aufgenommener Clip →
    /// Seed-Fallback (Wall/Ceiling-Defaults, inkl. deren Custom-Listen) →
    /// Warnung. Geseedete Miss-Penalties klingen damit ab Tag 1 wie bisher.
    /// </summary>
    public void PlayCustomPenaltyClip(string penaltyId)
    {
        if (!IsSoundAllowed() || string.IsNullOrEmpty(penaltyId))
            return;

        var clip = library.GetCustomClip(penaltyId);

        if (clip == null)
            clip = ResolveSeedPenaltyFallback(penaltyId);

        if (clip == null)
        {
            Debug.LogWarning($"[AUDIO] Kein Clip für Custom-Penalty {penaltyId}");
            return;
        }

        Enqueue(clip);
    }

    private AudioClip ResolveSeedPenaltyFallback(string penaltyId)
    {
        if (library == null)
            return null;

        // Legacy-Clip-Kette (Runtime → Inspector → Default) als Fallback für
        // migrierte Seeds: vorbereitete Legacy-Clips spielen weiter, bis ggf.
        // ein direkt unter der Custom-ID aufgenommener Clip übernimmt
        // (GetCustomClip wird zuerst gefragt und gewinnt immer).
        if (TryGetLegacyFallbackType(penaltyId, out PenaltyType type))
            return library.GetPenaltyClip(type);

        return null;
    }

    /// <summary>
    /// Ordnet Seed-/Custom-IDs einem Legacy-PenaltyType für die Clip-Fallback-
    /// Kette zu. Rein/testbar (keine Unity-Abhängigkeit).
    /// </summary>
    public static bool TryGetLegacyFallbackType(string penaltyId, out PenaltyType type)
    {
        type = default;

        if (string.IsNullOrEmpty(penaltyId))
            return false;

        if (string.Equals(penaltyId, "custom_wall", System.StringComparison.OrdinalIgnoreCase))
        {
            type = PenaltyType.Wall;
            return true;
        }

        if (string.Equals(penaltyId, "custom_ceiling", System.StringComparison.OrdinalIgnoreCase))
        {
            type = PenaltyType.Ceiling;
            return true;
        }

        if (penaltyId.StartsWith("builtin:score", System.StringComparison.OrdinalIgnoreCase))
        {
            type = PenaltyType.Schnapszahl;
            return true;
        }

        if (string.Equals(penaltyId, "builtin:threeones", System.StringComparison.OrdinalIgnoreCase))
        {
            type = PenaltyType.ThreeOnes;
            return true;
        }

        if (string.Equals(penaltyId, "builtin:allmiss", System.StringComparison.OrdinalIgnoreCase))
        {
            type = PenaltyType.AllMiss;
            return true;
        }

        if (string.Equals(penaltyId, "builtin:leglost", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(penaltyId, "builtin:setlost", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(penaltyId, "builtin:gamelost", System.StringComparison.OrdinalIgnoreCase))
        {
            type = PenaltyType.LostGame;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Custom-Audio-Event-Sound (stabile String-ID). Kette: aufgenommener
    /// Custom-Clip → Seed-Fallback (hinterlegter Special-/Score-Sound) →
    /// Warnung. Geseedete Events klingen damit ab Tag 1 wie bisher.
    /// </summary>
    public void PlayCustomEvent(string eventId)
    {
        if (!IsSoundAllowed() || string.IsNullOrEmpty(eventId))
            return;

        var clip = library.GetCustomClip(eventId);

        if (clip == null)
            clip = ResolveSeedFallback(eventId);

        if (clip == null)
        {
            Debug.LogWarning($"[AUDIO] Kein Clip für Custom-Event {eventId}");
            return;
        }

        Enqueue(clip);
    }

    private AudioClip ResolveSeedFallback(string eventId)
    {
        if (library == null)
            return null;

        var seed = SeededAudioEvents.Find(eventId);
        if (seed == null)
            return null;

        // Score zuerst (z.B. eigene 420-Aufnahme), dann Special-Kette
        // (die selbst Custom-Runtime → Inspector → Default auflöst).
        if (seed.FallbackScore.HasValue)
        {
            var scoreClip = library.GetScoreClip(seed.FallbackScore.Value);
            if (scoreClip != null)
                return scoreClip;
        }

        if (seed.FallbackSpecial.HasValue)
            return library.GetSpecialClip(seed.FallbackSpecial.Value);

        return null;
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

private bool UseCustomSounds()
    {
        var settings = AppSettingsManager.Instance.Settings;
        return settings != null && settings.Sound != null && settings.Sound.UseCustomSounds;
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

