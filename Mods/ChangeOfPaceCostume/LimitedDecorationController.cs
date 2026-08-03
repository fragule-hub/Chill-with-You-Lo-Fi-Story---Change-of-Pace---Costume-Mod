using System;
using System.Collections.Generic;
using Bulbul;
using FastEnumUtility;
using NestopiSystem.DIContainers;
using UnityEngine;

namespace ChangeOfPaceCostume;

/// <summary>
/// Rules for decorations that need special handling (headwear / conflict groups).
/// Only decorations with non-default behavior are configured; everything else
/// falls back to the generic path with zero configuration.
/// </summary>
internal sealed class DecorRules
{
    public bool ReplaceHeadphones;            // headwear: replace cat-ear headphones (idempotent)
    public bool ReplaceGlassesWithGrasses2;   // April Fools: swap glasses decoration to Grasses_2
    public string MutexGroup;                 // decorations in the same group are mutually exclusive
}

/// <summary>Unified limited-decoration entry (hat enum value or discovered event).</summary>
internal sealed class DecorationEntry
{
    public string Key;          // stable public identifier (enum name)
    public bool IsHat;          // true -> HatChangeService, false -> LimitedTimeEventBase
    public HatChangeService.HatType HatType;
    public LimitedTimeEventBase Event;
    public DecorRules Rules;
}

/// <summary>
/// Unified access + rules for limited/story-exclusive decorations.
///
/// Anti-breakage (survives game updates):
///  - PUBLIC APIs only: LimitedTimeEventBase (EventType/Activate/Deactivate),
///    FindObjectsOfType, RoomLifetimeScope.Resolve, enums (HatType / EventType).
///  - No reflection on private fields.
///  - Entries discovered dynamically: hats via HatType enum, events via scene scan.
///    New decorations show up automatically with zero code changes.
///  - Rules table is keyed by the stable enum name; unknown keys get empty rules.
/// </summary>
internal static class LimitedDecorationController
{
    // Minimal special-case table. Everything not listed here is fully generic.
    private static readonly Dictionary<string, DecorRules> RulesTable = new Dictionary<string, DecorRules>
    {
        ["MagicalPrincess_Beret"] = new DecorRules { ReplaceHeadphones = true, MutexGroup = "Head" },
        ["Christmas2025"] = new DecorRules { ReplaceHeadphones = true, MutexGroup = "Head" },
        ["AprilFool2026"] = new DecorRules { ReplaceGlassesWithGrasses2 = true },
    };

    private static List<DecorationEntry> _entries;
    private static HatChangeService _hatService;
    private static DecorationService _decorationService;

    public static List<DecorationEntry> GetAllEntries()
    {
        // Scene-rebuild guard: if the cached event components were destroyed
        // (Unity fake-null), rebuild the list instead of using stale references.
        if (_entries != null && _entries.Count > 0)
        {
            bool stale = false;
            foreach (var e in _entries)
            {
                if (!e.IsHat && e.Event == null) { stale = true; break; }
            }
            if (stale) _entries = null;
        }

        if (_entries == null)
        {
            _entries = new List<DecorationEntry>();
            try
            {
                // Hats: enumerate HatType (skip None)
                foreach (var v in FastEnum.GetValues<HatChangeService.HatType>())
                {
                    if (v == HatChangeService.HatType.None) continue;
                    _entries.Add(new DecorationEntry
                    {
                        Key = v.ToString(),
                        IsHat = true,
                        HatType = v,
                        Rules = GetRules(v.ToString()),
                    });
                }
                // Limited-time events: discovered from the scene
                foreach (var evt in UnityEngine.Object.FindObjectsOfType<LimitedTimeEventBase>())
                {
                    if (evt == null) continue;
                    var key = EventKeyOf(evt);
                    if (key == null) continue;
                    _entries.Add(new DecorationEntry
                    {
                        Key = key,
                        IsHat = false,
                        Event = evt,
                        Rules = GetRules(key),
                    });
                }
                Plugin.Log.LogInfo($"[Limited] Discovered {_entries.Count} limited decoration(s).");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Limited] Enumerate failed: {ex}");
            }
        }
        return _entries;
    }

    public static bool IsEnabled(string key) => ModSaveData.GetDecorEnabled(key);

    /// <summary>UI toggle. Enabling silently closes other entries in the same mutex group first.</summary>
    public static void Toggle(string key)
    {
        var entry = FindEntry(key);
        if (entry == null)
        {
            Plugin.Log.LogWarning($"[Limited] Toggle: unknown key '{key}'.");
            return;
        }

        bool on = !IsEnabled(key);
        if (on && !string.IsNullOrEmpty(entry.Rules.MutexGroup))
        {
            foreach (var e in GetAllEntries())
            {
                if (e.Key == key) continue;
                if (e.Rules.MutexGroup != entry.Rules.MutexGroup) continue;
                if (IsEnabled(e.Key))
                {
                    ModSaveData.SetDecorEnabled(e.Key, false);
                    ApplyEntry(e, false);
                }
            }
        }

        ModSaveData.SetDecorEnabled(key, on);
        ApplyEntry(entry, on);
    }

    /// <summary>
    /// Apply enabled toggles at system reset points.
    /// IMPORTANT: entries that are disabled are NOT touched here — at these points the
    /// game has already cleared its own decorations (StoryTidying/Setup), and calling
    /// Deactivate ourselves could also tear down decorations the GAME itself activated
    /// (e.g. the live Christmas event). Only user-initiated Toggle() performs Deactivate.
    /// Mutex groups apply only the first enabled entry.
    /// </summary>
    public static void ApplyAll()
    {
        var enabledGroups = new HashSet<string>();
        foreach (var e in GetAllEntries())
        {
            if (!IsEnabled(e.Key)) continue;

            var g = e.Rules.MutexGroup;
            if (!string.IsNullOrEmpty(g) && !enabledGroups.Add(g))
            {
                continue; // another entry in this mutex group already applied
            }
            ApplyEntry(e, true);
        }
    }

    private static DecorationEntry FindEntry(string key)
    {
        foreach (var e in GetAllEntries())
        {
            if (e.Key == key) return e;
        }
        return null;
    }

    private static void ApplyEntry(DecorationEntry e, bool on)
    {
        try
        {
            if (e.IsHat)
            {
                EnsureHatService();
                _hatService.ChangeHat(on ? e.HatType : HatChangeService.HatType.None);
            }
            else if (e.Event != null)
            {
                if (on) e.Event.Activate();
                else e.Event.Deactivate();
            }

            if (on)
            {
                if (e.Rules.ReplaceHeadphones) ReplaceHeadphones();
                if (e.Rules.ReplaceGlassesWithGrasses2) ReplaceGlassesToGrasses2();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[Limited] Apply '{e.Key}' on={on} failed: {ex}");
        }
    }

    private static DecorRules GetRules(string key)
    {
        return RulesTable.TryGetValue(key, out var r) ? r : new DecorRules();
    }

    private static string EventKeyOf(LimitedTimeEventBase evt)
    {
        try
        {
            return evt.EventType().ToString();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Limited] EventType failed: {ex.Message}");
            return evt.GetType().Name;
        }
    }

    private static void EnsureHatService()
    {
        if (_hatService == null)
        {
            _hatService = RoomLifetimeScope.Resolve<HatChangeService>();
        }
    }

    private static void ReplaceHeadphones()
    {
        if (_decorationService == null)
        {
            _decorationService = RoomLifetimeScope.Resolve<DecorationService>();
        }
        _decorationService.ReplaceCatEarHeadphoneWithDefaultTemporarily();
    }

    private static void ReplaceGlassesToGrasses2()
    {
        if (_decorationService == null)
        {
            _decorationService = RoomLifetimeScope.Resolve<DecorationService>();
        }
        _decorationService.ChangeDecoration(DecorationService.DecorationSkinType.Grasses_2, isSave: false);
    }
}
