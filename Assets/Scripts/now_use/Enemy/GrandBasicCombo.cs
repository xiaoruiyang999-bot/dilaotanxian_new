using System.Collections;
using UnityEngine;

/// <summary>
/// 格兰近战连段。Brain 是唯一启动方；每一爪仍由 EnemyCombat 消费 AttackData，
/// 因而前摇、有效、收招、判定和伤害保持同源。
/// </summary>
public class GrandBasicCombo : MonoBehaviour
{
    [Header("爪击资产（右/左独立定义）")]
    [SerializeField] private AttackData rightClaw;
    [SerializeField] private AttackData leftClaw;

    [Header("一阶段节奏")]
    [SerializeField, Min(0f)] private float gapBetweenClaws = 1.0f;
    [SerializeField, Min(0f)] private float retreatDistance = 2.2f;
    [SerializeField, Min(0f)] private float retreatSpeed = 3.2f;
    [SerializeField, Range(0f, 180f)] private float maxTurnAnglePerClaw = 35f;

    [Header("临界狂躁四段")]
    [SerializeField, Min(0f)] private float criticalComboGap = 0.55f;
    [SerializeField, Min(0f)] private float criticalRecovery = 1.0f;

    private EnemyCombat combat;
    private EnemyController controller;
    private Rigidbody2D rb;
    private bool cancelled;

    public event System.Action<bool> OnClawStarted;
    public event System.Action OnRetreatStarted;

    private void Awake()
    {
        combat = GetComponent<EnemyCombat>();
        controller = GetComponent<EnemyController>();
        rb = GetComponent<Rigidbody2D>();
    }

    public void Cancel()
    {
        cancelled = true;
        combat?.CancelCurrentAttack();
        controller?.StopMoving();
    }

    public void WireAssets(AttackData right, AttackData left)
    {
        rightClaw = right;
        leftClaw = left;
    }

    /// <summary>第一阶段：右爪→左爪→主动后退。</summary>
    public IEnumerator Run(Transform player, GrandBossBrain brain)
    {
        cancelled = false;

        yield return Claw(rightClaw, player, 180f);
        if (cancelled) yield break;
        yield return WaitCancelable(gapBetweenClaws);
        if (cancelled) yield break;

        yield return Claw(leftClaw, player, maxTurnAnglePerClaw);
        if (cancelled) yield break;

        if (retreatDistance <= 0f || rb == null || player == null) yield break;

        brain.EnterSubState(GrandBossBrain.BossState.Retreat);
        OnRetreatStarted?.Invoke();
        Vector2 away = ((Vector2)transform.position - (Vector2)player.position).normalized;
        if (away.sqrMagnitude < 0.001f) away = Vector2.left;
        controller?.FaceTowards(-away);

        float travelled = 0f;
        while (travelled < retreatDistance && !cancelled)
        {
            float step = Mathf.Min(retreatSpeed * Time.fixedDeltaTime, retreatDistance - travelled);
            rb.MovePosition(rb.position + away * step);
            travelled += step;
            yield return new WaitForFixedUpdate();
        }
        controller?.StopMoving();
    }

    /// <summary>临界狼嚎后：右、左、右、双爪下砸的四段灰盒；结束后不后退。</summary>
    public IEnumerator RunCritical(Transform player)
    {
        cancelled = false;
        yield return Claw(rightClaw, player, 180f);
        if (cancelled) yield break;
        yield return WaitCancelable(criticalComboGap);
        if (cancelled) yield break;

        yield return Claw(leftClaw, player, maxTurnAnglePerClaw);
        if (cancelled) yield break;
        yield return WaitCancelable(criticalComboGap);
        if (cancelled) yield break;

        yield return Claw(rightClaw, player, maxTurnAnglePerClaw);
        if (cancelled) yield break;
        yield return WaitCancelable(criticalComboGap);
        if (cancelled) yield break;

        yield return Claw(leftClaw, player, maxTurnAnglePerClaw);
        if (cancelled) yield break;
        yield return WaitCancelable(criticalRecovery);
    }

    private IEnumerator Claw(AttackData claw, Transform player, float maxTurn)
    {
        if (combat == null || claw == null || player == null) yield break;

        float readyTimeout = Mathf.Max(0.25f, claw.AttackCooldown + 0.5f);
        while (!combat.CanAttack && readyTimeout > 0f && !cancelled)
        {
            readyTimeout -= Time.deltaTime;
            yield return null;
        }
        if (cancelled || !combat.CanAttack) yield break;

        ApplyFacing(player, maxTurn);
        OnClawStarted?.Invoke(claw == leftClaw);
        if (!combat.TryStartAttack(player, claw)) yield break;

        float timeout = claw.WindupTime + claw.ActiveDuration + claw.RecoveryTime + 0.5f;
        while (combat.IsAttacking && timeout > 0f && !cancelled)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitCancelable(float seconds)
    {
        float remaining = seconds;
        while (remaining > 0f && !cancelled)
        {
            remaining -= Time.deltaTime;
            yield return null;
        }
    }

    private void ApplyFacing(Transform player, float maxTurn)
    {
        if (controller == null || player == null) return;
        Vector2 desired = ((Vector2)player.position - (Vector2)transform.position).normalized;
        if (desired.sqrMagnitude < 0.001f) return;

        if (maxTurn >= 179.9f)
        {
            controller.FaceTowards(desired);
            return;
        }

        Vector2 current = transform.right;
        float delta = Mathf.Clamp(Vector2.SignedAngle(current, desired), -maxTurn, maxTurn);
        Vector2 limited = Quaternion.Euler(0f, 0f, delta) * current;
        controller.FaceTowards(limited);
    }
}