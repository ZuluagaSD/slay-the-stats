using Godot;

namespace SlayTheStats.UI;

public static class UIConstants
{
    // Toggle key
    public static readonly Key ToggleKey = Key.F7;

    // --- Game-matched palette ---
    // Warm golds and bronzes (from game's currency, borders, ornaments)
    public static readonly Color GoldColor = new(0.85f, 0.65f, 0.20f);        // Warm gold
    public static readonly Color GoldBorder = new(0.60f, 0.45f, 0.15f, 0.8f); // Muted bronze border
    public static readonly Color GoldDim = new(0.50f, 0.38f, 0.12f);          // Dim gold for accents

    // Stat colors (warmer, less saturated to blend with game)
    public static readonly Color DamageColor = new(0.85f, 0.35f, 0.28f);      // Warm red (game's HP bar)
    public static readonly Color BlockColor = new(0.40f, 0.65f, 0.85f);       // Muted steel-blue (shields)
    public static readonly Color CardColor = new(0.70f, 0.55f, 0.85f);        // Soft lavender
    public static readonly Color HealColor = new(0.30f, 0.75f, 0.45f);        // Muted green
    public static readonly Color BuffColor = new(0.30f, 0.75f, 0.45f);
    public static readonly Color DebuffColor = new(0.85f, 0.35f, 0.28f);

    // Text colors
    public static readonly Color TextPrimary = new(0.92f, 0.88f, 0.78f);      // Warm off-white (parchment)
    public static readonly Color TextSecondary = new(0.65f, 0.60f, 0.50f);    // Muted warm gray
    public static readonly Color TextHeader = new(0.85f, 0.75f, 0.50f);       // Gold-tinted header

    // Player colors (warmer tones)
    public static readonly Color[] PlayerColors =
    [
        new(0.45f, 0.65f, 0.85f),  // Steel blue
        new(0.85f, 0.40f, 0.30f),  // Warm red
        new(0.35f, 0.75f, 0.45f),  // Forest green
        new(0.85f, 0.65f, 0.20f),  // Gold
    ];

    // Panel styling
    public static readonly Color PanelBackground = new(0.08f, 0.07f, 0.10f, 0.88f);
    public static readonly Color PanelBorder = new(0.55f, 0.40f, 0.15f, 0.50f);     // Bronze border
    public static readonly Color SeparatorColor = new(0.55f, 0.40f, 0.15f, 0.30f);  // Subtle gold line

    // Layout
    public const int OverlayMargin = 12;
    public const int RowHeight = 24;
    public const int ColumnWidth = 60;
    public const int BarMaxWidth = 180;
    public const int BarHeight = 18;
    public const int FontSize = 13;
    public const int HeaderFontSize = 14;
    public const int TitleFontSize = 16;
    public const int Padding = 8;
    public const int PanelCornerRadius = 6;
    public const int BorderWidth = 1;

    // Overlay layer (above game UI)
    public const int OverlayLayer = 100;

    public static Color GetPlayerColor(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < PlayerColors.Length)
            return PlayerColors[playerIndex];
        return TextPrimary;
    }
}
