using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ChangeOfPaceCostume;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    private const string PluginGuid = "com.chillwithyou.changeofpacecostume";
    private const string PluginName = "Change of Pace - Costume";
    private const string PluginVersion = "1.7.1";

    internal static ManualLogSource Log;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"[{PluginName} v{PluginVersion}] loaded.");

        // Diagnostics: log environment + patch binding results so that
        // "loaded but no UI in game" issues can be pinpointed from the log alone.
        try
        {
            Log.LogInfo($"[Init] Unity {Application.unityVersion} | BepInEx {typeof(BepInEx.BaseUnityPlugin).Assembly.GetName().Version}");
            var harmony = Harmony.CreateAndPatchAll(typeof(DecorationPatches), PluginGuid);
            var patched = harmony.GetPatchedMethods().ToList();
            Log.LogInfo($"[Init] Harmony patches applied: {patched.Count} method(s).");
            foreach (var m in patched)
            {
                Log.LogInfo($"[Init]   - {m.DeclaringType?.FullName}.{m.Name}");
            }
        }
        catch (Exception ex)
        {
            Log.LogError($"[Init] Harmony PatchAll FAILED (mod features will NOT work): {ex}");
        }
    }
}
