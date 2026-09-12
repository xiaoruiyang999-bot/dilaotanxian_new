using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// v2.0.6 第三批 商店目录（V2 §13.3）：星蓝币购买的商品池与定价（纯函数，seed 复现）。
/// 定价按层数缩放（V2：价格按稀有度和当前层数缩放——MVP 线性 floorScale）；
/// 刷新重掷目录（一次，调用方管次数）。同一次 Run 内已购商品由交互层标记，不进纯数据。
/// </summary>
public static class ShopService
{
    public enum GoodsKind { HealPotion, ArmorKit, AttackCharm, MaxHpCharm }

    public struct Goods
    {
        public GoodsKind Kind;
        public int Price;          // 星蓝币
        public string Title;
        public string Description;
        public Color Color;
        public float Value;        // 效果量（治疗/护甲/攻强比/生命上限）
    }

    /// <summary>基础定价（V2 §13.3 首轮测试值，按层数线性 +15%/层）。</summary>
    private static int Price(int basePrice, int floor) => Mathf.RoundToInt(basePrice * (1f + 0.15f * Mathf.Max(0, floor - 1)));

    /// <summary>按商店 seed 掷 3 件商品（治疗/护甲/攻强——必含治疗保底，防止商店全攻击件）。</summary>
    public static List<Goods> Roll(int shopSeed, int floor)
    {
        var rng = new System.Random(shopSeed * 131 + floor * 17);
        float heal = rng.Next(35, 51);
        float armor = rng.Next(12, 21);
        float atk = rng.Next(10, 16) / 100f;

        var list = new List<Goods>
        {
            new Goods
            {
                Kind = GoodsKind.HealPotion, Value = heal, Price = Price(25, floor),
                Title = "治疗药剂", Description = $"回复 {heal:0} 点生命",
                Color = new Color(0.4f, 0.85f, 0.5f),
            },
            new Goods
            {
                Kind = GoodsKind.ArmorKit, Value = armor, Price = Price(15, floor),
                Title = "护甲补给", Description = $"护甲 +{armor:0}",
                Color = new Color(0.55f, 0.75f, 1f),
            },
            new Goods
            {
                Kind = GoodsKind.AttackCharm, Value = atk, Price = Price(40, floor),
                Title = "磨砺护符", Description = $"本局攻击 +{atk * 100f:0}%",
                Color = new Color(0.9f, 0.45f, 0.35f),
            },
        };

        // 陈列顺序洗牌（seed 驱动）
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    /// <summary>刷新价（V2 §13.3：一次刷新；价格随次数上涨——MVP 固定 15 币×层缩放）。</summary>
    public static int RefreshPrice(int floor) => Price(15, floor);
}
