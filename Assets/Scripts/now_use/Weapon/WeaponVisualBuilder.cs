using UnityEngine;

/// <summary>
/// 武器视觉构建器（v0.6.3，计划书 §四 / 美术清单 §二）：运行时程序化多色块拼接，无 prefab。
/// - BuildHeldVisual：手持视觉根 "WeaponVisual"，调用方挂 WeaponPivot 下并设 localRotation -90°
///   （与现有 WeaponSprite 同约定），所以部件沿 +Y 向上排布，手握处在 y≈0；
///   每把武器在最上端部件处带 "Effect" 子物体（PlayerCombat 蓄力时将其 color lerp 到白）；
/// - BuildMapIcon：同款缩略构建 + 底部职业色底板，根缩放内置 ≈0.7（WeaponPickup.Drop 调用）。
/// 复用 ProjectileVisualBuilder 的白图/圆形缓存。
/// </summary>
public static class WeaponVisualBuilder
{
    // 调色板（美术清单 1.2）
    private static readonly Color silver = new Color(0.741f, 0.765f, 0.780f);       // #BDC3C7 刃面
    private static readonly Color silverLight = new Color(0.835f, 0.859f, 0.867f);  // #D5DBDB 刃尖浅银
    private static readonly Color darkGray = new Color(0.498f, 0.549f, 0.553f);     // #7F8C8D 刃背/弹匣
    private static readonly Color gold = new Color(0.945f, 0.769f, 0.059f);         // #F1C40F 装饰金
    private static readonly Color darkGold = new Color(0.718f, 0.584f, 0.043f);     // #B7950B 暗金
    private static readonly Color brown = new Color(0.553f, 0.431f, 0.388f);        // #8D6E63 木质
    private static readonly Color deepBrown = new Color(0.365f, 0.251f, 0.216f);    // #5D4037 深棕握柄
    private static readonly Color warriorRed = new Color(0.753f, 0.224f, 0.169f);   // #C0392B 战士红
    private static readonly Color archerGreen = new Color(0.153f, 0.682f, 0.376f);  // #27AE60 射手绿
    private static readonly Color emerald = new Color(0.180f, 0.800f, 0.443f);      // #2ECC71 翠绿（治疗）
    private static readonly Color lightGreen = new Color(0.663f, 0.875f, 0.749f);   // #A9DFBF 浅绿
    private static readonly Color manaBlue = new Color(0.204f, 0.596f, 0.859f);     // #3498DB 法力蓝
    private static readonly Color manaLight = new Color(0.522f, 0.757f, 0.914f);    // #85C1E9 浅蓝
    private static readonly Color magePurple = new Color(0.557f, 0.267f, 0.678f);   // #8E44AD 法师蓝紫
    private static readonly Color indigo = new Color(0.357f, 0.173f, 0.435f);       // #5B2C6F 靛蓝杖杆
    private static readonly Color offWhite = new Color(0.925f, 0.941f, 0.945f);     // #ECF0F1 白弦

    /// <summary>
    /// 构建手持武器视觉：根命名 "WeaponVisual"，部件沿 +Y 排布（手握处 y≈0；弓例外，见 BuildBow）。
    /// 按（职业 × 行为类型 × 蓄力规则）数据分发——与 DisplayName 解耦，武器改名不影响视觉。
    /// </summary>
    public static GameObject BuildHeldVisual(WeaponData data)
    {
        GameObject root = new GameObject("WeaponVisual");
        if (data == null)
        {
            BuildFallback(root.transform, Color.white);
            return root;
        }

        // 数据驱动分发：六把武器在（职业, 行为, 蓄力规则）上唯一
        if (data.RequiredClass == ClassType.Warrior && data.BehaviorType == WeaponBehaviorType.Melee)
        {
            if (data.ChargeRule == ChargeRule.RectScale) BuildSpear(root.transform);
            else BuildKnife(root.transform);
        }
        else if (data.RequiredClass == ClassType.Archer && data.BehaviorType == WeaponBehaviorType.Ranged)
        {
            if (data.ChargeRule == ChargeRule.ProjectileBoost) BuildBow(root.transform);
            else BuildCrossbow(root.transform);
        }
        else if (data.RequiredClass == ClassType.Mage && data.BehaviorType == WeaponBehaviorType.SelfCast)
        {
            BuildHealingStaff(root.transform);
        }
        else if (data.RequiredClass == ClassType.Mage && data.BehaviorType == WeaponBehaviorType.Ranged)
        {
            BuildEnergyStaff(root.transform);
        }
        else
        {
            BuildFallback(root.transform, data.WeaponColor);
        }
        return root;
    }

    /// <summary>
    /// 构建地图掉落小图标（美术清单 2.7）：同款武器构建 + 底部职业色底板（0.4×0.12），
    /// 根 localScale 内置 0.7（调用方不要再缩放）。
    /// </summary>
    public static GameObject BuildMapIcon(WeaponData data)
    {
        GameObject root = BuildHeldVisual(data);
        root.name = "WeaponMapIcon";

        // 职业色底板（武器下方，远距离可识"地上有把某职业武器"）
        Color plateColor = data != null ? GetClassColor(data.RequiredClass) : darkGray;
        CreatePart(root.transform, "ClassPlate", new Vector2(0.4f, 0.12f),
            new Vector3(0f, -0.12f), plateColor, sortingOrder: 2);

        root.transform.localScale = Vector3.one * 0.7f;
        return root;
    }

    /// <summary>职业色映射：战士红 / 射手绿 / 法师蓝紫。</summary>
    private static Color GetClassColor(ClassType classType)
    {
        switch (classType)
        {
            case ClassType.Warrior: return warriorRed;
            case ClassType.Archer: return archerGreen;
            case ClassType.Mage: return magePurple;
            default: return darkGray;
        }
    }

    // ========== 六把武器（部件下→上，沿 +Y，手握处 y≈0） ==========

    /// <summary>刀（战士 · 近战扇形）：深棕柄 → 金黄护手+红宝石 → 银灰刃+浅银尖，长 ≈1.2。</summary>
    private static void BuildKnife(Transform root)
    {
        CreatePart(root, "Handle", new Vector2(0.09f, 0.28f), new Vector3(0f, 0.14f), deepBrown);
        CreatePart(root, "Guard", new Vector2(0.26f, 0.06f), new Vector3(0f, 0.31f), gold);
        CreatePart(root, "GuardGem", new Vector2(0.08f, 0.08f), new Vector3(0f, 0.31f), warriorRed,
            sortingOrder: 4);
        CreatePart(root, "Blade", new Vector2(0.14f, 0.62f), new Vector3(0f, 0.65f), silver);
        CreatePart(root, "BladeTip", new Vector2(0.10f, 0.16f), new Vector3(0f, 1.04f), silverLight);
        // 蓄力发光位：刃尖（银灰，计划书 §四）
        CreatePart(root, "Effect", new Vector2(0.12f, 0.12f), new Vector3(0f, 1.04f), silver,
            sortingOrder: 5);
    }

    /// <summary>枪矛（战士 · 近战矩形）：金属柄尾 → 深棕长杆 → 暗金环+红缨 → 银灰枪头，长 ≈1.8。</summary>
    private static void BuildSpear(Transform root)
    {
        CreatePart(root, "ButtEnd", new Vector2(0.10f, 0.10f), new Vector3(0f, 0.05f), darkGray);
        CreatePart(root, "Shaft", new Vector2(0.07f, 1.15f), new Vector3(0f, 0.675f), deepBrown);
        CreatePart(root, "Collar", new Vector2(0.14f, 0.08f), new Vector3(0f, 1.29f), darkGold);
        CreatePart(root, "Tassel", new Vector2(0.06f, 0.14f), new Vector3(0.09f, 1.38f), warriorRed,
            sortingOrder: 4);
        CreatePart(root, "SpearHead", new Vector2(0.14f, 0.45f), new Vector3(0f, 1.555f), silver);
        // 蓄力发光位：枪头（银灰）
        CreatePart(root, "Effect", new Vector2(0.12f, 0.12f), new Vector3(0f, 1.70f), silver,
            sortingOrder: 5);
    }

    /// <summary>
    /// 弓箭（射手 · 远程蓄力）：竖持长弓——弓把/弧臂沿 builder ±X 一条直线（-90° 挂载后与弹道垂直），
    /// 梢端反曲小片 + 弓弦在后（-Y 朝玩家）+ 箭矢沿 +Y 指向目标。
    /// </summary>
    private static void BuildBow(Transform root)
    {
        // 弓把（弓身中段，与两臂一条直线）
        CreatePart(root, "Grip", new Vector2(0.22f, 0.10f), new Vector3(0f, 0f), archerGreen);
        // 两段直臂（不旋转，避免交叠成"X"），梢端加反曲小片
        CreatePart(root, "BowLimbRight", new Vector2(0.48f, 0.07f), new Vector3(0.32f, 0f), deepBrown);
        CreatePart(root, "BowLimbLeft", new Vector2(0.48f, 0.07f), new Vector3(-0.32f, 0f), deepBrown);
        CreatePart(root, "RecurveRight", new Vector2(0.14f, 0.06f), new Vector3(0.58f, 0.05f), deepBrown,
            rotation: 25f);
        CreatePart(root, "RecurveLeft", new Vector2(0.14f, 0.06f), new Vector3(-0.58f, 0.05f), deepBrown,
            rotation: -25f);
        // 弓弦：在弓臂后方（-Y，靠玩家一侧）
        CreatePart(root, "BowString", new Vector2(0.92f, 0.025f), new Vector3(0f, -0.14f), offWhite,
            sortingOrder: 4);
        // 箭矢：沿 +Y 指向目标（杆 + 银箭头）
        CreatePart(root, "ArrowShaft", new Vector2(0.045f, 0.5f), new Vector3(0f, 0.16f), brown,
            sortingOrder: 4);
        CreatePart(root, "ArrowHead", new Vector2(0.09f, 0.10f), new Vector3(0f, 0.44f), silver,
            sortingOrder: 4);
        // 蓄力发光位：箭簇处（绿色，计划书 §四）
        CreatePart(root, "Effect", new Vector2(0.12f, 0.12f), new Vector3(0f, 0.44f), archerGreen,
            sortingOrder: 5);
    }

    /// <summary>连弩（射手 · 远程连发）：绿把 → 深棕机身+灰弹匣 → 金黄双臂，0.9×0.7 横宽剪影。</summary>
    private static void BuildCrossbow(Transform root)
    {
        CreatePart(root, "Grip", new Vector2(0.09f, 0.18f), new Vector3(0f, 0.09f), archerGreen);
        CreatePart(root, "Rail", new Vector2(0.12f, 0.72f), new Vector3(0f, 0.54f), deepBrown);
        CreatePart(root, "Magazine", new Vector2(0.18f, 0.14f), new Vector3(0f, 0.30f), darkGray,
            sortingOrder: 4);
        CreatePart(root, "CrossbowArmLeft", new Vector2(0.30f, 0.07f), new Vector3(-0.17f, 0.84f), gold,
            rotation: 20f);
        CreatePart(root, "CrossbowArmRight", new Vector2(0.30f, 0.07f), new Vector3(0.17f, 0.84f), gold,
            rotation: -20f);
        // Effect 统一放最上端部件处（连弩无蓄力，仅保持结构一致）
        CreatePart(root, "Effect", new Vector2(0.12f, 0.12f), new Vector3(0f, 0.86f), gold,
            sortingOrder: 5);
    }

    /// <summary>治疗法杖（法师 · 自身回复）：柄尾 → 靛蓝杖杆 → 金环 → 翠绿珠+浅绿环片，长 ≈1.3。</summary>
    private static void BuildHealingStaff(Transform root)
    {
        CreatePart(root, "ButtEnd", new Vector2(0.10f, 0.08f), new Vector3(0f, 0.04f), darkGold);
        CreatePart(root, "Shaft", new Vector2(0.08f, 0.85f), new Vector3(0f, 0.505f), indigo);
        CreatePart(root, "HeadRing", new Vector2(0.18f, 0.06f), new Vector3(0f, 0.96f), gold);
        CreatePart(root, "StaffHead", new Vector2(0.24f, 0.24f), new Vector3(0f, 1.14f), emerald,
            circle: true);
        CreatePart(root, "HeadRingletL", new Vector2(0.06f, 0.06f), new Vector3(-0.15f, 1.14f), lightGreen,
            circle: true, sortingOrder: 4);
        CreatePart(root, "HeadRingletR", new Vector2(0.06f, 0.06f), new Vector3(0.15f, 1.14f), lightGreen,
            circle: true, sortingOrder: 4);
        // Effect 放头部宝珠位（翠绿）
        CreatePart(root, "Effect", new Vector2(0.12f, 0.12f), new Vector3(0f, 1.14f), emerald,
            circle: true, sortingOrder: 5);
    }

    /// <summary>能量法杖（法师 · 远程单发）：柄尾 → 靛蓝杖杆 → 金三爪 → 蓝珠+浅蓝芯，长 ≈1.3。</summary>
    private static void BuildEnergyStaff(Transform root)
    {
        CreatePart(root, "ButtEnd", new Vector2(0.10f, 0.08f), new Vector3(0f, 0.04f), darkGold);
        CreatePart(root, "Shaft", new Vector2(0.08f, 0.85f), new Vector3(0f, 0.505f), indigo);
        CreatePart(root, "ProngL", new Vector2(0.05f, 0.18f), new Vector3(-0.10f, 1.00f), gold,
            rotation: 25f);
        CreatePart(root, "ProngM", new Vector2(0.05f, 0.18f), new Vector3(0f, 1.03f), gold);
        CreatePart(root, "ProngR", new Vector2(0.05f, 0.18f), new Vector3(0.10f, 1.00f), gold,
            rotation: -25f);
        CreatePart(root, "OrbCore", new Vector2(0.22f, 0.22f), new Vector3(0f, 1.16f), manaBlue,
            circle: true);
        CreatePart(root, "OrbInner", new Vector2(0.11f, 0.11f), new Vector3(0f, 1.16f), manaLight,
            circle: true, sortingOrder: 4);
        // Effect 放宝珠位（蓝）
        CreatePart(root, "Effect", new Vector2(0.12f, 0.12f), new Vector3(0f, 1.16f), manaBlue,
            circle: true, sortingOrder: 5);
    }

    /// <summary>兜底：weaponColor 单色长条 + 柄 + Effect（未知武器名时退化呈现）。</summary>
    private static void BuildFallback(Transform root, Color color)
    {
        CreatePart(root, "Handle", new Vector2(0.09f, 0.24f), new Vector3(0f, 0.12f), deepBrown);
        CreatePart(root, "Blade", new Vector2(0.12f, 0.76f), new Vector3(0f, 0.62f), color);
        CreatePart(root, "Effect", new Vector2(0.12f, 0.12f), new Vector3(0f, 0.95f), color,
            sortingOrder: 5);
    }

    /// <summary>创建一个染色方块/圆形部件（独立 SpriteRenderer，便于染色/闪烁）。</summary>
    private static SpriteRenderer CreatePart(Transform parent, string name, Vector2 size, Vector3 localPos,
        Color color, float rotation = 0f, bool circle = false, int sortingOrder = 3)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = circle ? ProjectileVisualBuilder.GetCircleSprite() : ProjectileVisualBuilder.GetWhiteSprite();
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        return sr;
    }
}
