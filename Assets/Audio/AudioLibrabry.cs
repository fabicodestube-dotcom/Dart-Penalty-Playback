using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

    private Dictionary<string, AudioClip> lastPlayedClips = new();

    // =========================================================
    // COUNTERS (FILE NAMING)
    // =========================================================

    private Dictionary<int, int> scoreCounters = new();
    private Dictionary<PenaltyType, int> penaltyCounters = new();
    private Dictionary<SpecialAudioType, int> specialCounters = new();

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
        
        SaveEntry(new RecordedAudioClipDto
        {
            category = AudioCategory.Score,
            score = score,
            filePath = path
        });

        OnLibraryChanged?.Invoke(AudioCategory.Score);
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
        
        SaveEntry(new RecordedAudioClipDto
        {
            category = AudioCategory.Penalty,
            penaltyType = type,
            filePath = path
        });

        OnLibraryChanged?.Invoke(AudioCategory.Penalty);
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

        SaveEntry(new RecordedAudioClipDto
        {
            category = AudioCategory.Special,
            specialType = type,
            filePath = path
        });

        OnLibraryChanged?.Invoke(AudioCategory.Special);
        return path;
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
    AudioClip newClip)
    {
        if (list == null || index < 0 || index >= list.Count)
            return false;

        var db = LoadDatabase();

        int matchCount = 0;
        int targetIndex = -1;

        for (int i = 0; i < db.entries.Count; i++)
        {
            var entry = db.entries[i];

            if (entry.category != category)
                continue;

            bool matches = category switch
            {
                AudioCategory.Score => entry.score == score,
                AudioCategory.Penalty => entry.penaltyType == penaltyType,
                AudioCategory.Special => entry.specialType == specialType,
                _ => false
            };

            if (!matches)
                continue;

            if (matchCount == index)
            {
                targetIndex = i;
                break;
            }

            matchCount++;
        }

        if (targetIndex < 0)
            return false;

        var oldEntry = db.entries[targetIndex];

        // =====================================
        // FILE REPLACEMENT
        // =====================================

        if (!string.IsNullOrEmpty(oldEntry.filePath))
        {
            if (File.Exists(oldEntry.filePath))
                File.Delete(oldEntry.filePath);
        }

        SaveWav(oldEntry.filePath, newClip);

        // =====================================
        // UPDATE RUNTIME LIST
        // =====================================

        list[index] = newClip;

        // =====================================
        // DATABASE ENTRY bleibt gleich (nur file ersetzt)
        // =====================================

        File.WriteAllText(SaveFilePath, JsonUtility.ToJson(db, true));

        OnLibraryChanged?.Invoke(category);

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

    private bool DeleteRecordedClip(List<AudioClip> list, int index, AudioCategory category, int score, PenaltyType penaltyType, SpecialAudioType specialType)
    {
        if (list == null || index < 0 || index >= list.Count)
            return false;

        var db = LoadDatabase();
        int matchCount = 0;
        int removeIndex = -1;

        for (int i = 0; i < db.entries.Count; i++)
        {
            var entry = db.entries[i];
            bool matches = entry.category == category;

            if (matches)
            {
                switch (category)
                {
                    case AudioCategory.Score:
                        matches = entry.score == score;
                        break;
                    case AudioCategory.Penalty:
                        matches = entry.penaltyType == penaltyType;
                        break;
                    case AudioCategory.Special:
                        matches = entry.specialType == specialType;
                        break;
                }
            }

            if (!matches)
                continue;

            if (matchCount == index)
            {
                removeIndex = i;
                break;
            }

            matchCount++;
        }

        if (removeIndex < 0)
            return false;

        var removedEntry = db.entries[removeIndex];
        db.entries.RemoveAt(removeIndex);
        File.WriteAllText(SaveFilePath, JsonUtility.ToJson(db, true));

        if (!string.IsNullOrEmpty(removedEntry.filePath) && File.Exists(removedEntry.filePath))
        {
            File.Delete(removedEntry.filePath);
        }

        list.RemoveAt(index);
        OnLibraryChanged?.Invoke(category);
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
    // PERSISTENCE
    // =========================================================

    private void SaveEntry(RecordedAudioClipDto entry)
    {
        var db = LoadDatabase();
        db.entries.Add(entry);

        File.WriteAllText(SaveFilePath, JsonUtility.ToJson(db, true));
    }

    private AudioLibraryDatabase LoadDatabase()
    {
        if (!File.Exists(SaveFilePath))
            return new AudioLibraryDatabase();

        return JsonUtility.FromJson<AudioLibraryDatabase>(File.ReadAllText(SaveFilePath));
    }

    private IEnumerator LoadLibrary()
    {
        if (_loadedOnce)
            yield break;

        _loadedOnce = true;

        runtimeScoreClips.Clear();
        runtimePenaltyClips.Clear();
        runtimeSpecialClips.Clear();

        var db = LoadDatabase();

        foreach (var entry in db.entries)
            yield return LoadClip(entry);

        IsReady = true;
    }

    private IEnumerator LoadClip(RecordedAudioClipDto dto)
    {
        if (!File.Exists(dto.filePath))
            yield break;

        using var req = UnityWebRequestMultimedia.GetAudioClip(
            "file://" + dto.filePath,
            AudioType.WAV
        );

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Audio load failed: {dto.filePath}");
            yield break;
        }

        var clip = DownloadHandlerAudioClip.GetContent(req);

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
        }
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
    Special
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