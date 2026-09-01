using NUnit.Framework;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 远程敌人接线门禁（v1.0.11）：Enemy_Ranged 变体的 EnemyCombat 必须配置 projectileData
/// （历史上远程 AI 曾在合并中丢失，本测试防止再次断链）。
/// </summary>
public class RangedEnemyWiringTests
{
    private const string PrefabPath = "Assets/Prefabs/Enemy_Ranged.prefab";

    [Test]
    public void EnemyRanged_HasProjectileDataAndRangedFlag()
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(root, $"未找到 {PrefabPath}");

        EnemyCombat combat = root.GetComponent<EnemyCombat>();
        Assert.IsNotNull(combat, "Enemy_Ranged 缺少 EnemyCombat 组件");
        Assert.IsTrue(combat.IsRanged, "Enemy_Ranged 的 EnemyCombat.projectileData 未配置（远程攻击会退化成隐形近战）");
    }
}
