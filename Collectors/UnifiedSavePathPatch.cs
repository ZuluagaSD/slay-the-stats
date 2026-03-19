using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;

namespace SlayTheStats.Collectors;

/// <summary>
/// Patches UserDataPathProvider so the game uses the vanilla save directory
/// even when mods are loaded. No more "Running Modded" screen.
/// Based on: https://www.nexusmods.com/slaythespire2/mods/6
/// </summary>
[HarmonyPatch(typeof(UserDataPathProvider))]
public static class UnifiedSavePathPatch
{
    [HarmonyPatch(nameof(UserDataPathProvider.IsRunningModded), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool PatchGetIsRunningModded(ref bool __result)
    {
        __result = false;
        return false;
    }

    [HarmonyPatch(nameof(UserDataPathProvider.IsRunningModded), MethodType.Setter)]
    [HarmonyPrefix]
    public static bool PatchSetIsRunningModded()
    {
        return false;
    }

    [HarmonyPatch(nameof(UserDataPathProvider.GetProfileDir))]
    [HarmonyPrefix]
    public static bool PatchGetProfileDir(ref string __result, int profileId)
    {
        // Build the unmodded profile path directly using reflection to call GetBaseDir
        try
        {
            var baseDir = typeof(UserDataPathProvider)
                .GetMethod("GetBaseDir", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?.Invoke(null, null) as string;

            if (baseDir is not null)
            {
                __result = $"{baseDir}/profile{profileId}";
                return false;
            }
        }
        catch { }

        // Fallback: let original run
        return true;
    }
}
