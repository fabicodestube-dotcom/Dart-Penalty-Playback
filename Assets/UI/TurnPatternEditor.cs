using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Geteilter Pattern-Editor für Penalty- und AudioEvent-Popup.
/// Slots: 1-3 Darts (slotCount), je Slot 3 Dropdowns (Value/Multiplier/Hit)
/// + Reihenfolge-CustomToggle (OnClick → ToggleMatchOrder()).
/// - Hit-Optionen füllt der Code dynamisch: Any, Board, Wall, Ceiling, Miss
///   (kein Boardtreffer: Wall/Ceiling/Custom-Sektor, z.B. für No-board-hit)
///   + Sektoren aus Tab 1 (Index 5+i). Bei Sektor-/Miss-Wahl sind Value/
///   Multiplier bedeutungslos (dimmen via Interactable + CanvasGroup).
/// - Triple 25 gibt es nicht: Value 25 blendet Triple aus der Mult-Liste aus
///   und umgekehrt (Options-Rebuild, Auswahl bleibt logisch erhalten).
/// - Der Reihenfolge-Toggle steuert matchOrder: true = Slot i muss Wurf i
///   entsprechen, false = Reihenfolge egal. Anzahl der Würfe muss immer der
///   Slot-Anzahl entsprechen. Penalty- und AudioEvent-Popup teilen sich
///   Toggle und Semantik.
/// Index-Mapping Value: [Any,0..25], Mult: [Any,Single,Double,Triple],
/// Hit: [Any,Board,Wall,Ceiling,Miss,Custom...].
/// </summary>
public class TurnPatternEditor : MonoBehaviour
{
    [Header("Slots (je 3, Reihenfolge fest)")]
    public TMP_Dropdown[] valueDropdowns = new TMP_Dropdown[3];
    public TMP_Dropdown[] multDropdowns = new TMP_Dropdown[3];
    public TMP_Dropdown[] hitDropdowns = new TMP_Dropdown[3];

    [Header("Slot-Anzahl (1-3 Darts)")]
    public int slotCount = 3;
    public GameObject[] slotRows = new GameObject[3];
    public TMP_Dropdown slotCountDropdown;

    [Header("Modus")]
    public CustomToggle matchOrderToggle;

    private readonly List<string> customSectorIds = new List<string>();
    private bool updating;
    private bool initialized;

    private List<string> valueOptionsFull;
    private List<string> valueOptionsNoBull;
    private List<string> multOptionsFull;
    private List<string> multOptionsNoTriple;

    private static List<string> BuildValueOptions(bool excludeBull)
    {
        var options = new List<string> { LocalizationText.Get("Any", "Any") };
        for (int v = 0; v <= 25; v++)
        {
            if (excludeBull && v == 25)
                continue;
            options.Add(v.ToString());
        }
        return options;
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        initialized = true;

        valueOptionsFull = BuildValueOptions(false);
        valueOptionsNoBull = BuildValueOptions(true);
        multOptionsFull = new List<string>
        {
            LocalizationText.Get("Any", "Any"), "Single", "Double", "Triple"
        };
        multOptionsNoTriple = new List<string>
        {
            LocalizationText.Get("Any", "Any"), "Single", "Double"
        };

        FillValueDropdowns();
        FillMultDropdowns();
        FillHitDropdowns();

        for (int i = 0; i < 3; i++)
        {
            int slot = i;
            if (valueDropdowns != null && i < valueDropdowns.Length && valueDropdowns[i] != null)
                valueDropdowns[i].onValueChanged.AddListener(_ => OnSlotValueChanged(slot));
            if (multDropdowns != null && i < multDropdowns.Length && multDropdowns[i] != null)
                multDropdowns[i].onValueChanged.AddListener(_ => OnSlotMultChanged(slot));
            if (hitDropdowns != null && i < hitDropdowns.Length && hitDropdowns[i] != null)
                hitDropdowns[i].onValueChanged.AddListener(_ => ApplySlotDimming(slot));
        }

        if (slotCountDropdown != null)
        {
            slotCountDropdown.ClearOptions();
            slotCountDropdown.AddOptions(new List<string>
            {
                LocalizationText.Get("One_dart", "1 Dart"),
                LocalizationText.Get("Two_darts", "2 Darts"),
                LocalizationText.Get("Three_darts", "3 Darts")
            });
            slotCountDropdown.SetValueWithoutNotify(Mathf.Clamp(slotCount, 1, 3) - 1);
            slotCountDropdown.onValueChanged.AddListener(_ => OnSlotCountChanged());
        }

        ApplySlotVisibility();
    }

    public void OnSlotCountChanged()
    {
        SetSlotCount(slotCountDropdown != null ? slotCountDropdown.value + 1 : slotCount);
    }

    public void SetSlotCount(int count)
    {
        EnsureInitialized();

        slotCount = Mathf.Clamp(count, 1, 3);

        if (slotCountDropdown != null)
            slotCountDropdown.SetValueWithoutNotify(slotCount - 1);

        ApplySlotVisibility();
    }

    private void ApplySlotVisibility()
    {
        if (slotRows == null)
            return;

        for (int i = 0; i < slotRows.Length && i < 3; i++)
        {
            if (slotRows[i] != null)
                slotRows[i].SetActive(i < slotCount);
        }
    }

    public void ToggleMatchExact()
    {
        matchOrderToggle?.Toggle();
    }

    public void ToggleMatchOrder()
    {
        matchOrderToggle?.Toggle();
    }

    public void LoadEntries(List<TurnPatternEntry> entries, bool matchOrder)
    {
        EnsureInitialized();

        // Hit-Optionen neu aufbauen (Custom-Sektoren können sich geändert haben).
        FillHitDropdowns();

        for (int i = 0; i < 3; i++)
        {
            TurnPatternEntry slot = entries != null && i < entries.Count ? entries[i] : null;

            if (valueDropdowns != null && i < valueDropdowns.Length && valueDropdowns[i] != null)
                SetDropdownIndex(valueDropdowns[i], slot == null || slot.anyValue ? 0 : slot.value + 1);

            if (multDropdowns != null && i < multDropdowns.Length && multDropdowns[i] != null)
                SetDropdownIndex(multDropdowns[i], slot == null || slot.anyMultiplier ? 0 : (int)slot.multiplier + 1);

            if (hitDropdowns != null && i < hitDropdowns.Length && hitDropdowns[i] != null)
                hitDropdowns[i].SetValueWithoutNotify(HitIndexForSlot(slot));

            RefreshSlotConstraints(i);
            ApplySlotDimming(i);
        }

        if (matchOrderToggle != null)
            matchOrderToggle.Initialize(matchOrder);
    }

    public List<TurnPatternEntry> SaveEntries()
    {
        var list = new List<TurnPatternEntry>(slotCount);

        for (int i = 0; i < slotCount; i++)
        {
            var slot = new TurnPatternEntry();

            int valueIndex = valueDropdowns != null && i < valueDropdowns.Length && valueDropdowns[i] != null
                ? valueDropdowns[i].value : 0;
            slot.anyValue = valueIndex <= 0;
            slot.value = slot.anyValue ? 0 : Mathf.Clamp(valueIndex - 1, 0, 25);

            int multIndex = multDropdowns != null && i < multDropdowns.Length && multDropdowns[i] != null
                ? multDropdowns[i].value : 0;
            slot.anyMultiplier = multIndex <= 0;
            slot.multiplier = slot.anyMultiplier
                ? DartMultiplier.Single
                : (DartMultiplier)Mathf.Clamp(multIndex - 1, 0, 2);

            int h = hitDropdowns != null && i < hitDropdowns.Length && hitDropdowns[i] != null
                ? hitDropdowns[i].value : 0;
            slot.anyHitType = h <= 0;
            slot.anyMissHitType = h == 4;
            slot.hitType = HitType.Board;
            slot.customHitId = null;

            if (h == 1) slot.hitType = HitType.Board;
            else if (h == 2) slot.hitType = HitType.Wall;
            else if (h == 3) slot.hitType = HitType.Ceiling;
            else if (h == 4)
            {
                // Miss-Slot (No board hit): Value/Multiplier sind Wildcards.
                slot.hitType = HitType.Board;
                slot.anyHitType = false;
                slot.anyValue = true;
                slot.anyMultiplier = true;
            }
            else if (h >= 5)
            {
                slot.hitType = HitType.Custom;
                int sectorIndex = h - 5;
                if (sectorIndex >= 0 && sectorIndex < customSectorIds.Count)
                    slot.customHitId = customSectorIds[sectorIndex];
            }

            list.Add(slot);
        }

        return list;
    }

    public bool LoadMatchOrder()
    {
        return matchOrderToggle == null || matchOrderToggle.IsActive();
    }

    private int HitIndexForSlot(TurnPatternEntry slot)
    {
        if (slot == null || slot.anyHitType)
            return 0;

        if (slot.anyMissHitType)
            return 4;

        if (slot.hitType == HitType.Board) return 1;
        if (slot.hitType == HitType.Wall) return 2;
        if (slot.hitType == HitType.Ceiling) return 3;

        if (slot.hitType == HitType.Custom)
        {
            for (int i = 0; i < customSectorIds.Count; i++)
            {
                if (string.Equals(customSectorIds[i], slot.customHitId,
                        System.StringComparison.OrdinalIgnoreCase))
                    return 5 + i;
            }
            return 0;
        }

        return 0;
    }

    private void OnSlotValueChanged(int slot)
    {
        if (updating)
            return;

        RefreshSlotConstraints(slot);
    }

    private void OnSlotMultChanged(int slot)
    {
        if (updating)
            return;

        // Triple gewählt + Value 25 -> Value auf Any zurücksetzen (kein Triple Bull).
        int multIndex = multDropdowns != null && slot < multDropdowns.Length && multDropdowns[slot] != null
            ? multDropdowns[slot].value : 0;
        int valueIndex = valueDropdowns != null && slot < valueDropdowns.Length && valueDropdowns[slot] != null
            ? valueDropdowns[slot].value : 0;

        if (multIndex == 3 && valueIndex == 26)
            SetDropdownIndex(valueDropdowns[slot], 0);

        RefreshSlotConstraints(slot);
    }

    /// <summary>Hält Value-/Mult-Optionen T25-frei (Auswahl bleibt logisch erhalten).</summary>
    private void RefreshSlotConstraints(int slot)
    {
        if (updating)
            return;

        updating = true;
        try
        {
            int valueIndex = valueDropdowns != null && slot < valueDropdowns.Length && valueDropdowns[slot] != null
                ? valueDropdowns[slot].value : 0;
            int multIndex = multDropdowns != null && slot < multDropdowns.Length && multDropdowns[slot] != null
                ? multDropdowns[slot].value : 0;

            if (valueDropdowns != null && slot < valueDropdowns.Length && valueDropdowns[slot] != null)
            {
                bool wantNoBull = multIndex == 3;
                var want = wantNoBull ? valueOptionsNoBull : valueOptionsFull;
                if (!SameOptions(valueDropdowns[slot], want))
                    SetDropdownIndex(valueDropdowns[slot], valueIndex, want);
            }

            if (multDropdowns != null && slot < multDropdowns.Length && multDropdowns[slot] != null)
            {
                int correctedValueIndex = valueDropdowns != null && slot < valueDropdowns.Length && valueDropdowns[slot] != null
                    ? valueDropdowns[slot].value : 0;
                bool wantNoTriple = correctedValueIndex == 26;
                var want = wantNoTriple ? multOptionsNoTriple : multOptionsFull;
                if (!SameOptions(multDropdowns[slot], want))
                    SetDropdownIndex(multDropdowns[slot], multIndex, want);
            }
        }
        finally
        {
            updating = false;
        }
    }

    /// <summary>
    /// Bei Sektor-Wahl (Wall/Ceiling/Custom) sind Value/Multiplier bedeutungslos
    /// und werden deaktiviert (Interactable + CanvasGroup-Dimmung).
    /// </summary>
    private void ApplySlotDimming(int slot)
    {
        int h = hitDropdowns != null && slot < hitDropdowns.Length && hitDropdowns[slot] != null
            ? hitDropdowns[slot].value : 0;
        bool valueRelevant = h <= 1;

        SetDropdownEnabled(valueDropdowns, slot, valueRelevant);
        SetDropdownEnabled(multDropdowns, slot, valueRelevant);
    }

    private static void SetDropdownEnabled(TMP_Dropdown[] dropdowns, int slot, bool enabled)
    {
        if (dropdowns == null || slot >= dropdowns.Length || dropdowns[slot] == null)
            return;

        dropdowns[slot].interactable = enabled;

        var cg = dropdowns[slot].GetComponent<CanvasGroup>();
        if (cg != null)
            cg.alpha = enabled ? 1f : 0.4f;
    }

    private void FillValueDropdowns()
    {
        SetOptions(valueDropdowns, valueOptionsFull);
    }

    private void FillMultDropdowns()
    {
        SetOptions(multDropdowns, multOptionsFull);
    }

    private void FillHitDropdowns()
    {
        customSectorIds.Clear();

        var options = new List<string>
        {
            LocalizationText.Get("Any", "Any"),
            LocalizationText.Get("Board", "Board"),
            LocalizationText.Get("Wall", "Wall"),
            LocalizationText.Get("Ceiling", "Ceiling"),
            LocalizationText.Get("Miss", "Miss")
        };

        foreach (var def in CustomizationStore.GetHitSectors())
        {
            if (def == null || string.IsNullOrEmpty(def.id))
                continue;

            // Wand/Decke haben feste Einträge oben (kein Duplikat).
            if (CustomHitDef.IsBuiltInSector(def.id))
                continue;

            options.Add(def.name);
            customSectorIds.Add(def.id);
        }

        SetOptions(hitDropdowns, options);
    }

    private static void SetOptions(TMP_Dropdown[] dropdowns, List<string> options)
    {
        if (dropdowns == null)
            return;

        foreach (var dropdown in dropdowns)
        {
            if (dropdown == null)
                continue;

            dropdown.ClearOptions();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
        }
    }

    private static bool SameOptions(TMP_Dropdown dropdown, List<string> options)
    {
        if (dropdown == null || dropdown.options == null || options == null)
            return false;

        if (dropdown.options.Count != options.Count)
            return false;

        for (int i = 0; i < options.Count; i++)
        {
            if (dropdown.options[i].text != options[i])
                return false;
        }

        return true;
    }

    private static void SetDropdownIndex(TMP_Dropdown dropdown, int index)
    {
        if (dropdown == null || dropdown.options == null)
            return;

        SetDropdownIndex(dropdown, index, dropdown.options.ConvertAll(o => o.text));
    }

    private static void SetDropdownIndex(TMP_Dropdown dropdown, int index, List<string> options)
    {
        if (dropdown == null)
            return;

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(Mathf.Clamp(index, 0, Mathf.Max(0, options.Count - 1)));
        dropdown.RefreshShownValue();
    }

    private static string DropdownText(TMP_Dropdown[] dropdowns, int slot)
    {
        if (dropdowns == null || slot >= dropdowns.Length || dropdowns[slot] == null)
            return "Any";

        var dropdown = dropdowns[slot];
        if (dropdown.options == null || dropdown.options.Count == 0)
            return "Any";

        int index = Mathf.Clamp(dropdown.value, 0, dropdown.options.Count - 1);
        return dropdown.options[index].text;
    }

    private static int ParseInt(string text)
    {
        return int.TryParse(text, out int value) ? value : 0;
    }

    private static DartMultiplier ParseMultiplier(string text)
    {
        return text switch
        {
            "Double" => DartMultiplier.Double,
            "Triple" => DartMultiplier.Triple,
            _ => DartMultiplier.Single
        };
    }
}
