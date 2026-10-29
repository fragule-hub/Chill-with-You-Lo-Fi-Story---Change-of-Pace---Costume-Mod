using System;
using System.Collections.Generic;
using Bulbul;
using UnityEngine;

namespace ChangeOfPaceCostume;

/// <summary>
/// Headwear-to-hairstyle policy, kept separate from <see cref="HairController"/>.
///
///  - Plain headwear (locked by the game but no special hairstyle): unlock and apply
///    the player's saved hairstyle so hairpins stay selectable.
///  - Special headwear (applies its own hair BlendShape, e.g. the beret): follow the
///    game default (keep the lock and the special style) and only remember the
///    player's hairstyle so it can be put back when the special state ends.
///
/// The special/plain decision itself is made by LimitedDecorationController by
/// comparing hair BlendShape weights before and after the hat is applied.
/// </summary>
internal static class HairLockPolicy
{
    private struct HairBackup
    {
        public bool Valid;
        public HairStyle Kind;
        public HairPinKind Pin;
    }

    private static HairBackup _backup;
    private static string _backupKey;

    public static bool IsLocked() => HairController.IsHairLocked();

    /// <summary>
    /// Headwear without a special hairstyle: lift the game lock and restore the
    /// saved style (hairpins allowed).
    /// </summary>
    public static void OnPlainHeadwearApplied()
    {
        try
        {
            HairController.UnlockHair();

            var data = HairController.GetSavedHair();
            if (data == null) return;
            HairController.ApplyHairVisual(data.Kind, data.PinKind);
            Plugin.Log.LogInfo($"[Hair] Headwear unlocked: applied saved hair {data.Kind} {data.PinKind}.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Hair] Headwear unlock failed: {ex.Message}");
        }
    }

    /// <summary>Snapshot of every blend shape weight on the Normal hair meshes.</summary>
    public static float[] CaptureBlendWeights()
    {
        try
        {
            var source = GameServices.RoomComponent<HairSource>();
            if (source == null || source.Normal == null || source.Normal.Count == 0) return null;

            var weights = new List<float>();
            foreach (var go in source.Normal)
            {
                if (go == null) continue;
                var renderer = go.GetComponent<SkinnedMeshRenderer>();
                if (renderer == null || renderer.sharedMesh == null) continue;
                int count = renderer.sharedMesh.blendShapeCount;
                for (int i = 0; i < count; i++) weights.Add(renderer.GetBlendShapeWeight(i));
            }
            return weights.Count > 0 ? weights.ToArray() : null;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Hair] Blend shape capture failed: {ex.Message}");
            return null;
        }
    }

    public static bool AnyBlendWeightIncreased(float[] before, float[] after)
    {
        if (before == null || after == null) return false;
        int count = Math.Min(before.Length, after.Length);
        for (int i = 0; i < count; i++)
        {
            if (after[i] > before[i] + 0.01f) return true;
        }
        return false;
    }

    public static void RecordSpecialHair(string key, bool preserveExisting)
    {
        if (_backup.Valid && (preserveExisting || string.IsNullOrEmpty(key)))
        {
            _backupKey = key;
            Plugin.Log.LogInfo($"[Hair] Keeping recorded hairstyle {_backup.Kind} {_backup.Pin} for '{key}'.");
            return;
        }

        var data = HairController.GetSavedHair();
        if (data == null) return;
        _backup = new HairBackup { Valid = true, Kind = data.Kind, Pin = data.PinKind };
        _backupKey = key;
        Plugin.Log.LogInfo(
            $"[Hair] Recorded hairstyle before '{key}': {data.Kind} {data.PinKind} " +
            "(will be restored when the special hairstyle ends).");
    }

    public static void ReleaseSpecialHair(string key)
    {
        if (!_backup.Valid) return;
        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(_backupKey) && key != _backupKey) return;

        var backup = _backup;
        string releasedKey = _backupKey;
        _backup = default(HairBackup);
        _backupKey = null;

        try
        {
            // The game normally restores the saved style by itself; only rewrite the
            // save when the recorded style and the stored style differ (e.g. a daily
            // lottery ran while the special style was active).
            var current = HairController.GetSavedHair();
            if (current == null || current.Kind != backup.Kind || current.PinKind != backup.Pin)
            {
                HairController.PersistHair(backup.Kind, backup.Pin);
            }

            HairController.ApplyHairVisual(backup.Kind, backup.Pin);
            Plugin.Log.LogInfo($"[Hair] Restored recorded hairstyle after '{releasedKey}': {backup.Kind} {backup.Pin}.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Hair] Restore recorded hairstyle failed: {ex.Message}");
        }
    }
}
