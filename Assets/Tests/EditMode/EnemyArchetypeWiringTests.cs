using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 四种特殊怪物的 Prefab 接线门禁。
/// 合并时即使 C# 能编译，Prefab 的行为配置或特殊 AttackData 丢失也会静默退化成普通近战。
/// </summary>
public class EnemyArchetypeWiringTests
{
    [TestCase("Enemy_Ranged", EnemyBehaviorType.Ranged)]
    [TestCase("Enemy_Skirmisher", EnemyBehaviorType.Skirmisher)]
    [TestCase("Enemy_Charger", EnemyBehaviorType.Charger)]
    [TestCase("Enemy_Summoner", EnemyBehaviorType.Summoner)]
    public void SpecialEnemy_HasExpectedBehaviorConfig(string prefabName, EnemyBehaviorType expected)
    {
        GameObject root = LoadPrefab(prefabName);
        EnemyAI ai = root.GetComponent<EnemyAI>();

        Assert.IsNotNull(ai, $"{prefabName} 缺少 EnemyAI");
        Assert.IsNotNull(ai.behaviorConfig, $"{prefabName} 未配置 behaviorConfig，会退化成 Melee");
        Assert.AreEqual(expected, ai.behaviorConfig.behaviorType,
            $"{prefabName} 的行为类型与预期不一致");
    }

    [Test]
    public void Charger_UsesChargeAttackData()
    {
        AttackData attack = LoadPrefab("Enemy_Charger").GetComponent<EnemyCombat>().CurrentAttackData;
        Assert.IsNotNull(attack, "Enemy_Charger 的 EnemyCombat 未配置 AttackData");
        Assert.IsTrue(attack.IsCharge, "Enemy_Charger 的 AttackData 未启用冲锋标记");
    }

    [Test]
    public void Skirmisher_HasCombatAttackData()
    {
        EnemyCombat combat = LoadPrefab("Enemy_Skirmisher").GetComponent<EnemyCombat>();
        Assert.IsNotNull(combat, "Enemy_Skirmisher 缺少 EnemyCombat");
        Assert.IsNotNull(combat.CurrentAttackData,
            "Enemy_Skirmisher 的 EnemyCombat 未配置 AttackData，无法启动游击攻击流程");
    }

    [Test]
    public void Summoner_UsesSummonAttackDataWithMinionPrefab()
    {
        AttackData attack = LoadPrefab("Enemy_Summoner").GetComponent<EnemyCombat>().CurrentAttackData;
        Assert.IsNotNull(attack, "Enemy_Summoner 的 EnemyCombat 未配置 AttackData");
        Assert.IsTrue(attack.IsSummon, "Enemy_Summoner 的 AttackData 未启用召唤标记");
        Assert.IsNotNull(attack.SummonPrefab, "Enemy_Summoner 的召唤物 Prefab 为空");
    }

    [Test]
    public void Ranged_HasProjectileData()
    {
        EnemyCombat combat = LoadPrefab("Enemy_Ranged").GetComponent<EnemyCombat>();
        Assert.IsNotNull(combat, "Enemy_Ranged 缺少 EnemyCombat");
        Assert.IsTrue(combat.IsRanged, "Enemy_Ranged 未配置 ProjectileData");
    }

    [Test]
    public void Elite_RestoresDistanceBasedAttackSelection()
    {
        EnemyCombat combat = LoadPrefab("Enemy_Elite").GetComponent<EnemyCombat>();
        Assert.IsNotNull(combat, "Enemy_Elite 缺少 EnemyCombat");
        Assert.AreEqual(AttackSelectionMode.Distance, combat.SelectionMode,
            "Enemy_Elite 的距离选招模式在合并后失效");
        Assert.GreaterOrEqual(combat.AttackPoolCount, 3, "Enemy_Elite 多招池未完整接线");
    }

    [Test]
    public void Boss_RestoresWeightedAttackSelectionAndPhaseTwo()
    {
        GameObject boss = LoadPrefab("Enemy_Boss");
        EnemyCombat combat = boss.GetComponent<EnemyCombat>();
        BossPhaseController phase = boss.GetComponent<BossPhaseController>();

        Assert.IsNotNull(combat, "Enemy_Boss 缺少 EnemyCombat");
        Assert.AreEqual(AttackSelectionMode.Weighted, combat.SelectionMode,
            "Enemy_Boss 的权重选招模式在合并后失效");
        Assert.GreaterOrEqual(combat.AttackPoolCount, 3, "Enemy_Boss P1 多招池未完整接线");
        Assert.IsNotNull(phase, "Enemy_Boss 缺少 BossPhaseController");
        Assert.IsTrue(phase.enabled, "Enemy_Boss 的阶段控制器未启用");

        SerializedObject serializedPhase = new SerializedObject(phase);
        SerializedProperty phase2 = serializedPhase.FindProperty("phase2Attacks");
        Assert.IsNotNull(phase2, "BossPhaseController 缺少 P2 招式池字段");
        Assert.GreaterOrEqual(phase2.arraySize, 2, "Enemy_Boss P2 招式池未完整接线");
    }

    private static GameObject LoadPrefab(string prefabName)
    {
        string path = $"Assets/Prefabs/{prefabName}.prefab";
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.IsNotNull(root, $"未找到 {path}");
        return root;
    }
}
