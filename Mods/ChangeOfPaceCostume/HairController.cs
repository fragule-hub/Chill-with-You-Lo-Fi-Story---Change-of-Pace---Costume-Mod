using System;
using System.Collections.Generic;
using System.Reflection;
using Bulbul;
using FastEnumUtility;
using HarmonyLib;
using UnityEngine;

namespace ChangeOfPaceCostume;

/// <summary>
/// One selectable hairstyle. Discovered from HairStyle / HairPinKind so game
/// updates that add enum values show up as extra buttons automatically.
/// </summary>
internal sealed class HairEntry
{
    public string Key;
    public HairStyle Style;
    public HairPinKind PinKind;
    public string FallbackLabel;
    public string LocalizeKey;
}

/// <summary>
/// Hair switching for the Change of Pace panel.
/// Persistence is always "today" (HairLotteryData, 6:00 day boundary).
/// Official APIs first; unknown future HairStyle values fall back to a
/// HairSource public list whose name matches the enum.
///
/// Headwear-related policy (unlock / special style bookkeeping) lives in
/// <see cref="HairLockPolicy"/> so this class only deals with hair itself.
/// </summary>
internal static class HairController
{
    private static List<HairEntry> _entries;
    private static ChangeHairService _service;
    private static Traverse _lockedTraverse;

    public static List<HairEntry> GetAllEntries()
    {
        if (_entries != null) return _entries;

        _entries = new List<HairEntry>();
        try
        {
            foreach (var style in FastEnum.GetValues<HairStyle>())
            {
                if (style == HairStyle.HairPin)
                {
                    foreach (var pin in FastEnum.GetValues<HairPinKind>())
                        _entries.Add(MakeEntry(style, pin));
                }
                else
                {
                    _entries.Add(MakeEntry(style, default));
                }
            }
            Plugin.Log.LogInfo($"[Hair] Discovered {_entries.Count} hairstyle(s).");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[Hair] Enumerate failed: {ex}");
        }
        return _entries;
    }

    /// <summary>
    /// UI click. Follows the game default: while a headwear (or the finale) locks
    /// hair, the click is ignored instead of overwriting the saved hairstyle.
    /// </summary>
    public static void Select(string key)
    {
        var entry = FindEntry(key);
        if (entry == null)
        {
            Plugin.Log.LogWarning($"[Hair] Select: unknown key '{key}'.");
            return;
        }

        if (IsSuppressedByHeadwear())
        {
            Plugin.Log.LogInfo($"[Hair] Ignored '{key}' (hairstyle is locked right now).");
            return;
        }

        SaveToday(entry);
        ApplyVisual(entry);
        Plugin.Log.LogInfo($"[Hair] Applied '{key}' (today).");
    }

    /// <summary>
    /// After hats/events change: leave Normal while a locking hat is on
    /// (ChangeHat already did that), otherwise restore today's saved hair.
    /// Does not write save data. Christmas does not lock hair.
    /// </summary>
    public static void SyncVisualToHeadwear()
    {
        try
        {
            if (IsSuppressedByHeadwear()) return;

            var data = GetSavedData();
            if (data == null) return;
            ApplyHairVisual(data.Kind, data.PinKind);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[Hair] SyncVisual failed: {ex}");
        }
    }

    public static string GetWantedKey()
    {
        var data = GetSavedData();
        if (data == null) return KeyOf(HairStyle.Normal, default);
        return KeyOf(data.Kind, data.PinKind);
    }

    public static string GetShownKey()
    {
        if (IsSuppressedByHeadwear()) return KeyOf(HairStyle.Normal, default);
        return GetWantedKey();
    }

    public static bool IsSuppressedByHeadwear() => IsHairLocked();

    /// <summary>Drops cached services (scene rebuild).</summary>
    internal static void Invalidate()
    {
        _service = null;
        _lockedTraverse = null;
    }

    /// <summary>
    /// The game's actual lock flag first: a mod-equipped plain hat is unlocked on
    /// purpose, while a story-driven / special hat keeps the vanilla lock.
    /// CurrentHatType is only a conservative fallback if the field disappears.
    /// </summary>
    internal static bool IsHairLocked()
    {
        try
        {
            EnsureService();
            if (_lockedTraverse != null) return _lockedTraverse.GetValue<bool>();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Hair] Lock flag unavailable, using CurrentHatType: {ex.Message}");
        }

        try
        {
            var hat = GameServices.RoomComponent<HatChangeService>();
            if (hat != null && hat.CurrentHatType != null)
            {
                return hat.CurrentHatType.CurrentValue != HatChangeService.HatType.None;
            }
        }
        catch
        {
            // ignored
        }
        return false;
    }

    // ── small internal surface used by HairLockPolicy ──

    internal static HairChangeData GetSavedHair() => GetSavedData();

    internal static void PersistHair(HairStyle kind, HairPinKind pin)
    {
        var entry = FindEntry(KeyOf(kind, pin));
        if (entry != null) SaveToday(entry);
    }

    internal static void ApplyHairVisual(HairStyle kind, HairPinKind pin)
    {
        EnsureService();
        _service.ChangeHairWithoutSave(new HairChangeData
        {
            Kind = kind,
            PinKind = pin,
            Changed = DateTime.Now,
        });
    }

    internal static void UnlockHair()
    {
        EnsureService();
        _service.SetHairChangeLocked(false);
    }

    /// <summary>
    /// Same 6:00 boundary as ChangeHairService.ShouldHairChangeDate.
    /// True → vanilla should lottery (saved hair is from a previous day).
    /// </summary>
    public static bool ShouldLotteryNewHair(DateTime latestChangeDate, DateTime now)
    {
        int hour = MyDefine.HairChangeTime;
        DateTime Day(DateTime dt) => dt.Hour < hour ? dt.Date.AddDays(-1.0) : dt.Date;
        return Day(latestChangeDate) < Day(now);
    }

    /// <summary>True while the finale window makes the mod defer to the game.</summary>
    public static bool IsFinalePending() => GameState.IsFinalePending();

    private static HairEntry MakeEntry(HairStyle style, HairPinKind pin)
    {
        string key = KeyOf(style, pin);
        return new HairEntry
        {
            Key = key,
            Style = style,
            PinKind = pin,
            FallbackLabel = style == HairStyle.HairPin ? pin.ToString() : style.ToString(),
            LocalizeKey = "hair_" + key.Replace(':', '_'),
        };
    }

    private static string KeyOf(HairStyle style, HairPinKind pin)
    {
        return style == HairStyle.HairPin ? style + ":" + pin : style.ToString();
    }

    private static HairEntry FindEntry(string key)
    {
        foreach (var e in GetAllEntries())
        {
            if (e.Key == key) return e;
        }
        return null;
    }

    private static HairChangeData GetSavedData()
    {
        try
        {
            return SaveDataManager.Instance.HairLotteryData?.LatestChangeData;
        }
        catch
        {
            return null;
        }
    }

    private static HairChangeData ToChangeData(HairEntry entry)
    {
        return new HairChangeData
        {
            Kind = entry.Style,
            PinKind = entry.PinKind,
            Changed = DateTime.Now,
        };
    }

    private static void SaveToday(HairEntry entry)
    {
        SaveDataManager.Instance.HairLotteryData.LatestChangeData = ToChangeData(entry);
        SaveDataManager.Instance.SaveHairLotteryData();
    }

    private static void ApplyVisual(HairEntry entry)
    {
        try
        {
            ApplyHairVisual(entry.Style, entry.PinKind);
            return;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Hair] ChangeHairWithoutSave '{entry.Key}' failed: {ex.Message}");
        }

        if (!TryApplyViaHairSource(entry))
            Plugin.Log.LogError($"[Hair] No apply path for '{entry.Key}'.");
    }

    /// <summary>
    /// Last resort for a HairStyle the official switch does not know:
    /// deactivate Normal/Pin lists, activate a HairSource public GameObject
    /// list whose property name matches the enum (e.g. TwinTail).
    /// </summary>
    private static bool TryApplyViaHairSource(HairEntry entry)
    {
        var source = GameServices.RoomComponent<HairSource>();
        if (source == null) return false;

        try
        {
            // Hairpin variants are data-driven: activate the matching pin GameObject
            // even when a future HairPinKind is unknown to the service switch.
            if (entry.Style == HairStyle.HairPin && source.HairPins != null)
            {
                bool matchedPin = false;
                SetActiveList(source.Normal, false);
                SetActiveList(source.HairPinStyles, true);
                foreach (var kv in source.HairPins)
                {
                    if (kv.Value == null) continue;
                    bool isMatch = kv.Key == entry.PinKind;
                    kv.Value.SetActive(isMatch);
                    matchedPin |= isMatch;
                }
                if (matchedPin) return true;
            }

            SetActiveList(source.Normal, false);
            SetActiveList(source.HairPinStyles, false);
            if (source.HairPins != null)
            {
                foreach (var kv in source.HairPins)
                {
                    if (kv.Value != null) kv.Value.SetActive(false);
                }
            }

            bool matched = false;
            string styleName = entry.Style.ToString();
            foreach (var prop in typeof(HairSource).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.Name == nameof(HairSource.Normal)
                    || prop.Name == nameof(HairSource.HairPinStyles)
                    || prop.Name == nameof(HairSource.HairPins)
                    || prop.PropertyType == typeof(string)
                    || !typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType))
                {
                    continue;
                }

                bool match = string.Equals(prop.Name, styleName, StringComparison.OrdinalIgnoreCase);
                if (match) matched = true;
                if (prop.GetValue(source) is IEnumerable<GameObject> gos)
                    SetActiveList(gos, match);
            }
            return matched;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[Hair] HairSource apply '{entry.Key}' failed: {ex}");
            return false;
        }
    }

    private static void SetActiveList(IEnumerable<GameObject> list, bool on)
    {
        if (list == null) return;
        foreach (var go in list)
        {
            if (go != null) go.SetActive(on);
        }
    }

    private static void EnsureService()
    {
        if (_service != null) return;
        _service = GameServices.Room<ChangeHairService>();
        if (_service == null)
            throw new InvalidOperationException("ChangeHairService is not available.");
        _lockedTraverse = Traverse.Create(_service).Field("isHairChangeLocked");
    }
}
