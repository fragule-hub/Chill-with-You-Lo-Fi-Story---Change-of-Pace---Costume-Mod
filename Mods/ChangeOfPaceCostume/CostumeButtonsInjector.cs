using System;
using System.Collections.Generic;
using System.Linq;
using Bulbul;
using Cysharp.Threading.Tasks;
using FastEnumUtility;
using HarmonyLib;
using NestopiSystem.DIContainers;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChangeOfPaceCostume;

internal static class CostumeButtonsInjector
{
    // ── cached state ──
    private static bool _injected;
    private static CostumeChangeService _costumeService;
    private static CostumeChangeService.CostumeSkinType[] _allSkins;
    private static Dictionary<CostumeChangeService.CostumeSkinType, Image> _buttonImages;
    private static GameObject _costumeSection;
    private static ScrollRect _cachedScrollRect;
    private static Traverse _skinTypeTraverse;

    // ── game-native text (TMP + FontSupplier) ──
    private static FontSupplier _fontSupplier;
    private static LanguageSupplier _languageSupplier;

    // ── localized text refresh registry (hot language switch) ──
    private static readonly List<(TextMeshProUGUI Tmp, string Key)> _localizedTexts = new();
    private static IDisposable _languageSubscription;

    // ── request options ──
    private const int OptionOnce = 0;
    private const int OptionToday = 1;
    private const int OptionForever = 2;
    private static Dictionary<int, Image> _requestOptionImages;
    private static int _selectedRequestOption = -1; // -1 = none

    // ── unified limited decorations (hats + events) ──
    private static Dictionary<string, Image> _decorButtonImages;

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
            [GameLanguageType.Japanese] = "一回",
            [GameLanguageType.English] = "Once",
            [GameLanguageType.ChineseSimplified] = "一次",
        },
        ["option_today"] = new Dictionary<GameLanguageType, string>
        {
            [GameLanguageType.Japanese] = "一日",
            [GameLanguageType.English] = "One day",
            [GameLanguageType.ChineseSimplified] = "一日",
        },
        ["option_forever"] = new Dictionary<GameLanguageType, string>
        {
            [GameLanguageType.Japanese] = "一生",
            [GameLanguageType.English] = "Forever",
            [GameLanguageType.ChineseSimplified] = "一生",
        },
        ["limited_title"] = new Dictionary<GameLanguageType, string>
        {
            [GameLanguageType.Japanese] = "限定装饰",
            [GameLanguageType.English] = "Limited Decor",
            [GameLanguageType.ChineseSimplified] = "限定装饰",
        },
        ["limited_decor_MagicalPrincess_Beret"] = new Dictionary<GameLanguageType, string>
        {
            [GameLanguageType.Japanese] = "魔法少女ベレー帽",
            [GameLanguageType.English] = "Magical Princess Beret",
            [GameLanguageType.ChineseSimplified] = "魔法公主贝雷帽",
        },
        ["limited_decor_Christmas2025"] = new Dictionary<GameLanguageType, string>
        {
            [GameLanguageType.Japanese] = "クリスマス2025",
            [GameLanguageType.English] = "Christmas 2025",
            [GameLanguageType.ChineseSimplified] = "圣诞 2025",
        },
        ["limited_decor_AprilFool2026"] = new Dictionary<GameLanguageType, string>
        {
            [GameLanguageType.Japanese] = "エイプリルフール2026",
            [GameLanguageType.English] = "April Fools 2026",
            [GameLanguageType.ChineseSimplified] = "愚人节 2026",
        },
        ["limited_hint"] = new Dictionary<GameLanguageType, string>
        {
            [GameLanguageType.Japanese] = "帽子系は猫耳ヘッドホンを一時的に交換します",
            [GameLanguageType.English] = "Headwear temporarily replaces cat-ear headphones",
            [GameLanguageType.ChineseSimplified] = "头部装饰会临时替换猫耳耳机",
        },
    };

    // ══════════════════════════════════════════════
    //  PUBLIC API — called from Harmony patches
    // ══════════════════════════════════════════════

    public static void Inject(ScrollRect scrollRect)
    {
        // Scene-rebuild guard: if our injected UI was destroyed, allow a fresh inject.
        if (_injected && _costumeSection == null)
        {
            _injected = false;
            Plugin.Log.LogInfo("[Inject] Costume section was destroyed; re-injecting.");
        }
        if (_injected) return;
        _injected = true;

        Plugin.Log.LogInfo($"[Inject] Enter: scrollRect='{scrollRect?.name ?? "NULL"}' content='{scrollRect?.content?.name ?? "NULL"}'");

        try
        {
            _costumeService = RoomLifetimeScope.Resolve<CostumeChangeService>();
            _allSkins = FastEnum.GetValues<CostumeChangeService.CostumeSkinType>().ToArray();
            _cachedScrollRect = scrollRect;
            _fontSupplier = ProjectLifetimeScope.Resolve<FontSupplier>();
            _languageSupplier = ProjectLifetimeScope.Resolve<LanguageSupplier>();
            SubscribeLanguageChanges();
            _skinTypeTraverse = Traverse.Create(_costumeService).Field("_currentSkinType");
            Plugin.Log.LogInfo($"[Inject] Services resolved (skins: {_allSkins.Length}).");

            var content = scrollRect.content;

            // Fix content VLG: disable childControlWidth to prevent zero-width children
            // (content has stretch anchors with 0 width — childControlWidth sets children to 0)
            var contentVLG = content.GetComponent<VerticalLayoutGroup>();
            if (contentVLG != null)
            {
                contentVLG.childControlWidth = false;
                Plugin.Log.LogInfo("[Inject] Disabled content VLG childControlWidth.");
            }
            else
            {
                Plugin.Log.LogWarning("[Inject] content has no VerticalLayoutGroup (layout may differ).");
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
            CreateTmpText(_costumeSection.transform, "Costume", 18);

            // Skin buttons
            _buttonImages = new Dictionary<CostumeChangeService.CostumeSkinType, Image>();
            foreach (var skin in _allSkins)
            {
                var btn = CreateSkinButton(_costumeSection.transform, skin);
                var capturedSkin = skin;
                btn.onClick.AddListener(() => OnSkinButtonClicked(capturedSkin));
            }

            // ── Request Options Section ──
            CreateTmpText(_costumeSection.transform, GetLocalizedText("costume_request_title"), 16, "costume_request_title");

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
                var btn = CreateOptionButton(hGroup.transform, GetLocalizedText(optionKeys[i]), optionKeys[i]);
                var img = btn.GetComponent<Image>();
                _requestOptionImages[i] = img;
                var capturedIndex = i;
                btn.onClick.AddListener(() => OnRequestOptionClicked(capturedIndex));
            }

            // ── Limited Decorations Section: one unified toggle per entry ──
            // Entries come from LimitedDecorationController (hat enum + discovered events),
            // so future decorations appear automatically with zero code changes.
            var entries = LimitedDecorationController.GetAllEntries();
            if (entries.Count > 0)
            {
                CreateTmpText(_costumeSection.transform, GetLocalizedText("limited_title"), 16, "limited_title");

                _decorButtonImages = new Dictionary<string, Image>();
                foreach (var entry in entries)
                {
                    // Display name: localized if known, otherwise the internal id (enum name)
                    var key = "limited_decor_" + entry.Key;
                    var displayName = GetLocalizedText(key);
                    var btn = CreateOptionButton(_costumeSection.transform, displayName, key);
                    _decorButtonImages[entry.Key] = btn.GetComponent<Image>();
                    var capturedKey = entry.Key;
                    btn.onClick.AddListener(() => OnDecorButtonClicked(capturedKey));
                }
                UpdateDecorHighlights();

                CreateTmpText(_costumeSection.transform, GetLocalizedText("limited_hint"), 12, "limited_hint");
            }
            else
            {
                Plugin.Log.LogWarning("[Inject] No limited decorations found; skipping limited decor section.");
            }

            _costumeSection.SetActive(false);

            Plugin.Log.LogInfo(
                $"[Inject] Complete: skins={_buttonImages.Count}, options={_requestOptionImages.Count}, " +
                $"decor={(_decorButtonImages?.Count ?? 0)}, section='{_costumeSection.name}' (inactive until panel opens).");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[Inject] FAILED: {ex}");
        }
    }

    public static void OnActivate()
    {
        if (_costumeSection == null || _cachedScrollRect == null)
        {
            Plugin.Log.LogWarning($"[Inject] OnActivate skipped (section={_costumeSection?.name ?? "NULL"}, scrollRect={_cachedScrollRect?.name ?? "NULL"}).");
            return;
        }

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

            UpdateDecorHighlights();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[Activate] Failed: {ex}");
        }
    }

    public static void OnDeactivate()
    {
        if (_costumeSection == null)
        {
            Plugin.Log.LogWarning("[Inject] OnDeactivate skipped (section is NULL).");
            return;
        }
        _costumeSection.SetActive(false);
        // Reset request options on deactivate (Option 3 will be restored on next Activate)
        _selectedRequestOption = -1;
        Plugin.Log.LogInfo("[Inject] Panel deactivated; section hidden.");
    }

    // ══════════════════════════════════════════════
    //  SKIN BUTTON LOGIC
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
    //  REQUEST OPTION LOGIC
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
            var lang = GetCurrentLanguage();
            if (LocalizedTexts.TryGetValue(key, out var dict))
            {
                if (dict.TryGetValue(lang, out var text)) return text;
                // Fallback to English for languages the mod has not configured
                if (dict.TryGetValue(GameLanguageType.English, out var fallback)) return fallback;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Localization] Failed to resolve language: {ex.Message}");
        }
        return key;
    }

    // ── game-native font: apply language font asset + material (mirrors TextLocalizationBehaviour.FontSet) ──

    private static GameLanguageType GetCurrentLanguage()
    {
        try
        {
            if (_languageSupplier == null)
            {
                _languageSupplier = ProjectLifetimeScope.Resolve<LanguageSupplier>();
            }
            return _languageSupplier.Get();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Localization] Failed to resolve language: {ex.Message}");
            return GameLanguageType.Japanese;
        }
    }

    // ── hot language switch: refresh every registered localized text ──

    private static void SubscribeLanguageChanges()
    {
        if (_languageSubscription != null || _languageSupplier == null) return;
        try
        {
            // Skip(1): ignore the initial value, only react to actual changes
            _languageSubscription = _languageSupplier.Language.Skip(1).Subscribe(_ => OnLanguageChanged());
            Plugin.Log.LogInfo("[Localization] Subscribed to language changes.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Localization] Subscribe failed: {ex.Message}");
        }
    }

    private static void RegisterLocalizedText(TextMeshProUGUI tmp, string key)
    {
        if (tmp == null || string.IsNullOrEmpty(key)) return;
        _localizedTexts.Add((tmp, key));
    }

    private static void OnLanguageChanged()
    {
        for (int i = 0; i < _localizedTexts.Count; i++)
        {
            var entry = _localizedTexts[i];
            if (entry.Tmp == null) continue; // destroyed (e.g. scene rebuild) — skip
            entry.Tmp.text = GetLocalizedText(entry.Key);
            ApplyTmpFont(entry.Tmp);
        }
    }

    private static void ApplyTmpFont(TextMeshProUGUI text)
    {
        if (_fontSupplier == null)
        {
            try
            {
                _fontSupplier = ProjectLifetimeScope.Resolve<FontSupplier>();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[Font] Failed to resolve FontSupplier: {ex.Message}");
                return;
            }
        }

        try
        {
            var lang = GetCurrentLanguage();
            // Set font FIRST: GetFontMaterial() reads text.fontMaterial.name, which is null on a fresh TMP
            // (no font yet). Assigning the font makes fontMaterial non-null so the lookup is safe.
            var fontAsset = _fontSupplier.GetFontAsset(lang);
            if (fontAsset == null)
            {
                Plugin.Log.LogWarning($"[Font] No font asset for language: {lang}");
                return;
            }
            text.font = fontAsset;
            var fontMaterial = _fontSupplier.GetFontMaterial(text, lang);
            if (fontMaterial != null)
            {
                text.fontMaterial = fontMaterial;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Font] Failed to apply font: {ex.Message}");
        }
    }

    // ══════════════════════════════════════════════
    //  UI HELPER METHODS
    // ══════════════════════════════════════════════

    private static Button CreateSkinButton(Transform parent, CostumeChangeService.CostumeSkinType skin)
    {
        var btn = CreateTmpButton(parent, $"Btn_{skin}", 34f, 14, skin.ToString(),
            flexibleWidth: false, NormalColor, ActiveColor, new Color(0.5f, 0.5f, 0.5f, 1f));
        var img = btn.GetComponent<Image>();
        img.color = NormalColor;
        _buttonImages[skin] = img;
        return btn;
    }

    private static Button CreateOptionButton(Transform parent, string label, string localizeKey = null)
    {
        var btn = CreateTmpButton(parent, $"Opt_{label}", 30f, 13, label,
            flexibleWidth: true, OptionNormalColor, OptionSelectedColor, new Color(0.5f, 0.4f, 0.3f, 1f));
        if (localizeKey != null)
        {
            var labelTmp = btn.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (labelTmp != null) RegisterLocalizedText(labelTmp, localizeKey);
        }
        return btn;
    }

    /// <summary>Shared button builder: Image + Button + Label(TMP).</summary>
    private static Button CreateTmpButton(Transform parent, string name, float height, int fontSize, string label,
        bool flexibleWidth, Color normalColor, Color highlightColor, Color pressedColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, height);

        var img = go.AddComponent<Image>();
        img.color = normalColor;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        if (flexibleWidth) le.flexibleWidth = 1;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = highlightColor;
        colors.pressedColor = pressedColor;
        btn.colors = colors;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var labelRT = labelGo.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.sizeDelta = Vector2.zero;
        var text = labelGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        ApplyTmpFont(text);

        return btn;
    }

    private static void CreateTmpText(Transform parent, string content, int fontSize, string localizeKey = null)
    {
        var go = new GameObject("Title");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 8;
        le.minHeight = fontSize + 8;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        ApplyTmpFont(text);

        if (localizeKey != null) RegisterLocalizedText(text, localizeKey);
    }

    //  ── unified limited decorations ──

    private static void OnDecorButtonClicked(string key)
    {
        LimitedDecorationController.Toggle(key);
        UpdateDecorHighlights();
    }

    private static void UpdateDecorHighlights()
    {
        if (_decorButtonImages == null) return;
        foreach (var kvp in _decorButtonImages)
        {
            bool on = LimitedDecorationController.IsEnabled(kvp.Key);
            kvp.Value.color = on ? OptionSelectedColor : OptionNormalColor;
        }
    }

}
