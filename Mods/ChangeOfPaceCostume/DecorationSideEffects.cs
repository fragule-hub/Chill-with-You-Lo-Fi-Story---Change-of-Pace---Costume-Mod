using System;
using System.Collections.Generic;
using Bulbul;
using UnityEngine;

namespace ChangeOfPaceCostume;

/// <summary>
/// All temporary decoration swaps in one place, together with their undo path:
///  - headwear replaces the cat-ear headphones with the default pair;
///  - April Fools replaces the glasses with the "no glasses" skin (Grasses_2).
///
/// The game does not undo either swap by itself, and every mod close path (toggle,
/// mutex conflict, same-hat auto-disable) must run the same undo logic. Keeping
/// apply/restore symmetric here prevents the "glasses never came back" class of bug.
/// </summary>
internal static class DecorationSideEffects
{
    private static DecorationService _decorationService;
    private static MasterDataLoader _masterDataLoader;

    /// <summary>Drops cached services (scene rebuild).</summary>
    public static void Invalidate()
    {
        _decorationService = null;
        _masterDataLoader = null;
    }

    public static void ReplaceHeadphones()
    {
        try
        {
            var service = GetDecorationService();
            service?.ReplaceCatEarHeadphoneWithDefaultTemporarily();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Limited] Cat-ear headphone swap failed: {ex.Message}");
        }
    }

    public static void ReplaceGlassesWithGrasses2()
    {
        try
        {
            var service = GetDecorationService();
            service?.ChangeDecoration(DecorationService.DecorationSkinType.Grasses_2, isSave: false);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Limited] Glasses swap failed: {ex.Message}");
        }
    }

    public static void RestoreHeadphonesIfIdle()
    {
        try
        {
            if (AnyEnabledWith(r => r.ReplaceHeadphones)) return;

            var service = GetDecorationService();
            if (service == null) return;

            if (service.IsCatEarHeadphoneTemporarilyLocked)
            {
                Plugin.Log.LogInfo("[Limited] Cat-ear headphones are locked by the game right now; not restoring.");
                return;
            }

            var saved = GetSavedSkin(DecorationService.DecorationCategoryType.Headphone);
            if (!saved.HasValue)
            {
                Plugin.Log.LogWarning("[Limited] No saved headphone decoration found; nothing to restore.");
                return;
            }

            service.ChangeDecoration(saved.Value, isSave: false);
            Plugin.Log.LogInfo($"[Limited] Restored saved headphone decoration: {saved.Value}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Limited] Headphone restore failed: {ex.Message}");
        }
    }

    public static void RestoreGlassesIfIdle()
    {
        try
        {
            if (AnyEnabledWith(r => r.ReplaceGlassesWithGrasses2)) return;

            // During the live April Fools event the game itself wants Grasses_2.
            if (SaveDataManager.Instance.LimitedTimeEventSaveData.CurrentType.Value
                == LimitedTimeEventType.AprilFool2026)
            {
                Plugin.Log.LogInfo("[Limited] April Fools event is active; keeping the event glasses.");
                return;
            }

            var saved = GetSavedSkin(DecorationService.DecorationCategoryType.Glasses);
            if (!saved.HasValue)
            {
                Plugin.Log.LogWarning("[Limited] No saved glasses decoration found; nothing to restore.");
                return;
            }

            var service = GetDecorationService();
            service?.ChangeDecoration(saved.Value, isSave: false);
            Plugin.Log.LogInfo($"[Limited] Restored saved glasses decoration: {saved.Value}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Limited] Glasses restore failed: {ex.Message}");
        }
    }

    private static bool AnyEnabledWith(Func<DecorRules, bool> predicate)
    {
        foreach (var entry in LimitedDecorationController.GetAllEntries())
        {
            if (LimitedDecorationController.IsEnabled(entry.Key) && predicate(entry.Rules)) return true;
        }
        return false;
    }

    private static DecorationService GetDecorationService()
    {
        if (_decorationService == null)
            _decorationService = GameServices.RoomComponent<DecorationService>();
        return _decorationService;
    }

    private static DecorationService.DecorationSkinType? GetSavedSkin(
        DecorationService.DecorationCategoryType category)
    {
        if (_masterDataLoader == null)
            _masterDataLoader = GameServices.Project<MasterDataLoader>();
        if (_masterDataLoader == null) return null;

        foreach (var skin in _masterDataLoader.DecorationMaster.GetSkinsByCategory(category))
        {
            if (SaveDataManager.Instance.DecorationSaveData.DecorationDic.TryGetValue(skin.SkinType, out var data)
                && data.IsActive.Value)
            {
                return skin.SkinType;
            }
        }
        return null;
    }
}
