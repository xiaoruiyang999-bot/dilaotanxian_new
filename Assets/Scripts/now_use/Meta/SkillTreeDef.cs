using System.Collections.Generic;
using UnityEngine;

/// <summary>技能树节点效果类型（v1.1.47）。百分比效果单位为 %（8 = +8%）。</summary>
public enum SkillTreeNodeEffect
{
    DamagePct,       // 伤害 +%（乘进 PlayerStats.Attack）
    MoveSpeedPct,    // 移速 +%
    AttackSpeedPct,  // 攻速 +%
    HpFlat,          // HP 上限 +N
    ArmorFlat,       // 护甲上限 +N
    CritDamagePct    // 暴击伤害 +%
}

/// <summary>技能树节点定义（纯代码定义，v1.1.47：无 SO 资产依赖，图标走支线色块——SkillData.IconColor 先例；后续要编辑器化再迁 SO）。</summary>
public class SkillTreeNodeDef
{
    public int id;                      // branch*10 + tier
    public string name;
    public SkillTreeNodeEffect effect;
    public float value;                 // 百分比效果单位 %
    public int cost;                    // 魂晶消耗
    public int requiresId;              // 前置节点 id；-1 = 线首无前置
    public int branch;                  // 0 力量 / 1 迅捷 / 2 坚韧
    public int tier;                    // 0~3，从左到右
}

/// <summary>
/// 技能树定义与效果聚合（v1.1.47）：三条支线 × 四层，线内线性前置，跨线独立。
/// 数值定位：全树点满约 +30% 伤害 / +14% 移速 / +20% 攻速 / +50 HP / +25 甲 / +25% 暴伤——
/// 数局可点满一层线，全线毕业是长期目标。消耗 3/5/8/12（死亡每击杀 1 魂晶，一局约 20~40）。
/// </summary>
public static class SkillTreeDef
{
    public const int BranchCount = 3;
    public const int TierCount = 4;

    public static readonly Color[] BranchColors =
    {
        new Color(0.90f, 0.35f, 0.30f),   // 力量·赤
        new Color(0.35f, 0.80f, 0.45f),   // 迅捷·翠
        new Color(0.35f, 0.60f, 0.90f),   // 坚韧·靛
    };

    public static readonly string[] BranchNames = { "力 量", "迅 捷", "坚 韧" };

    public static readonly SkillTreeNodeDef[] Nodes =
    {
        // 力量线（branch 0）
        new SkillTreeNodeDef { id = 0,  name = "重击 I",   effect = SkillTreeNodeEffect.DamagePct,     value = 8f,  cost = 3,  requiresId = -1, branch = 0, tier = 0 },
        new SkillTreeNodeDef { id = 1,  name = "重击 II",  effect = SkillTreeNodeEffect.DamagePct,     value = 10f, cost = 5,  requiresId = 0,  branch = 0, tier = 1 },
        new SkillTreeNodeDef { id = 2,  name = "重击 III", effect = SkillTreeNodeEffect.DamagePct,     value = 12f, cost = 8,  requiresId = 1,  branch = 0, tier = 2 },
        new SkillTreeNodeDef { id = 3,  name = "致命精髓", effect = SkillTreeNodeEffect.CritDamagePct, value = 25f, cost = 12, requiresId = 2,  branch = 0, tier = 3 },
        // 迅捷线（branch 1）
        new SkillTreeNodeDef { id = 10, name = "轻步 I",   effect = SkillTreeNodeEffect.MoveSpeedPct,   value = 6f,  cost = 3,  requiresId = -1, branch = 1, tier = 0 },
        new SkillTreeNodeDef { id = 11, name = "迅刀 I",   effect = SkillTreeNodeEffect.AttackSpeedPct, value = 8f,  cost = 5,  requiresId = 10, branch = 1, tier = 1 },
        new SkillTreeNodeDef { id = 12, name = "轻步 II",  effect = SkillTreeNodeEffect.MoveSpeedPct,   value = 8f,  cost = 8,  requiresId = 11, branch = 1, tier = 2 },
        new SkillTreeNodeDef { id = 13, name = "迅刀 II",  effect = SkillTreeNodeEffect.AttackSpeedPct, value = 12f, cost = 12, requiresId = 12, branch = 1, tier = 3 },
        // 坚韧线（branch 2）
        new SkillTreeNodeDef { id = 20, name = "硬皮 I",   effect = SkillTreeNodeEffect.HpFlat,         value = 20f, cost = 3,  requiresId = -1, branch = 2, tier = 0 },
        new SkillTreeNodeDef { id = 21, name = "铁骨 I",   effect = SkillTreeNodeEffect.ArmorFlat,      value = 10f, cost = 5,  requiresId = 20, branch = 2, tier = 1 },
        new SkillTreeNodeDef { id = 22, name = "硬皮 II",  effect = SkillTreeNodeEffect.HpFlat,         value = 30f, cost = 8,  requiresId = 21, branch = 2, tier = 2 },
        new SkillTreeNodeDef { id = 23, name = "铁骨 II",  effect = SkillTreeNodeEffect.ArmorFlat,      value = 15f, cost = 12, requiresId = 22, branch = 2, tier = 3 },
    };

    public static SkillTreeNodeDef Find(int id)
    {
        foreach (var n in Nodes) if (n.id == id) return n;
        return null;
    }

    /// <summary>节点效果描述（UI 显示）。</summary>
    public static string Describe(SkillTreeNodeDef node)
    {
        switch (node.effect)
        {
            case SkillTreeNodeEffect.DamagePct: return $"伤害 +{node.value:0}%";
            case SkillTreeNodeEffect.MoveSpeedPct: return $"移速 +{node.value:0}%";
            case SkillTreeNodeEffect.AttackSpeedPct: return $"攻速 +{node.value:0}%";
            case SkillTreeNodeEffect.HpFlat: return $"生命上限 +{node.value:0}";
            case SkillTreeNodeEffect.ArmorFlat: return $"护甲上限 +{node.value:0}";
            case SkillTreeNodeEffect.CritDamagePct: return $"暴击伤害 +{node.value:0}%";
            default: return node.name;
        }
    }

    /// <summary>解锁前置是否满足（线首恒真）。</summary>
    public static bool RequirementMet(SkillTreeNodeDef node, IReadOnlyList<int> unlockedIds)
    {
        if (node.requiresId < 0) return true;
        foreach (int id in unlockedIds) if (id == node.requiresId) return true;
        return false;
    }

    /// <summary>聚合已解锁节点的全部加成（PlayerStats.RefreshSkillTreeBonuses 消费）。</summary>
    public static void Aggregate(IReadOnlyList<int> unlockedIds,
        out float damagePct, out float moveSpeedPct, out float attackSpeedPct,
        out float hpFlat, out float armorFlat, out float critDamagePct)
    {
        damagePct = moveSpeedPct = attackSpeedPct = hpFlat = armorFlat = critDamagePct = 0f;
        foreach (int id in unlockedIds)
        {
            var node = Find(id);
            if (node == null) continue;   // 容错：存档里的未知 id 忽略
            switch (node.effect)
            {
                case SkillTreeNodeEffect.DamagePct: damagePct += node.value; break;
                case SkillTreeNodeEffect.MoveSpeedPct: moveSpeedPct += node.value; break;
                case SkillTreeNodeEffect.AttackSpeedPct: attackSpeedPct += node.value; break;
                case SkillTreeNodeEffect.HpFlat: hpFlat += node.value; break;
                case SkillTreeNodeEffect.ArmorFlat: armorFlat += node.value; break;
                case SkillTreeNodeEffect.CritDamagePct: critDamagePct += node.value; break;
            }
        }
    }
}
