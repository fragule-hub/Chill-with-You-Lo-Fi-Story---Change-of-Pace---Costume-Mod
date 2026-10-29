using System;
using System.Reflection;
using Bulbul;
using Bulbul.MasterData;
using HarmonyLib;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

namespace ChangeOfPaceCostume;

/// <summary>
/// Harmony patch registration.
///
/// Two rules keep the mod resilient to game updates:
///  1. Every target is resolved with an EXPLICIT parameter list. Patching by name
///     alone breaks as soon as the game adds an overload (Harmony cannot guess which
///     one is meant and throws AmbiguousMatchException).
///  2. Every target is patched INDIVIDUALLY and guarded. Harmony's PatchAll is
///     all-or-nothing: one bad target rolls back every patch in the class. Here a
///     broken target only disables its own feature; everything else keeps working.
/// </summary>
internal static class DecorationPatches
{
    private static readonly Type[] NoParameters = Type.EmptyTypes;

    // Added by the game's 2026-10 update. The new overload is what all story-end
    // paths call, so we hook it in addition to the parameterless variant.
    private static readonly Type[] StoryTidyingParameters =
    {
        typeof(ScenarioType), typeof(float), typeof(bool)
    };

    private static readonly Type[] StoryReadyParameters = { typeof(ScenarioType) };

    public static void Apply(Harmony harmony)
    {
        // UI injection timing
        Patch(harmony, typeof(DecorationListUI), nameof(DecorationListUI.Setup), NoParameters,
            postfix: nameof(DecorationListUI_Setup_Postfix));
        Patch(harmony, typeof(DecorationListUI), nameof(DecorationListUI.Activate), NoParameters,
            postfix: nameof(DecorationListUI_Activate_Postfix));
        Patch(harmony, typeof(DecorationListUI), nameof(DecorationListUI.Deactivate), NoParameters,
            postfix: nameof(DecorationListUI_Deactivate_Postfix));

        // Costume "Forever" mode
        Patch(harmony, typeof(CostumeChangeService), nameof(CostumeChangeService.ChangeTodayCostume), NoParameters,
            prefix: nameof(CostumeChangeService_ChangeTodayCostume_Prefix));

        // Today's hairstyle restore
        Patch(harmony, typeof(ChangeHairService), nameof(ChangeHairService.TodayChangeHair), NoParameters,
            prefix: nameof(ChangeHairService_TodayChangeHair_Prefix));

        // Limited decorations: re-apply after the game resets them
        Patch(harmony, typeof(HatChangeService), nameof(HatChangeService.Setup), NoParameters,
            postfix: nameof(HatChangeService_Setup_Postfix));
        Patch(harmony, typeof(HatChangeService), nameof(HatChangeService.StoryTidying), NoParameters,
            postfix: nameof(HatChangeService_StoryTidying_Postfix));
        Patch(harmony, typeof(LimitedTimeEventService), nameof(LimitedTimeEventService.Setup), NoParameters,
            postfix: nameof(LimitedTimeEventService_Setup_Postfix));
        Patch(harmony, typeof(LimitedTimeEventService), nameof(LimitedTimeEventService.OnStoryReady), StoryReadyParameters,
            postfix: nameof(LimitedTimeEventService_OnStoryReady_Postfix));
        Patch(harmony, typeof(LimitedTimeEventService), nameof(LimitedTimeEventService.OnStoryTidying), NoParameters,
            postfix: nameof(LimitedTimeEventService_OnStoryTidying_Postfix));
        Patch(harmony, typeof(LimitedTimeEventService), nameof(LimitedTimeEventService.OnStoryTidying),
            StoryTidyingParameters, postfix: nameof(LimitedTimeEventService_OnStoryTidying3_Postfix));
    }

    private static void Patch(Harmony harmony, Type type, string methodName, Type[] parameters,
        string prefix = null, string postfix = null)
    {
        string label = $"{type.FullName}.{methodName}({Describe(parameters)})";
        try
        {
            var target = AccessTools.Method(type, methodName, parameters);
            if (target == null)
            {
                Plugin.Log.LogError(
                    $"[Init] Target not found: {label}. This patch is skipped; all other features keep working. " +
                    "Most likely the game was updated - report this log line to the mod author.");
                return;
            }

            harmony.Patch(target,
                prefix: prefix == null ? null : new HarmonyMethod(Handler(prefix)),
                postfix: postfix == null ? null : new HarmonyMethod(Handler(postfix)));

            Plugin.Log.LogInfo($"[Init] Patched {label}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[Init] Failed to patch {label} (this feature only): {ex}");
        }
    }

    private static MethodInfo Handler(string name)
    {
        var method = typeof(DecorationPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException($"Patch handler '{name}' not found.");
        return method;
    }

    private static string Describe(Type[] parameters)
    {
        if (parameters == null || parameters.Length == 0) return "no args";
        var names = new string[parameters.Length];
        for (int i = 0; i < parameters.Length; i++) names[i] = parameters[i].Name;
        return string.Join(", ", names);
    }

    // ── UI ──

    private static void DecorationListUI_Setup_Postfix(DecorationListUI __instance)
    {
        Plugin.Log.LogInfo("[Patch] DecorationListUI.Setup triggered.");
        var scrollRect = Traverse.Create(__instance)
            .Field("_scrollRect").GetValue<ScrollRect>();
        if (scrollRect == null)
        {
            // Compatibility fallback: the field may be renamed by a game update.
            foreach (var candidate in __instance.GetComponentsInChildren<ScrollRect>(true))
            {
                if (candidate != null && candidate.content != null)
                {
                    scrollRect = candidate;
                    break;
                }
            }
            if (scrollRect != null)
            {
                Plugin.Log.LogWarning(
                    "[Patch] '_scrollRect' field unavailable; using a child ScrollRect as fallback.");
            }
        }
        if (scrollRect == null)
        {
            Plugin.Log.LogError(
                $"[Patch] No ScrollRect found on DecorationListUI (name='{__instance.name}'). " +
                "The mod UI will NOT be injected. This usually means the game was updated; " +
                "report this log line to the mod author.");
            return;
        }
        Plugin.Log.LogInfo($"[Patch] _scrollRect OK: '{scrollRect.name}' (content: '{scrollRect.content?.name ?? "NULL"}')");
        GameServices.Invalidate();
        HairController.Invalidate();
        DecorationSideEffects.Invalidate();
        LimitedDecorationController.RefreshEntries();
        CostumeButtonsInjector.Inject(scrollRect);
    }

    private static void DecorationListUI_Activate_Postfix()
    {
        Plugin.Log.LogInfo("[Patch] DecorationListUI.Activate triggered.");
        CostumeButtonsInjector.OnActivate();
    }

    private static void DecorationListUI_Deactivate_Postfix()
    {
        Plugin.Log.LogInfo("[Patch] DecorationListUI.Deactivate triggered.");
        CostumeButtonsInjector.OnDeactivate();
    }

    // ── costume ──

    private static bool CostumeChangeService_ChangeTodayCostume_Prefix(CostumeChangeService __instance)
    {
        try
        {
            var permanent = ModSaveData.GetPermanentSkin();
            if (permanent.HasValue)
            {
                Plugin.Log.LogInfo($"[ChangeTodayCostume] Applying permanent costume: {permanent.Value}");
                __instance.ChangeCostume(permanent.Value).Forget();
                return false;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[ChangeTodayCostume] Prefix failed, falling back to vanilla: {ex.Message}");
        }
        return true;
    }

    // ── hair ──

    // Same-day hair restore, including for players who have not yet met the
    // pomodoro unlock. Vanilla TodayChangeHair ForceNormals (and overwrites
    // the save) when unlocked time is too low. Episode 32 is left to the game.
    private static bool ChangeHairService_TodayChangeHair_Prefix(ChangeHairService __instance)
    {
        try
        {
            if (HairController.IsFinalePending())
            {
                Plugin.Log.LogInfo("[TodayChangeHair] Finale pending - leaving vanilla ForceNormal.");
                return true;
            }

            var latest = SaveDataManager.Instance.HairLotteryData?.LatestChangeData;
            if (latest == null) return true;
            if (HairController.ShouldLotteryNewHair(latest.Changed, DateTime.Now)) return true;

            Plugin.Log.LogInfo($"[TodayChangeHair] Restoring today's hair: {latest.Kind} {latest.PinKind}");
            __instance.ChangeHairWithoutSave(latest);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[TodayChangeHair] Prefix failed, falling back to vanilla: {ex.Message}");
            return true;
        }
    }

    // ── limited decorations ──
    // We patch only the "system reset" entries (Setup / StoryTidying), NOT ChangeHat
    // itself, so in-story hat changes during the collab scenario keep working.
    //
    // ApplyAll is deferred by one frame: ChangeDecoration (used by the glasses rule)
    // depends on DecorationService fields that are not safe to touch synchronously
    // during the startup Setup window (NRE risk). One frame later everything is ready.

    private static void HatChangeService_Setup_Postfix() => ApplyAllDeferred();

    private static void HatChangeService_StoryTidying_Postfix() => ApplyAllDeferred();

    private static void LimitedTimeEventService_Setup_Postfix() => ApplyAllDeferred();

    /// <summary>
    /// Story start during the finale window: strip mod headwear so the finale keeps the
    /// game's canonical look. The saved toggles are untouched and reactivate later.
    /// </summary>
    private static void LimitedTimeEventService_OnStoryReady_Postfix(ScenarioType scenarioType)
    {
        if (scenarioType == ScenarioType.MainScenario && LimitedDecorationController.IsFinaleHandlingPeriod())
        {
            LimitedDecorationController.SuspendHeadwearForFinale();
        }
    }

    private static void LimitedTimeEventService_OnStoryTidying_Postfix() => ApplyAllDeferred();

    /// <summary>
    /// Overload added by the 2026-10 game update. All story-end paths call this one;
    /// it skips the game's own event re-activation for the main finale (ep32).
    /// We mirror that: cancel any ApplyAll that was scheduled earlier this frame
    /// (HatChangeService.StoryTidying runs first) and stay silent, so the finale keeps
    /// the game's intended look. Talk-log replays are treated normally.
    /// </summary>
    private static void LimitedTimeEventService_OnStoryTidying3_Postfix(object[] __args)
    {
        try
        {
            var scenarioType = (ScenarioType)__args[0];
            float episodeNumber = (float)__args[1];
            bool isTalkLog = (bool)__args[2];

            if (!isTalkLog && scenarioType == ScenarioType.MainScenario && episodeNumber == 32f)
            {
                _applySerial++;
                Plugin.Log.LogInfo("[Limited] Finale (main ep32): skipping auto re-apply (game default).");
                return;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Limited] Finale check failed, applying normally: {ex.Message}");
        }

        ApplyAllDeferred();
    }

    // Hat Setup + Event Setup both fire during Initialize; keep only the last
    // scheduled run so we do not ApplyAll four times on the same frame.
    private static int _applySerial;

    private static void ApplyAllDeferred()
    {
        int serial = ++_applySerial;
        UniTask.Void(async () =>
        {
            try
            {
                await UniTask.DelayFrame(1);
                if (serial != _applySerial) return;
                LimitedDecorationController.ApplyAll();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Limited] Deferred apply failed: {ex}");
            }
        });
    }
}
