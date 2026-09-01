using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class McpFeatureCompletenessTests
{
    [Test]
    public void CombatSpawnTable_RetainsAllFiveArchetypesAndAffixes()
    {
        SpawnTable table = AssetDatabase.LoadAssetAtPath<SpawnTable>(
            "Assets/Data/Dungeon/EnemyTable_Combat.asset");

        Assert.That(table, Is.Not.Null);
        Assert.That(table.entries.Select(e => e.archetype).Distinct(),
            Is.SupersetOf(new[]
            {
                EnemyArchetype.Melee,
                EnemyArchetype.Ranged,
                EnemyArchetype.Skirmisher,
                EnemyArchetype.Charger,
                EnemyArchetype.Summoner
            }));
        Assert.That(table.affixes.Count(a => a != null), Is.GreaterThanOrEqualTo(2));
        Assert.That(table.distanceBands.Count, Is.GreaterThan(0));
    }

    [TestCase("Assets/Prefabs/Enemy_Ranged.prefab", EnemyBehaviorType.Ranged)]
    [TestCase("Assets/Prefabs/Enemy_Skirmisher.prefab", EnemyBehaviorType.Skirmisher)]
    [TestCase("Assets/Prefabs/Enemy_Charger.prefab", EnemyBehaviorType.Charger)]
    [TestCase("Assets/Prefabs/Enemy_Summoner.prefab", EnemyBehaviorType.Summoner)]
    public void SpecialEnemyPrefab_RetainsMcpRuntimeComponents(string path, EnemyBehaviorType behavior)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        Assert.That(prefab, Is.Not.Null, path);
        Assert.That(prefab.GetComponent<EnemyAI>(), Is.Not.Null, path);
        Assert.That(prefab.GetComponent<EnemyCombat>(), Is.Not.Null, path);
        Assert.That(prefab.GetComponent<EnemyPerception>(), Is.Not.Null, path);
        Assert.That(prefab.GetComponent<EnemyAI>().behaviorConfig, Is.Not.Null, path);
        Assert.That(prefab.GetComponent<EnemyAI>().behaviorConfig.behaviorType, Is.EqualTo(behavior), path);
    }

    [Test]
    public void Summoner_RetainsMinionPrefabAndSummonAttack()
    {
        GameObject summoner = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Enemy_Summoner.prefab");
        AttackData attack = summoner.GetComponent<EnemyCombat>().CurrentAttackData;

        Assert.That(attack, Is.Not.Null);
        Assert.That(attack.IsSummon, Is.True);
        Assert.That(attack.SummonPrefab, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(attack.SummonPrefab),
            Is.EqualTo("Assets/Prefabs/Enemy_Minion.prefab"));
    }
}
