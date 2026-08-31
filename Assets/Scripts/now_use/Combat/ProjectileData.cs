using UnityEngine;

/// <summary>
/// 子弹视觉类型（v0.6.3）。
/// Arrow：箭矢（弓箭）；Bolt：弩矢（连弩）；
/// EnergyOrb：能量弹（能量法杖）；SpiritOrb：精灵弹（v0.6.5 宠物预留）。
/// </summary>
public enum ProjectileVisualKind
{
    Arrow = 0,
    Bolt = 1,
    EnergyOrb = 2,
    SpiritOrb = 3
}

/// <summary>
/// 子弹配置数据（v0.6.3，计划书 §三 / 美术清单 §三）。纯数据容器。
/// 视觉由 ProjectileVisualBuilder 运行时按 visualKind 程序化拼接（多色块，无 prefab）。
/// </summary>
[CreateAssetMenu(fileName = "ProjectileData", menuName = "Combat/Projectile Data")]
public class ProjectileData : ScriptableObject
{
    [SerializeField] private string displayName;
    [Tooltip("飞行速度（单位/秒）")]
    [SerializeField] private float speed = 12f;
    [SerializeField] private float damage = 1f;
    [Tooltip("存活时间兜底（秒），到期自毁")]
    [SerializeField] private float lifetime = 3f;
    [Tooltip("碰撞半径（CircleCollider2D）")]
    [SerializeField] private float radius = 0.12f;
    [SerializeField] private ProjectileVisualKind visualKind = ProjectileVisualKind.Arrow;
    [Tooltip("主体配色（命中特效染色也用此色）")]
    [SerializeField] private Color bodyColor = Color.white;
    [Tooltip("点缀配色（内芯/高光等）")]
    [SerializeField] private Color accentColor = Color.gray;
    [Tooltip("可命中目标层（Enemy+Obstacle）")]
    [SerializeField] private LayerMask targetLayer;

    public string DisplayName => displayName;
    public float Speed => speed;
    public float Damage => damage;
    public float Lifetime => lifetime;
    public float Radius => radius;
    public ProjectileVisualKind VisualKind => visualKind;
    public Color BodyColor => bodyColor;
    public Color AccentColor => accentColor;
    public LayerMask TargetLayer => targetLayer;

    private void OnValidate()
    {
        // 钳制正数，防 Inspector 误填 0/负值
        if (speed < 0.01f) speed = 0.01f;
        if (damage < 0f) damage = 0f;
        if (lifetime < 0.1f) lifetime = 0.1f;
        if (radius < 0.01f) radius = 0.01f;
    }
}
