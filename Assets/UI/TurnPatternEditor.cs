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
/// - Audio-Events nutzen weiter LoadEntries(pattern, matchExact-Bool),
///   LoadMatchExact() und SaveEntries() (Verhalten unverändert).
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

    private static readonly List<string> ValueOptionsFull = BuildValueOptions(false);
    private static readonly List<string> ValueOptionsNoBull = BuildValueOptions(true);
    private static readonly List<string> MultOptionsFull = new List<string> { "Any", "Single", "Double", "Triple" };
    private static readonly List<string> MultOptionsNoTriple = new List<string> { "Any", "Single", "Double" };

    private static List<string> BuildValueOptions(bool excludeBull)
    {
        var options = new List<string> { "Any" };
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
            slotCountDropdown.AddOptions(new List<string> { "1 Dart", "2 Darts", "3 Darts" });
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
        // Hit-Optionen neu aufbauen (Custom-Sektoren können sich geändert haben).
        FillHitDropdowns();

        for (int i = 0; i < 3; i++)
        {
            TurnPatternEntry slot = entries != null && i < entries.Count ? entries[i] : null;

            if (valueDropdowns != null && i < valueDropdowns.Length && valueDropdowns[i] != null)
                SetDropdownByText(valueDropdowns[i], slot == null || slot.anyValue ? "Any" : slot.value.ToString());

            if (multDropdowns != null && i < multDropdowns.Length && multDropdowns[i] != null)
                SetDropdownByText(multDropdowns[i], slot == null || slot.anyMultiplier ? "Any" : slot.multiplier.ToString());

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

            string valueText = DropdownText(valueDropdowns, i);
            slot.anyValue = string.IsNullOrEmpty(valueText) || valueText == "Any";
            slot.value = slot.anyValue ? 0 : Mathf.Clamp(ParseInt(valueText), 0, 25);

            string multText = DropdownText(multDropdowns, i);
            slot.anyMultiplier = string.IsNullOrEmpty(multText) || multText == "Any";
            slot.multiplier = slot.anyMultiplier ? DartMultiplier.Single : ParseMultiplier(multText);

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

    public bool LoadMatchExact()
    {
        return matchOrderToggle == null || matchOrderToggle.IsActive();
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
        string multText = DropdownText(multDropdowns, slot);
        string valueText = DropdownText(valueDropdowns, slot);

        if (multText == "Triple" && valueText == "25")
            SetDropdownByText(valueDropdowns[slot], "Any");

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
            string valueText = DropdownText(valueDropdowns, slot);
            string multText = DropdownText(multDropdowns, slot);

            if (valueDropdowns != null && slot < valueDropdowns.Length && valueDropdowns[slot] != null)
            {
                bool wantNoBull = multText == "Triple";
                var want = wantNoBull ? ValueOptionsNoBull : ValueOptionsFull;
                if (!SameOptions(valueDropdowns[slot], want))
                    SetDropdownByText(valueDropdowns[slot], valueText, want);
            }

            if (multDropdowns != null && slot < multDropdowns.Length && multDropdowns[slot] != null)
            {
                // Value nach möglicher Korrektur neu lesen
                valueText = DropdownText(valueDropdowns, slot);
                bool wantNoTriple = valueText == "25";
                var want = wantNoTriple ? MultOptionsNoTriple : MultOptionsFull;
                if (!SameOptions(multDropdowns[slot], want))
                    SetDropdownByText(multDropdowns[slot], multText, want);
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
        SetOptions(valueDropdowns, ValueOptionsFull);
    }

    private void FillMultDropdowns()
    {
        SetOptions(multDropdowns, MultOptionsFull);
    }

    private void FillHitDropdowns()
    {
        customSectorIds.Clear();

        var options = new List<string> { "Any", "Board", "Wall", "Ceiling", "Miss" };

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

    private static void SetDropdownByText(TMP_Dropdown dropdown, string text)
    {
        if (dropdown == null || dropdown.options == null)
            return;

        SetDropdownByText(dropdown, text, dropdown.options.ConvertAll(o => o.text));
    }

    private static void SetDropdownByText(TMP_Dropdown dropdown, string text, List<string> options)
    {
        if (dropdown == null)
            return;

        dropdown.ClearOptions();
        dropdown.AddOptions(options);

        int index = string.IsNullOrEmpty(text) ? 0 : Mathf.Max(0, options.IndexOf(text));
        dropdown.SetValueWithoutNotify(index);
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
