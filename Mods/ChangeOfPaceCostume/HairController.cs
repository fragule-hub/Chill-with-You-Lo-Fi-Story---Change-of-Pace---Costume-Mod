using System;
using System.Collections.Generic;
using System.Reflection;
using Bulbul;
using FastEnumUtility;
using HarmonyLib;
using NestopiSystem.DIContainers;
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

    /// <summary>UI click: always save as today; skip visual while a hair-locking hat is on.</summary>
    public static void Select(string key)
    {
        var entry = FindEntry(key);
        if (entry == null)
        {
            Plugin.Log.LogWarning($"[Hair] Select: unknown key '{key}'.");
            return;
        }

        SaveToday(entry);

        if (IsSuppressedByHeadwear())
        {
            Plugin.Log.LogInfo($"[Hair] Saved '{key}' for today; visual deferred (hat lock).");
            return;
        }

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
            EnsureService();
            _service.ChangeHairWithoutSave(data);
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

    public static bool IsSuppressedByHeadwear()
    {
        return LimitedDecorationController.HasHairLockingHatEnabled() || IsHairChangeLocked();
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

    public static bool IsEpisode32()
    {
        try
        {
            return SaveDataManager.Instance.ScenarioProgressData.NextEpisodeNumber == 32f;
        }
        catch
        {
            return false;
        }
    }

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
        EnsureService();
        try
        {
            _service.ChangeHairWithoutSave(ToChangeData(entry));
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
        HairSource source;
        try
        {
            source = RoomLifetimeScope.Resolve<HairSource>();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Hair] Resolve HairSource failed: {ex.Message}");
            return false;
        }
        if (source == null) return false;

        try
        {
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
        _service = RoomLifetimeScope.Resolve<ChangeHairService>();
        _lockedTraverse = Traverse.Create(_service).Field("isHairChangeLocked");
    }

    private static bool IsHairChangeLocked()
    {
        try
        {
            EnsureService();
            return _lockedTraverse != null && _lockedTraverse.GetValue<bool>();
        }
        catch
        {
            return false;
        }
    }
}
