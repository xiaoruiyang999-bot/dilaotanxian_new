using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// v2.0.6 第三批 商店目录门禁（V2 §13.3）：三商品/必含治疗保底、层数定价缩放、
/// 描述值=应用值（数值自洽）、seed 复现、刷新价。
/// </summary>
public class ShopServiceTests
{
    [Test]
    public void Roll_ThreeGoods_AlwaysContainsHeal()
    {
        for (int seed = 1; seed <= 30; seed++)
        {
            List<ShopService.Goods> goods = ShopService.Roll(seed, floor: 1);
            Assert.AreEqual(3, goods.Count, $"seed={seed} 必须三货架");
            Assert.IsTrue(goods.Any(g => g.Kind == ShopService.GoodsKind.HealPotion),
                $"seed={seed} 治疗保底缺失");
            Assert.AreEqual(3, goods.Select(g => g.Kind).Distinct().Count(), $"seed={seed} 商品不得重复");
        }
    }

    [Test]
    public void Roll_PriceScalesWithFloor()
    {
        List<ShopService.Goods> f1 = ShopService.Roll(5, floor: 1);
        List<ShopService.Goods> f4 = ShopService.Roll(5, floor: 4);
        float heal1 = f1.Find(g => g.Kind == ShopService.GoodsKind.HealPotion).Price;
        float heal4 = f4.Find(g => g.Kind == ShopService.GoodsKind.HealPotion).Price;
        Assert.Greater(heal4, heal1, "层数越高定价越高（V2 §13.3 层数缩放）");
    }

    [Test]
    public void Roll_DescriptionMatchesValue()
    {
        List<ShopService.Goods> goods = ShopService.Roll(9, floor: 2);
        var heal = goods.Find(g => g.Kind == ShopService.GoodsKind.HealPotion);
        StringAssert.Contains(((int)heal.Value).ToString(), heal.Description, "治疗描述值=应用值");
        var armor = goods.Find(g => g.Kind == ShopService.GoodsKind.ArmorKit);
        StringAssert.Contains(((int)armor.Value).ToString(), armor.Description);
    }

    [Test]
    public void Roll_SameSeed_Deterministic()
    {
        List<ShopService.Goods> a = ShopService.Roll(21, floor: 3);
        List<ShopService.Goods> b = ShopService.Roll(21, floor: 3);
        for (int i = 0; i < 3; i++)
        {
            Assert.AreEqual(a[i].Kind, b[i].Kind);
            Assert.AreEqual(a[i].Price, b[i].Price);
            Assert.AreEqual(a[i].Value, b[i].Value);
        }
    }

    [Test]
    public void RefreshPrice_PositiveAndScales()
    {
        Assert.Greater(ShopService.RefreshPrice(1), 0);
        Assert.Greater(ShopService.RefreshPrice(5), ShopService.RefreshPrice(1));
    }
}
