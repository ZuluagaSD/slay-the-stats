using Godot;

namespace SlayTheStats.UI.Components;

/// <summary>
/// A single row showing one player's stats: Name | DMG | BLK | Cards
/// Styled with warm parchment tones to match the game.
/// </summary>
public partial class PlayerStatRow : HBoxContainer
{
    private Label _nameLabel = null!;
    private Label _damageLabel = null!;
    private Label _blockLabel = null!;
    private Label _cardsLabel = null!;

    private int _playerId;
    private Color _playerColor;

    public PlayerStatRow() { }

    public PlayerStatRow(int playerId, string playerName, Color playerColor)
    {
        _playerId = playerId;
        _playerColor = playerColor;
        Initialize(playerName);
    }

    private void Initialize(string playerName)
    {
        AddThemeConstantOverride("separation", 4);

        _nameLabel = CreateLabel(playerName, _playerColor, 100);
        _damageLabel = CreateLabel("0", UIConstants.DamageColor, UIConstants.ColumnWidth);
        _blockLabel = CreateLabel("0", UIConstants.BlockColor, UIConstants.ColumnWidth);
        _cardsLabel = CreateLabel("0", UIConstants.CardColor, UIConstants.ColumnWidth);

        AddChild(_nameLabel);
        AddChild(_damageLabel);
        AddChild(_blockLabel);
        AddChild(_cardsLabel);
    }

    private static Label CreateLabel(string text, Color color, int minWidth)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(minWidth, UIConstants.RowHeight),
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", UIConstants.FontSize);
        return label;
    }

    public void UpdateStats(int damage, int block, int cardsPlayed)
    {
        _damageLabel.Text = FormatNumber(damage);
        _blockLabel.Text = FormatNumber(block);
        _cardsLabel.Text = cardsPlayed.ToString();
    }

    public void UpdateName(string name)
    {
        _nameLabel.Text = name;
    }

    private static string FormatNumber(int value)
    {
        return value >= 1000 ? $"{value / 1000.0:F1}k" : value.ToString();
    }
}
