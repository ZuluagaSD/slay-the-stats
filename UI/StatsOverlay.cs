using Godot;
using SlayTheStats.Core;
using SlayTheStats.UI.Components;

namespace SlayTheStats.UI;

/// <summary>
/// Main overlay showing live combat stats. Toggled with F7.
/// Styled to match Slay the Spire 2's dark-fantasy aesthetic.
/// </summary>
public partial class StatsOverlay : CanvasLayer
{
    private PanelContainer _panel = null!;
    private VBoxContainer _container = null!;
    private Label _titleLabel = null!;
    private Label _turnLabel = null!;
    private VBoxContainer _rowsContainer = null!;

    private readonly Dictionary<int, PlayerStatRow> _playerRows = [];
    private bool _visible = true;
    private bool _dirty = true;
    private bool _f7WasPressed;
    private bool _shiftF7WasPressed;
    private int _positionIndex; // 0=top-right, 1=top-left, 2=bottom-right, 3=bottom-left

    public void Init()
    {
        Layer = UIConstants.OverlayLayer;
        BuildUI();
        SubscribeToEvents();
        MainFile.Log("StatsOverlay initialized.");
    }

    public void OnFrame()
    {
        bool f7Now = Input.IsKeyPressed(Key.F7);
        bool shiftHeld = Input.IsKeyPressed(Key.Shift);

        // Shift+F7: cycle position
        bool shiftF7Now = f7Now && shiftHeld;
        if (shiftF7Now && !_shiftF7WasPressed)
        {
            _positionIndex = (_positionIndex + 1) % 4;
            ApplyPosition();
            MainFile.Log($"Overlay position: {_positionIndex}");
        }
        _shiftF7WasPressed = shiftF7Now;

        // F7 alone: toggle visibility
        if (f7Now && !shiftHeld && !_f7WasPressed)
        {
            _visible = !_visible;
            _panel.Visible = _visible;
            MainFile.Log($"Overlay toggled: {(_visible ? "ON" : "OFF")}");
        }
        _f7WasPressed = f7Now;

        if (!_visible || !_dirty) return;
        _dirty = false;
        RefreshUI();
    }

    private void ApplyPosition()
    {
        const int m = UIConstants.OverlayMargin;
        switch (_positionIndex)
        {
            case 0: // top-right
                _panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
                _panel.Position = new Vector2(-310, m);
                break;
            case 1: // top-left
                _panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
                _panel.Position = new Vector2(m, m);
                break;
            case 2: // bottom-right
                _panel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
                _panel.Position = new Vector2(-310, -150);
                break;
            case 3: // bottom-left
                _panel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
                _panel.Position = new Vector2(m, -150);
                break;
        }
    }

    private void BuildUI()
    {
        _panel = new PanelContainer();

        var styleBox = new StyleBoxFlat
        {
            BgColor = UIConstants.PanelBackground,
            BorderColor = UIConstants.PanelBorder,
            BorderWidthTop = UIConstants.BorderWidth,
            BorderWidthBottom = UIConstants.BorderWidth,
            BorderWidthLeft = UIConstants.BorderWidth,
            BorderWidthRight = UIConstants.BorderWidth,
            CornerRadiusTopLeft = UIConstants.PanelCornerRadius,
            CornerRadiusTopRight = UIConstants.PanelCornerRadius,
            CornerRadiusBottomLeft = UIConstants.PanelCornerRadius,
            CornerRadiusBottomRight = UIConstants.PanelCornerRadius,
            ContentMarginLeft = UIConstants.Padding + 2,
            ContentMarginRight = UIConstants.Padding + 2,
            ContentMarginTop = UIConstants.Padding,
            ContentMarginBottom = UIConstants.Padding,
            ShadowColor = new Color(0, 0, 0, 0.4f),
            ShadowSize = 4,
            ShadowOffset = new Vector2(0, 2),
        };
        _panel.AddThemeStyleboxOverride("panel", styleBox);

        _panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _panel.Position = new Vector2(-310, UIConstants.OverlayMargin);
        _panel.CustomMinimumSize = new Vector2(290, 0);
        _panel.MouseFilter = Control.MouseFilterEnum.Ignore;

        _container = new VBoxContainer();
        _container.AddThemeConstantOverride("separation", 3);

        // Title row: "SlayTheStats" centered, gold-tinted
        _titleLabel = new Label
        {
            Text = "Combat Stats",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _titleLabel.AddThemeColorOverride("font_color", UIConstants.TextHeader);
        _titleLabel.AddThemeFontSizeOverride("font_size", UIConstants.TitleFontSize);
        _container.AddChild(_titleLabel);

        // Turn indicator
        _turnLabel = new Label
        {
            Text = "Turn: -",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _turnLabel.AddThemeColorOverride("font_color", UIConstants.TextSecondary);
        _turnLabel.AddThemeFontSizeOverride("font_size", UIConstants.FontSize - 1);
        _container.AddChild(_turnLabel);

        // Separator — thin gold line
        var sep = new ColorRect
        {
            Color = UIConstants.SeparatorColor,
            CustomMinimumSize = new Vector2(0, 1),
        };
        _container.AddChild(sep);

        // Column headers
        var headers = new HBoxContainer();
        headers.AddThemeConstantOverride("separation", 4);
        AddColumnHeader(headers, "Player", UIConstants.TextSecondary, 100);
        AddColumnHeader(headers, "DMG", UIConstants.DamageColor * new Color(1, 1, 1, 0.7f), UIConstants.ColumnWidth);
        AddColumnHeader(headers, "BLK", UIConstants.BlockColor * new Color(1, 1, 1, 0.7f), UIConstants.ColumnWidth);
        AddColumnHeader(headers, "Cards", UIConstants.CardColor * new Color(1, 1, 1, 0.7f), UIConstants.ColumnWidth);
        _container.AddChild(headers);

        // Player rows
        _rowsContainer = new VBoxContainer();
        _rowsContainer.AddThemeConstantOverride("separation", 1);
        _container.AddChild(_rowsContainer);

        _panel.AddChild(_container);
        AddChild(_panel);

        // Make entire overlay click-through so it doesn't block game buttons
        SetMouseIgnoreRecursive(_panel);
    }

    private static void SetMouseIgnoreRecursive(Node node)
    {
        if (node is Control control)
            control.MouseFilter = Control.MouseFilterEnum.Ignore;
        foreach (var child in node.GetChildren())
            SetMouseIgnoreRecursive(child);
    }

    private static void AddColumnHeader(HBoxContainer parent, string text, Color color, int minWidth)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(minWidth, UIConstants.RowHeight),
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", UIConstants.FontSize - 1);
        parent.AddChild(label);
    }

    private void SubscribeToEvents()
    {
        var sm = StatManager.Instance;
        sm.OnDamageRecorded += _ => _dirty = true;
        sm.OnBlockRecorded += _ => _dirty = true;
        sm.OnCardPlayRecorded += _ => _dirty = true;
        sm.OnTurnChanged += _ => _dirty = true;
        sm.OnCombatStarted += _ => { ClearPlayerRows(); _dirty = true; };
        sm.OnCombatEnded += _ => _dirty = true;
    }

    private void ClearPlayerRows()
    {
        foreach (var child in _rowsContainer.GetChildren())
            child.QueueFree();
        _playerRows.Clear();
    }

    private void RefreshUI()
    {
        var combat = StatManager.Instance.CurrentCombat;
        if (combat is null)
        {
            _turnLabel.Text = "No active combat";
            return;
        }

        _turnLabel.Text = $"Turn {combat.CurrentTurn}";

        var run = StatManager.Instance.CurrentRun;
        if (run is null) return;

        var damageByPlayer = combat.DamageByPlayer();
        var blockByPlayer = combat.BlockByPlayer();
        var cardsByPlayer = combat.CardsPlayedByPlayer();

        var allPlayerIds = new HashSet<int>();
        foreach (var id in damageByPlayer.Keys) allPlayerIds.Add(id);
        foreach (var id in blockByPlayer.Keys) allPlayerIds.Add(id);
        foreach (var id in cardsByPlayer.Keys) allPlayerIds.Add(id);

        foreach (int playerId in allPlayerIds)
        {
            damageByPlayer.TryGetValue(playerId, out int dmg);
            blockByPlayer.TryGetValue(playerId, out int blk);
            cardsByPlayer.TryGetValue(playerId, out int cards);

            if (!_playerRows.TryGetValue(playerId, out var row))
            {
                var playerStats = run.Players.GetValueOrDefault(playerId);
                string name = playerStats?.PlayerName ?? $"Player {playerId + 1}";
                var color = UIConstants.GetPlayerColor(playerId);

                row = new PlayerStatRow(playerId, name, color);
                _playerRows[playerId] = row;
                _rowsContainer.AddChild(row);
                SetMouseIgnoreRecursive(row);
            }

            row.UpdateStats(dmg, blk, cards);
        }
    }
}
