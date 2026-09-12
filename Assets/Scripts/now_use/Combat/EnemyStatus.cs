using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VS 第三批 敌人状态效果容器（V2 §8.2 MVP 首发：流血/破甲）：
/// 流血 = 叠层持续真伤（每层独立周期，重复命中刷新层叠上限）；破甲 = 护甲减量 buff（易伤窗口）。
/// 挂 EnemyHealth 同物体，由攻击方（WeaponHitbox 结算侧/遗物）调用 Apply；
/// 周期结算走协程（互斥：同状态仅一条协程，层数进数据）；死亡/失活即停（OnDisable 清理）。
/// 数值通道：StatusTuning SO 就绪前用常量（VS 灰盒口径，数据化在遗物批次一并收口）。
/// </summary>
public class EnemyStatus : MonoBehaviour
{
    // VS 灰盒数值（V2 §8.2 首轮测试值；数据化收口排期见类注）
    public const int BleedMaxStacks = 5;
    public const float BleedDamagePerTick = 2f;
    public const float BleedTickInterval = 0.8f;
    public const float BleedLayerDuration = 4f;
    public const float ArmorBreakReduction = 6f;
    public const float ArmorBreakDuration = 5f;

    private EnemyHealth health;
    private Coroutine bleedRoutine;
    private readonly List<float> bleedLayers = new List<float>();   // 每层剩余时间

    private float armorBreakRemaining;
    private float armorBrokenAmount;
    private bool armorBreakActive;

    public int BleedStacks => bleedLayers.Count;
    public bool ArmorBreakActive => armorBreakActive;

    public static EnemyStatus Ensure(Component on)
    {
        EnemyStatus s = on.GetComponent<EnemyStatus>();
        if (s == null) s = on.gameObject.AddComponent<EnemyStatus>();
        return s;
    }

    private void Awake() => health = GetComponent<EnemyHealth>();

    private void OnDisable()
    {
        // 死亡/失活清理：停协程、清层、还原破甲（不依赖 OnDestroy 时序——对象池复用安全）
        if (bleedRoutine != null) { StopCoroutine(bleedRoutine); bleedRoutine = null; }
        bleedLayers.Clear();
        RestoreArmor();
    }

    /// <summary>施加流血一层（叠至上限刷新最旧层的剩余时间）。</summary>
    public void ApplyBleed()
    {
        if (health == null || health.IsDead) return;
        if (bleedLayers.Count >= BleedMaxStacks)
            bleedLayers[0] = BleedLayerDuration;   // 满层：刷新最旧
        else
            bleedLayers.Add(BleedLayerDuration);
        if (bleedRoutine == null)
            bleedRoutine = StartCoroutine(BleedTick());
    }

    /// <summary>施加破甲（重复命中刷新持续时间，减量不叠——取常量）。</summary>
    public void ApplyArmorBreak()
    {
        if (health == null || health.IsDead) return;
        if (!armorBreakActive)
        {
            armorBrokenAmount = Mathf.Min(ArmorBreakReduction, health.CurrentArmor);
            health.ModifyArmor(-armorBrokenAmount);
            armorBreakActive = true;
        }
        armorBreakRemaining = ArmorBreakDuration;
    }

    private void RestoreArmor()
    {
        if (!armorBreakActive) return;
        if (health != null && !health.IsDead) health.ModifyArmor(armorBrokenAmount);
        armorBreakActive = false;
        armorBrokenAmount = 0f;
        armorBreakRemaining = 0f;
    }

    private IEnumerator BleedTick()
    {
        var wait = new WaitForSeconds(BleedTickInterval);
        while (true)
        {
            yield return wait;
            if (health == null || health.IsDead) yield break;

            // 层时间结算（独立于 tick 相位的简化：按 tick 间隔统一扣）
            for (int i = bleedLayers.Count - 1; i >= 0; i--)
            {
                bleedLayers[i] -= BleedTickInterval;
                if (bleedLayers[i] <= 0f) bleedLayers.RemoveAt(i);
            }

            if (bleedLayers.Count > 0)
                health.TakeTrueDamage(bleedLayers.Count * BleedDamagePerTick);   // 流血=真伤（不吃减伤甲）
            else
                yield break;   // 层尽：协程自灭（下次 Apply 重启）
        }
    }

    private void Update()
    {
        if (armorBreakActive && (armorBreakRemaining -= Time.deltaTime) <= 0f)
            RestoreArmor();
    }
}
