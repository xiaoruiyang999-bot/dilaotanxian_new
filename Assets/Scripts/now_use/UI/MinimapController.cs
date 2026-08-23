using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 小地图（v0.6.0）：屏幕右上角常显的楼层缩略图，方便查看角色所在位置。
/// 数据流：DungeonManager.OnGenerated → Rebuild 整图重建；房间色块消费 Room.Bounds/Type
/// （配色与 DungeonManager Gizmos 一致）；连线消费 Layout.connections；玩家标记在
/// LateUpdate 做世界坐标 → 窗口坐标映射；当前房间白色描边由 Room.OnRoomEntered 驱动。
/// 纯代码 UI（Screen Space Overlay Canvas，无 prefab / 资产依赖），实现风格同
/// PlayerWorldStatusBar：Image 不设 Sprite 即为纯色矩形，契合程序员美术。
/// </summary>
public class MinimapController : MonoBehaviour
{
    [Header("数据源")]
    [Tooltip("留空则自动查找场景中的 DungeonManager")]
    [SerializeField] private DungeonManager dungeonManager;

    [Header("窗口（UI 像素）")]
    [SerializeField] private Vector2 windowSize = new Vector2(260f, 190f);
    [Tooltip("窗口距屏幕右上角的边距")]
    [SerializeField] private float windowMargin = 12f;
    [Tooltip("地图内容距窗口内边的留白")]
    [SerializeField] private float contentPadding = 12f;
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);

    [Header("标记")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float playerDotSize = 7f;
    [SerializeField] private float connectionLineWidth = 3f;
    [Tooltip("当前房间描边相对房间块的外扩像素")]
    [SerializeField] private float outlineExtra = 3f;

    private RectTransform contentRoot;
    private readonly Dictionary<int, Room> roomsById = new Dictionary<int, Room>();
    private RectTransform playerDot;
    private RectTransform currentRoomOutline;
    private Text floorLabel;

    private Transform player;
    private Rect worldBounds;
    private float mapScale;
    private Vector2 mapOrigin;     // 世界包围盒左下角映射到的 content 局部坐标

    private static Font builtinFont;

    /// <summary>内置字体公开缓存（v0.7.0：CoinHUD 等纯代码 UI 复用，同风格）。</summary>
    public static Font BuiltinFont
    {
        get
        {
            if (builtinFont == null)
                builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return builtinFont;
        }
    }

    void Awake()
    {
        if (dungeonManager == null)
            dungeonManager = FindAnyObjectByType<DungeonManager>();
        if (dungeonManager == null)
        {
            Debug.LogWarning("[Minimap] 场景中未找到 DungeonManager，小地图停用。");
            enabled = false;
            return;
        }

        BuildCanvas();
    }

    void OnEnable()
    {
        if (dungeonManager != null)
            dungeonManager.OnGenerated += Rebuild;
    }

    void OnDisable()
    {
        if (dungeonManager != null)
            dungeonManager.OnGenerated -= Rebuild;
    }

    void Start()
    {
        // DungeonManager.Start 会生成第 1 层。若本组件 Start 晚于它（脚本执行顺序不定），
        // Layout 已就绪但 OnGenerated 错过了，这里补建一次；早于它则等事件触发。
        if (dungeonManager.Layout != null && dungeonManager.Rooms.Count > 0)
            Rebuild();
    }

    void OnDestroy() => ClearMap();

    void LateUpdate()
    {
        if (playerDot == null || contentRoot == null) return;
        if (player == null) FindPlayer();
        if (player == null) return;
        playerDot.anchoredPosition = WorldToMap(player.position);
    }

    // ---------- 建图 ----------

    /// <summary>整图重建（生成事件驱动）：清旧 → 收集房间 → fit 缩放 → 画连线/色块/描边/玩家点。</summary>
    private void Rebuild()
    {
        ClearMap();
        DungeonLayout layout = dungeonManager.Layout;
        if (layout == null) return;

        // 1. 收集房间 + 世界包围盒
        bool first = true;
        foreach (KeyValuePair<int, Room> kv in dungeonManager.Rooms)
        {
            Room room = kv.Value;
            if (room == null) continue;
            roomsById[room.Id] = room;
            room.OnRoomEntered += HandleRoomEntered;
            worldBounds = first ? room.Bounds : Union(worldBounds, room.Bounds);
            first = false;
        }
        if (roomsById.Count == 0) return;

        // 2. 比例：整层 fit 进窗口（内容居中，mapOrigin 为左下角在 content 局部系的坐标）
        Vector2 avail = windowSize - Vector2.one * (contentPadding * 2f);
        mapScale = Mathf.Min(avail.x / worldBounds.width, avail.y / worldBounds.height);
        mapOrigin = -worldBounds.size * mapScale * 0.5f;

        // 3. 连线垫底（与 Gizmos 调试图同源：a/b 中心连线）
        foreach (RoomConnection conn in layout.connections)
        {
            if (!roomsById.TryGetValue(conn.a.id, out Room ra) || !roomsById.TryGetValue(conn.b.id, out Room rb))
                continue;
            DrawLine(WorldToMap(ra.Center), WorldToMap(rb.Center));
        }

        // 4. 房间色块
        foreach (KeyValuePair<int, Room> kv in roomsById)
        {
            Room room = kv.Value;
            Image block = CreateImage($"Room_{room.Id}_{room.Type}", contentRoot, RoomColor(room.Type));
            // 略缩 2px 形成块间缝隙，相邻房不会糊成一片
            block.rectTransform.sizeDelta = room.Bounds.size * mapScale - Vector2.one * 2f;
            block.rectTransform.anchoredPosition = WorldToMap(room.Center);
        }

        // 5. 当前房间描边（垫在色块下，白色底块露出边缘形成描边效果）
        currentRoomOutline = CreateImage("CurrentRoomOutline", contentRoot, Color.white).rectTransform;
        currentRoomOutline.gameObject.SetActive(false);
        currentRoomOutline.SetAsFirstSibling();
        Room entered = null;
        foreach (KeyValuePair<int, Room> kv in roomsById)
            if (kv.Value.State != RoomState.Unvisited) { entered = kv.Value; break; }
        if (entered != null) MoveOutline(entered);

        // 6. 玩家标记（最后创建，自然置于最上层）
        playerDot = CreateImage("PlayerDot", contentRoot, Color.white).rectTransform;
        playerDot.sizeDelta = Vector2.one * playerDotSize;
        FindPlayer();

        if (floorLabel != null)
            floorLabel.text = $"F{dungeonManager.FloorNumber}·{DungeonManager.GetFloorTheme(dungeonManager.FloorNumber).name}";
    }

    private void ClearMap()
    {
        foreach (KeyValuePair<int, Room> kv in roomsById)
            if (kv.Value != null)
                kv.Value.OnRoomEntered -= HandleRoomEntered;
        roomsById.Clear();

        if (contentRoot != null)
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
                Destroy(contentRoot.GetChild(i).gameObject);

        playerDot = null;
        currentRoomOutline = null;
    }

    // ---------- 事件 ----------

    private void HandleRoomEntered(Room room)
    {
        if (room == null) return;
        MoveOutline(room);
    }

    private void MoveOutline(Room room)
    {
        if (currentRoomOutline == null) return;
        currentRoomOutline.anchoredPosition = WorldToMap(room.Center);
        currentRoomOutline.sizeDelta = room.Bounds.size * mapScale + Vector2.one * outlineExtra;
        currentRoomOutline.gameObject.SetActive(true);
    }

    // ---------- 坐标与绘制 ----------

    /// <summary>世界坐标 → 小地图内容局部坐标（contentRoot 中心为原点）。</summary>
    private Vector2 WorldToMap(Vector2 world) => mapOrigin + (world - worldBounds.min) * mapScale;

    private void DrawLine(Vector2 a, Vector2 b)
    {
        Vector2 delta = b - a;
        float length = delta.magnitude;
        if (length < 0.01f) return;
        Image line = CreateImage("Connection", contentRoot, new Color(1f, 1f, 1f, 0.35f));
        line.rectTransform.sizeDelta = new Vector2(length, connectionLineWidth);
        line.rectTransform.anchoredPosition = (a + b) * 0.5f;
        line.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    /// <summary>房型配色：与 DungeonManager.OnDrawGizmos 完全一致，编辑器调试视图与游戏内观感统一。</summary>
    private static Color RoomColor(RoomType type)
    {
        return type switch
        {
            RoomType.Start    => Color.green,
            RoomType.Elite    => new Color(1f, 0.45f, 0f),
            RoomType.Treasure => Color.yellow,
            RoomType.Shop     => Color.blue,
            RoomType.Event    => new Color(0.7f, 0.3f, 1f),
            RoomType.Boss     => Color.red,
            _                 => new Color(1f, 1f, 1f, 0.5f),   // Combat
        };
    }

    private static Rect Union(Rect a, Rect b)
    {
        Vector2 min = Vector2.Min(a.min, b.min);
        Vector2 max = Vector2.Max(a.max, b.max);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // ---------- UI 构建 ----------

    /// <summary>一次性搭建 Overlay Canvas：右上角半透明窗口 + 内容根 + 楼层角标。</summary>
    private void BuildCanvas()
    {
        var canvasGo = new GameObject("MinimapCanvas", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        Image window = CreateImage("Window", canvasGo.transform, backgroundColor);
        window.rectTransform.anchorMin = window.rectTransform.anchorMax = new Vector2(1f, 1f);
        window.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        window.rectTransform.anchoredPosition = new Vector2(
            -windowSize.x * 0.5f - windowMargin, -windowSize.y * 0.5f - windowMargin);
        window.rectTransform.sizeDelta = windowSize;

        var contentGo = new GameObject("MapContent", typeof(RectTransform));
        contentGo.transform.SetParent(window.rectTransform, false);
        contentRoot = contentGo.GetComponent<RectTransform>();
        contentRoot.anchorMin = Vector2.zero;
        contentRoot.anchorMax = Vector2.one;
        contentRoot.offsetMin = contentRoot.offsetMax = Vector2.zero;

        floorLabel = CreateLabel(window.rectTransform);
        floorLabel.text = "F1";
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateLabel(Transform parent)
    {
        var go = new GameObject("FloorLabel", typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = CachedBuiltinFont();
        text.fontSize = 13;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.raycastTarget = false;
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0f, 1f);
        text.rectTransform.pivot = new Vector2(0f, 1f);
        text.rectTransform.anchoredPosition = new Vector2(8f, -3f);
        text.rectTransform.sizeDelta = new Vector2(50f, 18f);
        return text;
    }

    private static Font CachedBuiltinFont()
    {
        if (builtinFont == null)
            builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return builtinFont;
    }

    private void FindPlayer()
    {
        if (player != null) return;
        GameObject p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;
    }
}
