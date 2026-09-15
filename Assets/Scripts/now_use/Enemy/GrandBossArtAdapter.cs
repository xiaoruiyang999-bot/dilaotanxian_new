using UnityEngine;

/// <summary>
/// v2.0.10 格兰美术适配层（预留接口的消费示范 + 占位表现）：
/// 订阅 GrandBossBrain/招式模块的全部美术事件，把状态映射到占位表现
/// （变色/闪烁/缩放脉冲——纯程序视觉，美术资产到位后只改本类，逻辑零改动）。
/// 挂载：GrandBossBrain.EnsureOn 时自动附带；占位渲染挂 ArtRoot（缺省=Boss 自身）。
/// 美术接入合同（正式资产替换时）：
/// - Animator：订阅 OnStateEntered 做状态机驱动（参数名与 BossState 同名转 Hash）；
/// - 序列帧：同事件切换帧组；
/// - VFX：OnLeapTakeoff/OnLeapLanded/OnClawStarted/OnStunned 挂粒子/尘土；
/// - 阶段变色沿用 BossPhaseController（P2 染红/P3 赤红——勿重复着色）。
/// </summary>
public class GrandBossArtAdapter : MonoBehaviour
{
    private GrandBossBrain brain;
    private GrandTripleLeap leap;
    private GrandBasicCombo combo;
    private SpriteRenderer body;
    private Color baseColor;

    private static readonly Color LeapTint = new Color(1f, 0.75f, 0.5f);   // 跃击暖色
    private static readonly Color StunTint = new Color(0.6f, 0.7f, 1f);    // 眩晕冷色

    /// <summary>EnsureOn 调用（幂等）。</summary>
    public static GrandBossArtAdapter EnsureOn(GameObject boss, GrandBossBrain brain)
    {
        var adapter = boss.GetComponent<GrandBossArtAdapter>();
        if (adapter == null)
        {
            adapter = boss.AddComponent<GrandBossArtAdapter>();
            adapter.Bind(brain);
        }
        return adapter;
    }

    private void Bind(GrandBossBrain brainOwner)
    {
        brain = brainOwner;
        leap = GetComponent<GrandTripleLeap>();
        combo = GetComponent<GrandBasicCombo>();
        // 占位渲染挂在 ArtRoot 指向的物体（缺省=自身——Boss 的 SpriteRenderer）
        body = (brain.ArtRoot != null ? brain.ArtRoot : transform).GetComponentInChildren<SpriteRenderer>();
        if (body != null) baseColor = body.color;

        brain.OnStateEntered += OnState;
        if (leap != null)
        {
            leap.OnLeapTakeoff += OnTakeoff;
            leap.OnLeapLanded += OnLanded;
            leap.OnStunned += OnStunned;
        }
        if (combo != null)
        {
            combo.OnClawStarted += OnClaw;
            combo.OnRetreatStarted += OnRetreat;
        }
    }

    private void OnDestroy()
    {
        if (brain != null) brain.OnStateEntered -= OnState;
        if (leap != null)
        {
            leap.OnLeapTakeoff -= OnTakeoff;
            leap.OnLeapLanded -= OnLanded;
            leap.OnStunned -= OnStunned;
        }
        if (combo != null)
        {
            combo.OnClawStarted -= OnClaw;
            combo.OnRetreatStarted -= OnRetreat;
        }
    }

    // ---------- 占位表现（正式美术到位后整段替换本区） ----------

    private void OnState(GrandBossBrain.BossState state)
    {
        if (body == null) return;
        switch (state)
        {
            case GrandBossBrain.BossState.Stunned:
                body.color = StunTint;
                break;
            case GrandBossBrain.BossState.Dead:
                body.color = new Color(0.25f, 0.25f, 0.25f);
                break;
            default:
                body.color = baseColor;   // 非特殊态回基色（阶段变色由 PhaseController 管理，此处不覆盖）
                break;
        }
    }

    private void OnTakeoff(int index, Vector2 pos) { /* VFX 挂点：起跳尘土 */ }
    private void OnLanded(int index, Vector2 pos, bool centerHit) { /* VFX 挂点：落地冲击 */ }
    private void OnStunned() { /* VFX 挂点：眩晕星星 */ }
    private void OnClaw(bool isLeftClaw) { /* VFX 挂点：爪击拖影 */ }
    private void OnRetreat() { /* VFX 挂点：后撤尘土 */ }
}
