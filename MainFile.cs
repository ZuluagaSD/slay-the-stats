using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using SlayTheStats.Core;
using SlayTheStats.Persistence;
using SlayTheStats.UI;

namespace SlayTheStats;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    private static Harmony? _harmony;
    private static MainFile? _instance;

    private StatsOverlay? _overlay;
    private CombatSummaryPanel? _combatSummary;
    private RunSummaryPanel? _runSummary;

    public static MainFile? Instance => _instance;

    public static void Initialize()
    {
        Log("SlayTheStats v0.1 initializing...");

        _harmony = new Harmony("com.slaythestats.mod");
        int patchCount = 0;
        foreach (var type in typeof(MainFile).Assembly.GetTypes())
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
                continue;
            try
            {
                _harmony.CreateClassProcessor(type).Patch();
                patchCount++;
            }
            catch (Exception ex)
            {
                LogError($"Failed to patch {type.Name}: {ex.Message}");
            }
        }
        Log($"Harmony patches applied: {patchCount} succeeded, {_harmony.GetPatchedMethods().Count()} methods patched.");

        StatManager.EnsureInitialized();
        RunExporter.Initialize();

        // Add ourselves to the scene tree via deferred call
        try
        {
            var sceneTree = Engine.GetMainLoop() as SceneTree;
            var root = sceneTree?.Root;
            if (root is not null)
            {
                _instance = new MainFile();
                Callable.From(() =>
                {
                    try
                    {
                        root.AddChild(_instance);
                        Log("MainFile node added to scene tree.");

                        // Build UI directly — _Ready() won't fire without source generators
                        _instance.InitializeUI();

                        // Connect to ProcessFrame for per-frame updates (replaces _Process/_Input)
                        sceneTree!.ProcessFrame += _instance.OnProcessFrame;
                        Log("ProcessFrame signal connected.");
                    }
                    catch (Exception ex)
                    {
                        LogError($"Deferred setup failed: {ex}");
                    }
                }).CallDeferred();
            }
            else
            {
                Log("WARNING: No scene root — UI won't load.");
            }
        }
        catch (Exception ex)
        {
            LogError($"UI setup failed: {ex.Message}");
        }

        Log("SlayTheStats initialized successfully.");
    }

    private void InitializeUI()
    {
        _overlay = new StatsOverlay();
        AddChild(_overlay);
        _overlay.Init();

        _combatSummary = new CombatSummaryPanel();
        AddChild(_combatSummary);
        _combatSummary.Init();

        _runSummary = new RunSummaryPanel();
        AddChild(_runSummary);
        _runSummary.Init();

        Log("SlayTheStats UI loaded.");
    }

    private void OnProcessFrame()
    {
        try
        {
            _overlay?.OnFrame();
            _combatSummary?.OnFrame();
            _runSummary?.OnFrame();
        }
        catch (Exception ex)
        {
            LogError($"Frame update error: {ex.Message}");
        }
    }

    public static void Log(string message)
    {
        GD.Print($"[SlayTheStats] {message}");
    }

    public static void LogError(string message)
    {
        GD.PrintErr($"[SlayTheStats] {message}");
    }

    public static void LogWarning(string message)
    {
        GD.Print($"[SlayTheStats] WARNING: {message}");
    }
}
