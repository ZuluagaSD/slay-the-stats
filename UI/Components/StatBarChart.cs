using Godot;

namespace SlayTheStats.UI.Components;

/// <summary>
/// Displays horizontal bars comparing a stat across players.
/// Uses warm game-matched colors with subtle styling.
/// </summary>
public partial class StatBarChart : VBoxContainer
{
    private readonly Label _titleLabel;
    private readonly VBoxContainer _barsContainer;
    private readonly Color _barColor;

    public StatBarChart()
    {
        _barColor = Colors.White;
        _titleLabel = new Label();
        _barsContainer = new VBoxContainer();
        AddChild(_titleLabel);
        AddChild(_barsContainer);
    }

    public StatBarChart(string title, Color barColor) : this()
    {
        _barColor = barColor;
        _titleLabel.Text = title;
        _titleLabel.AddThemeColorOverride("font_color", UIConstants.TextHeader);
        _titleLabel.AddThemeFontSizeOverride("font_size", UIConstants.HeaderFontSize);

        _barsContainer.AddThemeConstantOverride("separation", 3);
    }

    public void SetData(IEnumerable<(string Name, int Value, Color Color)> entries)
    {
        foreach (var child in _barsContainer.GetChildren())
            child.QueueFree();

        var list = entries.ToList();
        if (list.Count == 0) return;

        int maxValue = list.Max(e => e.Value);
        if (maxValue <= 0) maxValue = 1;

        foreach (var (name, value, color) in list)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);

            // Player name — warm parchment text
            var nameLabel = new Label
            {
                Text = name,
                CustomMinimumSize = new Vector2(100, UIConstants.BarHeight),
                VerticalAlignment = VerticalAlignment.Center,
            };
            nameLabel.AddThemeColorOverride("font_color", color);
            nameLabel.AddThemeFontSizeOverride("font_size", UIConstants.FontSize);
            row.AddChild(nameLabel);

            // Bar with rounded ends via PanelContainer + StyleBoxFlat
            float barWidth = Math.Max(4, (float)value / maxValue * UIConstants.BarMaxWidth);
            var barPanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(barWidth, UIConstants.BarHeight),
            };
            var barStyle = new StyleBoxFlat
            {
                BgColor = _barColor * new Color(1, 1, 1, 0.6f),
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
            };
            barPanel.AddThemeStyleboxOverride("panel", barStyle);
            row.AddChild(barPanel);

            // Value text
            var valueLabel = new Label
            {
                Text = value.ToString(),
                CustomMinimumSize = new Vector2(50, UIConstants.BarHeight),
                VerticalAlignment = VerticalAlignment.Center,
            };
            valueLabel.AddThemeColorOverride("font_color", UIConstants.TextPrimary);
            valueLabel.AddThemeFontSizeOverride("font_size", UIConstants.FontSize);
            row.AddChild(valueLabel);

            _barsContainer.AddChild(row);
        }
    }
}
