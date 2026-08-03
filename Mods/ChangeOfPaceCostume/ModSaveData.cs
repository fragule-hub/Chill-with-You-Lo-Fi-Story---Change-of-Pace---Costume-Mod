using System;
using Bulbul;
using UnityEngine;

namespace ChangeOfPaceCostume;

internal static class ModSaveData
{
    private const string PermSkinKey = "ChangeOfPaceCostume_PermanentSkin";
    private const string DecorKeyPrefix = "ChangeOfPaceCostume_Decor_";

    public static CostumeChangeService.CostumeSkinType? GetPermanentSkin()
    {
        string val = PlayerPrefs.GetString(PermSkinKey, "");
        if (string.IsNullOrEmpty(val))
            return null;
        if (Enum.TryParse<CostumeChangeService.CostumeSkinType>(val, out var result))
            return result;
        return null;
    }

    public static void SetPermanentSkin(CostumeChangeService.CostumeSkinType skin)
    {
        PlayerPrefs.SetString(PermSkinKey, skin.ToString());
        PlayerPrefs.Save();
    }

    public static void ClearPermanentSkin()
    {
        PlayerPrefs.DeleteKey(PermSkinKey);
        PlayerPrefs.Save();
    }

    // ── Unified limited-decoration toggle ──
    // key = stable public identifier (HatType / LimitedTimeEventType enum name).
    // New decorations added by game updates get their own key automatically.

    public static bool GetDecorEnabled(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return PlayerPrefs.GetInt(DecorKeyPrefix + key, 0) == 1;
    }

    public static void SetDecorEnabled(string key, bool enabled)
    {
        if (string.IsNullOrEmpty(key)) return;
        PlayerPrefs.SetInt(DecorKeyPrefix + key, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}
