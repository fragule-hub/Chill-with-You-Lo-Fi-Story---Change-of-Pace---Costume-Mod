using System;
using Bulbul;
using UnityEngine;

namespace ChangeOfPaceCostume;

internal static class ModSaveData
{
    private const string PermSkinKey = "ChangeOfPaceCostume_PermanentSkin";

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
}
