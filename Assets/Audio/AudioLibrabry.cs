using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Zentrale Audio-Clip-Bibliothek: hält die vom User aufgenommenen Clips
/// (Runtime-Dicts + SQLite-Index) und liefert den eingebauten Fallback aus
/// dem Inspector.
///
/// Die eingebauten Clips (Default-Set oder Custom-Set) sind KEINE
/// User-Aufnahmen: sie liegen ausschließlich in den Inspector-Feldern und
/// tauchen daher in keiner Clip-Count-Anzeige des Customization-Screens auf.
/// Sie dienen nur als Rückfall, wenn der User für eine ID noch nichts
/// aufgenommen hat.
///
/// Set-Wahl: WillBePublished == true → Default-Set, false → Custom-Set.
///
/// Der eingebaute Rückfall ist abschaltbar (Option "Default-Clips als
/// Fallback", Sound.UseDefaultClipsAsFallback). Ist er aus, laufen nur die
/// vom User aufgenommenen Clips — ohne eigene Aufnahme bleibt es stumm.
/// </summary>
public class AudioLibrary : MonoBehaviour
{
    /// <summary>
    /// true → das Default-Set (numberClips, *Clip) ist der eingebaute Fallback.
    /// false → das Custom-Set (customNumberClips, custom*Clip) ist der Fallback.
    /// </summary>
    public bool WillBePublished;

    public static AudioLibrary Instance;

    // =========================================================
    // DEFAULT CLIPS
    // =========================================================

    [Header("Default Score Clips")]
    public AudioClip[] numberClips;

    [Header("Default Penalty Clips (single clips)")]
    public AudioClip schnapszahlClip;
    public AudioClip threeOnesClip;
    public AudioClip wallClip;
    public AudioClip ceilingClip;
    public AudioClip allMissClip;


    [Header("Default Special Clips")]
    public AudioClip startGameClip;
    public AudioClip bustClip;
    public AudioClip fourTwentyClip;
    public AudioClip undoClip;
    public AudioClip matchWonClip;
    public AudioClip suddenDeathClip;
    public AudioClip cricketMarksClip;
    public AudioClip atcHitsClip;
    public AudioClip atcStreakClip;

    // =========================================================
    // CUSTOM (INSPECTOR) CLIPS
    // =========================================================

    [Header("Custom Score Clips (Inspector)")]
    public AudioClip[] customNumberClips;

    [Header("Custom Penalty Clips (Inspector)")]
    public List<AudioClip> customSchnapszahlClip;
    public List<AudioClip>  customThreeOnesClip;
    public List<AudioClip>  customWallClip;
    public List<AudioClip>  customCeilingClip;
    public List<AudioClip>  customAllMissClip;

    [Header("Custom Special Clips (Inspector)")]
    public List<AudioClip>  customStartGameClip;
    public List<AudioClip>  customBustClip;
    public List<AudioClip>  customFourTwentyClip;
    public List<AudioClip>  customUndoClip;
    public List<AudioClip>  customMatchWonClip;
    public List<AudioClip>  customSuddenDeathClip;
    public List<AudioClip>  customCricketMarksClip;
    public List<AudioClip>  customAtcHitsClip;
    public List<AudioClip>  customAtcStreakClip;
    // =========================================================
    // RUNTIME RECORDED CLIPS
    // =========================================================

    private Dictionary<int, List<AudioClip>> runtimeScoreClips = new();
    private Dictionary<PenaltyType, List<AudioClip>> runtimePenaltyClips = new();
    private Dictionary<SpecialAudioType, List<AudioClip>> runtimeSpecialClips = new();

    /// <summary>
    /// Clips für Custom-Penalties und Custom-Audio-Events, geschlüsselt per
    /// stabiler String-ID (GUID). Damit bleiben Clips beim Editieren verlinkt.
    /// </summary>
    private Dictionary<string, List<AudioClip>> runtimeCustomClips = new();

    private Dictionary<string, AudioClip> lastPlayedClips = new();

    // =========================================================
    // COUNTERS (FILE NAMING)
    // =========================================================

    private Dictionary<int, int> scoreCounters = new();
    private Dictionary<PenaltyType, int> penaltyCounters = new();
    private Dictionary<SpecialAudioType, int> specialCounters = new();
    private Dictionary<string, int> customCounters = new();

    // =========================================================
    // PATHS
    // =========================================================

    private string RecordingsRoot =>
        Path.Combine(Application.persistentDataPath, "Recordings");

    private string ScoresFolder =>
        Path.Combine(RecordingsRoot, "Scores");

    private string PenaltiesFolder =>
        Path.Combine(RecordingsRoot, "Penalties");

    private string SpecialFolder =>
        Path.Combine(RecordingsRoot, "Special");

    private bool _loadedOnce;


    // =========================================================
    // INITIALISIERUNG
    // =========================================================
    public bool IsReady { get; private set; }
    

    // =========================================================
    // EVENTS
    // =========================================================
    public event Action<AudioCategory> OnLibraryChanged;

    /// <summary>
    /// Feuert OnLibraryChanged ausfallsicher: Snapshot der Subscriber plus
    /// try/catch pro Empfänger — ein fehlerhafter Subscriber (z.B. zerstörte
    /// Row) bricht die Kette für alle anderen nicht mehr ab. Zentrale Stelle
    /// für alle Library-Refreshes (Load-Ende, Add/Delete/Replace).
    /// </summary>
    private void NotifyLibraryChanged(AudioCategory category)
    {
        Debug.Log("Notify Library Changed: " + category);
        var handler = OnLibraryChanged;
        if (handler == null)
            return;

        foreach (Action<AudioCategory> subscriber in handler.GetInvocationList())
        {
            Debug.Log("Notifying subscriber: " + subscriber.Method.Name);
            try
            {
                subscriber?.Invoke(category);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AUDIO] Library-Subscriber fehlerhaft ({category}): {e.Message}");
            }
        }
    }

    // =========================================================
    // SINGLETON
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureFolders();

        if (!_loadedOnce)
            StartCoroutine(LoadLibrary());
    }

    private void EnsureFolders()
    {
        Directory.CreateDirectory(ScoresFolder);
        Directory.CreateDirectory(PenaltiesFolder);
        Directory.CreateDirectory(SpecialFolder);
    }

    // =========================================================
    // FILE NAME BUILDERS
    // =========================================================

    private string BuildScoreFileName(int score, int index)
        => $"score{score}_{index}.wav";

    private string BuildPenaltyFileName(PenaltyType type, int index)
        => $"penalty_{type.ToString().ToLower()}_{index}.wav";

    private string BuildSpecialFileName(SpecialAudioType type, int index)
        => $"special_{type.ToString().ToLower()}_{index}.wav";

    private string BuildCustomFileName(string customKey, int index)
        => $"custom_{SanitizeKey(customKey)}_{index}.wav";

    /// <summary>Dateisystem-sicherer Anteil aus beliebiger String-ID.</summary>
    private static string SanitizeKey(string customKey)
    {
        if (string.IsNullOrEmpty(customKey))
            return "key";

        var chars = customKey.ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
            if (!ok)
                chars[i] = '_';
        }

        string result = new string(chars);
        return string.IsNullOrEmpty(result) ? "key" : result;
    }

    private string GetFullPath(string folder, string fileName)
        => Path.Combine(folder, fileName);

    // =========================================================
    // PUBLIC ADD API
    // =========================================================

    public string AddScoreClip(int score, AudioClip clip)
    {
        if (clip == null) return null;

        if (!scoreCounters.ContainsKey(score))
            scoreCounters[score] = 0;

        int index = ++scoreCounters[score];

        string fileName = BuildScoreFileName(score, index);
        string path = GetFullPath(ScoresFolder, fileName);

        if (!runtimeScoreClips.ContainsKey(score))
            runtimeScoreClips[score] = new List<AudioClip>();

        runtimeScoreClips[score].Add(clip);

        SaveWav(path, clip);
        
        InsertClipIndex(AudioCategory.Score, score, default, default, null, fileName, index);

        NotifyLibraryChanged(AudioCategory.Score);
        return path;
    }

    public string AddPenaltyClip(PenaltyType type, AudioClip clip)
    {
        if (clip == null) return null;

        Debug.Log("Lib speichert Clip");

        if (!penaltyCounters.ContainsKey(type))
            penaltyCounters[type] = 0;

        int index = ++penaltyCounters[type];

        string fileName = BuildPenaltyFileName(type, index);
        string path = GetFullPath(PenaltiesFolder, fileName);

        if (!runtimePenaltyClips.ContainsKey(type))
            runtimePenaltyClips[type] = new List<AudioClip>();

        runtimePenaltyClips[type].Add(clip);

        SaveWav(path, clip);
        
        InsertClipIndex(AudioCategory.Penalty, 0, type, default, null, fileName, index);

        NotifyLibraryChanged(AudioCategory.Penalty);
        return path;
    }

    public string AddSpecialClip(SpecialAudioType type, AudioClip clip)
    {
        if (clip == null) return null;

        if (!specialCounters.ContainsKey(type))
            specialCounters[type] = 0;

        int index = ++specialCounters[type];

        string fileName = BuildSpecialFileName(type, index);
        string path = GetFullPath(SpecialFolder, fileName);

        if (!runtimeSpecialClips.ContainsKey(type))
            runtimeSpecialClips[type] = new List<AudioClip>();

        runtimeSpecialClips[type].Add(clip);

        SaveWav(path, clip);

        InsertClipIndex(AudioCategory.Special, 0, default, type, null, fileName, index);

        NotifyLibraryChanged(AudioCategory.Special);
        return path;
    }

    /// <summary>
    /// Clip für Custom-Penalty oder Custom-Audio-Event (stabile String-ID).
    /// </summary>
    public string AddCustomClip(string customKey, AudioClip clip)
    {
        if (clip == null || string.IsNullOrEmpty(customKey)) return null;

        if (!customCounters.ContainsKey(customKey))
            customCounters[customKey] = 0;

        int index = ++customCounters[customKey];

        string fileName = BuildCustomFileName(customKey, index);
        string path = GetFullPath(SpecialFolder, fileName);

        if (!runtimeCustomClips.ContainsKey(customKey))
            runtimeCustomClips[customKey] = new List<AudioClip>();

        runtimeCustomClips[customKey].Add(clip);

        SaveWav(path, clip);

        InsertClipIndex(AudioCategory.Custom, 0, default, default, customKey, fileName, index);

        NotifyLibraryChanged(AudioCategory.Custom);
        return path;
    }

    /// <summary>
    /// Clip für eine Custom-ID (Penalty- oder Audio-Event-ID).
    /// Kette: vom User aufgenommener Clip → eingebauter Fallback aus dem
    /// aktiven Set (Default bei WillBePublished, sonst Custom) → anderes Set
    /// als Sicherheitsnetz → null.
    /// </summary>
    public AudioClip GetCustomClip(string customKey)
    {
        if (!IsSoundEnabled() || string.IsNullOrEmpty(customKey))
            return null;

        // 1) VOM USER AUFGENOMMEN
        AudioClip userClip = null;
        if (runtimeCustomClips.TryGetValue(customKey, out var runtimeClips))
        {
            userClip = GetRandomClip(runtimeClips, $"custom_{customKey}");
        }

        // 2) EINGEBAUT (Default- oder Custom-Set) — nur wenn als Fallback erlaubt
        return ResolveClip(userClip, () => GetBuiltInClipForKey(customKey), UseBuiltInFallbackEnabled);
    }

    /// <summary>Aufgerufen vom Recorder/Browser-UI (z.B. für Delete-Warnung).</summary>
    public List<AudioClip> GetRuntimeCustomList(string customKey)
        => !string.IsNullOrEmpty(customKey) && runtimeCustomClips.TryGetValue(customKey, out var l) ? l : null;

    public int GetCustomClipCount(string customKey)
        => GetRuntimeCustomList(customKey)?.Count ?? 0;

    public bool DeleteCustomClip(string customKey, int index)
    {
        if (string.IsNullOrEmpty(customKey)) return false;

        if (!runtimeCustomClips.TryGetValue(customKey, out var list))
            return false;

        return DeleteRecordedClip(list, index, AudioCategory.Custom, 0, default, default, customKey);
    }

    public bool ReplaceCustomClip(string customKey, int index, AudioClip newClip)
    {
        if (newClip == null || string.IsNullOrEmpty(customKey)) return false;

        if (!runtimeCustomClips.TryGetValue(customKey, out var list))
            return false;

        return ReplaceRecordedClip(list, index, AudioCategory.Custom, 0, default, default, newClip, customKey);
    }


    // =========================================================
    // PUBLIC REPLACE API
    // =========================================================
    public bool ReplaceScoreClip(int score, int index, AudioClip newClip)
    {
        if (newClip == null) return false;

        if (!runtimeScoreClips.TryGetValue(score, out var list))
            return false;

        return ReplaceRecordedClip(list, index, AudioCategory.Score, score, default, default, newClip);
    }

    public bool ReplacePenaltyClip(PenaltyType type, int index, AudioClip newClip)
    {
        if (newClip == null) return false;

        if (!runtimePenaltyClips.TryGetValue(type, out var list))
            return false;

        return ReplaceRecordedClip(list, index, AudioCategory.Penalty, default, type, default, newClip);
    }

    public bool ReplaceSpecialClip(SpecialAudioType type, int index, AudioClip newClip)
    {
        if (newClip == null) return false;

        if (!runtimeSpecialClips.TryGetValue(type, out var list))
            return false;

        return ReplaceRecordedClip(list, index, AudioCategory.Special, default, default, type, newClip);
    }

    private bool ReplaceRecordedClip(
    List<AudioClip> list,
    int index,
    AudioCategory category,
    int score,
    PenaltyType penaltyType,
    SpecialAudioType specialType,
    AudioClip newClip,
    string customKey = null)
    {
        if (list == null || index < 0 || index >= list.Count)
            return false;

        var rows = SqliteDatabaseRepository.LoadAllRecordedClips();

        int matchCount = 0;
        SqliteDatabaseRepository.RecordedClipRow target = null;

        foreach (var row in rows)
        {
            if (!RowMatches(row, category, score, penaltyType, specialType, customKey))
                continue;

            if (matchCount == index)
            {
                target = row;
                break;
            }

            matchCount++;
        }

        if (target == null)
            return false;

        if (!Enum.TryParse<AudioCategory>(target.Category, out var targetCategory))
            return false;

        string path = GetFullPath(FolderForCategory(targetCategory), target.FileName);

        // =====================================
        // FILE REPLACEMENT (Index bleibt gleich)
        // =====================================

        if (File.Exists(path))
            File.Delete(path);

        SaveWav(path, newClip);

        // =====================================
        // UPDATE RUNTIME LIST
        // =====================================

        list[index] = newClip;

        NotifyLibraryChanged(category);

        return true;
    }



    // =====================================
    // PUBLIC DELETE API
    // =====================================

    public bool DeleteScoreClip(int score, int index)
    {
        if (!runtimeScoreClips.TryGetValue(score, out var list))
            return false;

        return DeleteRecordedClip(list, index, AudioCategory.Score, score, default, default);
    }

    public bool DeletePenaltyClip(PenaltyType type, int index)
    {
        if (!runtimePenaltyClips.TryGetValue(type, out var list))
            return false;

        return DeleteRecordedClip(list, index, AudioCategory.Penalty, default, type, default);
    }

    public bool DeleteSpecialClip(SpecialAudioType type, int index)
    {
        if (!runtimeSpecialClips.TryGetValue(type, out var list))
            return false;

        return DeleteRecordedClip(list, index, AudioCategory.Special, default, default, type);
    }

    private bool DeleteRecordedClip(List<AudioClip> list, int index, AudioCategory category, int score, PenaltyType penaltyType, SpecialAudioType specialType, string customKey = null)
    {
        if (list == null || index < 0 || index >= list.Count)
            return false;

        var rows = SqliteDatabaseRepository.LoadAllRecordedClips();

        int matchCount = 0;
        SqliteDatabaseRepository.RecordedClipRow target = null;

        foreach (var row in rows)
        {
            if (!RowMatches(row, category, score, penaltyType, specialType, customKey))
                continue;

            if (matchCount == index)
            {
                target = row;
                break;
            }

            matchCount++;
        }

        if (target == null)
            return false;

        SqliteDatabaseRepository.DeleteRecordedClipById(target.Id);

        if (Enum.TryParse<AudioCategory>(target.Category, out var targetCategory)
            && !string.IsNullOrEmpty(target.FileName))
        {
            string path = GetFullPath(FolderForCategory(targetCategory), target.FileName);
            if (File.Exists(path))
                File.Delete(path);
        }

        list.RemoveAt(index);
        NotifyLibraryChanged(category);
        return true;
    }

    // =========================================================
    // RESOLUTION
    // =========================================================

    /// <summary>
    /// Kern der Clip-Auflösung (rein, ohne Zustand):
    /// eigene Aufnahme → eingebauter Fallback → null.
    ///
    /// useBuiltInFallback == false → ohne eigene Aufnahme bleibt es stumm
    /// (Option "Default-Clips als Fallback" im Options-Screen).
    /// </summary>
    public static AudioClip ResolveClip(AudioClip userClip, System.Func<AudioClip> builtInFallback, bool useBuiltInFallback)
    {
        if (userClip != null)
            return userClip;

        if (!useBuiltInFallback)
            return null;

        return builtInFallback != null ? builtInFallback() : null;
    }

    // Aufgerufen vom AudioHandler
    public AudioClip GetScoreClip(int score)
    {
        if (!IsSoundEnabled())
            return null;

        // 1) VOM USER AUFGENOMMEN
        AudioClip userClip = null;
        if (runtimeScoreClips.TryGetValue(score, out var runtimeClips))
        {
            userClip = GetRandomClip(runtimeClips, $"score_{score}");
        }

        // 2) EINGEBAUT (Default- oder Custom-Set) — nur wenn als Fallback erlaubt
        return ResolveClip(userClip, () => GetBuiltInScoreClip(score), UseBuiltInFallbackEnabled);
    }



    public AudioClip GetPenaltyClip(PenaltyType type)
    {
        if (!IsSoundEnabled())
            return null;

        // 1) VOM USER AUFGENOMMEN
        AudioClip userClip = null;
        if (runtimePenaltyClips.TryGetValue(type, out var runtimeClips))
        {
            userClip = GetRandomClip(runtimeClips, $"penalty_{type}");
        }

        // 2) EINGEBAUT (Default- oder Custom-Set) — nur wenn als Fallback erlaubt
        return ResolveClip(userClip, () => GetBuiltInPenaltyClip(type), UseBuiltInFallbackEnabled);
    }

    public AudioClip GetSpecialClip(SpecialAudioType type)
    {
        if (!IsSoundEnabled())
            return null;

        // 1) VOM USER AUFGENOMMEN
        AudioClip userClip = null;
        if (runtimeSpecialClips.TryGetValue(type, out var runtimeClips))
        {
            userClip = GetRandomClip(runtimeClips, $"special_{type}");
        }

        // 2) EINGEBAUT (Default- oder Custom-Set) — nur wenn als Fallback erlaubt
        return ResolveClip(userClip, () => GetBuiltInSpecialClip(type), UseBuiltInFallbackEnabled);
    }

    // =========================================================
    // EINGEBAUTE CLIPS (FALLBACK — NICHT IM CUSTOMIZATION-SCREEN SICHTBAR)
    // =========================================================

    /// <summary>
    /// Aktives Set: Default bei WillBePublished == true, sonst Custom.
    /// Das jeweils andere Set dient nur als Sicherheitsnetz, falls im
    /// aktiven Set für eine ID kein Clip hinterlegt ist.
    /// </summary>
    private bool UsesDefaultSet => WillBePublished;

    /// <summary>
    /// true → die eingebauten Clips (Default-/Custom-Set) dienen als Fallback,
    /// wenn der User für eine ID noch nichts aufgenommen hat.
    /// false → es laufen nur eigene Aufnahmen; ohne eigene Aufnahme stumm.
    ///
    /// Quelle: Sound.UseDefaultClipsAsFallback (Default true).
    /// Ohne Settings (z. B. Editor) gilt der Default: Fallback erlaubt.
    /// </summary>
    public bool UseBuiltInFallbackEnabled
    {
        get
        {
            var settings = AppSettingsManager.Instance?.Settings?.Sound;
            return settings == null || settings.UseDefaultClipsAsFallback;
        }
    }

    /// <summary>Eingebauter Fallback für eine Score-Ansage (0–180).</summary>
    public AudioClip GetBuiltInScoreClip(int score)
    {
        AudioClip[] active = UsesDefaultSet ? numberClips : customNumberClips;
        AudioClip[] other = UsesDefaultSet ? customNumberClips : numberClips;

        return PickScoreClip(active, score) ?? PickScoreClip(other, score);
    }

    private static AudioClip PickScoreClip(AudioClip[] clips, int score)
    {
        if (clips == null || score < 0 || score >= clips.Length)
            return null;

        return clips[score];
    }

    /// <summary>Eingebauter Fallback für eine Strafe.</summary>
    public AudioClip GetBuiltInPenaltyClip(PenaltyType type)
    {
        if (UsesDefaultSet)
            return GetDefaultPenaltyClip(type) ?? GetCustomPenalty(type);

        return GetCustomPenalty(type) ?? GetDefaultPenaltyClip(type);
    }

    /// <summary>Eingebauter Fallback für ein Special-Event.</summary>
    public AudioClip GetBuiltInSpecialClip(SpecialAudioType type)
    {
        if (UsesDefaultSet)
            return GetDefaultSpecialClip(type) ?? GetCustomSpecial(type);

        return GetCustomSpecial(type) ?? GetDefaultSpecialClip(type);
    }

    /// <summary>
    /// Eingebauter Fallback für eine Custom-ID (Penalty- oder Audio-Event-ID).
    /// Penalty-IDs kommen aus dem Mapping unten, Event-IDs aus den Seeds
    /// (SeededAudioEvents: FallbackScore vor FallbackSpecial).
    /// Unbekannte IDs (z. B. Sektor-IDs, eigene Events) haben keinen
    /// eingebauten Clip → null (bleiben stumm, bis der User aufnimmt).
    /// </summary>
    public AudioClip GetBuiltInClipForKey(string customKey)
    {
        if (string.IsNullOrEmpty(customKey))
            return null;

        if (TryGetPenaltyTypeForKey(customKey, out var penaltyType))
            return GetBuiltInPenaltyClip(penaltyType);

        var seed = SeededAudioEvents.Find(customKey);
        if (seed != null)
        {
            // Score zuerst (z. B. 420), dann Special (Bust, Undo, Marks, ...).
            if (seed.FallbackScore.HasValue)
            {
                var scoreClip = GetBuiltInScoreClip(seed.FallbackScore.Value);
                if (scoreClip != null)
                    return scoreClip;
            }

            if (seed.FallbackSpecial.HasValue)
                return GetBuiltInSpecialClip(seed.FallbackSpecial.Value);
        }

        return null;
    }

    /// <summary>
    /// Ordnet die Penalty-IDs (Seeds aus dem CustomizationStore) einem
    /// PenaltyType zu. Rein/testbar (keine Unity-Abhängigkeit).
    /// </summary>
    public static bool TryGetPenaltyTypeForKey(string penaltyId, out PenaltyType type)
    {
        type = default;

        if (string.IsNullOrEmpty(penaltyId))
            return false;

        if (string.Equals(penaltyId, "custom_wall", StringComparison.OrdinalIgnoreCase))
        {
            type = PenaltyType.Wall;
            return true;
        }

        if (string.Equals(penaltyId, "custom_ceiling", StringComparison.OrdinalIgnoreCase))
        {
            type = PenaltyType.Ceiling;
            return true;
        }

        if (penaltyId.StartsWith("builtin:score", StringComparison.OrdinalIgnoreCase))
        {
            type = PenaltyType.Schnapszahl;
            return true;
        }

        if (string.Equals(penaltyId, "builtin:threeones", StringComparison.OrdinalIgnoreCase))
        {
            type = PenaltyType.ThreeOnes;
            return true;
        }

        if (string.Equals(penaltyId, "builtin:allmiss", StringComparison.OrdinalIgnoreCase))
        {
            type = PenaltyType.AllMiss;
            return true;
        }

        if (string.Equals(penaltyId, "builtin:leglost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(penaltyId, "builtin:setlost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(penaltyId, "builtin:gamelost", StringComparison.OrdinalIgnoreCase))
        {
            type = PenaltyType.LostGame;
            return true;
        }

        return false;
    }

    // Aufgerufen vom RecorderHandler
    public List<AudioClip> GetRuntimeScoreList(int score)
    => runtimeScoreClips.TryGetValue(score, out var l) ? l : null;

    public List<AudioClip> GetRuntimePenaltyList(PenaltyType type)
        => runtimePenaltyClips.TryGetValue(type, out var l) ? l : null;

    public List<AudioClip> GetRuntimeSpecialList(SpecialAudioType type)
        => runtimeSpecialClips.TryGetValue(type, out var l) ? l : null;

    // =========================================================
    // RANDOM
    // =========================================================

    private AudioClip GetRandomClip(List<AudioClip> clips, string key)
    {
        if (clips == null || clips.Count == 0) return null;
        if (clips.Count == 1) return clips[0];

        lastPlayedClips.TryGetValue(key, out var last);

        AudioClip selected;
        int attempts = 0;

        do
        {
            selected = clips[UnityEngine.Random.Range(0, clips.Count)];
            attempts++;
        }
        while (selected == last && attempts < 10);

        lastPlayedClips[key] = selected;
        return selected;
    }

    // =========================================================
    // DEFAULTS
    // =========================================================

    private AudioClip GetDefaultPenaltyClip(PenaltyType type)
    {
        return type switch
        {
            PenaltyType.Schnapszahl => schnapszahlClip,
            PenaltyType.ThreeOnes => threeOnesClip,
            PenaltyType.Wall => wallClip,
            PenaltyType.Ceiling => ceilingClip,
            PenaltyType.AllMiss => allMissClip,
            _ => null
        };
    }

    private AudioClip GetDefaultSpecialClip(SpecialAudioType type)
    {
        return type switch
        {
            SpecialAudioType.StartGame => startGameClip,
            SpecialAudioType.Bust => bustClip,
            SpecialAudioType.FourTwenty => fourTwentyClip,
            SpecialAudioType.Undo => undoClip,
            SpecialAudioType.MatchWon => matchWonClip,
            SpecialAudioType.SuddenDeath => suddenDeathClip,
            SpecialAudioType.Marks => cricketMarksClip,
            SpecialAudioType.Hits => atcHitsClip,
            SpecialAudioType.ATCStreak => atcStreakClip,
            _ => null
        };
    }

    // =========================================================
    // CUSTOM RESOLVERS (eingebautes Custom-Set)
    // =========================================================

    private AudioClip GetCustomPenalty(PenaltyType type)
    {
        return type switch
        {
            PenaltyType.Schnapszahl => GetRandomFromList(customSchnapszahlClip, $"builtin_penalty_{type}"),
            PenaltyType.ThreeOnes => GetRandomFromList(customThreeOnesClip, $"builtin_penalty_{type}"),
            PenaltyType.Wall => GetRandomFromList(customWallClip, $"builtin_penalty_{type}"),
            PenaltyType.Ceiling => GetRandomFromList(customCeilingClip, $"builtin_penalty_{type}"),
            PenaltyType.AllMiss => GetRandomFromList(customAllMissClip, $"builtin_penalty_{type}"),
            _ => null
        };
    }

    private AudioClip GetCustomSpecial(SpecialAudioType type)
    {
        return type switch
        {
            SpecialAudioType.StartGame => GetRandomFromList(customStartGameClip, $"builtin_special_{type}"),
            SpecialAudioType.Bust => GetRandomFromList(customBustClip, $"builtin_special_{type}"),
            SpecialAudioType.FourTwenty => GetRandomFromList(customFourTwentyClip, $"builtin_special_{type}"),
            SpecialAudioType.Undo => GetRandomFromList(customUndoClip, $"builtin_special_{type}"),
            SpecialAudioType.MatchWon => GetRandomFromList(customMatchWonClip, $"builtin_special_{type}"),
            SpecialAudioType.SuddenDeath => GetRandomFromList(customSuddenDeathClip, $"builtin_special_{type}"),
            SpecialAudioType.Marks => GetRandomFromList(customCricketMarksClip, $"builtin_special_{type}"),
            SpecialAudioType.Hits => GetRandomFromList(customAtcHitsClip, $"builtin_special_{type}"),
            SpecialAudioType.ATCStreak => GetRandomFromList(customAtcStreakClip, $"builtin_special_{type}"),
            _ => null
        };
    }

    /// <summary>
    /// Zufälliger Clip aus einer Liste des eingebauten Custom-Sets.
    /// Mit Wiederholungs-Vermeidung (kein direkter Repeat des zuletzt
    /// gespielten Clips), damit Varianten (z. B. 9 Undo-Sounds) abwechslungs-
    /// reich klingen.
    /// </summary>
    private AudioClip GetRandomFromList(List<AudioClip> list, string key)
    {
        if (list == null || list.Count == 0)
            return null;

        return GetRandomClip(list, key);
    }

    // =========================================================
    // PERSISTENCE (SQLite-Index)
    // =========================================================

    /// <summary>
    /// Trägt eine Aufnahme in den SQLite-Index ein (eine Zeile statt
    /// Full-File-Rewrite). Nur der Dateiname wird gespeichert, der Ordner
    /// wird pro Kategorie abgeleitet — keine absoluten Pfade mehr.
    /// </summary>
    private void InsertClipIndex(AudioCategory category, int score, PenaltyType penaltyType,
        SpecialAudioType specialType, string customKey, string fileName, int slotIndex)
    {
        SqliteDatabaseRepository.InsertRecordedClip(
            category.ToString(),
            score,
            category == AudioCategory.Penalty ? penaltyType.ToString() : null,
            category == AudioCategory.Special ? specialType.ToString() : null,
            category == AudioCategory.Custom ? customKey : null,
            fileName,
            slotIndex);
    }

    private string FolderForCategory(AudioCategory category)
    {
        return category switch
        {
            AudioCategory.Score => ScoresFolder,
            AudioCategory.Penalty => PenaltiesFolder,
            _ => SpecialFolder
        };
    }

    private static bool RowMatches(SqliteDatabaseRepository.RecordedClipRow row,
        AudioCategory category, int score, PenaltyType penaltyType,
        SpecialAudioType specialType, string customKey)
    {
        if (row == null || row.Category != category.ToString())
            return false;

        return category switch
        {
            AudioCategory.Score => row.Score == score,
            AudioCategory.Penalty => row.PenaltyType == penaltyType.ToString(),
            AudioCategory.Special => row.SpecialType == specialType.ToString(),
            AudioCategory.Custom => row.CustomKey == customKey,
            _ => false
        };
    }

    /// <summary>
    /// Lädt die vom User aufgenommenen Clips aus der SQLite-DB in die
    /// Runtime-Dicts. Die eingebauten Clips (Default-/Custom-Set) liegen im
    /// Inspector und müssen hier nicht geladen werden.
    /// </summary>
    private IEnumerator LoadLibrary()
    {
        if (_loadedOnce)
            yield break;

        _loadedOnce = true;

        // TEMP-Diagnose (Race Library-Load vs. UI): fliegt nach Verifikation raus.
        float loadStart = Time.realtimeSinceStartup;
        int loadOk = 0;
        int loadFailed = 0;

        runtimeScoreClips.Clear();
        runtimePenaltyClips.Clear();
        runtimeSpecialClips.Clear();
        runtimeCustomClips.Clear();

        try
        {
            var rows = SqliteDatabaseRepository.LoadAllRecordedClips();
            rows ??= new List<SqliteDatabaseRepository.RecordedClipRow>();

            Debug.Log($"[AUDIO] TEMP LoadLibrary Start: {rows?.Count ?? 0} DB-Zeilen");

            foreach (var row in rows)
            {
                if (row == null || string.IsNullOrEmpty(row.FileName))
                    continue;

                if (!Enum.TryParse<AudioCategory>(row.Category, out var category))
                    continue;

                string path = GetFullPath(FolderForCategory(category), row.FileName);
                if (!File.Exists(path))
                    continue;

                // LoadClip wirft nie (härtet intern): Yield ohne try/catch
                // außenrum (CS1626 verbietet yield in try+catch).
                var dto = DtoFromRow(row, category, path);
                if (dto == null)
                    continue;

                var fileResult = new ClipLoadResult();
                yield return LoadClip(dto, fileResult);

                if (fileResult.Ok)
                    loadOk++;
                else
                    loadFailed++;
            }

            try
            {
                RestoreCounters(rows);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AUDIO] Counter-Wiederherstellung fehlgeschlagen: {e.Message}");
            }
        }
        finally
        {
            IsReady = true;

            // TEMP-Diagnose (s.o.).
            Debug.Log($"[AUDIO] TEMP LoadLibrary Ende: ok={loadOk} fehler={loadFailed} " +
                $"dauer={(Time.realtimeSinceStartup - loadStart):F1}s — feuere Refresh-Events");

            // Nachlade-Refresh: Subscriber (Customization-Rows, Score-Grid)
            // wurden ggf. mit leeren Counts aufgebaut, bevor Clips geladen
            // waren. Liegt im finally → feuert IMMER, damit das UI aus jedem
            // Ladezustand heraus konsistent wird.
            NotifyLibraryChanged(AudioCategory.Score);
            NotifyLibraryChanged(AudioCategory.Penalty);
            NotifyLibraryChanged(AudioCategory.Special);
            NotifyLibraryChanged(AudioCategory.Custom);
        }
    }

    private static RecordedAudioClipDto DtoFromRow(
        SqliteDatabaseRepository.RecordedClipRow row, AudioCategory category, string path)
    {
        var dto = new RecordedAudioClipDto
        {
            category = category,
            filePath = path,
            score = row.Score,
            customKey = row.CustomKey
        };

        if (!string.IsNullOrEmpty(row.PenaltyType)
            && Enum.TryParse<PenaltyType>(row.PenaltyType, out var penaltyType))
            dto.penaltyType = penaltyType;

        if (!string.IsNullOrEmpty(row.SpecialType)
            && Enum.TryParse<SpecialAudioType>(row.SpecialType, out var specialType))
            dto.specialType = specialType;

        return dto;
    }

    /// <summary>
    /// Stellt File-Counter aus slot_index wieder her (kein Filename-Parsing).
    /// </summary>
    private void RestoreCounters(List<SqliteDatabaseRepository.RecordedClipRow> rows)
    {
        scoreCounters.Clear();
        penaltyCounters.Clear();
        specialCounters.Clear();
        customCounters.Clear();

        if (rows == null)
            return;

        foreach (var row in rows)
        {
            if (row == null || row.SlotIndex <= 0)
                continue;

            if (!Enum.TryParse<AudioCategory>(row.Category, out var category))
                continue;

            switch (category)
            {
                case AudioCategory.Score:
                    scoreCounters[row.Score] = Mathf.Max(
                        scoreCounters.TryGetValue(row.Score, out int s) ? s : 0, row.SlotIndex);
                    break;
                case AudioCategory.Penalty:
                    if (Enum.TryParse<PenaltyType>(row.PenaltyType, out var penaltyType))
                        penaltyCounters[penaltyType] = Mathf.Max(
                            penaltyCounters.TryGetValue(penaltyType, out int p) ? p : 0, row.SlotIndex);
                    break;
                case AudioCategory.Special:
                    if (Enum.TryParse<SpecialAudioType>(row.SpecialType, out var specialType))
                        specialCounters[specialType] = Mathf.Max(
                            specialCounters.TryGetValue(specialType, out int sp) ? sp : 0, row.SlotIndex);
                    break;
                case AudioCategory.Custom:
                    if (!string.IsNullOrEmpty(row.CustomKey))
                        customCounters[row.CustomKey] = Mathf.Max(
                            customCounters.TryGetValue(row.CustomKey, out int c) ? c : 0, row.SlotIndex);
                    break;
            }
        }
    }

    /// <summary>Ergebnis eines einzelnen Clip-Loads (Coroutine-Rückgabe).</summary>
    private sealed class ClipLoadResult
    {
        public bool Ok;
    }

    /// <summary>
    /// Lädt einen Clip ausfallsicher (wirft nie): Request-Fehler, korrupte
    /// Dateien und Dict-Probleme werden geloggt + übersprungen, damit ein
    /// einzelner fauler Clip den Gesamt-Load nicht abbricht.
    /// HINWEIS: Kein yield innerhalb von try/catch (CS1626) — nur der
    /// Request steht außerhalb, alles Abwehrende in sync-Blöcken.
    /// </summary>
    private IEnumerator LoadClip(RecordedAudioClipDto dto, ClipLoadResult result)
    {
        if (dto == null || string.IsNullOrEmpty(dto.filePath))
            yield break;

        if (!File.Exists(dto.filePath))
            yield break;

        using var req = UnityWebRequestMultimedia.GetAudioClip(
            "file://" + dto.filePath,
            AudioType.WAV
        );

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[AUDIO] TEMP Clip-Request fehlgeschlagen ({dto.filePath}): {req.error}");
            yield break;
        }

        AudioClip clip = null;
        try
        {
            clip = DownloadHandlerAudioClip.GetContent(req);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AUDIO] TEMP Clip-Decode fehlgeschlagen ({dto.filePath}): {e.Message}");
            yield break;
        }

        if (clip == null)
            yield break;

        try
        {
            switch (dto.category)
            {
                case AudioCategory.Score:
                    runtimeScoreClips.TryAdd(dto.score, new List<AudioClip>());
                    runtimeScoreClips[dto.score].Add(clip);
                    break;

                case AudioCategory.Penalty:
                    runtimePenaltyClips.TryAdd(dto.penaltyType, new List<AudioClip>());
                    runtimePenaltyClips[dto.penaltyType].Add(clip);
                    break;

                case AudioCategory.Special:
                    runtimeSpecialClips.TryAdd(dto.specialType, new List<AudioClip>());
                    runtimeSpecialClips[dto.specialType].Add(clip);
                    break;

                case AudioCategory.Custom:
                    if (string.IsNullOrEmpty(dto.customKey))
                        break;

                    runtimeCustomClips.TryAdd(dto.customKey, new List<AudioClip>());
                    runtimeCustomClips[dto.customKey].Add(clip);
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AUDIO] TEMP Clip-Registrierung fehlgeschlagen ({dto.filePath}): {e.Message}");
            yield break;
        }

        result.Ok = true;
    }

    private bool IsSoundEnabled()
    {
        var settings = AppSettingsManager.Instance?.Settings?.Sound;
        return settings != null && settings.Enabled;
    }

    private void SaveWav(string path, AudioClip clip)
    {
        if (clip == null) return;

        var samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        byte[] wav = WavUtility.FromAudioClip(clip); 
        File.WriteAllBytes(path, wav);
    }
}

// =========================================================
// DTO / ENUMS
// =========================================================

[Serializable]
public class RecordedAudioClipDto
{
    public AudioCategory category;
    public int score;
    public PenaltyType penaltyType;
    public SpecialAudioType specialType;
    public string customKey;
    public string filePath;
}

public enum AudioCategory
{
    Score,
    Penalty,
    Special,
    Custom
}

public enum SpecialAudioType
{
    StartGame,
    Bust,
    FourTwenty,
    Undo,
    MatchWon,
    SuddenDeath,
    Marks,
    Hits,
    ATCStreak
}