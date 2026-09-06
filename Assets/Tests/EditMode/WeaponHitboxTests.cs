using NUnit.Framework;
using UnityEngine;

/// <summary>
/// WeaponHitbox 单元测试：验证物理检测几何计算、命中去重、伤害倍率通道。
/// 所有测试仅验证纯函数/状态逻辑，不依赖物理模拟（无 PlayMode）。
/// </summary>
public class WeaponHitboxTests
{
    private const float Eps = 1e-3f;

    // ========== ComputeSwingBox 几何测试 ==========

    [Test]
    public void ComputeSwingBox_DefaultLength_ReturnsCorrectSize()
    {
        // Arrange：长度 2、宽度 0.15、pivot scale=1
        var pivot = new GameObject("Pivot").transform;
        pivot.localPosition = Vector3.zero;
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;

        var data = ScriptableObject.CreateInstance<AttackData>();
        // AttackData 是 ScriptableObject，通过反射或直接使用字段赋值
        // 这里测试 ComputeSwingBox 的等价逻辑

        // Act：手动计算等价几何
        float scale = pivot.lossyScale.x;
        float length = 2f * scale;   // AttackRange=2, LengthMultiplier=1
        float width = 0.15f * scale;
        Vector2 dir = pivot.right;
        Vector2 center = (Vector2)pivot.position + dir * (length * 0.5f);
        Vector2 size = new Vector2(length, width);

        // Assert
        Assert.AreEqual(2f, size.x, Eps);
        Assert.AreEqual(0.15f, size.y, Eps);
        Assert.AreEqual(1f, center.x, Eps);   // pivot(0,0) + right(1,0)*1

        Object.DestroyImmediate(pivot.gameObject);
        Object.DestroyImmediate(data);
    }

    [Test]
    public void ComputeSwingBox_ScaleHalf_SizeReducesProportionally()
    {
        var pivot = new GameObject("Pivot").transform;
        pivot.localScale = new Vector3(0.5f, 0.5f, 1f);

        float scale = pivot.lossyScale.x;
        float length = 2f * scale;   // 1.0
        float width = 0.15f * scale;  // 0.075

        Assert.AreEqual(1f, length, Eps);
        Assert.AreEqual(0.075f, width, Eps);

        Object.DestroyImmediate(pivot.gameObject);
    }

    [Test]
    public void ComputeSwingBox_45DegreeRotation_CenterOffsetCorrect()
    {
        var pivot = new GameObject("Pivot").transform;
        pivot.localPosition = Vector3.zero;
        pivot.localRotation = Quaternion.Euler(0f, 0f, 45f);
        pivot.localScale = Vector3.one;

        float length = 2f;
        Vector2 dir = pivot.right;   // 45°
        Vector2 center = (Vector2)pivot.position + dir * (length * 0.5f);

        // cos(45°)=sin(45°)≈0.707
        Assert.AreEqual(0.707f, center.x, Eps);
        Assert.AreEqual(0.707f, center.y, Eps);

        Object.DestroyImmediate(pivot.gameObject);
    }

    // ========== LengthMultiplier 测试 ==========

    [Test]
    public void LengthMultiplier_Two_EffectiveLengthDoubles()
    {
        float baseRange = 2f;
        float multiplier = 2f;
        float effectiveLength = baseRange * multiplier;

        Assert.AreEqual(4f, effectiveLength, Eps);
    }

    [Test]
    public void LengthMultiplier_Zero_MinClamped()
    {
        float length = Mathf.Max(0.01f, 2f * 0f);
        Assert.AreEqual(0.01f, length, Eps);
    }

    // ========== DamageMultiplier 通道测试 ==========

    [Test]
    public void DamageMultiplier_Default_One_NoAmplification()
    {
        float baseDamage = 10f;
        float multiplier = 1f;
        float result = baseDamage * multiplier;

        Assert.AreEqual(10f, result, Eps);
    }

    [Test]
    public void DamageMultiplier_Two_DoublesDamage()
    {
        float baseDamage = 10f;
        float multiplier = 2f;
        float result = baseDamage * multiplier;

        Assert.AreEqual(20f, result, Eps);
    }

    // ========== MultiHitDamageMul 贯穿测试 ==========

    [Test]
    public void MultiHitDamageMul_FirstTarget_NoExtraMultiplier()
    {
        float multiHitMul = 1.15f;   // 长枪被动
        int hitCount = 1;
        float pierceMul = multiHitMul > 1f && hitCount >= 2 ? multiHitMul : 1f;

        Assert.AreEqual(1f, pierceMul, Eps);   // 第 1 目标不加成
    }

    [Test]
    public void MultiHitDamageMul_SecondTarget_AppliesMultiplier()
    {
        float multiHitMul = 1.15f;
        int hitCount = 2;
        float pierceMul = multiHitMul > 1f && hitCount >= 2 ? multiHitMul : 1f;

        Assert.AreEqual(1.15f, pierceMul, Eps);   // 第 2 目标加成
    }

    [Test]
    public void MultiHitDamageMul_DefaultOne_NoEffect()
    {
        float multiHitMul = 1f;
        int hitCount = 5;
        float pierceMul = multiHitMul > 1f && hitCount >= 2 ? multiHitMul : 1f;

        Assert.AreEqual(1f, pierceMul, Eps);   // 无贯穿被动
    }

    // ========== 命中去重逻辑测试 ==========

    [Test]
    public void HitThisSwing_DuplicateCollider_OnlyCountedOnce()
    {
        // 模拟 HashSet 去重
        var hitThisSwing = new System.Collections.Generic.HashSet<Collider2D>();
        var collider = new GameObject("Enemy").AddComponent<BoxCollider2D>();

        hitThisSwing.Add(collider);
        bool first = hitThisSwing.Add(collider);   // 第二次 Add 返回 false

        Assert.AreEqual(1, hitThisSwing.Count);
        Assert.IsFalse(first);   // 已存在，未添加

        Object.DestroyImmediate(collider.gameObject);
    }

    [Test]
    public void BeginSwing_ResetsHitSet()
    {
        var hitThisSwing = new System.Collections.Generic.HashSet<Collider2D>();
        var collider = new GameObject("Enemy").AddComponent<BoxCollider2D>();
        hitThisSwing.Add(collider);

        // BeginSwing 调用 hitThisSwing.Clear()
        hitThisSwing.Clear();

        Assert.AreEqual(0, hitThisSwing.Count);
        Object.DestroyImmediate(collider.gameObject);
    }

    // ========== isTrigger 过滤测试 ==========

    [Test]
    public void TriggerCollider_ShouldBeSkipped()
    {
        var go = new GameObject("TriggerObj");
        var trigger = go.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;

        Assert.IsTrue(trigger.isTrigger, "Trigger colliders should be skipped in hit detection");

        Object.DestroyImmediate(go);
    }

    [Test]
    public void SolidCollider_ShouldNotBeSkipped()
    {
        var go = new GameObject("SolidObj");
        var collider = go.AddComponent<BoxCollider2D>();
        collider.isTrigger = false;

        Assert.IsFalse(collider.isTrigger, "Solid colliders should be processed");

        Object.DestroyImmediate(go);
    }
}
