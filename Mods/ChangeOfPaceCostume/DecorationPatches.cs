using Bulbul;
using HarmonyLib;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

namespace ChangeOfPaceCostume;

internal static class DecorationPatches
{
    [HarmonyPatch(typeof(DecorationListUI), nameof(DecorationListUI.Setup))]
    [HarmonyPostfix]
    private static void DecorationListUI_Setup_Postfix(DecorationListUI __instance)
    {
        var scrollRect = Traverse.Create(__instance)
            .Field("_scrollRect").GetValue<ScrollRect>();
        if (scrollRect == null)
        {
            Plugin.Log.LogWarning("[DecorationPatches] _scrollRect not found.");
            return;
        }
        CostumeButtonsInjector.Inject(scrollRect);
    }

    [HarmonyPatch(typeof(DecorationListUI), nameof(DecorationListUI.Activate))]
    [HarmonyPostfix]
    private static void DecorationListUI_Activate_Postfix()
    {
        CostumeButtonsInjector.OnActivate();
    }

    [HarmonyPatch(typeof(DecorationListUI), nameof(DecorationListUI.Deactivate))]
    [HarmonyPostfix]
    private static void DecorationListUI_Deactivate_Postfix()
    {
        CostumeButtonsInjector.OnDeactivate();
    }

    [HarmonyPatch(typeof(CostumeChangeService), nameof(CostumeChangeService.ChangeTodayCostume))]
    [HarmonyPrefix]
    private static bool CostumeChangeService_ChangeTodayCostume_Prefix(CostumeChangeService __instance)
    {
        var permanent = ModSaveData.GetPermanentSkin();
        if (permanent.HasValue)
        {
            Plugin.Log.LogInfo($"[ChangeTodayCostume] Applying permanent costume: {permanent.Value}");
            __instance.ChangeCostume(permanent.Value).Forget();
            return false;
        }
        return true;
    }
}
