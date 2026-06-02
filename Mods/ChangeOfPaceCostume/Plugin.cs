using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace ChangeOfPaceCostume;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    private const string PluginGuid = "com.chillwithyou.changeofpacecostume";
    private const string PluginName = "Change of Pace - Costume";
    private const string PluginVersion = "1.1.0";

    internal static ManualLogSource Log;

    private void Awake()
    {
        Log = Logger;
        Harmony.CreateAndPatchAll(typeof(DecorationPatches), PluginGuid);
        Log.LogInfo($"[{PluginName} v{PluginVersion}] loaded.");
    }
}
