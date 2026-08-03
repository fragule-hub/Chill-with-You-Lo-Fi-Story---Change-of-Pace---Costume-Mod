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
        Plugin.Log.LogInfo("[Patch] DecorationListUI.Setup triggered.");
        var scrollRect = Traverse.Create(__instance)
            .Field("_scrollRect").GetValue<ScrollRect>();
        if (scrollRect == null)
        {
            Plugin.Log.LogError(
                $"[Patch] '_scrollRect' not found on DecorationListUI (name='{__instance.name}'). " +
                "The mod UI will NOT be injected. This usually means the game was updated and " +
                "the field no longer exists; report this log line to the mod author.");
            return;
        }
        Plugin.Log.LogInfo($"[Patch] _scrollRect OK: '{scrollRect.name}' (content: '{scrollRect.content?.name ?? "NULL"}')");
        CostumeButtonsInjector.Inject(scrollRect);
    }

    [HarmonyPatch(typeof(DecorationListUI), nameof(DecorationListUI.Activate))]
    [HarmonyPostfix]
    private static void DecorationListUI_Activate_Postfix()
    {
        Plugin.Log.LogInfo("[Patch] DecorationListUI.Activate triggered.");
        CostumeButtonsInjector.OnActivate();
    }

    [HarmonyPatch(typeof(DecorationListUI), nameof(DecorationListUI.Deactivate))]
    [HarmonyPostfix]
    private static void DecorationListUI_Deactivate_Postfix()
    {
        Plugin.Log.LogInfo("[Patch] DecorationListUI.Deactivate triggered.");
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

    // ── Limited decorations: re-apply after the game forces removal ──
    // We patch only the "system reset" entries (Setup / StoryTidying), NOT ChangeHat itself,
    // so in-story hat changes during the collab scenario keep working normally.
    //
    // ApplyAll is deferred by one frame: ChangeDecoration (used by the glasses rule)
    // depends on DecorationService fields that are not safe to touch synchronously
    // during the startup Setup window (NRE risk). One frame later everything is ready.

    [HarmonyPatch(typeof(HatChangeService), nameof(HatChangeService.Setup))]
    [HarmonyPostfix]
    private static void HatChangeService_Setup_Postfix()
    {
        ApplyAllDeferred();
    }

    [HarmonyPatch(typeof(HatChangeService), nameof(HatChangeService.StoryTidying))]
    [HarmonyPostfix]
    private static void HatChangeService_StoryTidying_Postfix()
    {
        ApplyAllDeferred();
    }

    [HarmonyPatch(typeof(LimitedTimeEventService), nameof(LimitedTimeEventService.Setup))]
    [HarmonyPostfix]
    private static void LimitedTimeEventService_Setup_Postfix()
    {
        ApplyAllDeferred();
    }

    [HarmonyPatch(typeof(LimitedTimeEventService), nameof(LimitedTimeEventService.OnStoryTidying))]
    [HarmonyPostfix]
    private static void LimitedTimeEventService_OnStoryTidying_Postfix()
    {
        ApplyAllDeferred();
    }

    private static void ApplyAllDeferred()
    {
        UniTask.Void(async () =>
        {
            await UniTask.DelayFrame(1);
            LimitedDecorationController.ApplyAll();
        });
    }
}
