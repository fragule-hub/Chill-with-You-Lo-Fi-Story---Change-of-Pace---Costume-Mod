using System;
using System.Collections.Generic;
using Bulbul;
using FastEnumUtility;
using UnityEngine;

namespace ChangeOfPaceCostume;

/// <summary>
/// Rules for decorations that need special handling (headwear / conflict groups).
/// Only decorations with non-default behavior are configured; everything else
/// falls back to the generic path or to keyword rules, so future decorations work
/// without a mod update.
/// </summary>
internal sealed class DecorRules
{
    public bool ReplaceHeadphones;            // headwear: replace cat-ear headphones (idempotent)
    public bool ReplaceGlassesWithGrasses2;   // April Fools: swap glasses decoration to Grasses_2
    public string[] MutexGroups = new string[0]; // entries sharing ANY group cannot be equipped together
}

/// <summary>Unified limited-decoration entry (hat enum value or discovered event).</summary>
internal sealed class DecorationEntry
{
    public string Key;          // stable public identifier (enum name)
    public bool IsHat;          // true -> HatChangeService, false -> LimitedTimeEventBase
    public HatChangeService.HatType HatType;
    public LimitedTimeEventBase Event;
    public DecorRules Rules;

    // Hat actually worn after this entry is applied. Hats know it up front;
    // events are observed at runtime (some holiday events call ChangeHat internally).
    public HatChangeService.HatType? DrivenHat;
}

/// <summary>
/// Unified access + rules for limited/story-exclusive decorations.
///
/// Anti-breakage (survives game updates):
///  - PUBLIC APIs only: LimitedTimeEventBase (EventType/Activate/Deactivate),
///    HatChangeService.CurrentHatType, FindObjectsOfType, RoomLifetimeScope.Resolve,
///    enums (HatType / EventType).
///  - Entries discovered dynamically: hats via HatType enum, events via scene scan.
///    New decorations show up automatically with zero code changes.
///  - Rules are auto-derived: every HatType is headwear (Head group + cat-ear swap);
///    unknown event keys are classified by name keywords, and any event that turns
///    out to drive a hat is promoted to headwear at runtime.
///  - Per-entry and per-operation try/catch: one broken decoration never blocks the rest.
/// </summary>
internal static class LimitedDecorationController
{
    private const string HeadGroup = "Head";
    private const string EventGroup = "Event";

    // Minimal special-case table. Everything not listed here is fully generic.
    // Groups: Head = same head slot; Event = the game only supports one limited-time
    // event at a time, so event decorations are mutually exclusive as well.
    private static readonly Dictionary<string, DecorRules> RulesTable = new Dictionary<string, DecorRules>
    {
        ["MagicalPrincess_Beret"] = new DecorRules { ReplaceHeadphones = true, MutexGroups = new[] { HeadGroup } },
        ["Christmas2025"] = new DecorRules { ReplaceHeadphones = true, MutexGroups = new[] { HeadGroup, EventGroup } },
        ["Halloween2026"] = new DecorRules { ReplaceHeadphones = true, MutexGroups = new[] { HeadGroup, EventGroup } },
        ["AprilFool2026"] = new DecorRules { ReplaceGlassesWithGrasses2 = true, MutexGroups = new[] { EventGroup } },
    };

    // Fallback classification for decorations added by future updates.
    private static readonly string[] GlassesKeywords =
    {
        "glass", "sunglass", "megane", "aprilfool", "eyewear", "goggles",
    };

    private static readonly string[] HeadwearKeywords =
    {
        "hat", "beret", "cap", "crown", "tiara", "halo", "horn", "ears",
        "santa", "christmas", "halloween",
    };

    private static List<DecorationEntry> _entries;
    private static HatChangeService _hatService;

    /// <summary>
    /// True while the game treats main episode 32 (the finale) specially:
    /// unlocked but not finished yet. Kept as a thin wrapper for callers that only
    /// know the decoration controller.
    /// </summary>
    public static bool IsFinaleHandlingPeriod() => GameState.IsFinalePending();

    /// <summary>Re-discovers hats/events (call when the room scene is rebuilt).</summary>
    public static void RefreshEntries()
    {
        _entries = null;
        _hatService = null;
    }

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
                        Rules = BuildRules(v.ToString(), isHat: true),
                    });
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Limited] Hat enumeration failed (limited decor section may be incomplete): {ex}");
            }

            try
            {
                // Limited-time events: discovered from the scene
                foreach (var evt in UnityEngine.Object.FindObjectsOfType<LimitedTimeEventBase>(true))
                {
                    if (evt == null) continue;
                    var key = EventKeyOf(evt);
                    if (key == null) continue;
                    _entries.Add(new DecorationEntry
                    {
                        Key = key,
                        IsHat = false,
                        Event = evt,
                        Rules = BuildRules(key, isHat: false),
                    });
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Limited] Event discovery failed (hats still available): {ex}");
            }

            Plugin.Log.LogInfo($"[Limited] Discovered {_entries.Count} limited decoration(s).");
        }
        return _entries;
    }

    public static bool IsEnabled(string key) => ModSaveData.GetDecorEnabled(key);

    /// <summary>
    /// Called when a main-story scene starts during the finale window: remove mod-applied
    /// headwear so the finale keeps the game's canonical look. Saved toggles are kept,
    /// so the decorations return once the finale has been read (next launch / ApplyAll).
    /// </summary>
    public static void SuspendHeadwearForFinale()
    {
        try
        {
            EnsureHatService();
            foreach (var e in GetAllEntries())
            {
                if (!e.IsHat || !IsEnabled(e.Key)) continue;
                _hatService.ChangeHat(HatChangeService.HatType.None);
                Plugin.Log.LogInfo($"[Limited] Finale: temporarily removed headwear '{e.Key}' (game default look).");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Limited] Finale headwear suspend failed: {ex.Message}");
        }
    }

    /// <summary>UI toggle. Enabling silently closes conflicting entries first.</summary>
    public static void Toggle(string key)
    {
        var entry = FindEntry(key);
        if (entry == null)
        {
            Plugin.Log.LogWarning($"[Limited] Toggle: unknown key '{key}'.");
            return;
        }

        bool on = !IsEnabled(key);
        if (on)
        {
            CloseConflicts(entry);

            // Hat entries know their hat up front; also close an event that
            // already drives the same hat (learned from a previous session).
            var knownHat = EffectiveHat(entry);
            if (knownHat.HasValue) ClearSameHatFlags(entry, knownHat.Value);
        }

        ModSaveData.SetDecorEnabled(key, on);
        ApplyEntry(entry, on);

        if (on)
        {
            // The entry tells us its hat after application (ApplyEntry observes it);
            // close same-hat leftovers only when the entry actually drives that hat.
            var hat = EffectiveHat(entry);
            if (hat.HasValue) ClearSameHatFlags(entry, hat.Value);
        }

        HairController.SyncVisualToHeadwear();
    }

    /// <summary>
    /// Apply enabled toggles at system reset points.
    /// IMPORTANT: entries that are disabled are NOT touched here - at these points the
    /// game has already cleared its own decorations (StoryTidying/Setup), and calling
    /// Deactivate ourselves could also tear down decorations the GAME itself activated
    /// (e.g. the live Christmas event). Only user-initiated Toggle() performs Deactivate.
    /// Mutex groups apply only the first enabled entry; the same hat is applied once.
    /// </summary>
    public static void ApplyAll()
    {
        // Main episode 32 is the game's finale: all limited-time events are suppressed,
        // hair is forced to Normal and the game uses its own ending presentation.
        // Do not fight it - stay silent until the finale has been read.
        if (IsFinaleHandlingPeriod())
        {
            Plugin.Log.LogInfo("[Limited] Finale handling period (main ep32): skipping auto re-apply (game default).");
            return;
        }

        var enabledGroups = new HashSet<string>();
        var appliedHats = new HashSet<HatChangeService.HatType>();

        foreach (var e in GetAllEntries())
        {
            if (!IsEnabled(e.Key)) continue;

            if (HasAppliedGroup(e.Rules, enabledGroups))
            {
                Plugin.Log.LogInfo($"[Limited] '{e.Key}' skipped (mutex group [{GroupsOf(e.Rules)}] already applied).");
                ModSaveData.SetDecorEnabled(e.Key, false);
                continue;
            }

            var knownHat = EffectiveHat(e);
            if (knownHat.HasValue && appliedHats.Contains(knownHat.Value))
            {
                Plugin.Log.LogInfo($"[Limited] '{e.Key}' skipped (hat '{knownHat.Value}' already applied).");
                ModSaveData.SetDecorEnabled(e.Key, false);
                continue;
            }

            ApplyEntry(e, true);

            var hat = EffectiveHat(e);
            if (hat.HasValue) appliedHats.Add(hat.Value);
            foreach (var group in e.Rules.MutexGroups) enabledGroups.Add(group);
        }

        HairController.SyncVisualToHeadwear();
    }

    private static DecorationEntry FindEntry(string key)
    {
        foreach (var e in GetAllEntries())
        {
            if (e.Key == key) return e;
        }
        return null;
    }

    /// <summary>Hat this entry currently wears, if known (public enum only, no reflection).</summary>
    private static HatChangeService.HatType? EffectiveHat(DecorationEntry e)
    {
        if (e == null) return null;
        return e.IsHat ? e.HatType : e.DrivenHat;
    }

    /// <summary>
    /// Close every enabled entry that conflicts with <paramref name="entry"/>:
    /// same mutex group or same hat. Closing applies the entry's own "off" path,
    /// so extra visuals of an event (e.g. a prop hat) are hidden as well.
    /// </summary>
    private static void CloseConflicts(DecorationEntry entry)
    {
        var hat = EffectiveHat(entry);

        foreach (var other in GetAllEntries())
        {
            if (other == entry || !IsEnabled(other.Key)) continue;

            bool groupConflict = SharesGroup(entry.Rules, other.Rules);
            bool hatConflict = hat.HasValue && EffectiveHat(other) == hat.Value;
            if (!groupConflict && !hatConflict) continue;

            ModSaveData.SetDecorEnabled(other.Key, false);
            ApplyEntry(other, false);
            Plugin.Log.LogInfo(
                $"[Limited] '{other.Key}' closed " +
                $"({(groupConflict ? "mutex group [" + GroupsOf(other.Rules) + "]" : "same hat '" + hat + "'")}) " +
                $"because '{entry.Key}' was enabled.");
        }
    }

    /// <summary>
    /// Clear the saved "on" flag of entries that drive the same hat WITHOUT calling
    /// their off path: the hat is still worn by the entry we just enabled.
    /// </summary>
    private static void ClearSameHatFlags(DecorationEntry entry, HatChangeService.HatType hat)
    {
        foreach (var other in GetAllEntries())
        {
            if (other == entry || !IsEnabled(other.Key)) continue;
            if (EffectiveHat(other) != hat) continue;

            ModSaveData.SetDecorEnabled(other.Key, false);

            // The shared hat stays (the new entry wears it), but the cleared entry's
            // independent swaps (glasses, headphones) must be undone when nothing
            // else needs them.
            if (other.Rules.ReplaceGlassesWithGrasses2) DecorationSideEffects.RestoreGlassesIfIdle();
            if (other.Rules.ReplaceHeadphones) DecorationSideEffects.RestoreHeadphonesIfIdle();

            Plugin.Log.LogInfo($"[Limited] '{other.Key}' auto-disabled (same hat '{hat}' as '{entry.Key}').");
        }
    }

    private static void ApplyEntry(DecorationEntry e, bool on)
    {
        float[] hairBefore = on ? HairLockPolicy.CaptureBlendWeights() : null;
        var previousHat = GetCurrentHatType();

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
                // Attribute a hat ONLY when this entry actually changed it. Otherwise an
                // event such as April Fools would "steal" a hat worn by another entry.
                ObserveHatDrivenEntry(e, previousHat);

                if (e.Rules.ReplaceHeadphones)
                {
                    DecorationSideEffects.ReplaceHeadphones();
                    ApplyHairPolicy(e, hairBefore, previousHat);
                }
                if (e.Rules.ReplaceGlassesWithGrasses2) DecorationSideEffects.ReplaceGlassesWithGrasses2();
            }
            else
            {
                if (e.Rules.ReplaceHeadphones)
                {
                    // Put back the hairstyle recorded when a special style was applied.
                    HairLockPolicy.ReleaseSpecialHair(e.Key);
                    DecorationSideEffects.RestoreHeadphonesIfIdle();
                }
                if (e.Rules.ReplaceGlassesWithGrasses2) DecorationSideEffects.RestoreGlassesIfIdle();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[Limited] Apply '{e.Key}' on={on} failed (other decorations unaffected): {ex}");
        }
    }

    /// <summary>
    /// Decides how a mod-equipped headwear interacts with hairstyles.
    ///  - The game locked hair and the hat applied its own hair shape (e.g. the beret)
    ///    => follow the game default: keep the lock and the special style, only record
    ///    the player's style for later restoration.
    ///  - The game locked hair without a special style (e.g. the Halloween hat)
    ///    => unlock and re-apply the saved hairstyle (hairpins allowed).
    /// Unknown future hats are classified by whether their application increases a
    /// hair blend shape weight, so no mod update is needed for the common case.
    /// </summary>
    private static void ApplyHairPolicy(DecorationEntry e, float[] before, HatChangeService.HatType previousHat)
    {
        if (!HairLockPolicy.IsLocked()) return;

        var after = HairLockPolicy.CaptureBlendWeights();
        bool knownSpecial = e.IsHat && e.HatType == HatChangeService.HatType.MagicalPrincess_Beret;
        bool shapeIncreased = HairLockPolicy.AnyBlendWeightIncreased(before, after);
        bool special = knownSpecial || shapeIncreased;

        Plugin.Log.LogInfo(
            $"[Hair] Headwear '{e.Key}' policy: special={special} " +
            $"(knownSpecial={knownSpecial}, shapeIncreased={shapeIncreased}, " +
            $"weightsBefore=[{FormatWeights(before)}], weightsAfter=[{FormatWeights(after)}]).");

        if (special)
        {
            bool wasOn = e.IsHat && previousHat == e.HatType;
            HairLockPolicy.RecordSpecialHair(e.Key, preserveExisting: wasOn);
        }
        else
        {
            HairLockPolicy.OnPlainHeadwearApplied();
        }
    }

    private static HatChangeService.HatType GetCurrentHatType()
    {
        try
        {
            EnsureHatService();
            return _hatService?.CurrentHatType?.CurrentValue ?? HatChangeService.HatType.None;
        }
        catch
        {
            return HatChangeService.HatType.None;
        }
    }

    private static string FormatWeights(float[] weights)
    {
        if (weights == null) return "null";
        var parts = new string[weights.Length];
        for (int i = 0; i < weights.Length; i++) parts[i] = weights[i].ToString("0.#");
        return string.Join(",", parts);
    }

    /// <summary>
    /// Read the game's public hat state after an event was switched on. If the event
    /// drives a hat (future holiday events call ChangeHat internally), promote it to
    /// headwear rules at runtime so mutex and headphone handling keep working without
    /// a mod update.
    /// </summary>
    private static void ObserveHatDrivenEntry(DecorationEntry e, HatChangeService.HatType previousHat)
    {
        if (e == null || e.IsHat) return;
        try
        {
            EnsureHatService();
            if (_hatService == null || _hatService.CurrentHatType == null) return;

            var hat = _hatService.CurrentHatType.CurrentValue;
            if (hat == HatChangeService.HatType.None) return;
            if (hat == previousHat) return; // this entry did not change the hat

            e.DrivenHat = hat;

            bool changed = false;
            if (!e.Rules.ReplaceHeadphones)
            {
                e.Rules.ReplaceHeadphones = true;
                changed = true;
            }
            if (!HasGroup(e.Rules, HeadGroup))
            {
                AddGroup(e.Rules, HeadGroup);
                changed = true;
            }

            if (changed)
            {
                Plugin.Log.LogInfo(
                    $"[Limited] '{e.Key}' drives hat '{hat}'; promoted to headwear rules " +
                    "(Head group + cat-ear swap).");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Limited] Hat observation for '{e.Key}' failed: {ex.Message}");
        }
    }

    private static DecorRules BuildRules(string key, bool isHat)
    {
        // Every HatType is headwear. Every LimitedTimeEventBase is an event, and the
        // game only supports one active limited-time event at a time, so events are
        // mutually exclusive too (a single hat like HalloweenHat is NOT an event).
        var rules = new DecorRules
        {
            MutexGroups = isHat ? new[] { HeadGroup } : new[] { EventGroup },
        };

        if (isHat)
        {
            rules.ReplaceHeadphones = true;
            return rules;
        }

        if (RulesTable.TryGetValue(key, out var known))
        {
            rules.ReplaceHeadphones = known.ReplaceHeadphones;
            rules.ReplaceGlassesWithGrasses2 = known.ReplaceGlassesWithGrasses2;
            foreach (var group in known.MutexGroups) AddGroup(rules, group);
            return rules;
        }

        // Unknown event from a future update: classify by its public enum name.
        if (LooksLike(key, GlassesKeywords))
        {
            rules.ReplaceGlassesWithGrasses2 = true;
        }
        else if (LooksLike(key, HeadwearKeywords))
        {
            rules.ReplaceHeadphones = true;
            AddGroup(rules, HeadGroup);
        }

        if (rules.ReplaceHeadphones || rules.ReplaceGlassesWithGrasses2)
        {
            Plugin.Log.LogInfo($"[Limited] Auto rules for '{key}': " +
                $"headwear={rules.ReplaceHeadphones}, glasses={rules.ReplaceGlassesWithGrasses2}, " +
                $"groups=[{GroupsOf(rules)}].");
        }
        return rules;
    }

    private static bool HasGroup(DecorRules rules, string group)
    {
        if (rules == null || rules.MutexGroups == null || string.IsNullOrEmpty(group)) return false;
        foreach (var g in rules.MutexGroups)
        {
            if (g == group) return true;
        }
        return false;
    }

    private static void AddGroup(DecorRules rules, string group)
    {
        if (rules == null || string.IsNullOrEmpty(group) || HasGroup(rules, group)) return;
        var groups = new List<string>(rules.MutexGroups ?? new string[0]) { group };
        rules.MutexGroups = groups.ToArray();
    }

    private static bool SharesGroup(DecorRules a, DecorRules b)
    {
        if (a == null || b == null || a.MutexGroups == null || b.MutexGroups == null) return false;
        foreach (var ga in a.MutexGroups)
        {
            foreach (var gb in b.MutexGroups)
            {
                if (ga == gb) return true;
            }
        }
        return false;
    }

    private static bool HasAppliedGroup(DecorRules rules, HashSet<string> applied)
    {
        if (rules == null || rules.MutexGroups == null) return false;
        foreach (var g in rules.MutexGroups)
        {
            if (applied.Contains(g)) return true;
        }
        return false;
    }

    private static string GroupsOf(DecorRules rules)
    {
        return rules == null || rules.MutexGroups == null ? "" : string.Join("+", rules.MutexGroups);
    }

    private static bool LooksLike(string key, string[] keywords)
    {
        if (string.IsNullOrEmpty(key)) return false;
        foreach (var keyword in keywords)
        {
            if (key.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
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
            _hatService = GameServices.RoomComponent<HatChangeService>();
        }
    }
}
