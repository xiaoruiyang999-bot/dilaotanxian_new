using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// v2.0.2 单一职业角色选择页。卡片同时展示并应用外观、属性、武器池、技能与职业资源。
/// 当前 MVP 只有狼人；新增角色只需追加 PlayableCharacterDefinition 与 Catalog 条目。
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private static CharacterSelectUI instance;
    private static readonly Color DimColor = new Color(1f, 1f, 1f, 0.15f);

    private readonly List<CharacterButton> buttons = new List<CharacterButton>();
    private GameObject canvasGo;
    private PlayableCharacterDefinition selected;

    private struct CharacterButton
    {
        public PlayableCharacterDefinition definition;
        public Image frame;
    }

    public static void Open()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("PlayableCharacterSelectUI");
            instance = go.AddComponent<CharacterSelectUI>();
            instance.Build();
        }
        instance.Show();
    }

    public static void Close()
    {
        if (instance != null) instance.Hide();
    }

    private void Show()
    {
        EnsureEventSystem();
        RunStateCarrier carrier = RunStateCarrier.Ensure();
        selected = carrier.HasPlayableCharacter
            ? carrier.ChosenPlayableCharacter
            : FirstAvailable();
        RefreshHighlights();
        canvasGo.SetActive(true);
        IsOpen = true;
    }

    private void Hide()
    {
        canvasGo.SetActive(false);
        IsOpen = false;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        IsOpen = false;
    }

    private void Build()
    {
        canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 205;
        PanelSprite.ConfigureCanvasScaler(canvasGo);
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasGo.transform, false);
        Image panelImg = panel.AddComponent<Image>();
        PanelSprite.ApplyStonePanel(panelImg, new Color(0f, 0f, 0f, 0.85f));
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(900f, 555f);

        Label(panelRect, "选择职业角色", 30, Color.white,
            new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(600f, 44f));
        Label(panelRect, "一次选择确定外观、属性、武器、技能与职业资源", 14,
            new Color(0.75f, 0.73f, 0.65f), new Vector2(0.5f, 1f),
            new Vector2(0f, -114f), new Vector2(700f, 24f));

        IReadOnlyList<PlayableCharacterDefinition> definitions = PlayableCharacterCatalog.All;
        int count = definitions.Count;
        for (int i = 0; i < count; i++)
        {
            PlayableCharacterDefinition definition = definitions[i];
            if (definition == null) continue;
            float x = (i - (count - 1) * 0.5f) * 365f;
            buttons.Add(BuildCharacterButton(panelRect, definition, new Vector2(x, -30f)));
        }

        UIHelper.CreateCloseButton(panelRect, Close);
        canvasGo.SetActive(false);
    }

    private CharacterButton BuildCharacterButton(
        Transform parent,
        PlayableCharacterDefinition definition,
        Vector2 pos)
    {
        GameObject frame = new GameObject($"Btn_Character_{definition.Id}", typeof(RectTransform));
        frame.transform.SetParent(parent, false);
        Image frameImg = frame.AddComponent<Image>();
        frameImg.color = DimColor;
        RectTransform frameRect = (RectTransform)frame.transform;
        frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.anchoredPosition = pos;
        frameRect.sizeDelta = new Vector2(340f, 320f);

        GameObject bg = new GameObject("Bg", typeof(RectTransform));
        bg.transform.SetParent(frame.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.035f, 0.04f, 0.045f, 0.82f);
        RectTransform bgRect = (RectTransform)bg.transform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = new Vector2(3f, 3f);
        bgRect.offsetMax = new Vector2(-3f, -3f);

        Label(bgRect, "[职业角色立绘待补]", 17, definition.CharacterColor,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 78f), new Vector2(280f, 72f));
        Label(bgRect, definition.DisplayName, 26, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(300f, 38f));
        Label(bgRect, BuildStatLine(definition), 14, new Color(0.8f, 0.78f, 0.7f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(300f, 70f));

        GameObject selectGo = new GameObject("Select", typeof(RectTransform));
        selectGo.transform.SetParent(frame.transform, false);
        Image selectImg = selectGo.AddComponent<Image>();
        RectTransform selectRect = (RectTransform)selectGo.transform;
        selectRect.anchorMin = selectRect.anchorMax = selectRect.pivot = new Vector2(0.5f, 0f);
        selectRect.anchoredPosition = new Vector2(0f, 18f);
        selectRect.sizeDelta = new Vector2(280f, 44f);
        Label(selectRect, "选 择", 17, Color.white,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 30f));
        Button selectButton = selectGo.AddComponent<Button>();
        PanelSprite.ApplyStoneButton(selectButton, selectImg, new Color(0.12f, 0.12f, 0.14f, 0.95f));
        selectButton.onClick.AddListener(() => Pick(definition));

        Button cardButton = frame.AddComponent<Button>();
        cardButton.targetGraphic = frameImg;
        cardButton.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = cardButton.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 0.35f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 0.35f);
        colors.fadeDuration = 0.08f;
        cardButton.colors = colors;
        cardButton.onClick.AddListener(() =>
        {
            selected = definition;
            RefreshHighlights();
        });

        return new CharacterButton { definition = definition, frame = frameImg };
    }

    private void Pick(PlayableCharacterDefinition definition)
    {
        if (definition == null) return;

        selected = definition;
        RefreshHighlights();

        RunStateCarrier carrier = RunStateCarrier.Ensure();
        carrier.SetPlayableCharacter(definition.Id);

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            player.GetStats().ApplyPlayableCharacter(definition);
            ApplyCharacterRuntime(player.gameObject, definition.Id);

            PlayerWeaponHolder holder = player.GetComponent<PlayerWeaponHolder>();
            if (holder == null) holder = player.gameObject.AddComponent<PlayerWeaponHolder>();
            if (carrier.LastWeapon != null
                && (holder.Current == null || holder.Current.Data != carrier.LastWeapon))
                holder.Equip(carrier.LastWeapon);
        }

        PrepRoomPlacer.RefreshWeapons(definition);
        Debug.Log($"[PlayableCharacter] 已选择：{definition.DisplayName}（{definition.Id}）");
        Hide();
    }

    public static void ApplyCharacterRuntime(GameObject player, PlayableCharacterId id)
    {
        if (player == null) return;

        bool isWerewolf = id == PlayableCharacterId.Werewolf;
        FrameAnimator frameAnimator = player.GetComponent<FrameAnimator>();
        if (frameAnimator != null) frameAnimator.SetWerewolfVisual(isWerewolf);

        if (isWerewolf)
        {
            WerewolfTransformation.EnsureOn(player);
            WerewolfDash.EnsureOn(player);
            return;
        }

        WerewolfTransformation transformation = player.GetComponent<WerewolfTransformation>();
        if (transformation != null) Destroy(transformation);
        WerewolfDash dash = player.GetComponent<WerewolfDash>();
        if (dash != null) Destroy(dash);
    }

    private void RefreshHighlights()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            CharacterButton button = buttons[i];
            if (button.frame == null || button.definition == null) continue;
            Color tint = button.definition.CharacterColor;
            button.frame.color = button.definition == selected
                ? new Color(tint.r, tint.g, tint.b, 0.85f)
                : DimColor;
        }
    }

    private static PlayableCharacterDefinition FirstAvailable()
    {
        IReadOnlyList<PlayableCharacterDefinition> definitions = PlayableCharacterCatalog.All;
        for (int i = 0; i < definitions.Count; i++)
            if (definitions[i] != null) return definitions[i];
        return null;
    }

    private static string BuildStatLine(PlayableCharacterDefinition definition)
    {
        return $"HP {definition.maxHp:0}  护甲 {definition.maxArmor:0}  攻击 {definition.attack:0}\n" +
               $"移速 {definition.moveSpeed:0.0}  暴击 {definition.critRate:P0}  怒痕 {definition.beastResourceMax:0}";
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    private static void Label(
        Transform parent,
        string text,
        int size,
        Color color,
        Vector2 anchor,
        Vector2 offset,
        Vector2 sizeDelta)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = TMPFontProvider.Font;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, anchor.y);
        rect.anchoredPosition = offset;
        rect.sizeDelta = sizeDelta;
    }
}
