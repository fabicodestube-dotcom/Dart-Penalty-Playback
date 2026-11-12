public enum ThemeColorRole
{
    // ==========================================================
    // Background / Panels
    // ==========================================================
    Background1,
    TextOnBackground1, // Text on top panel
    Background2,
    Background3,
    Background4,

    // ==========================================================
    // Image States
    // ==========================================================
    Accent1, // Active single selection buttons / any colored button
    TextOnAccent1,
    Accent2, // DEPRECATED → Accent1 (maintain enum value for serialization)
    // TextOnAccent2 probably not necessary?

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