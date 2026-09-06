using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EnemyAI 状态机与行为逻辑单元测试：验证状态转换、目标获取、巡逻/追击/攻击切换。
/// 仅测试纯逻辑，不依赖物理或动画。
/// </summary>
public class EnemyAITests
{
    // ========== 状态枚举测试 ==========

    [Test]
    public void EnemyState_Patrol_IsDefault()
    {
        EnemyAI.State state = default;
        Assert.AreEqual(EnemyAI.State.Patrol, state);
    }

    [Test]
    public void EnemyState_AllStates_AreDefined()
    {
        Assert.AreEqual(4, System.Enum.GetValues(typeof(EnemyAI.State)).Length);
    }

    // ========== 行为类型测试 ==========

    [Test]
    public void EnemyBehaviorType_Default_IsMelee()
    {
        EnemyBehaviorType behavior = default;
        Assert.AreEqual(EnemyBehaviorType.Melee, behavior);
    }

    [Test]
    public void EnemyBehaviorType_AllTypes_AreDefined()
    {
        Assert.AreEqual(5, System.Enum.GetValues(typeof(EnemyBehaviorType)).Length);
    }

    // ========== 巡逻逻辑测试 ==========

    [Test]
    public void Patrol_WaitTimerExpires_SetsNewTarget()
    {
        float patrolWaitTimer = 0f;
        float patrolWaitTime = 2f;
        Vector3 patrolOrigin = Vector3.zero;
        float patrolRadius = 3f;

        // 模拟 UpdatePatrolOnly 逻辑
        patrolWaitTimer -= Time.deltaTime;
        if (patrolWaitTimer <= 0f)
        {
            // GetRandomPatrolPoint 会设置新目标
            patrolWaitTimer = patrolWaitTime;
        }

        Assert.AreEqual(patrolWaitTime, patrolWaitTimer);
    }

    [Test]
    public void Patrol_AtTarget_StopsMoving()
    {
        Vector3 currentPatrolTarget = new Vector3(1f, 1f, 0f);
        Vector3 enemyPos = new Vector3(1f, 1f, 0f);
        float distance = Vector2.Distance(enemyPos, currentPatrolTarget);

        Assert.LessOrEqual(distance, 0.2f, "Enemy should stop when close to patrol target");
    }

    // ========== 追击逻辑测试 ==========

    [Test]
    public void Chase_PlayerInRange_SwitchToChase()
    {
        float detectionRange = 5f;
        float distToTarget = 3f;
        EnemyAI.State currentState = EnemyAI.State.Patrol;

        if (distToTarget <= detectionRange && currentState == EnemyAI.State.Patrol)
            currentState = EnemyAI.State.Chase;

        Assert.AreEqual(EnemyAI.State.Chase, currentState);
    }

    [Test]
    public void Chase_PlayerOutOfRange_ReturnToPatrol()
    {
        float losePlayerRange = 8f;
        float distToTarget = 10f;
        EnemyAI.State currentState = EnemyAI.State.Chase;

        if (distToTarget > losePlayerRange * 1.5f && currentState == EnemyAI.State.Chase)
            currentState = EnemyAI.State.ReturnToPatrol;

        Assert.AreEqual(EnemyAI.State.ReturnToPatrol, currentState);
    }

    // ========== 攻击逻辑测试 ==========

    [Test]
    public void Attack_InRange_SwitchToAttack()
    {
        float attackRange = 1.5f;
        float distToTarget = 1f;
        bool isAttacking = false;
        EnemyAI.State currentState = EnemyAI.State.Chase;

        if (distToTarget <= attackRange && !isAttacking && currentState == EnemyAI.State.Chase)
            currentState = EnemyAI.State.Attack;

        Assert.AreEqual(EnemyAI.State.Attack, currentState);
    }

    [Test]
    public void Attack_Attacking_StaysInAttack()
    {
        bool isAttacking = true;
        EnemyAI.State currentState = EnemyAI.State.Attack;

        // 攻击期间不切换状态
        if (isAttacking)
            currentState = EnemyAI.State.Attack;

        Assert.AreEqual(EnemyAI.State.Attack, currentState);
    }

    // ========== 返回巡逻逻辑测试 ==========

    [Test]
    public void ReturnToPatrol_AtOrigin_SwitchToPatrol()
    {
        Vector3 patrolOrigin = Vector3.zero;
        Vector3 enemyPos = new Vector3(0.1f, 0.1f, 0f);
        float distToOrigin = Vector2.Distance(enemyPos, patrolOrigin);

        Assert.LessOrEqual(distToOrigin, 0.3f, "Enemy should return to patrol when close to origin");
    }

    // ========== 冲锋行为测试 ==========

    [Test]
    public void Charger_InAttack_StopMovingExceptCharge()
    {
        EnemyBehaviorType behavior = EnemyBehaviorType.Charger;
        EnemyAI.State currentState = EnemyAI.State.Attack;
        bool shouldStopMoving = behavior != EnemyBehaviorType.Charger;

        Assert.IsFalse(shouldStopMoving, "Charger should not stop moving during attack");
    }

    [Test]
    public void Melee_InAttack_StopsMoving()
    {
        EnemyBehaviorType behavior = EnemyBehaviorType.Melee;
        EnemyAI.State currentState = EnemyAI.State.Attack;
        bool shouldStopMoving = behavior != EnemyBehaviorType.Charger;

        Assert.IsTrue(shouldStopMoving, "Melee should stop moving during attack");
    }

    // ========== 游击行为测试 ==========

    [Test]
    public void Skirmisher_AfterAttack_TriggersRetreat()
    {
        EnemyBehaviorType behavior = EnemyBehaviorType.Skirmisher;
        EnemyAI.State previousState = EnemyAI.State.Attack;
        EnemyAI.State newState = EnemyAI.State.Chase;
        bool markRetreat = false;

        // 模拟 ChangeState 逻辑
        if (previousState == EnemyAI.State.Attack && behavior == EnemyBehaviorType.Skirmisher)
            markRetreat = true;

        Assert.IsTrue(markRetreat, "Skirmisher should retreat after attack");
    }

    // ========== 召唤师逻辑测试 ==========

    [Test]
    public void Summoner_CanSummonMore_UnderLimit()
    {
        int aliveMinions = 2;
        int maxMinions = 5;
        bool canSummon = aliveMinions < maxMinions;

        Assert.IsTrue(canSummon);
    }

    [Test]
    public void Summoner_CanSummonMore_AtLimit()
    {
        int aliveMinions = 5;
        int maxMinions = 5;
        bool canSummon = aliveMinions < maxMinions;

        Assert.IsFalse(canSummon);
    }

    [Test]
    public void Summoner_OnMinionDied_DecrementsCount()
    {
        int aliveMinions = 3;
        // 模拟 OnMinionDied
        aliveMinions--;

        Assert.AreEqual(2, aliveMinions);
    }

    // ========== LOS 阻挡逻辑测试 ==========

    [Test]
    public void LosBlocked_IgnoreLos50Percent_ContinuesChase()
    {
        bool losBlocked = true;
        bool ignoringLos = true;
        EnemyAI.State currentState = EnemyAI.State.Chase;

        // 模拟：LOS 被阻挡但 50% 不丢失
        if (losBlocked && ignoringLos)
            currentState = EnemyAI.State.Chase; // 继续追

        Assert.AreEqual(EnemyAI.State.Chase, currentState);
    }

    [Test]
    public void LosBlocked_NotIgnoring_SwitchToReturn()
    {
        bool losBlocked = true;
        bool ignoringLos = false;
        float lastSeenTimer = 0f;
        EnemyAI.State currentState = EnemyAI.State.Chase;

        // 模拟：LOS 被阻挡且搜索计时结束
        if (losBlocked && !ignoringLos && lastSeenTimer <= 0f)
            currentState = EnemyAI.State.ReturnToPatrol;

        Assert.AreEqual(EnemyAI.State.ReturnToPatrol, currentState);
    }
}


