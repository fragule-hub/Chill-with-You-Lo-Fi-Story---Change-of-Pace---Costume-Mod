using System;
using System.Collections.Generic;
using System.Linq;
using Bulbul;
using Cysharp.Threading.Tasks;
using FastEnumUtility;
using HarmonyLib;
using NestopiSystem.DIContainers;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace ChangeOfPaceCostume;

internal static class CostumeButtonsInjector
{
    // ── v1.0 cached state ──
    private static bool _injected;
    private static CostumeChangeService _costumeService;
    private static CostumeChangeService.CostumeSkinType[] _allSkins;
    private static Dictionary<CostumeChangeService.CostumeSkinType, Image> _buttonImages;
    private static GameObject _costumeSection;
    private static ScrollRect _cachedScrollRect;
    private static Font _arialFont;
    private static Traverse _skinTypeTraverse;

    // ── v2.0 request options ──
    private const int OptionOnce = 0;
    private const int OptionToday = 1;
    private const int OptionForever = 2;
    private static Dictionary<int, Image> _requestOptionImages;
    private static int _selectedRequestOption = -1; // -1 = none

    // ── colors ──
    private static readonly Color ActiveColor = new Color(0.35f, 0.65f, 1f, 1f);
    private static readonly Color NormalColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    private static readonly Color OptionSelectedColor = new Color(1f, 0.75f, 0.4f, 1f);
    private static readonly Color OptionNormalColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    // ── localization ──
    private static readonly Dictionary<string, Dictionary<GameLanguageType, string>> LocalizedTexts = new Dictionary<string, Dictionary<GameLanguageType, string>>
    {
        ["costume_request_title"] = new Dictionary<GameLanguageType, string>
        {
            [GameLanguageType.Japanese] = "衣装リクエスト",
            [GameLanguageType.English] = "Costume Request",
            [GameLanguageType.ChineseSimplified] = "换装请求",
        },
        ["option_once"] = new Dictionary<GameLanguageType, string>
        {
            [GameLanguageType.Japanese] = "今回だけね~",
            [GameLanguageType.English] = "Just this once~",
            [GameLanguageType.ChineseSimplified] = "只这一次哦~",
        },
        ["option_today"] = new Dictionary<GameLanguageType, string>
        {
            [GameLanguageType.Japanese] = "今日だけね~",
            [GameLanguageType.English] = "Just today~",
            [GameLanguageType.ChineseSimplified] = "只有今天哦~",
        },
        ["option_forever"] = new Dictionary<GameLanguageType, string>
        {
            [GameLanguageType.Japanese] = "一生？！",
            [GameLanguageType.English] = "Forever?!",
            [GameLanguageType.ChineseSimplified] = "一辈子？！",
        },
    };

    // ══════════════════════════════════════════════
    //  PUBLIC API — called from Harmony patches
    // ══════════════════════════════════════════════

    public static void Inject(ScrollRect scrollRect)
    {
        if (_injected) return;
        _injected = true;

        try
        {
            _costumeService = RoomLifetimeScope.Resolve<CostumeChangeService>();
            _allSkins = FastEnum.GetValues<CostumeChangeService.CostumeSkinType>().ToArray();
            _cachedScrollRect = scrollRect;
            _arialFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _skinTypeTraverse = Traverse.Create(_costumeService).Field("_currentSkinType");

            var content = scrollRect.content;

            // Fix content VLG: disable childControlWidth to prevent zero-width children
            // (content has stretch anchors with 0 width — childControlWidth sets children to 0)
            var contentVLG = content.GetComponent<VerticalLayoutGroup>();
            if (contentVLG != null)
            {
                contentVLG.childControlWidth = false;
                Plugin.Log.LogInfo("[Inject] Disabled content VLG childControlWidth.");
            }

            // Build the costume section (participates in VLG layout for correct positioning)
            _costumeSection = new GameObject("CostumeSection");
            _costumeSection.transform.SetParent(content, false);
            var sectionRT = _costumeSection.AddComponent<RectTransform>();
            sectionRT.anchorMin = new Vector2(0, 1);
            sectionRT.anchorMax = new Vector2(1, 1);
            sectionRT.pivot = new Vector2(0.5f, 1);
            sectionRT.sizeDelta = new Vector2(0, 0);

            var sectionVLG = _costumeSection.AddComponent<VerticalLayoutGroup>();
            sectionVLG.childForceExpandWidth = true;
            sectionVLG.childForceExpandHeight = false;
            sectionVLG.childControlWidth = true;
            sectionVLG.childControlHeight = true;
            sectionVLG.spacing = 4f;
            sectionVLG.padding = new RectOffset(10, 10, 5, 15);

            var sectionCSF = _costumeSection.AddComponent<ContentSizeFitter>();
            sectionCSF.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sectionCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Title
            CreateText(_costumeSection.transform, "Costume", 18);

            // Skin buttons
            _buttonImages = new Dictionary<CostumeChangeService.CostumeSkinType, Image>();
            foreach (var skin in _allSkins)
            {
                var btn = CreateSkinButton(_costumeSection.transform, skin);
                var capturedSkin = skin;
                btn.onClick.AddListener(() => OnSkinButtonClicked(capturedSkin));
            }

            // ── Request Options Section ──
            CreateText(_costumeSection.transform, GetLocalizedText("costume_request_title"), 16);

            var hGroup = new GameObject("RequestOptionsRow");
            hGroup.transform.SetParent(_costumeSection.transform, false);
            var hGroupRT = hGroup.AddComponent<RectTransform>();
            hGroupRT.anchorMin = new Vector2(0, 1);
            hGroupRT.anchorMax = new Vector2(1, 1);
            hGroupRT.pivot = new Vector2(0.5f, 1);
            hGroupRT.sizeDelta = new Vector2(0, 30);
            var hGroupVLG = hGroup.AddComponent<HorizontalLayoutGroup>();
            hGroupVLG.childForceExpandWidth = true;
            hGroupVLG.childForceExpandHeight = false;
            hGroupVLG.childControlWidth = true;
            hGroupVLG.childControlHeight = true;
            hGroupVLG.spacing = 6f;
            hGroupVLG.childAlignment = TextAnchor.MiddleCenter;

            _requestOptionImages = new Dictionary<int, Image>();
            string[] optionKeys = { "option_once", "option_today", "option_forever" };
            for (int i = 0; i < 3; i++)
            {
                var btn = CreateOptionButton(hGroup.transform, GetLocalizedText(optionKeys[i]));
                var img = btn.GetComponent<Image>();
                _requestOptionImages[i] = img;
                var capturedIndex = i;
                btn.onClick.AddListener(() => OnRequestOptionClicked(capturedIndex));
            }

            _costumeSection.SetActive(false);

            Plugin.Log.LogInfo("[Inject] Costume section created.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[Inject] Failed: {ex}");
        }
    }

    public static void OnActivate()
    {
        if (_costumeSection == null || _cachedScrollRect == null) return;

        try
        {
            _costumeSection.SetActive(true);

            // Force layout rebuild so section is positioned by VLG
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_cachedScrollRect.content);

            // Set section width to match viewport (content rect width may be 0)
            float viewWidth = _cachedScrollRect.viewport.rect.width;
            var sectionRT = _costumeSection.GetComponent<RectTransform>();
            sectionRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewWidth);
            sectionRT.anchoredPosition = new Vector2(viewWidth / 2f, sectionRT.anchoredPosition.y);

            // Rebuild section internal layout with proper width
            LayoutRebuilder.ForceRebuildLayoutImmediate(sectionRT);

            UpdateActiveHighlights();
            RestoreRequestOptionState();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[Activate] Failed: {ex}");
        }
    }

    public static void OnDeactivate()
    {
        if (_costumeSection == null) return;
        _costumeSection.SetActive(false);
        // Reset request options on deactivate (Option 3 will be restored on next Activate)
        _selectedRequestOption = -1;
    }

    // ══════════════════════════════════════════════
    //  SKIN BUTTON LOGIC (v1.0 enhanced)
    // ══════════════════════════════════════════════

    private static void OnSkinButtonClicked(CostumeChangeService.CostumeSkinType skinType)
    {
        // Auto-select Option 1 if no option is selected
        if (_selectedRequestOption < 0)
        {
            _selectedRequestOption = OptionOnce;
            UpdateRequestOptionHighlights();
        }

        _costumeService.ChangeCostume(skinType).Forget();

        // Option 1: session-only, clear permanent
        // Option 2: save today, clear permanent
        // Option 3: save permanent + today
        if (_selectedRequestOption != OptionForever)
        {
            ModSaveData.ClearPermanentSkin();
        }
        if (_selectedRequestOption != OptionOnce)
        {
            SaveTodayCostume(skinType);
        }
        if (_selectedRequestOption == OptionForever)
        {
            ModSaveData.SetPermanentSkin(skinType);
        }

        UpdateActiveHighlights(skinType);
    }

    private static void SaveTodayCostume(CostumeChangeService.CostumeSkinType skinType)
    {
        var data = SaveDataManager.Instance.CostumeChangeSaveData.LatestChangeData;
        data.SetChangedCostumeDate();
        data.SetChangedCostumeSkinType(skinType);
        SaveDataManager.Instance.SaveCostumeChangeSaveData();
    }

    private static void UpdateActiveHighlights(CostumeChangeService.CostumeSkinType? overrideSkin = null)
    {
        if (_buttonImages == null) return;
        var current = overrideSkin ?? GetCurrentSkinType();
        foreach (var kvp in _buttonImages)
        {
            kvp.Value.color = (kvp.Key == current) ? ActiveColor : NormalColor;
        }
    }

    private static CostumeChangeService.CostumeSkinType GetCurrentSkinType()
    {
        try
        {
            return _skinTypeTraverse.GetValue<CostumeChangeService.CostumeSkinType>();
        }
        catch
        {
            return CostumeChangeService.CostumeSkinType.Default_1;
        }
    }

    // ══════════════════════════════════════════════
    //  REQUEST OPTION LOGIC (v2.0)
    // ══════════════════════════════════════════════

    private static void OnRequestOptionClicked(int optionIndex)
    {
        // Toggle: clicking the same option deselects it
        if (_selectedRequestOption == optionIndex)
        {
            _selectedRequestOption = -1;
        }
        else
        {
            _selectedRequestOption = optionIndex;
        }
        UpdateRequestOptionHighlights();
    }

    private static void UpdateRequestOptionHighlights()
    {
        if (_requestOptionImages == null) return;
        foreach (var kvp in _requestOptionImages)
        {
            kvp.Value.color = (kvp.Key == _selectedRequestOption) ? OptionSelectedColor : OptionNormalColor;
        }
    }

    private static void RestoreRequestOptionState()
    {
        // Check if a permanent costume is saved → auto-select Option 3
        var permanent = ModSaveData.GetPermanentSkin();
        _selectedRequestOption = permanent.HasValue ? OptionForever : -1;
        UpdateRequestOptionHighlights();
    }

    // ══════════════════════════════════════════════
    //  LOCALIZATION
    // ══════════════════════════════════════════════

    private static string GetLocalizedText(string key)
    {
        try
        {
            var lang = ProjectLifetimeScope.Resolve<LanguageSupplier>().Get();
            if (LocalizedTexts.TryGetValue(key, out var dict))
            {
                if (dict.TryGetValue(lang, out var text)) return text;
                if (dict.TryGetValue(GameLanguageType.Japanese, out var fallback)) return fallback;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Localization] Failed to resolve language: {ex.Message}");
        }
        return key;
    }

    // ══════════════════════════════════════════════
    //  UI HELPER METHODS
    // ══════════════════════════════════════════════

    private static Button CreateSkinButton(Transform parent, CostumeChangeService.CostumeSkinType skin)
    {
        var go = new GameObject($"Btn_{skin}");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 34);

        var img = go.AddComponent<Image>();
        img.color = NormalColor;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 34;
        le.minHeight = 34;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = NormalColor;
        colors.highlightedColor = ActiveColor;
        colors.pressedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        btn.colors = colors;

        // Label
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var labelRT = labelGo.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.sizeDelta = Vector2.zero;
        var text = labelGo.AddComponent<Text>();
        text.text = skin.ToString();
        text.fontSize = 14;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = _arialFont;

        _buttonImages[skin] = img;
        return btn;
    }

    private static Button CreateOptionButton(Transform parent, string label)
    {
        var go = new GameObject($"Opt_{label}");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 30);

        var img = go.AddComponent<Image>();
        img.color = OptionNormalColor;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 30;
        le.minHeight = 30;
        le.flexibleWidth = 1;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = OptionNormalColor;
        colors.highlightedColor = OptionSelectedColor;
        colors.pressedColor = new Color(0.5f, 0.4f, 0.3f, 1f);
        btn.colors = colors;

        // Label
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var labelRT = labelGo.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.sizeDelta = Vector2.zero;
        var text = labelGo.AddComponent<Text>();
        text.text = label;
        text.fontSize = 12;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = _arialFont;

        return btn;
    }

    private static void CreateText(Transform parent, string content, int fontSize)
    {
        var go = new GameObject("Title");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 8;
        le.minHeight = fontSize + 8;

        var text = go.AddComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = _arialFont;
    }

}
