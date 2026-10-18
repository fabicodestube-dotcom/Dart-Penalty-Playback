using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

public class AudioLibrary : MonoBehaviour
{
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

    private string SaveFilePath =>
        Path.Combine(Application.persistentDataPath, "audio_library.json");

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

    /// <summary>Aufgerufen vom AudioManager für Custom-Penalty/Event-Sounds.</summary>
    public AudioClip GetCustomClip(string customKey)
    {
        if (!IsSoundEnabled() || string.IsNullOrEmpty(customKey))
            return null;

        if (UseCustomSounds()
            && runtimeCustomClips.TryGetValue(customKey, out var runtimeClips))
        {
            var clip = GetRandomClip(runtimeClips, $"custom_{customKey}");
            if (clip != null) return clip;
        }

        return null;
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

    // Aufgerufen vom AudioHandler
    public AudioClip GetScoreClip(int score)
    {
        if (!IsSoundEnabled())
            return null;

        // 1) CUSTOM
        if (UseCustomSounds())
        {
            // 1.1) RUNTIME
            if (runtimeScoreClips.TryGetValue(score, out var runtimeClips))
            {
                var clip = GetRandomClip(runtimeClips, $"score_{score}");
                if (clip != null) return clip;
            }

            if (!WillBePublished)
            {
                // 1.2) INSPECTOR
                if (customNumberClips != null &&
                    score >= 0 &&
                    score < customNumberClips.Length &&
                    customNumberClips[score] != null)
                        return customNumberClips[score];
            }

        }

        // 2) DEFAULT
        if (numberClips == null ||
            score < 0 ||
            score >= numberClips.Length)
            return null;

        return numberClips[score];
    }



    public AudioClip GetPenaltyClip(PenaltyType type)
    {
        if (!IsSoundEnabled())
            return null;

        // 1) CUSTOM
        if (UseCustomSounds())
        {
             // 1.1) RUNTIME
            if (runtimePenaltyClips.TryGetValue(type, out var runtimeClips))
            {
                var clip = GetRandomClip(runtimeClips, $"penalty_{type}");
                if (clip != null) return clip;
            }

            if (!WillBePublished)
            {
                // 1.2) INSPECTOR
                var custom = GetCustomPenalty(type);
                if (custom != null) return custom;
            }
        }

        // 2) DEFAULT
        return GetDefaultPenaltyClip(type);
    }

    public AudioClip GetSpecialClip(SpecialAudioType type)
    {
        if (!IsSoundEnabled())
            return null;

        // 1) CUSTOM 
        if (UseCustomSounds())
        {
            // 1.1) RUNTIME
            if (runtimeSpecialClips.TryGetValue(type, out var runtimeClips))
            {
                var clip = GetRandomClip(runtimeClips, $"special_{type}");
                if (clip != null) return clip;
            }

            if (!WillBePublished)
            {
                //1.2) INSPECTOR
                var custom = GetCustomSpecial(type);
                if (custom != null) return custom; 
            }
        }

        // 2) DEFAULT
        return GetDefaultSpecialClip(type);
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
    // CUSTOM RESOLVERS
    // =========================================================

    private AudioClip GetCustomPenalty(PenaltyType type)
    {
        return type switch
        {
            PenaltyType.Schnapszahl => GetRandomFromList(customSchnapszahlClip),
            PenaltyType.ThreeOnes => GetRandomFromList(customThreeOnesClip),
            PenaltyType.Wall => GetRandomFromList(customWallClip),
            PenaltyType.Ceiling => GetRandomFromList(customCeilingClip),
            PenaltyType.AllMiss => GetRandomFromList(customAllMissClip),
            _ => null
        };
    }

    private AudioClip GetCustomSpecial(SpecialAudioType type)
    {
        return type switch
        {
            SpecialAudioType.StartGame => GetRandomFromList(customStartGameClip),
            SpecialAudioType.Bust => GetRandomFromList(customBustClip),
            SpecialAudioType.FourTwenty => GetRandomFromList(customFourTwentyClip),
            SpecialAudioType.Undo => GetRandomFromList(customUndoClip),
            SpecialAudioType.MatchWon => GetRandomFromList(customMatchWonClip),
            SpecialAudioType.SuddenDeath => GetRandomFromList(customSuddenDeathClip),
            SpecialAudioType.Marks => GetRandomFromList(customCricketMarksClip),
            SpecialAudioType.Hits => GetRandomFromList(customAtcHitsClip),
            SpecialAudioType.ATCStreak => GetRandomFromList(customAtcStreakClip),
            _ => null
        };
    }

    private AudioClip GetRandomFromList(List<AudioClip> list)
    {
        if (list == null || list.Count == 0)
            return null;

        if (list.Count == 1)
            return list[0];

        return list[UnityEngine.Random.Range(0, list.Count)];
    }

    // =========================================================
    // PERSISTENCE (SQLite-Index, ersetzt audio_library.json)
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
    /// Einmalige Migration: alte audio_library.json → SQLite-Tabelle.
    /// Danach wird die JSON zu .migrated umbenannt (Rollback möglich).
    /// </summary>
    private void MigrateJsonIndexIfNeeded()
    {
        if (SqliteDatabaseRepository.CountRecordedClips() > 0)
            return;

        if (!File.Exists(SaveFilePath))
            return;

        AudioLibraryDatabase legacy = null;
        try
        {
            legacy = JsonUtility.FromJson<AudioLibraryDatabase>(File.ReadAllText(SaveFilePath));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AUDIO] JSON-Migration übersprungen (unlesbar): " + e.Message);
            return;
        }

        if (legacy?.entries == null)
            return;

        foreach (var entry in legacy.entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.filePath))
                continue;

            string fileName = Path.GetFileName(entry.filePath);
            if (string.IsNullOrEmpty(fileName))
                continue;

            InsertClipIndex(entry.category, entry.score, entry.penaltyType,
                entry.specialType, entry.customKey, fileName, ParseFileIndex(entry.filePath));
        }

        try
        {
            File.Move(SaveFilePath, SaveFilePath + ".migrated");
            Debug.Log("[AUDIO] Audio-Index nach SQLite migriert.");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AUDIO] JSON konnte nach Migration nicht umbenannt werden: " + e.Message);
        }
    }

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
            MigrateJsonIndexIfNeeded();

            var rows = SqliteDatabaseRepository.LoadAllRecordedClips();
            rows ??= new List<SqliteDatabaseRepository.RecordedClipRow>();

            // Einmaliges Seeding aus StreamingAssets beim allerersten Start.
            // Prüfung über Persistenz-Flag (nicht über DB-Leerheit), damit
            // manuell gelöschte Clips nicht erneut geseedet werden.
            string seeded = SqliteDatabaseRepository.GetSetting("audio_clips_seeded");
            if (seeded != "1")
            {
                string sourceRoot = Path.Combine(Application.streamingAssetsPath, "AudioFiles");
                string subFolder = WillBePublished ? "Default" : "Custom";
                string sourceFolder = Path.Combine(sourceRoot, subFolder);

                if (Directory.Exists(sourceFolder))
                {
                    Debug.Log($"[AUDIO] Starte Clip-Seeding aus {sourceFolder}");
                    yield return SeedBuiltInClips(sourceFolder);
                    SqliteDatabaseRepository.SetSetting("audio_clips_seeded", "1");
                }
                else
                {
                    Debug.LogWarning($"[AUDIO] Seed-Quelle nicht gefunden: {sourceFolder}");
                    SqliteDatabaseRepository.SetSetting("audio_clips_seeded", "1");
                }

                // Nach dem Seeding die DB-Zeilen neu abfragen, damit die
                // nachfolgende Lade-Schleife die frisch eingefügten Clips lädt.
                rows = SqliteDatabaseRepository.LoadAllRecordedClips();
                rows ??= new List<SqliteDatabaseRepository.RecordedClipRow>();
            }

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

    // =========================================================
    // SEEDING (initiale Clips aus StreamingAssets beim ersten Start)
    // =========================================================

    /// <summary>
    /// Seeds initiale Audio-Clips aus StreamingAssets in die SQLite-Datenbank.
    /// Wird nur einmalig beim allerersten Start aufgerufen (gesteuert über das
    /// Persistenz-Flag "audio_clips_seeded").
    ///
    /// Die Clips werden hier nur als Datei kopiert und in die DB eingetragen.
    /// Das eigentliche Laden in die Runtime-Dicts übernimmt die normale
    /// Lade-Schleife in LoadLibrary() danach — damit gibt es kein Doppel-Laden.
    ///
    /// Ordnerstruktur (app-orientiert):
    ///   Sectors/         → Tab 1 Bereiche (wall, ceiling) → AudioCategory.Custom
    ///   Penalties/       → Tab 2 Penalties                 → AudioCategory.Custom
    ///   AudioEvents/     → Tab 3 Audio-Events
    ///     Global/        → undo, start_game, match_won     → AudioCategory.Custom
    ///     X01/           → bust, sudden_death, 420         → AudioCategory.Custom
    ///     Cricket/       → marks                           → AudioCategory.Custom
    ///     ATC/           → hits, atc_streak                → AudioCategory.Custom
    ///     Score/         → 0-180                           → AudioCategory.Score
    ///
    /// Alle Custom-Kategorien landen in SpecialFolder, Score in ScoresFolder.
    /// </summary>
    private IEnumerator SeedBuiltInClips(string sourceFolder)
    {
        int seeded = 0;

        // ---------- Sectors (Tab 1: Bereiche) ----------
        string sectorsFolder = Path.Combine(sourceFolder, "Sectors");
        if (Directory.Exists(sectorsFolder))
        {
            seeded += SeedCustomFolder(sectorsFolder, ResolveSectorCustomKey, "Sectors");
        }

        // ---------- Penalties (Tab 2) ----------
        string penaltiesFolder = Path.Combine(sourceFolder, "Penalties");
        if (Directory.Exists(penaltiesFolder))
        {
            seeded += SeedCustomFolder(penaltiesFolder, ResolvePenaltyCustomKey, "Penalties");
        }

        // ---------- Schnapszahlen (111-999) als Penalty-Clips ----------
        if (Directory.Exists(penaltiesFolder))
        {
            var digitFiles = Directory.GetFiles(penaltiesFolder, "tripledigit*.wav")
                .Concat(Directory.GetFiles(penaltiesFolder, "triple_digit*.wav"))
                .ToArray();
            if (digitFiles.Length > 0)
            {
                var slotIndexByScore = new Dictionary<int, int>();

                foreach (var score in new[] { 111, 222, 333, 444, 555, 666, 777, 888, 999 })
                {
                    string customKey = $"builtin:score{score}";
                    if (!slotIndexByScore.TryGetValue(score, out int slotIndex))
                        slotIndex = 0;

                    foreach (var digitFile in digitFiles)
                    {
                        string fileName = Path.GetFileName(digitFile);
                        string targetPath = GetFullPath(SpecialFolder, fileName);
                        if (!File.Exists(targetPath))
                            File.Copy(digitFile, targetPath, true);

                        slotIndex++;
                        slotIndexByScore[score] = slotIndex;

                        SqliteDatabaseRepository.InsertRecordedClip(
                            AudioCategory.Custom.ToString(), 0, null, null, customKey,
                            fileName, slotIndex);
                        seeded++;
                    }
                }
            }
        }

        // ---------- AudioEvents (Tab 3) ----------
        string audioEventsFolder = Path.Combine(sourceFolder, "AudioEvents");
        if (Directory.Exists(audioEventsFolder))
        {
            foreach (var subDir in Directory.GetDirectories(audioEventsFolder))
            {
                string subDirName = Path.GetFileName(subDir);

                if (string.Equals(subDirName, "Score", StringComparison.OrdinalIgnoreCase))
                {
                    // Score-Clips (0-180) als AudioCategory.Score
                    foreach (var filePath in Directory.GetFiles(subDir, "*.wav"))
                    {
                        string fileName = Path.GetFileName(filePath);
                        string baseName = Path.GetFileNameWithoutExtension(fileName);
                        if (!int.TryParse(baseName, out int score))
                            continue;

                        string targetPath = GetFullPath(ScoresFolder, fileName);
                        if (!File.Exists(targetPath))
                            File.Copy(filePath, targetPath, true);

                        SqliteDatabaseRepository.InsertRecordedClip(
                            AudioCategory.Score.ToString(), score, null, null, null,
                            fileName, 1);
                        seeded++;
                    }
                }
                else
                {
                    // Alle anderen Event-Subfolder als AudioCategory.Custom
                    seeded += SeedCustomFolder(subDir, ResolveEventCustomKey, "AudioEvents/" + subDirName);
                }
            }
        }

        Debug.Log($"[AUDIO] Seeding abgeschlossen: {seeded} Clips in DB eingetragen.");
        yield break;
    }

    /// <summary>
    /// Seeds alle WAV-Dateien eines Ordners als AudioCategory.Custom mit dem
    /// übergebenen Key-Resolver. Zielordner ist immer SpecialFolder (damit
    /// FolderForCategory(Custom) sie beim Laden findet).
    /// </summary>
    private int SeedCustomFolder(string folderPath, Func<string, string> resolveKey, string logTag)
    {
        int seeded = 0;
        var slotIndexByKey = new Dictionary<string, int>();

        foreach (var filePath in Directory.GetFiles(folderPath, "*.wav"))
        {
            string fileName = Path.GetFileName(filePath);
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string key = StripTrailingDigitsAndUnderscores(baseName).ToLowerInvariant();

            string customKey = resolveKey(key);
            if (string.IsNullOrEmpty(customKey))
                continue;

            if (!slotIndexByKey.TryGetValue(customKey, out int slotIndex))
                slotIndex = 0;
            slotIndex++;

            string targetPath = GetFullPath(SpecialFolder, fileName);
            if (!File.Exists(targetPath))
                File.Copy(filePath, targetPath, true);

            SqliteDatabaseRepository.InsertRecordedClip(
                AudioCategory.Custom.ToString(), 0, null, null, customKey,
                fileName, slotIndex);
            slotIndexByKey[customKey] = slotIndex;
            seeded++;
        }

        Debug.Log($"[AUDIO] {logTag}: {seeded} Clips eingeseedet.");
        return seeded;
    }

    /// <summary>Entfernt alle nachfolgenden Ziffern und Unterstriche (z.B. "wall1" → "wall", "triple1_1" → "triple").
    /// Wenn der Name nur aus Ziffern/Unterstrichen besteht, wird der ursprüngliche Name zurückgegeben.</summary>
    private static string StripTrailingDigitsAndUnderscores(string name)
    {
        int i = name.Length;
        while (i > 0)
        {
            char c = name[i - 1];
            if (char.IsDigit(c) || c == '_')
                i--;
            else
                break;
        }
        return i == 0 ? name : name.Substring(0, i);
    }

    private static string ResolveSectorCustomKey(string key)
    {
        switch (key)
        {
            case "wall": return "custom_wall";
            case "ceiling": return "custom_ceiling";
            default: return null;
        }
    }

    private static string ResolvePenaltyCustomKey(string key)
    {
        switch (key)
        {
            case "wall": return "custom_wall";
            case "ceiling": return "custom_ceiling";
            case "triple_wall": return "builtin:allmiss";
            case "triple_ones": return "builtin:threeones";
            case "triple": return "builtin:threeones";
            case "triple_miss": return "builtin:allmiss";
            case "triple_digit": return null; // wird als Score-Clip bei Schnapszahlen behandelt
            case "tripledigit": return null;  // wird als Score-Clip bei Schnapszahlen behandelt
            default: return null;
        }
    }

    private static string ResolveEventCustomKey(string key)
    {
        switch (key)
        {
            case "undo": return "menu_undo";
            case "start_game": return "menu_startgame";
            case "match_won": return "menu_matchwon";
            case "bust": return "x01_bust";
            case "sudden_death": return "x01_suddendeath";
            case "suddendeath": return "x01_suddendeath";
            case "420": return "x01_420";
            case "four_twenty": return "x01_420";
            case "marks": return "cricket_marks";
            case "hits": return "atc_hits";
            case "atc_streak": return "atc_streak";
            default: return null;
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

    private static int ParseFileIndex(string filePath)
    {
        string name = Path.GetFileNameWithoutExtension(filePath);
        int sep = name.LastIndexOf('_');
        if (sep < 0)
            return 0;

        return int.TryParse(name.Substring(sep + 1), out int index) ? index : 0;
    }

    private bool IsSoundEnabled()
    {
        var settings = AppSettingsManager.Instance?.Settings?.Sound;
        return settings != null && settings.Enabled;
    }

    private bool UseCustomSounds()
    {
        var settings = AppSettingsManager.Instance?.Settings?.Sound;
        return settings != null && settings.UseCustomSounds;
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

[Serializable]
public class AudioLibraryDatabase
{
    public List<RecordedAudioClipDto> entries = new();
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