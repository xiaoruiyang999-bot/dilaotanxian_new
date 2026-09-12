using UnityEngine;

/// <summary>
/// VS 第四批 遗物定义（V2 §8.1 构筑第三层：规则联动与跨系统触发）。
/// MVP 首批方向全部围绕状态系统（流血/破甲）与怒痕（V2 §6.2 首批构筑方向）——
/// 数值效果由 RelicEffects 集中消费（本 SO 只描述，不改数值）。
/// relicId 即合同，遗物实例（本局持有）由 RunStateCarrier 持有 id 列表。
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/Relic", fileName = "Relic_")]
public class RelicDefinition : ScriptableObject
{
    public enum EffectKind
    {
        BleedDeepen,       // 流血每跳伤害 +X%
        BleedLinger,       // 流血单层持续时间 +X 秒
        ArmorBreakExtend,  // 破甲持续时间 +X 秒
        RageFaster,        // 怒痕获取 +X%（时间与伤害恢复均吃）
        BeastLonger,       // 兽化持续时间 +X 秒
        CoinMagnet,        // 星蓝币掉落价值 +X%（拾取入账翻倍概率近似：+30%=30% 概率双倍）
    }

    [Header("身份")]
    public string relicId;
    public string displayName;
    [TextArea] public string description;

    [Header("效果（一枚一效，MVP 口径）")]
    public EffectKind effect;
    [Tooltip("效果量：百分比类填 0.3=30%；秒数类直接填秒")]
    public float value;
    public Color tint = new Color(0.8f, 0.65f, 0.35f);
}
