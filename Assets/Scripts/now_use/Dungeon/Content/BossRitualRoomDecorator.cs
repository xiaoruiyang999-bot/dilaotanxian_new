using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 固定 Boss 仪式厅表现层。优先消费 Resources/Art/SpecialRooms/BossRitual 下的正式素材，
/// 缺失时用现有石块/木桶与程序线条搭出完整占位构图；不添加额外碰撞，不消费随机数。
/// </summary>
public static class BossRitualRoomDecorator
{
    private const string SpecialRoot = "Art/SpecialRooms/BossRitual/";
    public const float RewardMinSeparation = 2.75f;
    private const string WallPropsRoot = "Art/Decor/WallProps/";
    private const string StonesRoot = "Art/Decor/Stones/";
    private const string BarrelsRoot = "Art/Decor/Barrels/";

    private static Sprite whiteSprite;
    private static Material lineMaterial;

    /// <summary>搭建固定表现并返回固定敌人世界坐标插槽。</summary>
    public static IReadOnlyList<Vector3> Build(Room room, BossRitualRoomLayout layout)
    {
        if (room == null || layout == null) return new List<Vector3>();

        var rootGo = new GameObject("BossRitualRoom_Fixed");
        rootGo.transform.SetParent(room.ContentRoot, false);
        rootGo.transform.localPosition = Vector3.zero;

        BuildRune(rootGo.transform, room, layout);
        BuildPillars(rootGo.transform, room, layout);
        BuildAltar(rootGo.transform, room, layout);
        BuildTorches(rootGo.transform, room, layout);
        BuildClutter(rootGo.transform, room, layout);
        BuildBonesSlot(rootGo.transform, room, layout);
        BuildRewardSockets(rootGo.transform, room, layout);

        return BossRitualRoomTemplate.BuildEnemySpawnPositions(layout);
    }

    private static void BuildRune(Transform root, Room room, BossRitualRoomLayout layout)
    {
        Transform slot = CreateSlot(root, "ART_SLOT_Rune", room, layout.Point(0f, 0.44f));
        Sprite formal = Resources.Load<Sprite>(SpecialRoot + "rune");
        if (formal != null)
        {
            CreateSprite(slot, "Rune_Formal", formal,
                new Vector2(layout.RitualDiameter, layout.RitualDiameter), 1,
                new Color(0.78f, 0.82f, 0.82f, 0.78f), layout.RotationDegrees);
            return;
        }

        slot.name += "_MISSING";
        float radius = layout.RitualDiameter * 0.5f;
        Color outer = new Color(0.52f, 0.60f, 0.62f, 0.58f);
        Color inner = new Color(0.66f, 0.70f, 0.68f, 0.62f);
        CreateCircle(slot, "Rune_Circle_Outer_Placeholder", radius, 48, 0.095f, outer);
        CreateCircle(slot, "Rune_Circle_Inner_Placeholder", radius * 0.78f, 40, 0.07f, outer);
        CreateRegularPolygon(slot, "Rune_Triangle_Up_Placeholder", 3, radius * 0.70f, 90f, 0.08f, inner);
        CreateRegularPolygon(slot, "Rune_Triangle_Down_Placeholder", 3, radius * 0.70f, -90f, 0.08f, inner);
        CreateCircle(slot, "Rune_Core_Placeholder", Mathf.Max(0.55f, radius * 0.11f), 20, 0.07f, inner);
    }

    private static void BuildPillars(Transform root, Room room, BossRitualRoomLayout layout)
    {
        Sprite formal = Resources.Load<Sprite>(SpecialRoot + "pillar");
        Sprite fallback = Resources.Load<Sprite>(WallPropsRoot + "prop_01");
        Sprite baseStone = Resources.Load<Sprite>(StonesRoot + "prop_31");
        IReadOnlyList<Vector2> centers = layout.PillarCenters;
        float width = 2f;
        float height = 2f;

        for (int i = 0; i < centers.Count; i++)
        {
            Transform slot = CreateSlot(root, $"ART_SLOT_Pillar_{i + 1:00}", room,
                centers[i]);
            if (formal != null)
                CreateSprite(slot, "Pillar_Formal", formal, new Vector2(width, height), 5,
                    Color.white, 0f);
            else
            {
                slot.name += "_MISSING";
                if (baseStone != null)
                    CreateSprite(slot, "Pillar_Base_Placeholder", baseStone,
                        new Vector2(width * 1.12f, width * 0.72f), 4,
                        new Color(0.70f, 0.76f, 0.70f), 0f);
                if (fallback != null)
                    CreateSprite(slot, "Pillar_Shaft_Placeholder", fallback,
                        new Vector2(width * 0.78f, height), 5,
                        new Color(0.64f, 0.71f, 0.67f), 0f);
                else
                    CreateBlock(slot, "Pillar_Block_Placeholder", new Vector2(width, height), 5,
                        new Color(0.24f, 0.30f, 0.29f, 1f), layout.RotationDegrees);
            }
        }
    }

    private static void BuildAltar(Transform root, Room room, BossRitualRoomLayout layout)
    {
        float k = layout.RitualDiameter;
        // 正立像素材保持屏幕朝上；正式生成器固定南入口，因此宽轴始终是世界 X。
        float rotation = 0f;
        Transform stairsSlot = CreateSlot(root, "ART_SLOT_Stairs", room, layout.Point(0f, 0.77f));
        Sprite stairs = Resources.Load<Sprite>(SpecialRoot + "stairs");
        if (stairs != null)
            CreateSprite(stairsSlot, "Stairs_Formal", stairs,
                new Vector2(k * 0.78f, Mathf.Max(2.4f, k * 0.25f)), 3, Color.white, rotation);
        else
        {
            stairsSlot.name += "_MISSING";
            Sprite stone = Resources.Load<Sprite>(StonesRoot + "prop_29");
            for (int i = 0; i < 3; i++)
            {
                float depth = 0.745f + i * 0.035f;
                Transform step = CreateSlot(stairsSlot, $"Step_{i + 1}_Placeholder", room,
                    layout.Point(0f, depth));
                float stepWidth = k * (0.76f - i * 0.11f);
                if (stone != null)
                    CreateSprite(step, "Stone_Step", stone,
                        new Vector2(stepWidth, Mathf.Max(1.1f, k * 0.10f)), 3 + i,
                        new Color(0.68f, 0.73f, 0.68f), rotation);
                else
                    CreateBlock(step, "Stone_Step_Block", new Vector2(stepWidth, 1.2f), 3 + i,
                        new Color(0.20f, 0.26f, 0.25f, 1f), rotation);
            }
        }

        Transform altarSlot = CreateSlot(root, "ART_SLOT_Altar", room, layout.Point(0f, 0.86f));
        Sprite altar = Resources.Load<Sprite>(SpecialRoot + "altar");
        if (altar != null)
            CreateSprite(altarSlot, "Altar_Formal", altar,
                new Vector2(k * 0.52f, Mathf.Max(2f, k * 0.20f)), 6, Color.white, rotation);
        else
        {
            altarSlot.name += "_MISSING";
            Sprite stone = Resources.Load<Sprite>(StonesRoot + "prop_30");
            if (stone != null)
                CreateSprite(altarSlot, "Altar_Stone_Placeholder", stone,
                    new Vector2(k * 0.50f, Mathf.Max(2f, k * 0.20f)), 6,
                    new Color(0.72f, 0.77f, 0.70f), rotation);
            else
                CreateBlock(altarSlot, "Altar_Block_Placeholder",
                    new Vector2(k * 0.50f, 2.2f), 6,
                    new Color(0.22f, 0.28f, 0.27f, 1f), rotation);
        }

        Transform bannerSlot = CreateSlot(root, "ART_SLOT_Banner", room, layout.Point(0f, 0.955f));
        Sprite banner = Resources.Load<Sprite>(SpecialRoot + "banner");
        if (banner != null)
            CreateSprite(bannerSlot, "Banner_Formal", banner,
                new Vector2(k * 0.42f, k * 0.40f), 2, Color.white, rotation);
        else
        {
            bannerSlot.name += "_MISSING";
            CreateBlock(bannerSlot, "Banner_Cloth_Placeholder",
                new Vector2(k * 0.38f, k * 0.34f), 2,
                new Color(0.10f, 0.12f, 0.16f, 0.96f), rotation);
            CreateRegularPolygon(bannerSlot, "Banner_Sigil_Placeholder", 3,
                k * 0.105f, 90f + rotation, 0.08f,
                new Color(0.40f, 0.48f, 0.52f, 0.85f), 3);
        }
    }

    private static void BuildTorches(Transform root, Room room, BossRitualRoomLayout layout)
    {
        Sprite formal = Resources.Load<Sprite>(SpecialRoot + "torch");
        var sockets = new[]
        {
            new Vector2(-0.18f, 0.84f), new Vector2(0.18f, 0.84f),
            new Vector2(-0.47f, 0.48f), new Vector2(0.47f, 0.48f),
            new Vector2(-0.37f, 0.12f), new Vector2(0.37f, 0.12f),
        };
        for (int i = 0; i < sockets.Length; i++)
        {
            Transform slot = CreateSlot(root, $"ART_SLOT_Torch_{i + 1:00}", room,
                layout.Point(sockets[i].x, sockets[i].y));
            if (formal != null)
            {
                CreateSprite(slot, "Torch_Formal", formal, new Vector2(1.4f, 2.2f), 8,
                    Color.white, 0f);
                continue;
            }

            slot.name += "_MISSING";
            CreateBlock(slot, "Torch_Sconce_Placeholder", new Vector2(0.30f, 1.0f), 7,
                new Color(0.22f, 0.20f, 0.17f, 1f), 0f);
            CreateBlock(slot, "Torch_Flame_Outer_Placeholder", new Vector2(0.82f, 0.82f), 8,
                new Color(1f, 0.30f, 0.04f, 0.72f), 45f);
            CreateBlock(slot, "Torch_Flame_Core_Placeholder", new Vector2(0.42f, 0.62f), 9,
                new Color(1f, 0.78f, 0.16f, 0.95f), 45f);
        }
    }

    private static void BuildClutter(Transform root, Room room, BossRitualRoomLayout layout)
    {
        var sockets = new[]
        {
            new Vector2(-0.40f, 0.74f), new Vector2(0.40f, 0.74f),
            new Vector2(-0.40f, 0.18f), new Vector2(0.40f, 0.18f),
        };
        var barrelNames = new[] { "barrel_18", "barrel_24", "barrel_22", "barrel_28" };
        var stoneNames = new[] { "prop_20", "prop_26", "prop_18", "prop_34" };
        for (int i = 0; i < sockets.Length; i++)
        {
            Transform slot = CreateSlot(root, $"Fixed_Clutter_{i + 1:00}", room,
                layout.Point(sockets[i].x, sockets[i].y));
            Sprite barrel = Resources.Load<Sprite>(BarrelsRoot + barrelNames[i]);
            if (barrel != null)
                CreateSprite(slot, "Barrel_Fixed", barrel, new Vector2(2.6f, 2.1f), 5,
                    Color.white, 0f);
            Sprite stone = Resources.Load<Sprite>(StonesRoot + stoneNames[i]);
            if (stone != null)
            {
                var stoneGo = CreateSprite(slot, "Rubble_Fixed", stone, new Vector2(1.8f, 1.3f), 4,
                    new Color(0.78f, 0.82f, 0.76f), 0f);
                float lateral = i % 2 == 0 ? 1.35f : -1.35f;
                Vector2 offset = (Vector2)layout.Right * lateral + (Vector2)layout.Forward * -0.65f;
                stoneGo.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            }
        }
    }

    private static void BuildBonesSlot(Transform root, Room room, BossRitualRoomLayout layout)
    {
        Transform slot = CreateSlot(root, "ART_SLOT_Bones", room, layout.Point(0.17f, 0.62f));
        Sprite bones = Resources.Load<Sprite>(SpecialRoot + "bones");
        if (bones != null)
        {
            CreateSprite(slot, "Bones_Formal", bones, new Vector2(2.8f, 1.8f), 4,
                Color.white, 0f);
            return;
        }
        slot.name += "_MISSING";
        var boneA = CreateBlock(slot, "Bones_Placeholder_A", new Vector2(1.45f, 0.16f), 4,
            new Color(0.69f, 0.66f, 0.52f, 0.72f), 28f + layout.RotationDegrees);
        Vector2 offsetA = (Vector2)layout.Right * -0.25f;
        boneA.transform.localPosition = new Vector3(offsetA.x, offsetA.y, 0f);
        var boneB = CreateBlock(slot, "Bones_Placeholder_B", new Vector2(1.15f, 0.15f), 4,
            new Color(0.69f, 0.66f, 0.52f, 0.72f), -38f + layout.RotationDegrees);
        Vector2 offsetB = (Vector2)layout.Right * 0.3f + (Vector2)layout.Forward * -0.15f;
        boneB.transform.localPosition = new Vector3(offsetB.x, offsetB.y, 0f);
    }

    private static void BuildRewardSockets(Transform root, Room room, BossRitualRoomLayout layout)
    {
        Transform socketsRoot = CreateSlot(root, "RewardSockets", room, room.Center);
        List<Vector3> positions = BossRitualRoomTemplate.BuildRewardSpawnPositions(layout);
        for (int i = 0; i + 1 < positions.Count; i += 2)
        {
            CreateSlot(socketsRoot, $"RewardChest_{i / 2}", room, positions[i]);
            CreateSlot(socketsRoot, $"ExitPortal_{i / 2}", room, positions[i + 1]);
        }
    }

    /// <summary>按固定优先级取首个通过 SpawnCells 来源 + NonAlloc 物理复核的奖励插槽。</summary>
    public static bool TryGetRewardSocket(Room room, bool portal, out Vector3 position,
        Vector3? reservedPosition = null, bool requirePhysicalClear = true)
    {
        position = default;
        if (room == null || room.ContentRoot == null) return false;
        string prefix = portal ? "ExitPortal_" : "RewardChest_";
        for (int i = 0; i < 3; i++)
        {
            Transform socket = room.ContentRoot.Find($"BossRitualRoom_Fixed/RewardSockets/{prefix}{i}");
            if (socket == null) continue;
            Vector3 candidate = socket.position;
            if (reservedPosition.HasValue
                && Vector3.Distance(candidate, reservedPosition.Value) < RewardMinSeparation) continue;
            if (requirePhysicalClear
                && !SpawnPositionHelper.IsFixedPositionClear(room, candidate)) continue;
            position = candidate;
            return true;
        }
        return false;
    }

    private static Transform CreateSlot(Transform parent, string name, Room room, Vector2 world)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        // 先挂父级再写世界坐标，嵌套在 Stairs 等子槽时也不会把房间偏移累计两次。
        go.transform.position = new Vector3(world.x, world.y, parent.position.z);
        return go.transform;
    }

    private static GameObject CreateSprite(Transform parent, string name, Sprite sprite,
        Vector2 targetSize, int sortingOrder, Color color, float rotation)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        Vector2 sourceSize = sprite != null ? sprite.bounds.size : Vector2.one;
        float scale = Mathf.Min(
            sourceSize.x > 0.0001f ? targetSize.x / sourceSize.x : 1f,
            sourceSize.y > 0.0001f ? targetSize.y / sourceSize.y : 1f);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        return go;
    }

    private static GameObject CreateBlock(Transform parent, string name, Vector2 size,
        int sortingOrder, Color color, float rotation)
    {
        GameObject go = CreateSprite(parent, name, WhiteSprite, Vector2.one, sortingOrder, color, rotation);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        return go;
    }

    private static void CreateCircle(Transform parent, string name, float radius,
        int segments, float width, Color color, int sortingOrder = 1)
    {
        var points = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.PI * 2f * i / segments;
            points[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
        }
        CreateLine(parent, name, points, true, width, color, sortingOrder);
    }

    private static void CreateRegularPolygon(Transform parent, string name, int sides,
        float radius, float startDegrees, float width, Color color, int sortingOrder = 1)
    {
        var points = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = (startDegrees + 360f * i / sides) * Mathf.Deg2Rad;
            points[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
        }
        CreateLine(parent, name, points, true, width, color, sortingOrder);
    }

    private static void CreateLine(Transform parent, string name, Vector3[] points,
        bool loop, float width, Color color, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.widthMultiplier = width;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.startColor = color;
        line.endColor = color;
        line.sortingOrder = sortingOrder;
        if (LineMaterial != null) line.sharedMaterial = LineMaterial;
    }

    private static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite != null) return whiteSprite;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "BossRitual_RuntimeWhite_Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            whiteSprite.name = "BossRitual_RuntimeWhite_Sprite";
            whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            return whiteSprite;
        }
    }

    private static Material LineMaterial
    {
        get
        {
            if (lineMaterial != null) return lineMaterial;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[BossRitualRoom] 找不到 Sprites/Default，程序法阵将使用 LineRenderer 默认材质。");
                return null;
            }
            lineMaterial = new Material(shader)
            {
                name = "BossRitual_Line_Material",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return lineMaterial;
        }
    }
}
