using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v2.0.6 第二批 节点奖励池（V2 §8.4/§13.2）：普通战斗节点完成弹三选一。
/// MVP 三类：星蓝币袋 / 治疗药剂 / 攻击强化（本局乘数）；精英取高阶值。
/// 纯函数（seed 驱动，可 EditMode 测试）；应用由 DungeonNodeRunner 执行（UI/服务分层）。
/// </summary>
public static class NodeRewardService
{
    public enum RewardKind { Coins, Heal, AttackUp, WeaponVariant }

    public struct RewardOption
    {
        public RewardKind Kind;
        public float Value;        // 币数 / 治疗量 / 攻击乘数增量
        public string Title;
        public string Description;
        public Color Color;
        public WeaponData Weapon;  // WeaponVariant 项：变体数据（其余为 null）
    }

    /// <summary>
    /// 按节点 seed 抽 3~4 个不重复奖励（V2 §7.2/§8.4：基础三类各一；35% 概率追加武器变体项——
    /// 变体从狼人武器池轮换，由调用方填 WeaponData；精英数值翻倍/高阶攻强）。
    /// </summary>
    public static List<RewardOption> Roll(int nodeSeed, bool elite)
    {
        var rng = new System.Random(nodeSeed * 31 + (elite ? 7 : 13));
        float mul = elite ? 2f : 1f;
        int coins = Mathf.RoundToInt(rng.Next(15, 26) * mul);
        int heal = Mathf.RoundToInt(rng.Next(18, 31) * mul);
        float atk = elite ? 0.25f : 0.12f;

        var options = new List<RewardOption>
        {
            new RewardOption
            {
                Kind = RewardKind.Coins, Value = coins,
                Title = elite ? "丰厚钱袋" : "星蓝币袋",
                Description = $"随身星蓝币 +{coins}",
                Color = new Color(0.55f, 0.75f, 1f),
            },
            new RewardOption
            {
                Kind = RewardKind.Heal, Value = heal,
                Title = elite ? "秘制药剂" : "治疗药剂",
                Description = $"立即回复 {heal} 点生命",
                Color = new Color(0.4f, 0.85f, 0.5f),
            },
            new RewardOption
            {
                Kind = RewardKind.AttackUp, Value = atk,
                Title = elite ? "嗜血锋刃" : "磨砺之刃",
                Description = $"本局攻击 +{Mathf.RoundToInt(atk * 100f)}%",
                Color = new Color(0.9f, 0.45f, 0.35f),
            },
        };

        // VS 第二批：35% 概率追加武器变体项（变体数据由调用方随后填入——纯函数不碰 Resources）
        if (rng.NextDouble() < 0.35)
        {
            options.Add(new RewardOption
            {
                Kind = RewardKind.WeaponVariant,
                Title = "未知刺刀",
                Description = "一把变异刺刀——拾取后揭晓",
                Color = new Color(0.8f, 0.7f, 0.5f),
            });
        }

        for (int i = options.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (options[i], options[j]) = (options[j], options[i]);
        }
        return options;
    }

    /// <summary>给 WeaponVariant 项填入变体数据（从角色武器池剔除当前已持武器后轮换）。</summary>
    public static void FillWeaponVariant(ref RewardOption option,
        System.Collections.Generic.IReadOnlyList<WeaponData> pool, WeaponData current, int roll)
    {
        if (option.Kind != RewardKind.WeaponVariant || pool == null || pool.Count == 0) return;
        var candidates = new System.Collections.Generic.List<WeaponData>();
        foreach (WeaponData w in pool)
            if (w != null && w != current) candidates.Add(w);
        if (candidates.Count == 0) return;
        WeaponData pick = candidates[roll % candidates.Count];
        option.Weapon = pick;
        option.Title = pick.DisplayName;
        option.Description = $"更换武器：{pick.DisplayName}（原武器收入背包）";
    }
}
