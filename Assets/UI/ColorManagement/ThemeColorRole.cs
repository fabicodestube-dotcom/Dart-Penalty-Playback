public enum ThemeColorRole
{
    // ==========================================================
    // Background / Panels
    // ==========================================================
    Background1,
    TextOnBackground1, // Text auf Top-Panel
    Background2,
    Background3,
    Background4,

    // ==========================================================
    // Image States
    // ==========================================================
    Accent1, // Aktive Single-Selection-Buttons / jeder gefärbte Button
    TextOnAccent1,
    Accent2, // Top Bar Elemente
    // TextOnAccent2, wahrscheinlich nicht nötig?

    // ==========================================================
    // Text Hierarchy
    // ==========================================================
    Text1,
    Text2,
    Text3,
    

    // Bottom Bar
    BottomBarInactive,
    
    // Single Selection Button
    SingleSelectionButtonInactive,
    OnSingleSelectionButtonInactive,

    // ==========================================================
    // Static Status Colors (semantic / not theme-dependent logic)
    // ==========================================================
    Error,
    Finished,
    Double,
    DoubleSelected,
    Triple,
    TripleSelected
}