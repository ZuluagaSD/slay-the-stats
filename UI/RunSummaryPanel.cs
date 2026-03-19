using Godot;
using SlayTheStats.Core;
using SlayTheStats.UI.Components;

namespace SlayTheStats.UI;

/// <summary>
/// Shown when a run ends. Displays overall totals, per-combat timeline, and overall MVP.
/// Styled to match Slay the Spire 2's dark-fantasy aesthetic.
/// </summary>
public partial class RunSummaryPanel : CanvasLayer
{
    private PanelContainer _panel = null!;
    private VBoxContainer _container = null!;
    private Label _titleLabel = null!;
    private Label _mvpLabel = null!;
    private Label _statsLabel = null!;
    private StatBarChart _totalDamageChart = null!;
    private StatBarChart _totalBlockChart = null!;
    private VBoxContainer _timelineContainer = null!;
    private Button _closeButton = null!;
    private bool _escWasPressed;

    public void Init()
    {
        Layer = UIConstants.OverlayLayer + 2;
        BuildUI();
        StatManager.Instance.OnRunEnded += ShowSummary;
        _panel.Visible = false;
        MainFile.Log("RunSummaryPanel initialized.");
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
            BgColor = new Color(0.05f, 0.04f, 0.07f, 0.95f),
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
            ContentMarginTop = 20,
            ContentMarginBottom = 20,
            ShadowColor = new Color(0, 0, 0, 0.5f),
            ShadowSize = 10,
            ShadowOffset = new Vector2(0, 4),
        };
        _panel.AddThemeStyleboxOverride("panel", styleBox);

        _panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _panel.CustomMinimumSize = new Vector2(480, 480);
        _panel.Position = new Vector2(-240, -240);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(460, 460),
        };

        _container = new VBoxContainer();
        _container.AddThemeConstantOverride("separation", 10);
        _container.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        // Title
        _titleLabel = new Label
        {
            Text = "Run Summary",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _titleLabel.AddThemeColorOverride("font_color", UIConstants.TextHeader);
        _titleLabel.AddThemeFontSizeOverride("font_size", 22);
        _container.AddChild(_titleLabel);

        // MVP
        _mvpLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _mvpLabel.AddThemeColorOverride("font_color", UIConstants.GoldColor);
        _mvpLabel.AddThemeFontSizeOverride("font_size", UIConstants.TitleFontSize);
        _container.AddChild(_mvpLabel);

        // Run stats
        _statsLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _statsLabel.AddThemeColorOverride("font_color", UIConstants.TextSecondary);
        _statsLabel.AddThemeFontSizeOverride("font_size", UIConstants.FontSize);
        _container.AddChild(_statsLabel);

        _container.AddChild(MakeSeparator());

        // Total damage chart
        _totalDamageChart = new StatBarChart("Total Damage", UIConstants.DamageColor);
        _container.AddChild(_totalDamageChart);

        // Total block chart
        _totalBlockChart = new StatBarChart("Total Block", UIConstants.BlockColor);
        _container.AddChild(_totalBlockChart);

        _container.AddChild(MakeSeparator());

        // Per-combat timeline header
        var timelineHeader = new Label
        {
            Text = "Per-Combat Breakdown",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        timelineHeader.AddThemeColorOverride("font_color", UIConstants.TextHeader);
        timelineHeader.AddThemeFontSizeOverride("font_size", UIConstants.HeaderFontSize);
        _container.AddChild(timelineHeader);

        _timelineContainer = new VBoxContainer();
        _timelineContainer.AddThemeConstantOverride("separation", 4);
        _container.AddChild(_timelineContainer);

        // Close button
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
        _closeButton.AddThemeColorOverride("font_color", UIConstants.TextPrimary);
        _closeButton.AddThemeFontSizeOverride("font_size", UIConstants.FontSize);
        _closeButton.Pressed += () => _panel.Visible = false;
        _container.AddChild(_closeButton);

        scroll.AddChild(_container);
        _panel.AddChild(scroll);
        AddChild(_panel);
    }

    private static ColorRect MakeSeparator()
    {
        return new ColorRect
        {
            Color = UIConstants.SeparatorColor,
            CustomMinimumSize = new Vector2(0, 1),
        };
    }

    private void ShowSummary(RunSession run)
    {
        var mvpId = run.OverallMvp();
        if (mvpId.HasValue)
        {
            var mvpName = run.Players.GetValueOrDefault(mvpId.Value)?.PlayerName
                ?? $"Player {mvpId.Value + 1}";
            _mvpLabel.Text = $"Overall MVP: {mvpName}";
        }
        else
        {
            _mvpLabel.Text = "";
        }

        int totalCombats = run.Combats.Count;
        int victories = run.Combats.Count(c => c.IsVictory);
        var duration = run.EndTime.HasValue ? run.EndTime.Value - run.StartTime : TimeSpan.Zero;
        _statsLabel.Text = $"{totalCombats} combats ({victories} victories) | {duration.Minutes}m {duration.Seconds}s";

        var totalDmg = run.RunDamageByPlayer();
        _totalDamageChart.SetData(BuildChartEntries(totalDmg, run));

        var totalBlk = new Dictionary<int, int>();
        foreach (var combat in run.Combats)
        {
            foreach (var (playerId, blk) in combat.BlockByPlayer())
            {
                totalBlk.TryGetValue(playerId, out var existing);
                totalBlk[playerId] = existing + blk;
            }
        }
        _totalBlockChart.SetData(BuildChartEntries(totalBlk, run));

        foreach (var child in _timelineContainer.GetChildren())
            child.QueueFree();

        for (int i = 0; i < run.Combats.Count; i++)
        {
            var combat = run.Combats[i];
            var result = combat.IsVictory ? "Victory" : "Defeat";
            var turns = combat.CurrentTurn;

            var label = new Label
            {
                Text = $"Combat {i + 1}: {result} ({turns} turns) | " +
                       $"DMG: {combat.DamageRecords.Sum(r => r.Amount)} | " +
                       $"BLK: {combat.BlockRecords.Sum(r => r.Amount)} | " +
                       $"Cards: {combat.CardPlayRecords.Count}",
            };
            label.AddThemeColorOverride("font_color", combat.IsVictory
                ? UIConstants.HealColor
                : UIConstants.DamageColor);
            label.AddThemeFontSizeOverride("font_size", UIConstants.FontSize);
            _timelineContainer.AddChild(label);
        }

        _panel.Visible = true;
    }

    private static List<(string Name, int Value, Color Color)> BuildChartEntries(
        Dictionary<int, int> dataByPlayer, RunSession run)
    {
        return dataByPlayer
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp =>
            {
                var name = run.Players.GetValueOrDefault(kvp.Key)?.PlayerName
                    ?? $"Player {kvp.Key + 1}";
                var color = UIConstants.GetPlayerColor(kvp.Key);
                return (name, kvp.Value, color);
            })
            .ToList();
    }
}
