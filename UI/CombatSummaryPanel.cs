using Godot;
using SlayTheStats.Core;
using SlayTheStats.UI.Components;

namespace SlayTheStats.UI;

/// <summary>
/// Shown automatically when combat ends. Displays MVP, bar charts for damage/block/cards.
/// Styled to match Slay the Spire 2's dark-fantasy aesthetic.
/// </summary>
public partial class CombatSummaryPanel : CanvasLayer
{
    private PanelContainer _panel = null!;
    private VBoxContainer _container = null!;
    private Label _titleLabel = null!;
    private Label _mvpLabel = null!;
    private StatBarChart _damageChart = null!;
    private StatBarChart _blockChart = null!;
    private StatBarChart _cardsChart = null!;
    private Button _closeButton = null!;
    private bool _escWasPressed;

    public void Init()
    {
        Layer = UIConstants.OverlayLayer + 1;
        BuildUI();
        StatManager.Instance.OnCombatEnded += ShowSummary;
        _panel.Visible = false;
        MainFile.Log("CombatSummaryPanel initialized.");
    }

    public void OnFrame()
    {
        if (!_panel.Visible) return;

        bool escNow = Input.IsKeyPressed(Key.Escape);
        if (escNow && !_escWasPressed)
        {
            _panel.Visible = false;
        }
        _escWasPressed = escNow;
    }

    private void BuildUI()
    {
        _panel = new PanelContainer();

        var styleBox = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.05f, 0.08f, 0.94f),
            BorderColor = UIConstants.PanelBorder,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 16,
            ContentMarginBottom = 16,
            ShadowColor = new Color(0, 0, 0, 0.5f),
            ShadowSize = 8,
            ShadowOffset = new Vector2(0, 4),
        };
        _panel.AddThemeStyleboxOverride("panel", styleBox);

        _panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _panel.CustomMinimumSize = new Vector2(420, 350);
        _panel.Position = new Vector2(-210, -175);

        _container = new VBoxContainer();
        _container.AddThemeConstantOverride("separation", 8);

        // Title
        _titleLabel = new Label
        {
            Text = "Combat Summary",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _titleLabel.AddThemeColorOverride("font_color", UIConstants.TextHeader);
        _titleLabel.AddThemeFontSizeOverride("font_size", 18);
        _container.AddChild(_titleLabel);

        // MVP
        _mvpLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _mvpLabel.AddThemeColorOverride("font_color", UIConstants.GoldColor);
        _mvpLabel.AddThemeFontSizeOverride("font_size", UIConstants.HeaderFontSize);
        _container.AddChild(_mvpLabel);

        // Separator
        var sep = new ColorRect
        {
            Color = UIConstants.SeparatorColor,
            CustomMinimumSize = new Vector2(0, 1),
        };
        _container.AddChild(sep);

        // Charts
        _damageChart = new StatBarChart("Damage Dealt", UIConstants.DamageColor);
        _container.AddChild(_damageChart);

        _blockChart = new StatBarChart("Block Gained", UIConstants.BlockColor);
        _container.AddChild(_blockChart);

        _cardsChart = new StatBarChart("Cards Played", UIConstants.CardColor);
        _container.AddChild(_cardsChart);

        // Close button — styled to match game
        _closeButton = new Button { Text = "Close" };
        var btnStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.12f, 0.10f, 0.9f),
            BorderColor = UIConstants.GoldDim,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        };
        _closeButton.AddThemeStyleboxOverride("normal", btnStyle);

        var btnHover = new StyleBoxFlat
        {
            BgColor = new Color(0.20f, 0.16f, 0.12f, 0.9f),
            BorderColor = UIConstants.GoldBorder,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        };
        _closeButton.AddThemeStyleboxOverride("hover", btnHover);
        _closeButton.AddThemeColorOverride("font_color", UIConstants.TextPrimary);
        _closeButton.AddThemeFontSizeOverride("font_size", UIConstants.FontSize);
        _closeButton.Pressed += () => _panel.Visible = false;
        _container.AddChild(_closeButton);

        _panel.AddChild(_container);
        AddChild(_panel);
    }

    private void ShowSummary(CombatSession combat)
    {
        var run = StatManager.Instance.CurrentRun;

        _titleLabel.Text = combat.IsVictory ? "Victory!" : "Combat Summary";

        var mvpId = combat.MvpPlayerId();
        if (mvpId.HasValue)
        {
            var mvpName = run?.Players.GetValueOrDefault(mvpId.Value)?.PlayerName
                ?? $"Player {mvpId.Value + 1}";
            _mvpLabel.Text = $"MVP: {mvpName}";
        }
        else
        {
            _mvpLabel.Text = "";
        }

        var damageByPlayer = combat.DamageByPlayer();
        var blockByPlayer = combat.BlockByPlayer();
        var cardsByPlayer = combat.CardsPlayedByPlayer();

        _damageChart.SetData(BuildChartEntries(damageByPlayer, run));
        _blockChart.SetData(BuildChartEntries(blockByPlayer, run));
        _cardsChart.SetData(BuildChartEntries(cardsByPlayer, run));

        _panel.Visible = true;
    }

    private static List<(string Name, int Value, Color Color)> BuildChartEntries(
        Dictionary<int, int> dataByPlayer, RunSession? run)
    {
        return dataByPlayer
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp =>
            {
                var name = run?.Players.GetValueOrDefault(kvp.Key)?.PlayerName
                    ?? $"Player {kvp.Key + 1}";
                var color = UIConstants.GetPlayerColor(kvp.Key);
                return (name, kvp.Value, color);
            })
            .ToList();
    }
}
