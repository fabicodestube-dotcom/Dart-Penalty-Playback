using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Check the user-like X01 seeds (Bah, 3x20, 3x19) as well as the new
/// global leg/set end seeds.
///
/// Menu: Darts/Tests/Seeded Audio Events.
/// Batchmode: -executeMethod SeededAudioEventTest.Run
/// </summary>
public static class SeededAudioEventTest
{
    [MenuItem("Darts/Tests/Seeded Audio Events")]
    public static void RunFromMenu()
    {
        Run();
    }

    public static void Run()
    {
        int failures = 0;

        void Check(bool cond, string name, string detail = "")
        {
            if (cond)
            {
                Debug.Log($"  OK   {name}");
            }
            else
            {
                failures++;
                Debug.LogError($"  FAIL {name} {detail}");
            }
        }

        void CheckShort(bool cond, string name) => Check(cond, name);

        CheckUserSeed(SeededAudioEvents.BahId, new[] { 1, 5, 20 }, CheckShort);
        CheckUserSeed(SeededAudioEvents.ThreeTwentiesId, new[] { 20, 20, 20 }, CheckShort);
        CheckUserSeed(SeededAudioEvents.ThreeNineteensId, new[] { 19, 19, 19 }, CheckShort);
        CheckBuiltInManualSeed(SeededAudioEvents.LegEndId, "Leg End", CheckShort);
        CheckBuiltInManualSeed(SeededAudioEvents.SetEndId, "Set End", CheckShort);

        if (failures == 0)
        {
            Debug.Log("[SEEDED-AUDIO-TEST] Alle Prüfungen bestanden.");
        }
        else
        {
            Debug.LogError($"[SEEDED-AUDIO-TEST] {failures} Prüfung(en) fehlgeschlagen.");
        }

        if (Application.isBatchMode)
            EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void CheckUserSeed(string id, int[] expectedValues,
        Action<bool, string> Check)
    {
        var seed = SeededAudioEvents.Find(id);
        Check(seed != null, id + "-exists");
        if (seed == null)
            return;

        Check(!seed.BuiltIn, id + "-not-built-in");
        Check(seed.OnlyCustomAudioSet, id + "-custom-set-only");
        Check(seed.Category == AudioEventCategory.X01, id + "-x01");
        Check(seed.FallbackSpecial == null && seed.FallbackScore == null,
            id + "-no-built-in-fallback");
        Check(seed.Triggers != null && seed.Triggers.Count > 0 && seed.Triggers.FirstOrDefault().kind == AudioEventTriggerKind.TurnPattern,
            id + "-turn-pattern");
        Check(seed.Triggers != null && seed.Triggers.Count > 0 && !seed.Triggers.FirstOrDefault().matchOrder, id + "-any-order");
        Check(seed.Triggers?.FirstOrDefault()?.pattern != null && seed.Triggers.FirstOrDefault().pattern.Count == 3,
            id + "-three-darts");

        if (seed.Triggers?.FirstOrDefault()?.pattern == null || seed.Triggers.FirstOrDefault().pattern.Count != 3)
            return;

        for (int i = 0; i < 3; i++)
        {
            var slot = seed.Triggers.FirstOrDefault().pattern[i];
            Check(slot != null && !slot.anyValue && slot.value == expectedValues[i],
                $"{id}-slot-{i}-value-{expectedValues[i]}");
            Check(slot != null && !slot.anyMultiplier && slot.multiplier == DartMultiplier.Single,
                $"{id}-slot-{i}-single");
            Check(slot != null && !slot.anyHitType && slot.hitType == HitType.Board,
                $"{id}-slot-{i}-board");
        }

        Check(AudioEventEngine.Matches(seed.Triggers.FirstOrDefault(),
            new AudioTriggerValues { throws = Throws(expectedValues) }),
            id + "-matches-expected-turn");

        var wrongValue = expectedValues.Clone() as int[];
        wrongValue[wrongValue.Length - 1] = wrongValue[wrongValue.Length - 1] == 20 ? 19 : 20;
        Check(!AudioEventEngine.Matches(seed.Triggers.FirstOrDefault(),
            new AudioTriggerValues { throws = Throws(wrongValue) }),
            id + "-rejects-wrong-value");

        var wrongMultiplier = new List<Throw>
        {
            T(expectedValues[0], DartMultiplier.Double),
            T(expectedValues[1]),
            T(expectedValues[2])
        };
        Check(!AudioEventEngine.Matches(seed.Triggers.FirstOrDefault(),
            new AudioTriggerValues { throws = wrongMultiplier }),
            id + "-rejects-wrong-multiplier");
    }

    private static void CheckBuiltInManualSeed(string id, string expectedName,
        Action<bool, string> Check)
    {
        var seed = SeededAudioEvents.Find(id);
        Check(seed != null, id + "-exists");
        if (seed == null)
            return;

        Check(seed.BuiltIn, id + "-built-in");
        Check(!seed.OnlyCustomAudioSet, id + "-global");
        Check(seed.Category == AudioEventCategory.Global, id + "-global-category");
        Check(seed.Triggers != null && seed.Triggers.Count > 0 && seed.Triggers.FirstOrDefault().kind == AudioEventTriggerKind.Manual,
            id + "-manual");
        Check(seed.Name == expectedName, id + "-name");
        Check(seed.FallbackSpecial == null && seed.FallbackScore == null,
            id + "-no-fallback");
    }

    private static List<Throw> Throws(int[] values)
    {
        var list = new List<Throw>(values.Length);
        foreach (int value in values)
            list.Add(T(value));
        return list;
    }

    private static Throw T(int value, DartMultiplier multiplier = DartMultiplier.Single)
    {
        return new Throw(multiplier, value, HitType.Board, false, -1, null);
    }
}
